using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed partial class InventoryService
{
    // Only the stocktake caller may request immediate posting, after its existing
    // counter/approver guard. Direct HTTP adjustment creation always stays DRAFT.
    private async Task<StockDocument> Adjustment(long actorUserId, InventoryAdjustment r, List<Warehouse> warehouses,
        DateTime now, bool postApprovedStocktake, CancellationToken ct)
    {
        if (r.DocumentDate == default) throw Invalid("DocumentDate is required.");
        Reason(r.Reason);
        if (r.Lines is null || r.Lines.Any(x => x is null) || r.Lines.Count == 0) throw Invalid("At least one delta is required.");
        var d = new StockDocument
        {
            DocumentType = "ADJUSTMENT", Status = postApprovedStocktake ? "POSTED" : "DRAFT",
            DestinationWarehouseId = r.WarehouseId, CreatedBy = actorUserId, CreatedAt = now,
            PostedBy = postApprovedStocktake ? actorUserId : null, PostedAt = postApprovedStocktake ? now : null,
            DocumentDate = r.DocumentDate, Reason = r.Reason.Trim()
        };
        foreach (var input in r.Lines)
        {
            if (Qty(input.DeltaBaseQty, true) == 0) throw Invalid("Zero delta is invalid.");
            var item = await Item(input.ItemId, ct);
            var magnitude = Math.Abs(input.DeltaBaseQty);
            d.StockDocumentLines.Add(new()
            {
                ItemId = item.ItemId, UomId = item.BaseUomId, ConversionFactor = 1,
                RequestedQty = input.DeltaBaseQty, ActualQty = magnitude, BaseQty = magnitude,
                LineNo = d.StockDocumentLines.Count + 1
            });
        }
        if (postApprovedStocktake) await AdjustmentMovements(d, actorUserId, now, ct);
        // Number allocation is last, after stock gates on the stocktake path.
        d.DocumentNo = await Number("ADJ", now, false, ct);
        db.StockDocuments.Add(d);
        await DocumentAudit(d, actorUserId, "CREATE", postApprovedStocktake ? "ADJUSTMENT_POSTED" : "ADJUSTMENT_DRAFT_CREATED", null, warehouses, now, ct);
        return d;
    }
    public async Task<InventoryDocumentDto> CreateAdjustment(long actorUserId, InventoryAdjustment r, CancellationToken ct)
    {
        await using var tx = await Begin(actorUserId, ct);
        var warehouses = await Warehouses([r.WarehouseId], true, ct);
        var d = await Adjustment(actorUserId, r, warehouses, DateTime.UtcNow, postApprovedStocktake: false, ct);
        await tx.CommitAsync(ct);
        return (await GetDocument(d.DocumentId, ct))!;
    }
    private async Task AdjustmentMovements(StockDocument d, long actor, DateTime now, CancellationToken ct)
    {
        if (d.SourceWarehouseId != null || d.DestinationWarehouseId is not long warehouse || d.StockDocumentLines.Count == 0)
            throw Invalid("Adjustment requires its authoritative destination warehouse and proposal lines.");
        foreach (var line in d.StockDocumentLines)
        {
            if (line.RequestedQty is not decimal delta || Qty(delta, true) == 0)
                throw Invalid("Adjustment proposal requires an exact nonzero signed RequestedQty.");
            var item = await Item(line.ItemId, ct);
            if (line.UomId != item.BaseUomId || line.ConversionFactor != 1
                || line.ActualQty != Math.Abs(delta) || line.BaseQty != Math.Abs(delta))
                throw Invalid("Adjustment magnitude/base UOM does not match its signed proposal.");
        }
        await StockGates(d.StockDocumentLines.Select(x => (warehouse, x.ItemId)), ct);
        foreach (var group in d.StockDocumentLines.GroupBy(x => x.ItemId))
            if (await Balance(warehouse, group.Key, ct) + group.Sum(x => x.RequestedQty!.Value) < 0)
                throw Invalid("Adjustment would make stock negative.");
        foreach (var line in d.StockDocumentLines)
            db.StockMovements.Add(new()
            {
                DocumentLine = line, WarehouseId = warehouse, ItemId = line.ItemId,
                SignedBaseQty = line.RequestedQty!.Value, OccurredAt = now, CreatedBy = actor
            });
    }
    public async Task<InventoryDocumentDto?> PostAdjustment(long id, long actorUserId, InventoryCommand r, CancellationToken ct)
    {
        await using var tx = await Begin(actorUserId, ct);
        var d = await Document(id, ct);
        if (d is null) return null;
        Version(d.RowVersion, r.ExpectedRowVersion);
        if (d.DocumentType != "ADJUSTMENT" || d.Status != "DRAFT" || d.PostedBy != null || d.PostedAt != null)
            throw Invalid("Only an unposted ADJUSTMENT DRAFT may be posted.");
        if (d.CreatedBy == actorUserId) throw Invalid("Adjustment poster must differ from its proposer.");
        if (await db.StockMovements.AnyAsync(x => x.DocumentLine.DocumentId == id, ct))
            throw Invalid("Adjustment DRAFT already has stock effects.");
        var warehouses = await Warehouses(WarehouseIds(d), true, ct);
        var before = Scalars(d); var now = DateTime.UtcNow;
        await AdjustmentMovements(d, actorUserId, now, ct);
        d.Status = "POSTED"; d.PostedBy = actorUserId; d.PostedAt = now;
        await DocumentAudit(d, actorUserId, "UPDATE", "ADJUSTMENT_POSTED", before, warehouses, now, ct);
        await tx.CommitAsync(ct);
        return await GetDocument(id, ct);
    }
    private async Task<List<StockMovement>> ReversalSource(StockDocument original, long? proposal, CancellationToken ct)
    {
        if (original.Status != "POSTED" || original.DocumentType is not ("RECEIPT" or "RETURN" or "ADJUSTMENT")
            || original.ReversalOfDocumentId != null
            || await db.StockDocuments.AnyAsync(x => x.ReversalOfDocumentId == original.DocumentId && (proposal == null || x.DocumentId != proposal.Value), ct))
            throw Invalid("Document is not eligible for reversal, or another proposal already exists.");
        var movements = await db.StockMovements.AsNoTracking().Where(x => x.DocumentLine.DocumentId == original.DocumentId)
            .OrderBy(x => x.MovementId).ToListAsync(ct);
        if (movements.Count == 0 || movements.Any(m => !original.StockDocumentLines.Any(l => l.LineId == m.DocumentLineId && l.ItemId == m.ItemId)))
            throw Invalid("Original stock effect cannot be reconciled with its immutable lines.");
        return movements;
    }
    public async Task<InventoryDocumentDto?> Reverse(long id, long actorUserId, InventoryCommand r, CancellationToken ct)
    {
        var reason = Reason(r.Reason);
        await using var tx = await Begin(actorUserId, ct);
        var original = await Document(id, ct);
        if (original is null) return null;
        Version(original.RowVersion, r.ExpectedRowVersion);
        var movements = await ReversalSource(original, null, ct);
        var warehouses = await Warehouses(movements.Select(x => x.WarehouseId).Concat(WarehouseIds(original)), true, ct);
        var now = DateTime.UtcNow;
        var d = new StockDocument
        {
            DocumentNo = await Number("REV", now, false, ct), DocumentType = "REVERSAL", Status = "DRAFT",
            ReversalOfDocumentId = id, SourceWarehouseId = original.SourceWarehouseId,
            DestinationWarehouseId = original.DestinationWarehouseId, CreatedBy = actorUserId, CreatedAt = now,
            DocumentDate = DateOnly.FromDateTime(now), Reason = reason
        };
        foreach (var lineId in movements.Select(x => x.DocumentLineId).Distinct())
        {
            var old = original.StockDocumentLines.Single(x => x.LineId == lineId);
            // Preserve the original immutable line number, including gaps from
            // zero-effect lines. Posting rechecks this exact persisted snapshot.
            d.StockDocumentLines.Add(new()
            {
                LineNo = old.LineNo, ItemId = old.ItemId, UomId = old.UomId, ConversionFactor = old.ConversionFactor,
                ActualQty = old.ActualQty, BaseQty = old.BaseQty, RequestedQty = old.RequestedQty,
                UnitCost = old.UnitCost, Note = old.Note
            });
        }
        db.StockDocuments.Add(d);
        await DocumentAudit(d, actorUserId, "CREATE", "REVERSAL_DRAFT_CREATED", null, warehouses, now, ct);
        await tx.CommitAsync(ct);
        return (await GetDocument(d.DocumentId, ct))!;
    }
    public async Task<InventoryDocumentDto?> PostReversal(long id, long actorUserId, InventoryCommand r, CancellationToken ct)
    {
        // Discovery chooses owner locks only; authoritative state is read below.
        var sourceId = await db.StockDocuments.AsNoTracking().Where(x => x.DocumentId == id)
            .Select(x => x.ReversalOfDocumentId).SingleOrDefaultAsync(ct);
        await using var tx = await Begin(actorUserId, ct);
        var owners = new Dictionary<long, StockDocument>();
        foreach (var key in new[] { (long?)id, sourceId }.Where(x => x.HasValue).Select(x => x!.Value).Distinct().Order())
        {
            var owner = await Document(key, ct);
            if (owner != null) owners.Add(key, owner);
        }
        if (!owners.TryGetValue(id, out var d)) return null;
        Version(d.RowVersion, r.ExpectedRowVersion);
        if (d.DocumentType != "REVERSAL" || d.Status != "DRAFT" || d.PostedBy != null || d.PostedAt != null
            || sourceId is not long originalId || d.ReversalOfDocumentId != originalId
            || !owners.TryGetValue(originalId, out var original))
            throw Invalid("Only a current REVERSAL DRAFT with its original may be posted. Reload changed context.");
        if (d.CreatedBy == actorUserId) throw Invalid("Reversal poster must differ from its proposer.");
        if (await db.StockMovements.AnyAsync(x => x.DocumentLine.DocumentId == id, ct))
            throw Invalid("Reversal DRAFT already has stock effects.");
        var movements = await ReversalSource(original, id, ct);
        if (d.SourceWarehouseId != original.SourceWarehouseId || d.DestinationWarehouseId != original.DestinationWarehouseId
            || d.StockDocumentLines.Count != movements.Select(x => x.DocumentLineId).Distinct().Count())
            throw Invalid("Reversal proposal no longer matches its original.");
        foreach (var lineId in movements.Select(x => x.DocumentLineId).Distinct())
        {
            var old = original.StockDocumentLines.Single(x => x.LineId == lineId);
            var line = d.StockDocumentLines.SingleOrDefault(x => x.LineNo == old.LineNo);
            if (line is null || line.ItemId != old.ItemId || line.UomId != old.UomId
                || line.ConversionFactor != old.ConversionFactor || line.ActualQty != old.ActualQty
                || line.BaseQty != old.BaseQty || line.RequestedQty != old.RequestedQty || line.UnitCost != old.UnitCost || line.Note != old.Note)
                throw Invalid("Reversal line snapshot no longer matches its original.");
        }
        // Existing replenishment owner/claim locks precede warehouse/stock locks.
        // Its receipt reconciliation must treat DRAFT reversals as ineffective.
        await HotelOps.Api.Services.Replenishments.ReplenishmentRules.ReverseLinkedReceipt(db, original, actorUserId, ct);
        var warehouses = await Warehouses(movements.Select(x => x.WarehouseId).Concat(WarehouseIds(original)), true, ct);
        await StockGates(movements.Select(x => (x.WarehouseId, x.ItemId)), ct);
        foreach (var group in movements.GroupBy(x => (x.WarehouseId, x.ItemId)))
            if (await Balance(group.Key.WarehouseId, group.Key.ItemId, ct) - group.Sum(x => x.SignedBaseQty) < 0)
                throw Invalid("Reversal would make stock negative.");
        var before = Scalars(d); var originalBefore = Scalars(original); var now = DateTime.UtcNow;
        foreach (var movement in movements)
        {
            var old = original.StockDocumentLines.Single(x => x.LineId == movement.DocumentLineId);
            var line = d.StockDocumentLines.Single(x => x.LineNo == old.LineNo);
            db.StockMovements.Add(new()
            {
                DocumentLine = line, WarehouseId = movement.WarehouseId, ItemId = movement.ItemId,
                SignedBaseQty = -movement.SignedBaseQty, UnitCost = movement.UnitCost, OccurredAt = now, CreatedBy = actorUserId
            });
        }
        d.Status = "POSTED"; d.PostedBy = actorUserId; d.PostedAt = now; original.Status = "REVERSED";
        await DocumentAudit(original, actorUserId, "UPDATE", "REVERSED", originalBefore, warehouses, now, ct);
        await DocumentAudit(d, actorUserId, "UPDATE", "REVERSAL_POSTED", before, warehouses, now, ct);
        await tx.CommitAsync(ct);
        return await GetDocument(id, ct);
    }
}
