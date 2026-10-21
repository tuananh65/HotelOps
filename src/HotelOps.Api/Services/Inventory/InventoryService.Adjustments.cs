using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed partial class InventoryService
{
    // Caller already owns catalog/owner/warehouse gates; number gate is acquired last.
    private async Task<StockDocument> Adjustment(InventoryAdjustment r, List<Warehouse> warehouses, DateTime now, CancellationToken ct)
    {
        if (r.DocumentDate == default) throw Invalid("DocumentDate is required.");
        Reason(r.Reason); if (r.Lines is null || r.Lines.Any(x => x is null) || r.Lines.Count == 0) throw Invalid("At least one delta is required.");
        var lines = new List<(StockDocumentLine Line, decimal Delta)>();
        foreach (var input in r.Lines)
        {
            if (Qty(input.DeltaBaseQty, true) == 0) throw Invalid("Zero delta is invalid.");
            var item = await Item(input.ItemId, ct); var magnitude = Math.Abs(input.DeltaBaseQty);
            lines.Add((new() { ItemId = item.ItemId, UomId = item.BaseUomId, ConversionFactor = 1, ActualQty = magnitude, BaseQty = magnitude, LineNo = lines.Count + 1 }, input.DeltaBaseQty));
        }
        await StockGates(lines.Select(x => (r.WarehouseId, x.Line.ItemId)), ct);
        foreach (var g in lines.GroupBy(x => x.Line.ItemId))
            if (await Balance(r.WarehouseId, g.Key, ct) + g.Sum(x => x.Delta) < 0) throw Invalid("Adjustment would make stock negative.");
        var d = new StockDocument
        {
            DocumentNo = await Number("ADJ", now, false, ct),
            DocumentType = "ADJUSTMENT",
            Status = "POSTED",
            DestinationWarehouseId = r.WarehouseId,
            CreatedBy = r.ActorUserId,
            PostedBy = r.ActorUserId,
            CreatedAt = now,
            PostedAt = now,
            DocumentDate = r.DocumentDate,
            Reason = r.Reason.Trim()
        };
        foreach (var (line, delta) in lines)
        {
            d.StockDocumentLines.Add(line); db.StockMovements.Add(new() { DocumentLine = line, WarehouseId = r.WarehouseId, ItemId = line.ItemId, SignedBaseQty = delta, OccurredAt = now, CreatedBy = r.ActorUserId });
        }
        db.StockDocuments.Add(d); await DocumentAudit(d, r.ActorUserId, "CREATE", "ADJUSTED", null, warehouses, now, ct); return d;
    }
    public async Task<InventoryDocumentDto> CreateAdjustment(InventoryAdjustment r, CancellationToken ct)
    {
        await using var tx = await Begin(r.ActorUserId, ct); var warehouses = await Warehouses([r.WarehouseId], true, ct);
        var d = await Adjustment(r, warehouses, DateTime.UtcNow, ct); await tx.CommitAsync(ct); return (await GetDocument(d.DocumentId, ct))!;
    }
    public async Task<InventoryDocumentDto?> Reverse(long id, InventoryCommand r, CancellationToken ct)
    {
        var reason = Reason(r.Reason); await using var tx = await Begin(r.ActorUserId, ct); var original = await Document(id, ct); if (original is null) return null;
        Version(original.RowVersion, r.ExpectedRowVersion);
        if (original.Status != "POSTED" || original.DocumentType is not ("RECEIPT" or "RETURN" or "ADJUSTMENT") || original.ReversalOfDocumentId.HasValue ||
            await db.StockDocuments.AnyAsync(x => x.ReversalOfDocumentId == id, ct)) throw Invalid("Document is not eligible for reversal.");
        var movements = await db.StockMovements.Where(x => x.DocumentLine.DocumentId == id).OrderBy(x => x.MovementId).ToListAsync(ct);
        if (movements.Count == 0) throw Invalid("Original has no recorded stock effect.");
        await HotelOps.Api.Services.Replenishments.ReplenishmentRules.ReverseLinkedReceipt(db, original, r.ActorUserId, ct);
        var warehouses = await Warehouses(movements.Select(x => x.WarehouseId).Concat(WarehouseIds(original)), true, ct);
        await StockGates(movements.Select(x => (x.WarehouseId, x.ItemId)), ct);
        foreach (var g in movements.GroupBy(x => (x.WarehouseId, x.ItemId)))
            if (await Balance(g.Key.WarehouseId, g.Key.ItemId, ct) - g.Sum(x => x.SignedBaseQty) < 0) throw Invalid("Reversal would make stock negative.");
        var now = DateTime.UtcNow; var before = Scalars(original);
        var d = new StockDocument
        {
            DocumentNo = await Number("REV", now, false, ct),
            DocumentType = "REVERSAL",
            Status = "POSTED",
            ReversalOfDocumentId = id,
            SourceWarehouseId = original.SourceWarehouseId,
            DestinationWarehouseId = original.DestinationWarehouseId,
            CreatedBy = r.ActorUserId,
            PostedBy = r.ActorUserId,
            CreatedAt = now,
            PostedAt = now,
            DocumentDate = DateOnly.FromDateTime(now),
            Reason = reason
        };
        foreach (var g in movements.GroupBy(x => x.DocumentLineId))
        {
            var old = original.StockDocumentLines.Single(x => x.LineId == g.Key);
            var line = new StockDocumentLine
            {
                LineNo = d.StockDocumentLines.Count + 1,
                ItemId = old.ItemId,
                UomId = old.UomId,
                ConversionFactor = old.ConversionFactor,
                ActualQty = old.ActualQty,
                BaseQty = old.BaseQty,
                RequestedQty = old.RequestedQty,
                UnitCost = old.UnitCost
            };
            d.StockDocumentLines.Add(line);
            foreach (var m in g) db.StockMovements.Add(new() { DocumentLine = line, WarehouseId = m.WarehouseId, ItemId = m.ItemId, SignedBaseQty = -m.SignedBaseQty, UnitCost = m.UnitCost, OccurredAt = now, CreatedBy = r.ActorUserId });
        }
        db.StockDocuments.Add(d); original.Status = "REVERSED";
        await DocumentAudit(original, r.ActorUserId, "UPDATE", "REVERSED", before, warehouses, now, ct);
        await DocumentAudit(d, r.ActorUserId, "CREATE", "REVERSAL_CREATED", null, warehouses, now, ct);
        await tx.CommitAsync(ct); return (await GetDocument(d.DocumentId, ct))!;
    }
}
