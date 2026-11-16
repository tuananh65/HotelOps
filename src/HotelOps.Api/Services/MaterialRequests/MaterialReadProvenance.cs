using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.MaterialRequests;

// MR_VIEW cannot disclose another request's lines or another branch's stock documents
// through malformed legacy links. Called under the read contract's transaction.
internal static class MaterialReadProvenance
{
    internal static async Task<bool> ValidAsync(HotelOpsDbContext db, long request, long branch, CancellationToken ct)
    {
        if (await db.MaterialRequestApprovalLineSnapshots.AsNoTracking().AnyAsync(s => s.ApprovalCycle.RequestId == request && s.RequestLine.RequestId != request, ct)
            || await db.MaterialRequestApprovals.AsNoTracking().AnyAsync(a => a.RequestId == request && a.ApprovalCycle != null && a.ApprovalCycle.RequestId != request, ct)
            || await db.MaterialFulfillmentLines.AsNoTracking().AnyAsync(l => l.Fulfillment.RequestId == request && l.RequestLine.RequestId != request, ct)
            || await db.MaterialFulfillments.AsNoTracking().AnyAsync(f => f.RequestId == request && (f.Warehouse.BranchId != branch
                || f.StockDocument != null && (f.StockDocument.DocumentType != "ISSUE" || f.StockDocument.ReferenceType != "MATERIAL_FULFILLMENT"
                    || f.StockDocument.ReferenceId != f.FulfillmentId || f.StockDocument.SourceWarehouseId != f.WarehouseId
                    || f.StockDocument.DestinationWarehouse != null && f.StockDocument.DestinationWarehouse.BranchId != branch)), ct)) return false;
        var documents = await db.MaterialFulfillments.AsNoTracking().Where(f => f.RequestId == request && f.StockDocumentId != null)
            .Select(f => f.StockDocumentId!.Value).ToArrayAsync(ct);
        if (documents.Length == 0) return true;
        return !await db.StockReturnLineDetails.AsNoTracking().AnyAsync(r => documents.Contains(r.OriginalIssueLine.DocumentId)
            && (r.StockDocumentLine.Document.DocumentType != "RETURN"
                || r.StockDocumentLine.Document.DestinationWarehouse == null || r.StockDocumentLine.Document.DestinationWarehouse.BranchId != branch
                || r.StockDocumentLine.Document.SourceWarehouse != null && r.StockDocumentLine.Document.SourceWarehouse.BranchId != branch), ct);
    }
}
