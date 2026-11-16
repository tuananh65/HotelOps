using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.MaterialRequests;

internal sealed partial class MaterialApprovalNotificationDelivery
{
    internal const string DecisionKind = "MATERIAL_REQUEST_DECIDED";
    internal const string DecisionSource = "MATERIAL_APPROVAL_DECISION";

    // The committed decision receipt is durable intent, including earlier cycles.
    private static IQueryable<MaterialRequestApproval> Decisions(HotelOpsDbContext db) => db.MaterialRequestApprovals.AsNoTracking()
        .Where(d => d.ApprovalCycle != null && d.ApprovalCycle.RequestId == d.RequestId
            && d.ApprovalCycle.AssignedApproverId == d.ApproverId && d.ApproverId != d.Request.RequesterId
            && d.ApprovalCycle.ResolvedAt == d.DecidedAt
            && (d.Decision == "RETURNED" || d.Decision == "APPROVED" || d.Decision == "REJECTED"));

    internal async Task<long[]> PendingDecisionsAsync(long after, CancellationToken ct)
    {
        if (!await InstalledAsync(ct)) return [];
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var cutoff = NotificationRetention.At(now, retention.Value).Cutoff;
        return await Decisions(db).Where(d => d.ApprovalId > after && d.DecidedAt >= cutoff && d.DecidedAt <= now
            && !db.NotificationRecipients.Any(r => r.Notification.SourceType == DecisionSource
                && r.Notification.SourceId == d.ApprovalId && r.Notification.NotificationType == DecisionKind
                && r.UserId == d.Request.RequesterId && r.DeliveryChannel == "IN_APP" && r.DeliveryStatus == "DELIVERED"))
            .OrderBy(d => d.ApprovalId).Select(d => d.ApprovalId).Take(100).ToArrayAsync(ct);
    }

    internal static async Task<long?> DecisionTargetAsync(HotelOpsDbContext db, long decisionId, long user, CancellationToken ct)
    {
        var locked = await new UserEligibilityLock(db).LockAsync([user], ct);
        if (!locked.TryGetValue(user, out var account) || account is not { DeletedAt: null, AccountStatus: "ACTIVE", EmploymentStatus: "ACTIVE" }) return null;
        var decision = await Decisions(db).Include(d => d.Request).SingleOrDefaultAsync(d => d.ApprovalId == decisionId, ct);
        if (decision == null || decision.Request.RequesterId != user) return null;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var scope = await MaterialRequestReadAccess.ReadAsync(db, user, decision.Request.BranchId, now, ct);
        return await scope.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(r => r.RequestId == decision.RequestId, ct)
            && await MaterialReadProvenance.ValidAsync(db, decision.RequestId, decision.Request.BranchId, ct) ? decision.RequestId : null;
    }

    internal async Task<string> DeliverDecisionAsync(long decisionId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()
            || System.Transactions.Transaction.Current != null || db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("Independent clean delivery context required.");
        if (!await InstalledAsync(ct)) return "SCHEMA_REQUIRED";
        var hint = await Decisions(db).Where(d => d.ApprovalId == decisionId)
            .Select(d => new { d.RequestId, d.Request.RequesterId, d.ApproverId, d.DecidedAt, d.Decision }).SingleOrDefaultAsync(ct);
        if (hint == null) return "SOURCE_UNAVAILABLE";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await DecisionTargetAsync(db, decisionId, hint.RequesterId, ct) != hint.RequestId) return "RECIPIENT_READ_DENIED";
        var current = await Decisions(db).SingleAsync(d => d.ApprovalId == decisionId, ct);
        if (current.ApproverId != hint.ApproverId || current.DecidedAt != hint.DecidedAt || current.Decision != hint.Decision) return "SOURCE_CHANGED";
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!NotificationRetention.At(now, retention.Value).Contains(current.DecidedAt)) return "EVENT_OUTSIDE_RETENTION";
        var notices = await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE source_type={DecisionSource} AND source_id={decisionId}")
            .AsNoTracking().ToArrayAsync(ct);
        if (notices.Length > 1 || notices.Any(n => n.NotificationType != DecisionKind || n.CreatedBy != current.ApproverId || n.CreatedAt != current.DecidedAt))
            throw new InvalidOperationException("Material decision notification provenance conflict.");
        var notice = notices.SingleOrDefault();
        if (notice == null)
        {
            notice = new Notification { NotificationType = DecisionKind, SourceType = DecisionSource, SourceId = decisionId,
                Title = "Material request review updated", Message = "A review decision was recorded for your material request. Open it to see the decision and current status.",
                Priority = "NORMAL", CreatedBy = current.ApproverId, CreatedAt = current.DecidedAt };
            db.Notifications.Add(notice);
            await db.SaveChangesAsync(ct);
        }
        var receipts = await db.NotificationRecipients.AsNoTracking().Where(r => r.NotificationId == notice.NotificationId).ToArrayAsync(ct);
        if (receipts.Length > 1 || receipts.Any(r => r.UserId != hint.RequesterId || r.DeliveryChannel != "IN_APP"
            || r.DeliveryStatus != "DELIVERED" || r.DeliveredAt == null)) throw new InvalidOperationException("Material decision receipt conflict.");
        if (receipts.Length == 0)
        {
            db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notice.NotificationId, UserId = hint.RequesterId,
                DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
            await db.SaveChangesAsync(ct);
        }
        if (await DecisionTargetAsync(db, decisionId, hint.RequesterId, ct) != hint.RequestId) return "RECIPIENT_READ_DENIED";
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return "DELIVERED";
    }
}
