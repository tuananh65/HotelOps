using HotelOps.Api.Entities;
using HotelOps.Api.Services.MaterialRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed partial class InventoryService
{
    private async Task RequireReversal(long actor, StockDocument original, CancellationToken ct)
    {
        var warehouses = WarehouseIds(original).Concat(await db.StockMovements.AsNoTracking()
            .Where(m => m.DocumentLine.DocumentId == original.DocumentId).Select(m => m.WarehouseId).Distinct().ToArrayAsync(ct)).Distinct().ToArray();
        if (warehouses.Length == 0) throw new InventoryAccessException(403);
        foreach (var warehouse in warehouses)
            await (stocktakeAuthority ?? throw Invalid("Reversal authority unavailable.")).RequireAsync(actor, warehouse, "reverse", ct);
    }

    private async Task ReversalDependencies(StockDocument original, CancellationToken ct)
    {
        var lines = original.StockDocumentLines.Select(l => l.LineId).ToArray();
        if (await db.StockReturnLineDetails.AsNoTracking().AnyAsync(r => lines.Contains(r.OriginalIssueLineId)
                && r.StockDocumentLine.Document.Status != "CANCELLED", ct)
            || await db.MaintenanceMaterialUsages.AsNoTracking().AnyAsync(u => lines.Contains(u.StockDocumentLineId), ct)
            || await db.MaintenanceWorkLogs.AsNoTracking().AnyAsync(l => l.MaterialIssueId == original.DocumentId, ct))
            throw Invalid("MSG34: Dependent return or maintenance evidence prevents reversal.");
        if (original.PostedAt == null || original.PostedBy == null || original.StockDocumentLines.Count == 0)
            throw Invalid("MSG34: Posting provenance is incomplete.");
        var movements = await db.StockMovements.AsNoTracking().Where(m => m.DocumentLine.DocumentId == original.DocumentId).ToArrayAsync(ct);
        var warehouse = original.DocumentType == "ISSUE" ? original.SourceWarehouseId : original.DestinationWarehouseId;
        if (warehouse == null || (original.DocumentType == "ISSUE" ? original.DestinationWarehouseId != null : original.SourceWarehouseId != null))
            throw Invalid("MSG34: Original warehouse lineage is inconsistent.");
        foreach (var line in original.StockDocumentLines)
        {
            var expected = original.DocumentType == "ADJUSTMENT" ? line.RequestedQty : original.DocumentType == "ISSUE" ? -line.BaseQty : line.BaseQty;
            if (expected == null || line.ConversionFactor <= 0 || line.BaseQty < 0 || line.ActualQty < 0
                || line.BaseQty != line.ActualQty * line.ConversionFactor
                || original.DocumentType == "ADJUSTMENT" && Math.Abs(expected.Value) != line.BaseQty)
                throw Invalid("MSG34: Original quantity evidence is inconsistent.");
            var effects = movements.Where(m => m.DocumentLineId == line.LineId).ToArray();
            if (effects.Length != (expected == 0 ? 0 : 1) || effects.Any(m => m.ItemId != line.ItemId || m.WarehouseId != warehouse
                || m.SignedBaseQty != expected || m.CreatedBy != original.PostedBy || m.OccurredAt != original.PostedAt))
                throw Invalid("MSG34: Original ledger does not match its posted snapshot.");
        }
    }

    private async Task<MaterialRequest?> ReverseIssueRequest(StockDocument original, long actor, CancellationToken ct)
    {
        if (original.DocumentType != "ISSUE") return null;
        var fulfillment = await db.MaterialFulfillments.AsNoTracking().Include(f => f.MaterialFulfillmentLines)
            .SingleOrDefaultAsync(f => f.StockDocumentId == original.DocumentId, ct);
        if (fulfillment == null || original.ReferenceType != "MATERIAL_FULFILLMENT" || original.ReferenceId != fulfillment.FulfillmentId
            || fulfillment.ReceivedAt != null || fulfillment.ReceivedBy != null || fulfillment.MaterialFulfillmentLines.Any(l => l.ReceivedQty != null))
            throw Invalid("MSG34: Issue lineage is missing or a later receiving acknowledgement exists.");
        await Gate($"HotelOps:MaterialRequest:Request:{fulfillment.RequestId}", ct);
        var request = await db.MaterialRequests.FromSqlInterpolated($"SELECT * FROM dbo.material_requests WITH (UPDLOCK,HOLDLOCK) WHERE request_id={fulfillment.RequestId}")
            .Include(r => r.MaterialRequestLines).SingleAsync(ct);
        if (request.Status is not ("FULLY_ISSUED" or "PARTIALLY_ISSUED") || request.RemainderClosedAt != null
            || request.SourceWarehouseId != original.SourceWarehouseId
            || !await db.Warehouses.AnyAsync(w => w.WarehouseId == request.SourceWarehouseId && w.BranchId == request.BranchId, ct))
            throw Invalid("MSG34: Request lifecycle or warehouse scope prevents issue reversal.");
        var capabilities = await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN OBJECT_ID('dbo.internal_request_material_links','U') IS NOT NULL
              AND OBJECT_ID('dbo.internal_request_closures','U') IS NOT NULL
              AND OBJECT_ID('dbo.fs014_cleaning_blocker_resolutions','U') IS NOT NULL THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct);
        if (capabilities != 1) throw Invalid("MSG34: Dependent workflow schema is required to prove safe issue reversal.");
        if (await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.internal_request_material_links l
              JOIN dbo.internal_request_closures c ON c.internal_request_id=l.internal_request_id WHERE l.material_request_id={request.RequestId})
              OR EXISTS(SELECT 1 FROM dbo.fs014_cleaning_blocker_resolutions b WHERE b.material_request_id={request.RequestId} AND b.resolved_at IS NOT NULL)
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) != 0)
            throw Invalid("MSG34: Completed dependent work relies on this material issue.");
        var batches = await db.MaterialFulfillments.AsNoTracking().Include(f => f.MaterialFulfillmentLines)
            .Include(f => f.StockDocument).ThenInclude(d => d!.StockDocumentLines).Where(f => f.RequestId == request.RequestId).ToListAsync(ct);
        batches = await MaterialReversalEvidence.EffectiveAsync(db, batches, ct);
        MaterialRequestService.ValidateFulfillments(request, batches);
        foreach (var line in fulfillment.MaterialFulfillmentLines)
        {
            var target = request.MaterialRequestLines.Single(l => l.LineId == line.RequestLineId);
            if (target.FulfilledQty < line.IssuedQty) throw Invalid("MSG34: Request issued quantity is inconsistent.");
        }
        return request;
    }

    private async Task RecordIssueReversal(MaterialRequest request, StockDocument original, StockDocument reversal, long actor, DateTime now, CancellationToken ct)
    {
        var before = new { request.Status, request.CompletedAt, Lines = request.MaterialRequestLines.Select(l => new { l.LineId, l.FulfilledQty }).ToArray() };
        var lines = await db.MaterialFulfillmentLines.AsNoTracking().Where(l => l.Fulfillment.StockDocumentId == original.DocumentId).ToArrayAsync(ct);
        foreach (var line in lines) request.MaterialRequestLines.Single(l => l.LineId == line.RequestLineId).FulfilledQty -= line.IssuedQty;
        request.Status = request.MaterialRequestLines.Any(l => l.IsActive && l.FulfilledQty > 0) ? "PARTIALLY_ISSUED" : "APPROVED";
        request.CompletedAt = null; request.UpdatedAt = now;
        db.AuditLogs.Add(new AuditLog { EntityType = "material_requests", EntityId = request.RequestId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            BranchId = request.BranchId, ActorUserId = actor, Action = "UPDATE", OccurredAt = now,
            OldValues = System.Text.Json.JsonSerializer.Serialize(before), NewValues = System.Text.Json.JsonSerializer.Serialize(new {
                BusinessEvent = "ISSUE_REVERSED", OriginalDocumentId = original.DocumentId, ReversalDocumentId = reversal.DocumentId,
                State = new { request.Status, request.CompletedAt, Lines = request.MaterialRequestLines.Select(l => new { l.LineId, l.FulfilledQty }).ToArray() } }) });
    }
}
