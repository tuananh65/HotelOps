using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed class TaskReopenNotificationDelivery(HotelOpsDbContext db, TimeProvider clock)
{
    internal async Task<long[]> ReadPendingAsync(long after, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        if (!await TaskReopenStore.InstalledAsync(db, ct)) return [];
        return await TaskReopenStore.Query(db).Where(e => e.AuditId > after && !db.Notifications.Any(n => n.SourceType == "TASK_REOPEN"
            && n.SourceId == e.AuditId && n.NotificationType == "TASK_REOPENED" && n.NotificationRecipients.Any(r => r.UserId == e.AssigneeId
                && r.DeliveryChannel == "IN_APP" && r.DeliveryStatus == "DELIVERED"))).OrderBy(e => e.AuditId).Take(limit).Select(e => e.AuditId).ToArrayAsync(ct);
    }
    internal async Task<string> DeliverAsync(long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any() || System.Transactions.Transaction.Current != null || db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("Independent clean delivery context required.");
        if (!await TaskReopenStore.InstalledAsync(db, ct)) return "SCHEMA_REQUIRED";
        var hint = await TaskReopenStore.Query(db).SingleOrDefaultAsync(s => s.AuditId == id, ct);
        if (hint == null) return "SOURCE_DENIED";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([hint.ReopenedBy, hint.AssigneeId], ct);
        var row = await TaskReopenStore.Query(db).SingleOrDefaultAsync(s => s.AuditId == id, ct);
        if (row == null || row.AssigneeId != hint.AssigneeId || row.TaskId != hint.TaskId || row.AssignmentId != hint.AssignmentId) return "SOURCE_DENIED";
        var now = clock.GetUtcNow().UtcDateTime;
        if (!await TaskAssignmentEligibility.Candidates(db, row.BranchId, row.DepartmentId, now).AnyAsync(u => u.UserId == row.AssigneeId, ct)
            || !await TaskVerificationStore.ExecutorReadAsync(db, row.AssigneeId, row.BranchId, row.TaskId, now, ct)) return "RECIPIENT_READ_DENIED";
        var notices = await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE source_type='TASK_REOPEN' AND source_id={id}").AsNoTracking().ToArrayAsync(ct);
        if (notices.Length > 1 || notices.Any(n => n.NotificationType != "TASK_REOPENED" || n.CreatedBy != row.ReopenedBy || n.CreatedAt != row.ReopenedAt))
            throw new InvalidOperationException("Reopen notification receipt conflict.");
        var notice = notices.SingleOrDefault();
        if (notice == null)
        {
            notice = new Notification { NotificationType = "TASK_REOPENED", SourceType = "TASK_REOPEN", SourceId = id,
                Title = "Task reopened", Message = "A closed task has been reopened and assigned to you. Review the remediation instructions and accept it before starting work.",
                Priority = "NORMAL", CreatedBy = row.ReopenedBy, CreatedAt = row.ReopenedAt };
            db.Notifications.Add(notice); await db.SaveChangesAsync(ct);
        }
        var receipts = await db.NotificationRecipients.AsNoTracking().Where(r => r.NotificationId == notice.NotificationId && r.UserId == row.AssigneeId).ToArrayAsync(ct);
        if (receipts.Length > 0)
        {
            if (receipts.Length != 1 || receipts[0].DeliveryChannel != "IN_APP" || receipts[0].DeliveryStatus != "DELIVERED" || receipts[0].DeliveredAt == null)
                throw new InvalidOperationException("Reopen delivery receipt conflict.");
        }
        else
        {
            db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notice.NotificationId, UserId = row.AssigneeId, DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct); db.ChangeTracker.Clear();
        return "DELIVERED";
    }
}
