using System.Globalization;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Inventory;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal static class StockReturnNotificationSource
{
    internal const string SourceType = "STOCK_RETURN_EVENT";
    internal const string Submitted = "STOCK_RETURN_SUBMITTED";
    internal const string Posted = "STOCK_RETURN_POSTED";
    internal sealed record Source(long AuditId, long DocumentId, long RequesterId, long BranchId, long ActorId, DateTime At, string Kind);

    internal static async Task<Source?> ReadAsync(HotelOpsDbContext db, long id, CancellationToken ct)
    {
        var audit = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a => a.AuditId == id, ct);
        if (audit == null || audit.Action != "CREATE" || audit.OldValues != null || audit.ActorUserId is not > 0 || audit.BranchId is not > 0) return null;
        if (audit.EntityType == "stock_return_notification_intents")
        {
            var posted = await StockReturnPostedSource.ReadAsync(db, id, ct);
            return posted == null ? null : new(id, posted.DocumentId, posted.RequesterId, posted.BranchId, posted.ActorId, posted.At, Posted);
        }
        if (audit.EntityType != "stock_return_request_intents" || !long.TryParse(audit.EntityId, NumberStyles.None, CultureInfo.InvariantCulture, out var documentId)) return null;
        var document = await db.StockDocuments.AsNoTracking().Where(d => d.DocumentId == documentId && d.DocumentType == "RETURN"
            && d.Status == "DRAFT" && d.CreatedBy == audit.ActorUserId && d.CreatedAt == audit.OccurredAt
            && d.DestinationWarehouse != null && d.DestinationWarehouse.BranchId == audit.BranchId).SingleOrDefaultAsync(ct);
        if (document == null || audit.NewValues != await StockReturnApprovalStore.SnapshotAsync(db, documentId, ct)) return null;
        return new(id, documentId, document.CreatedBy, audit.BranchId.Value, document.CreatedBy, document.CreatedAt, Submitted);
    }

    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db, long user, Source source, CancellationToken ct)
    {
        if (source.Kind == Submitted) return await StockReturnReadAccess.StorekeeperAsync(db, user, source.DocumentId, ct);
        if (source.Kind != Posted || user != source.RequesterId || !await StockReturnReadAccess.RequesterAsync(db, user, source.BranchId, ct)) return false;
        if (!await db.StockDocuments.AsNoTracking().AnyAsync(d => d.DocumentId == source.DocumentId && d.CreatedBy == user
            && d.SourceWarehouseId == null && d.DestinationWarehouse != null && d.DestinationWarehouse.IsActive && d.DestinationWarehouse.BranchId == source.BranchId, ct)) return false;
        var origins = await db.StockDocumentLines.AsNoTracking().Where(l => l.DocumentId == source.DocumentId)
            .Select(l => (long?)l.StockReturnLineDetailStockDocumentLine!.OriginalIssueLine.Document.SourceWarehouse!.BranchId).ToArrayAsync(ct);
        return origins.Length > 0 && origins.All(branch => branch == source.BranchId);
    }

    internal static string Path(Source source) => source.Kind == Submitted
        ? $"/inventory/returns/{source.DocumentId}" : $"/inventory/return-requests/{source.DocumentId}";
}
