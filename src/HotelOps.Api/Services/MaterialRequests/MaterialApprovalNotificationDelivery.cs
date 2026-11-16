using System.ComponentModel.DataAnnotations;
using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.MaterialRequests;

// The approval cycle is the durable intent: the writer persists it with request,
// line snapshots and audit. Rolled-back submissions cannot appear in this scan.
internal sealed class MaterialApprovalNotificationDelivery(HotelOpsDbContext db, IOptions<NotificationCenterOptions> retention)
{
    internal const string Kind = "MATERIAL_APPROVAL_REQUESTED";
    internal const string Source = "MATERIAL_APPROVAL_CYCLE";

    internal async Task<bool> InstalledAsync(CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.MaterialLifecycle.V1' AND CONVERT(nvarchar(20),value)='1')
              AND OBJECT_ID('dbo.material_request_approval_cycles','U') IS NOT NULL
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    private IQueryable<MaterialRequestApprovalCycle> Pending() => db.MaterialRequestApprovalCycles.AsNoTracking()
        .Where(c => c.Status == "PENDING" && c.ResolvedAt == null && c.Request.Status == "PENDING_APPROVAL"
            && c.AssignedApproverId != c.Request.RequesterId && c.AssignedApproverId != c.SubmittedBy
            && !db.MaterialRequestApprovalCycles.Any(later => later.RequestId == c.RequestId && later.RevisionNo > c.RevisionNo));

    internal async Task<long[]> PendingAsync(long after, CancellationToken ct)
    {
        if (!await InstalledAsync(ct)) return [];
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var cutoff = NotificationRetention.At(now, retention.Value).Cutoff;
        return await Pending().Where(c => c.ApprovalCycleId > after && c.SubmittedAt >= cutoff && c.SubmittedAt <= now
            && !db.NotificationRecipients.Any(r => r.Notification.SourceType == Source && r.Notification.SourceId == c.ApprovalCycleId
                && r.Notification.NotificationType == Kind && r.UserId == c.AssignedApproverId
                && r.DeliveryChannel == "IN_APP" && r.DeliveryStatus == "DELIVERED"))
            .OrderBy(c => c.ApprovalCycleId).Select(c => c.ApprovalCycleId).Take(100).ToArrayAsync(ct);
    }

    private async Task<bool> EligibleAsync(MaterialRequest request, long approver, DateTime now, CancellationToken ct)
    {
        try { await MaterialApprovalAuthority.ValidateAsync(db, request, approver, ct); }
        catch (ValidationException) { return false; }
        var scope = await MaterialRequestReadAccess.ReadAsync(db, approver, request.BranchId, now, ct);
        return await scope.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(r => r.RequestId == request.RequestId, ct);
    }

    internal async Task<string> DeliverAsync(long cycleId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()
            || System.Transactions.Transaction.Current != null || db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("Independent clean delivery context required.");
        if (!await InstalledAsync(ct)) return "SCHEMA_REQUIRED";
        var hint = await Pending().Where(c => c.ApprovalCycleId == cycleId)
            .Select(c => new { c.RequestId, c.Request.RequesterId, c.AssignedApproverId, c.SubmittedBy, c.SubmittedAt }).SingleOrDefaultAsync(ct);
        if (hint == null) return "SOURCE_UNAVAILABLE";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([hint.RequesterId, hint.AssignedApproverId, hint.SubmittedBy], ct);
        var request = await db.MaterialRequests.FromSqlInterpolated($"SELECT * FROM dbo.material_requests WITH(UPDLOCK,HOLDLOCK) WHERE request_id={hint.RequestId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        var current = await Pending().SingleOrDefaultAsync(c => c.ApprovalCycleId == cycleId, ct);
        if (request == null || request.RequesterId != hint.RequesterId || current == null || current.RequestId != hint.RequestId
            || current.AssignedApproverId != hint.AssignedApproverId || current.SubmittedBy != hint.SubmittedBy
            || current.SubmittedAt != hint.SubmittedAt) return "SOURCE_CHANGED";
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!NotificationRetention.At(now, retention.Value).Contains(current.SubmittedAt)) return "EVENT_OUTSIDE_RETENTION";
        if (!await EligibleAsync(request, current.AssignedApproverId, now, ct)) return "RECIPIENT_READ_DENIED";
        var notices = await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE source_type={Source} AND source_id={cycleId}")
            .AsNoTracking().ToArrayAsync(ct);
        if (notices.Length > 1 || notices.Any(n => n.NotificationType != Kind || n.CreatedBy != current.SubmittedBy || n.CreatedAt != current.SubmittedAt))
            throw new InvalidOperationException("Material approval notification provenance conflict.");
        var notice = notices.SingleOrDefault();
        if (notice == null)
        {
            notice = new Notification { NotificationType = Kind, SourceType = Source, SourceId = cycleId,
                Title = "Material request awaiting approval", Message = "A material request awaits your review. Current permission is required to open it.",
                Priority = "NORMAL", CreatedBy = current.SubmittedBy, CreatedAt = current.SubmittedAt };
            db.Notifications.Add(notice);
            await db.SaveChangesAsync(ct);
        }
        var receipts = await db.NotificationRecipients.AsNoTracking().Where(r => r.NotificationId == notice.NotificationId).ToArrayAsync(ct);
        if (receipts.Length > 1 || receipts.Any(r => r.UserId != current.AssignedApproverId || r.DeliveryChannel != "IN_APP"
            || r.DeliveryStatus != "DELIVERED" || r.DeliveredAt == null))
            throw new InvalidOperationException("Material approval notification receipt conflict.");
        if (receipts.Length == 0)
        {
            db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notice.NotificationId, UserId = current.AssignedApproverId,
                DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
            await db.SaveChangesAsync(ct);
        }
        now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await EligibleAsync(request, current.AssignedApproverId, now, ct)) return "RECIPIENT_READ_DENIED";
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return "DELIVERED";
    }
}
