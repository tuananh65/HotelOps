using System.ComponentModel.DataAnnotations;
using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.MaterialRequests;

internal sealed partial class MaterialApprovalNotificationDelivery
{
    internal const string CancellationKind = "MATERIAL_REQUEST_CANCELLED";
    internal const string CancellationSource = "MATERIAL_CANCELLATION";

    // Persisted with cancellation and audit; delivery only sees committed intent.
    internal static void EnqueueCancellation(HotelOpsDbContext db, MaterialRequest request) => db.Notifications.Add(new Notification
    {
        NotificationType = CancellationKind, SourceType = CancellationSource, SourceId = request.RequestId,
        Title = "Material request cancelled", Message = "A material request submitted for your review was cancelled. Current permission is required to open it.",
        Priority = "NORMAL", CreatedBy = request.RequesterId, CreatedAt = request.UpdatedAt!.Value
    });

    internal async Task<long[]> PendingCancellationsAsync(long after, CancellationToken ct)
    {
        if (!await InstalledAsync(ct)) return [];
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var cutoff = NotificationRetention.At(now, retention.Value).Cutoff;
        return await db.Notifications.AsNoTracking().Where(n => n.NotificationId > after
            && n.NotificationType == CancellationKind && n.SourceType == CancellationSource
            && n.CreatedAt >= cutoff && n.CreatedAt <= now
            && !n.NotificationRecipients.Any(r => r.DeliveryChannel == "IN_APP" && r.DeliveryStatus == "DELIVERED"))
            .OrderBy(n => n.NotificationId).Select(n => n.NotificationId).Take(100).ToArrayAsync(ct);
    }

    // Caller supplies a Serializable transaction, both during delivery and source opening.
    internal static async Task<long?> CancellationTargetAsync(HotelOpsDbContext db, long noticeId, long recipient, DateTime now, CancellationToken ct)
    {
        var notice = await db.Notifications.AsNoTracking().SingleOrDefaultAsync(n => n.NotificationId == noticeId
            && n.NotificationType == CancellationKind && n.SourceType == CancellationSource, ct);
        if (notice?.SourceId is not long requestId || notice.CreatedBy is not long requester) return null;
        await new UserEligibilityLock(db).LockAsync([requester, recipient], ct);
        var request = await db.MaterialRequests.AsNoTracking().SingleOrDefaultAsync(r => r.RequestId == requestId, ct);
        if (request == null || request.RequesterId != requester || request.Status != "CANCELLED"
            || request.UpdatedAt != notice.CreatedAt || string.IsNullOrWhiteSpace(request.CancellationReason)) return null;
        var assigned = await db.MaterialRequestApprovalCycles.AsNoTracking().Where(c => c.RequestId == requestId)
            .OrderByDescending(c => c.RevisionNo).Select(c => (long?)c.AssignedApproverId).FirstOrDefaultAsync(ct);
        if (assigned != recipient) return null;
        try { await MaterialApprovalAuthority.ValidateAsync(db, request, recipient, ct); }
        catch (ValidationException) { return null; }
        var scope = await MaterialRequestReadAccess.ReadAsync(db, recipient, request.BranchId, now, ct);
        return await scope.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(r => r.RequestId == requestId, ct)
            && await MaterialReadProvenance.ValidAsync(db, requestId, request.BranchId, ct) ? requestId : null;
    }

    internal async Task<string> DeliverCancellationAsync(long noticeId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()
            || System.Transactions.Transaction.Current != null || db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("Independent clean delivery context required.");
        if (!await InstalledAsync(ct)) return "SCHEMA_REQUIRED";
        var hint = await db.Notifications.AsNoTracking().SingleOrDefaultAsync(n => n.NotificationId == noticeId
            && n.NotificationType == CancellationKind && n.SourceType == CancellationSource, ct);
        if (hint == null) return "SOURCE_UNAVAILABLE";
        var recipient = await db.MaterialRequestApprovalCycles.AsNoTracking().Where(c => c.RequestId == hint.SourceId)
            .OrderByDescending(c => c.RevisionNo).Select(c => (long?)c.AssignedApproverId).FirstOrDefaultAsync(ct);
        if (recipient == null) return "SOURCE_UNAVAILABLE";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!NotificationRetention.At(now, retention.Value).Contains(hint.CreatedAt)) return "EVENT_OUTSIDE_RETENTION";
        if (await CancellationTargetAsync(db, noticeId, recipient.Value, now, ct) == null) return "RECIPIENT_READ_DENIED";
        var locked = await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE notification_id={noticeId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (locked == null || locked.SourceId != hint.SourceId || locked.CreatedAt != hint.CreatedAt || locked.CreatedBy != hint.CreatedBy) return "SOURCE_CHANGED";
        var receipts = await db.NotificationRecipients.AsNoTracking().Where(r => r.NotificationId == noticeId).ToArrayAsync(ct);
        if (receipts.Length > 1 || receipts.Any(r => r.UserId != recipient || r.DeliveryChannel != "IN_APP"
            || r.DeliveryStatus != "DELIVERED" || r.DeliveredAt == null)) throw new InvalidOperationException("Cancellation receipt conflict.");
        if (receipts.Length == 0)
        {
            db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = noticeId, UserId = recipient.Value,
                DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
            await db.SaveChangesAsync(ct);
        }
        now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (await CancellationTargetAsync(db, noticeId, recipient.Value, now, ct) == null) return "RECIPIENT_READ_DENIED";
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return "DELIVERED";
    }
}
