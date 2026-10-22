using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed partial class InventoryService : IInventoryIssuePosting
{
    public async Task<InventoryIssueResult> PostIssue(InventoryIssueRequest r, CancellationToken ct)
    {
        if (r.DocumentDate == default) throw Invalid("DocumentDate is required.");
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("FE-09 must own the transaction and DbContext.");
        await Gate("HotelOps:Inventory:Catalog", ct); await Actor(r.ActorUserId, ct);
        await Gate($"HotelOps:Inventory:Fulfillment:{r.FulfillmentId}", ct);
        var fulfillment = await db.MaterialFulfillments.AsNoTracking().SingleOrDefaultAsync(x => x.FulfillmentId == r.FulfillmentId, ct)
            ?? throw Invalid("Fulfillment does not exist.");
        if (fulfillment.WarehouseId != r.WarehouseId || fulfillment.StockDocumentId.HasValue ||
            await db.StockDocuments.AnyAsync(x => x.ReferenceType == "MATERIAL_FULFILLMENT" && x.ReferenceId == r.FulfillmentId, ct)) throw Invalid("Fulfillment warehouse mismatch or already issued.");
        if (r.Lines is null || r.Lines.Any(x => x is null) || r.Lines.Count == 0 || r.Lines.Select(l => l.RequestLineId).Distinct().Count() != r.Lines.Count) throw Invalid("Distinct request-line correlations are required.");
        var warehouses = await Warehouses([r.WarehouseId], true, ct);
        var d = new StockDocument
        {
            DocumentType = "ISSUE",
            Status = "POSTED",
            SourceWarehouseId = r.WarehouseId,
            CreatedBy = r.ActorUserId,
            PostedBy = r.ActorUserId,
            DocumentDate = r.DocumentDate,
            ReferenceType = "MATERIAL_FULFILLMENT",
            ReferenceId = r.FulfillmentId
        };
        foreach (var input in r.Lines)
        {
            if (!await db.MaterialRequestLines.AnyAsync(x => x.LineId == input.RequestLineId && x.RequestId == fulfillment.RequestId && x.ItemId == input.ItemId, ct)) throw Invalid("Request-line correlation is invalid.");
            if (Qty(input.Quantity) <= 0) throw Invalid("Issue quantity must be positive.");
            var item = await Item(input.ItemId, ct); var factor = await Factor(item, input.UomId, ct);
            d.StockDocumentLines.Add(new() { LineNo = d.StockDocumentLines.Count + 1, ItemId = input.ItemId, UomId = input.UomId, ActualQty = input.Quantity, RequestedQty = input.Quantity, ConversionFactor = factor, BaseQty = Base(input.Quantity, factor) });
        }
        await StockGates(d.StockDocumentLines.Select(x => (r.WarehouseId, x.ItemId)), ct);
        foreach (var g in d.StockDocumentLines.GroupBy(x => x.ItemId)) if (await Balance(r.WarehouseId, g.Key, ct) < g.Sum(x => x.BaseQty)) throw Invalid("Insufficient stock.");
        var now = DateTime.UtcNow; d.CreatedAt = now; d.PostedAt = now; d.DocumentNo = await Number("ISS", now, false, ct);
        db.StockDocuments.Add(d);
        foreach (var line in d.StockDocumentLines) db.StockMovements.Add(new() { DocumentLine = line, WarehouseId = r.WarehouseId, ItemId = line.ItemId, SignedBaseQty = -line.BaseQty, CreatedBy = r.ActorUserId, OccurredAt = now });
        await DocumentAudit(d, r.ActorUserId, "CREATE", "ISSUE_POSTED", null, warehouses, now, ct);
        return new(d.DocumentId, d.StockDocumentLines.OrderBy(x => x.LineNo).Select((x, i) => new InventoryIssueCorrelation(r.Lines[i].RequestLineId, x.LineId)).ToList());
    }
}
