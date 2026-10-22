using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed partial class InventoryService
{
    private async Task<StockDocument?> Document(long id, CancellationToken ct)
    {
        await Gate($"HotelOps:Inventory:Document:{id}", ct);
        var root = await db.StockDocuments.FromSqlInterpolated($"SELECT * FROM dbo.stock_documents WITH (UPDLOCK,HOLDLOCK) WHERE document_id={id}").SingleOrDefaultAsync(ct);
        if (root is not null) await db.Entry(root).Collection(x => x.StockDocumentLines).LoadAsync(ct);
        return root;
    }
    private static IEnumerable<long> WarehouseIds(StockDocument d) => new[] { d.SourceWarehouseId, d.DestinationWarehouseId }.Where(x => x.HasValue).Select(x => x!.Value);
    private async Task DocumentAudit(StockDocument d, long actor, string action, string businessEvent, object? before, List<Warehouse> warehouses, DateTime now, CancellationToken ct)
    {
        db.ChangeTracker.DetectChanges();
        var changed = db.Entry(d).Properties.Where(x => x.IsModified && x.Metadata.Name != "RowVersion" && !Equals(x.OriginalValue, x.CurrentValue)).Select(x => x.Metadata.GetColumnName()!).ToList();
        if (action == "CREATE") changed = Scalars(d).Keys.ToList();
        if ((action == "CREATE" && d.StockDocumentLines.Count > 0) || db.ChangeTracker.Entries<StockDocumentLine>().Any(x => x.State is EntityState.Modified or EntityState.Added or EntityState.Deleted)) changed.Add("lines");
        if (db.ChangeTracker.Entries<StockReceiptLineDetail>().Any(x => x.State == EntityState.Added)) changed.Add("receipt_details");
        if (db.ChangeTracker.Entries<StockReturnLineDetail>().Any(x => x.State is EntityState.Added or EntityState.Modified)) changed.Add("return_details");
        if (db.ChangeTracker.Entries<StockTransferReceipt>().Any(x => x.State == EntityState.Added)) changed.Add("receiving_batches");
        await Save(ct);
        var source = warehouses.SingleOrDefault(x => x.WarehouseId == d.SourceWarehouseId);
        var destination = warehouses.SingleOrDefault(x => x.WarehouseId == d.DestinationWarehouseId);
        var cross = d.DocumentType == "TRANSFER" && source!.BranchId != destination!.BranchId;
        var scopes = cross ? new[] { (source!.BranchId, "SOURCE"), (destination!.BranchId, "DESTINATION") } :
            warehouses.Select(x => (x.BranchId, "WAREHOUSE")).Distinct().ToArray();
        foreach (var (branch, role) in scopes)
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "stock_documents",
                EntityId = d.DocumentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ActorUserId = actor,
                BranchId = branch,
                Action = action,
                OccurredAt = now,
                OldValues = before is null ? null : System.Text.Json.JsonSerializer.Serialize(before),
                NewValues = System.Text.Json.JsonSerializer.Serialize(new
                {
                    businessEvent,
                    scopeRole = role,
                    document = Scalars(d),
                    lines = d.StockDocumentLines.Select(Scalars).ToList(),
                    receiptDetails = d.StockDocumentLines.Where(l => l.StockReceiptLineDetail is not null).Select(l => Scalars(l.StockReceiptLineDetail!)).ToList(),
                    returnDetails = d.StockDocumentLines.Where(l => l.StockReturnLineDetailStockDocumentLine is not null).Select(l => Scalars(l.StockReturnLineDetailStockDocumentLine!)).ToList(),
                    receivingBatches = d.StockTransferReceipts.Select(b => new { header = Scalars(b), lines = b.StockTransferReceiptLines.Select(Scalars).ToList() }).ToList()
                }),
                ChangedFields = System.Text.Json.JsonSerializer.Serialize(changed.Distinct())
            });
        await Save(ct);
    }
    private async Task<StockDocumentLine> Original(long id, long item, CancellationToken ct)
    {
        var line = await db.StockDocumentLines.Include(x => x.Document).SingleOrDefaultAsync(x => x.LineId == id, ct);
        if (line is null || line.ItemId != item || line.Document.DocumentType != "ISSUE" ||
            !await db.StockMovements.AnyAsync(x => x.DocumentLineId == id && x.SignedBaseQty < 0, ct))
            throw Invalid("OriginalIssueLineId must identify an effective ISSUE line for the same item.");
        if (line.Document.Status is "DRAFT" or "CANCELLED" or "REVERSED") throw Invalid("Original ISSUE is not effective.");
        return line;
    }
    private async Task ReturnCap(long original, decimal additionalBase, long? excludeDocument, CancellationToken ct)
    {
        await ValidateCapacity(original, 0, additionalBase, excludeDocument, ct);
    }
    public async Task<InventoryDocumentDto?> SaveDraft(string type, long? id, InventoryDraft request, CancellationToken ct)
    {
        if (request.DocumentDate == default) throw Invalid("DocumentDate is required.");
        if (type is not ("RECEIPT" or "RETURN" or "TRANSFER")) throw Invalid("Unsupported draft type.");
        if (request.Lines is null || request.Lines.Any(x => x is null) || request.Lines.Count == 0) throw Invalid("At least one line is required.");
        await using var tx = await Begin(request.ActorUserId, ct);
        StockDocument d;
        object? before = null;
        if (id.HasValue)
        {
            var existing = await Document(id.Value, ct); if (existing is null) return null; d = existing;
            Version(d.RowVersion, request.ExpectedRowVersion);
            if (d.DocumentType != type || d.Status != "DRAFT") throw Invalid("Only the matching DRAFT document can be edited.");
            before = new { document = Scalars(d), lines = d.StockDocumentLines.Select(Scalars).ToList() };
        }
        else d = new StockDocument { DocumentType = type, Status = "DRAFT", CreatedBy = request.ActorUserId };
        if (type == "TRANSFER" && (!request.SourceWarehouseId.HasValue || !request.DestinationWarehouseId.HasValue || request.SourceWarehouseId == request.DestinationWarehouseId))
            throw Invalid("Transfer requires different source and destination warehouses.");
        if (type != "TRANSFER" && (request.SourceWarehouseId.HasValue || !request.DestinationWarehouseId.HasValue))
            throw Invalid("Receipt/return requires only a destination warehouse.");
        var warehouseIds = new[] { request.SourceWarehouseId, request.DestinationWarehouseId }.Where(x => x.HasValue).Select(x => x!.Value).Concat(WarehouseIds(d));
        var warehouses = await Warehouses(warehouseIds, true, ct);
        var now = DateTime.UtcNow;
        var lines = new List<StockDocumentLine>();
        foreach (var input in request.Lines)
        {
            if (Qty(input.ExpectedQty) <= 0) throw Invalid("ExpectedQty must be positive.");
            var item = await Item(input.ItemId, ct); var factor = await Factor(item, input.UomId, ct);
            var line = new StockDocumentLine
            {
                LineNo = lines.Count + 1,
                ItemId = item.ItemId,
                UomId = input.UomId,
                RequestedQty = input.ExpectedQty,
                ActualQty = 0,
                BaseQty = 0,
                ConversionFactor = factor
            };
            if (type == "RETURN")
            {
                if (!input.OriginalIssueLineId.HasValue) throw Invalid("OriginalIssueLineId is required.");
                await Original(input.OriginalIssueLineId.Value, input.ItemId, ct);
                line.StockReturnLineDetailStockDocumentLine = new StockReturnLineDetail { OriginalIssueLineId = input.OriginalIssueLineId.Value };
            }
            else if (input.OriginalIssueLineId.HasValue) throw Invalid("OriginalIssueLineId is only valid for returns.");
            lines.Add(line);
        }
        if (type == "RETURN") foreach (var group in lines.GroupBy(x => x.StockReturnLineDetailStockDocumentLine!.OriginalIssueLineId))
            await ReturnCap(group.Key, group.Sum(x => Base(x.RequestedQty!.Value, x.ConversionFactor)), id, ct);
        if (id.HasValue)
        {
            var same = d.SourceWarehouseId == request.SourceWarehouseId && d.DestinationWarehouseId == request.DestinationWarehouseId &&
                d.DocumentDate == request.DocumentDate && d.Reason == request.Reason && d.Note == request.Note &&
                d.StockDocumentLines.OrderBy(x => x.LineNo).Select(x => (x.ItemId, x.UomId, x.RequestedQty, x.ConversionFactor)).SequenceEqual(lines.Select(x => (x.ItemId, x.UomId, x.RequestedQty, x.ConversionFactor)));
            if (type == "RETURN")
            {
                await db.Entry(d).Collection(x => x.StockDocumentLines).Query().Include(x => x.StockReturnLineDetailStockDocumentLine).LoadAsync(ct);
                same &= d.StockDocumentLines.OrderBy(x => x.LineNo).Select(x => x.StockReturnLineDetailStockDocumentLine?.OriginalIssueLineId).SequenceEqual(lines.Select(x => (long?)x.StockReturnLineDetailStockDocumentLine!.OriginalIssueLineId));
            }
            if (same) { await tx.CommitAsync(ct); return await GetDocument(d.DocumentId, ct); }
            foreach (var old in d.StockDocumentLines.ToList())
            {
                if (old.StockReturnLineDetailStockDocumentLine is { } detail) db.StockReturnLineDetails.Remove(detail);
                db.StockDocumentLines.Remove(old);
            }
            await Save(ct);
        }
        d.SourceWarehouseId = request.SourceWarehouseId; d.DestinationWarehouseId = request.DestinationWarehouseId;
        d.DocumentDate = request.DocumentDate; d.Reason = request.Reason; d.Note = request.Note;
        d.StockDocumentLines = lines;
        if (!id.HasValue) { d.CreatedAt = now; d.DocumentNo = await Number(type == "RECEIPT" ? "GRN" : type == "RETURN" ? "RTN" : "TRF", now, false, ct); db.StockDocuments.Add(d); }
        else db.Entry(d).Property(x => x.Status).IsModified = true;
        await DocumentAudit(d, request.ActorUserId, id.HasValue ? "UPDATE" : "CREATE", "DRAFT_SAVED", before, warehouses, now, ct);
        await tx.CommitAsync(ct); return await GetDocument(d.DocumentId, ct);
    }
    public async Task<InventoryDocumentDto?> Post(string type, long id, InventoryPost request, CancellationToken ct)
    {
        await using var tx = await Begin(request.ActorUserId, ct);
        var document = await PostCore(type, id, request, ct);
        if (document is null) return null;
        await tx.CommitAsync(ct);
        return await GetDocument(id, ct);
    }
    private async Task<StockDocument?> PostCore(string type, long id, InventoryPost request, CancellationToken ct)
    {
        var d = await Document(id, ct); if (d is null) return null;
        Version(d.RowVersion, request.ExpectedRowVersion);
        if (d.Status != "DRAFT" || d.DocumentType != type || type is not ("RECEIPT" or "RETURN")) throw Invalid("Only the matching DRAFT receipt/return may be posted.");
        if (request.Lines is null || request.Lines.Any(x => x is null) || request.Lines.Count != d.StockDocumentLines.Count || request.Lines.Select(x => x.LineId).Distinct().Count() != request.Lines.Count ||
            request.Lines.Any(x => !d.StockDocumentLines.Any(l => l.LineId == x.LineId))) throw Invalid("Supply every document line exactly once.");
        var warehouses = await Warehouses(WarehouseIds(d), true, ct);
        await StockGates(d.StockDocumentLines.Select(x => (d.DestinationWarehouseId!.Value, x.ItemId)), ct);
        var now = DateTime.UtcNow; var before = new { document = Scalars(d), lines = d.StockDocumentLines.Select(Scalars).ToList() };
        foreach (var input in request.Lines)
        {
            var l = d.StockDocumentLines.Single(x => x.LineId == input.LineId);
            Qty(input.AcceptedQty); Qty(input.RejectedQty); Qty(input.DamagedQty);
            var item = await Item(l.ItemId, ct); l.ConversionFactor = await Factor(item, l.UomId, ct);
            if (input.AcceptedQty != l.RequestedQty || input.RejectedQty > 0 || input.DamagedQty > 0) Reason(input.DiscrepancyReason);
            l.ActualQty = input.AcceptedQty; l.BaseQty = Base(input.AcceptedQty, l.ConversionFactor);
            if (d.DocumentType == "RECEIPT")
                l.StockReceiptLineDetail = new() { RejectedQty = input.RejectedQty, DamagedQty = input.DamagedQty, DiscrepancyReason = input.DiscrepancyReason };
            else
            {
                if (input.RejectedQty != 0) throw Invalid("Returns persist damaged quantity, not rejected quantity.");
                await db.Entry(l).Reference(x => x.StockReturnLineDetailStockDocumentLine).LoadAsync(ct);
                var detail = l.StockReturnLineDetailStockDocumentLine ?? throw Invalid("Return lineage is missing.");
                await Original(detail.OriginalIssueLineId, l.ItemId, ct);
                if (input.AcceptedQty + input.DamagedQty > l.RequestedQty) throw Invalid("Accepted usable + damaged exceeds requested return.");
                detail.DamagedQty = input.DamagedQty; detail.Note = input.DiscrepancyReason;
            }
            if (l.BaseQty > 0) db.StockMovements.Add(new() { DocumentLine = l, WarehouseId = d.DestinationWarehouseId!.Value, ItemId = l.ItemId, SignedBaseQty = l.BaseQty, CreatedBy = request.ActorUserId, OccurredAt = now });
        }
        if (d.DocumentType == "RETURN") foreach (var g in d.StockDocumentLines.GroupBy(x => x.StockReturnLineDetailStockDocumentLine!.OriginalIssueLineId))
            await ReturnCap(g.Key, g.Sum(x => Base(x.ActualQty + x.StockReturnLineDetailStockDocumentLine!.DamagedQty, x.ConversionFactor)), id, ct);
        d.Status = "POSTED"; d.PostedBy = request.ActorUserId; d.PostedAt = now;
        await DocumentAudit(d, request.ActorUserId, "UPDATE", "POSTED", before, warehouses, now, ct);
        return d;
    }
    public async Task<InventoryDocumentDto?> CancelDocument(long id, InventoryCommand request, CancellationToken ct)
    {
        var reason = Reason(request.Reason); await using var tx = await Begin(request.ActorUserId, ct);
        var d = await Document(id, ct); if (d is null) return null; Version(d.RowVersion, request.ExpectedRowVersion);
        if (d.Status != "DRAFT" || d.DocumentType is not ("RECEIPT" or "RETURN" or "TRANSFER")) throw Invalid("Only an eligible DRAFT may be cancelled.");
        var warehouses = await Warehouses(WarehouseIds(d), false, ct); var before = Scalars(d); var now = DateTime.UtcNow;
        d.Status = "CANCELLED"; d.CancelledAt = now; d.Reason = reason;
        await DocumentAudit(d, request.ActorUserId, "CANCEL", "CANCELLED", before, warehouses, now, ct);
        await tx.CommitAsync(ct); return await GetDocument(id, ct);
    }
    public async Task<InventoryDocumentDto?> Dispatch(long id, InventoryCommand request, CancellationToken ct)
    {
        await using var tx = await Begin(request.ActorUserId, ct); var d = await Document(id, ct); if (d is null) return null;
        Version(d.RowVersion, request.ExpectedRowVersion); if (d.Status != "DRAFT" || d.DocumentType != "TRANSFER") throw Invalid("Only a DRAFT transfer can be dispatched.");
        var warehouses = await Warehouses(WarehouseIds(d), true, ct);
        await StockGates(d.StockDocumentLines.SelectMany(x => WarehouseIds(d).Select(w => (w, x.ItemId))), ct);
        var before = Scalars(d); var now = DateTime.UtcNow;
        foreach (var l in d.StockDocumentLines)
        {
            var item = await Item(l.ItemId, ct); l.ConversionFactor = await Factor(item, l.UomId, ct);
            l.ActualQty = l.RequestedQty ?? throw Invalid("Dispatch quantity is missing.");
            if (Qty(l.ActualQty) <= 0) throw Invalid("Dispatch quantity must be positive."); l.BaseQty = Base(l.ActualQty, l.ConversionFactor);
        }
        foreach (var g in d.StockDocumentLines.GroupBy(x => x.ItemId))
            if (await Balance(d.SourceWarehouseId!.Value, g.Key, ct) < g.Sum(x => x.BaseQty)) throw Invalid("Insufficient stock.");
        foreach (var l in d.StockDocumentLines) db.StockMovements.Add(new() { DocumentLine = l, WarehouseId = d.SourceWarehouseId!.Value, ItemId = l.ItemId, SignedBaseQty = -l.BaseQty, OccurredAt = now, CreatedBy = request.ActorUserId });
        d.Status = "IN_TRANSIT";
        await DocumentAudit(d, request.ActorUserId, "UPDATE", "DISPATCHED", before, warehouses, now, ct);
        await tx.CommitAsync(ct); return await GetDocument(id, ct);
    }
    public async Task<InventoryDocumentDto?> Receive(long id, InventoryReceive request, CancellationToken ct)
    {
        await using var tx = await Begin(request.ActorUserId, ct); var d = await Document(id, ct); if (d is null) return null;
        Version(d.RowVersion, request.ExpectedRowVersion); if (d.DocumentType != "TRANSFER" || d.Status is not ("IN_TRANSIT" or "PARTIALLY_RECEIVED")) throw Invalid("Transfer must be in transit or partially received.");
        if (request.Lines is null || request.Lines.Any(x => x is null) || request.Lines.Count == 0 || request.Lines.Select(x => x.TransferLineId).Distinct().Count() != request.Lines.Count) throw Invalid("Supply distinct transfer lines for this batch.");
        var warehouses = await Warehouses(WarehouseIds(d), true, ct);
        await StockGates(d.StockDocumentLines.SelectMany(x => WarehouseIds(d).Select(w => (w, x.ItemId))), ct);
        var before = Scalars(d); var now = DateTime.UtcNow;
        var previous = await db.StockTransferReceiptLines.Where(x => x.TransferReceipt.TransferDocumentId == id).ToListAsync(ct);
        var batch = new StockTransferReceipt { TransferDocumentId = id, ReceivedBy = request.ActorUserId, ReceivedAt = now, Note = request.Note };
        foreach (var input in request.Lines)
        {
            var l = d.StockDocumentLines.SingleOrDefault(x => x.LineId == input.TransferLineId) ?? throw Invalid("Transfer line does not belong to this document.");
            var physical = Qty(input.ReceivedQty) + Qty(input.DamagedQty) + Qty(input.MissingQty);
            if (physical <= 0) throw Invalid("Batch entry must reconcile a positive quantity.");
            var reconciled = previous.Where(x => x.TransferLineId == l.LineId).Sum(x => x.ReceivedQty + x.DamagedQty + x.MissingQty);
            if (reconciled + physical > l.ActualQty) throw Invalid("Batch exceeds remaining dispatched quantity.");
            // Dispatch factor is immutable once stock effects exist.
            await Item(l.ItemId, ct);
            var detail = new StockTransferReceiptLine { TransferLineId = l.LineId, ReceivedQty = input.ReceivedQty, DamagedQty = input.DamagedQty, MissingQty = input.MissingQty, Note = input.Note };
            batch.StockTransferReceiptLines.Add(detail); previous.Add(detail);
            var usable = Base(input.ReceivedQty, l.ConversionFactor);
            if (usable > 0) db.StockMovements.Add(new() { DocumentLine = l, WarehouseId = d.DestinationWarehouseId!.Value, ItemId = l.ItemId, SignedBaseQty = usable, CreatedBy = request.ActorUserId, OccurredAt = now });
        }
        db.StockTransferReceipts.Add(batch);
        d.Status = d.StockDocumentLines.All(l => previous.Where(x => x.TransferLineId == l.LineId).Sum(x => x.ReceivedQty + x.DamagedQty + x.MissingQty) == l.ActualQty) ? "RECEIVED" : "PARTIALLY_RECEIVED";
        db.Entry(d).Property(x => x.Status).IsModified = true;
        if (d.Status == "RECEIVED") { d.PostedAt = now; d.PostedBy = request.ActorUserId; }
        await DocumentAudit(d, request.ActorUserId, "UPDATE", "BATCH_RECEIVED", before, warehouses, now, ct);
        await tx.CommitAsync(ct); return await GetDocument(id, ct);
    }
}
