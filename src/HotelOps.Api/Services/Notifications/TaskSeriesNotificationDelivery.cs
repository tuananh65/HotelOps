using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed class TaskSeriesNotificationDelivery(HotelOpsDbContext db, TimeProvider clock)
{
    internal async Task<long[]> ReadPendingAsync(long after, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        if (!await TaskSeriesStore.InstalledAsync(db, ct)) return [];
        return await TaskSeriesNotificationStore.Query(db).Where(e => e.AuditId > after && !db.Notifications.Any(n => n.SourceType == "TASK_SERIES"
            && n.SourceId == e.AuditId && n.NotificationType == "TASK_SERIES_STOPPED" && n.NotificationRecipients.Any(r => r.UserId == e.RecipientId
                && r.DeliveryChannel == "IN_APP" && r.DeliveryStatus == "DELIVERED"))).OrderBy(e => e.AuditId).Take(limit).Select(e => e.AuditId).ToArrayAsync(ct);
    }
    internal async Task<string> DeliverAsync(long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any() || System.Transactions.Transaction.Current != null || db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("Independent clean delivery context required.");
        if (!await TaskSeriesStore.InstalledAsync(db, ct)) return "SCHEMA_REQUIRED";
        var hint = await TaskSeriesNotificationStore.Query(db).SingleOrDefaultAsync(s => s.AuditId == id, ct);
        if (hint == null) return "SOURCE_DENIED";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([hint.ActorId, hint.RecipientId], ct);
        var row = await TaskSeriesNotificationStore.Query(db).SingleOrDefaultAsync(s => s.AuditId == id, ct);
        if (row == null || row.RecipientId != hint.RecipientId || row.TaskId != hint.TaskId || row.SeriesId != hint.SeriesId) return "SOURCE_DENIED";
        var now = clock.GetUtcNow().UtcDateTime;
        if (!await TaskAssignmentEligibility.Candidates(db, row.BranchId, row.DepartmentId, now).AnyAsync(u => u.UserId == row.RecipientId, ct)
            || !await TaskVerificationStore.ExecutorReadAsync(db, row.RecipientId, row.BranchId, row.TaskId, now, ct)) return "RECIPIENT_READ_DENIED";
        var notices = await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE source_type='TASK_SERIES' AND source_id={id}").AsNoTracking().ToArrayAsync(ct);
        if (notices.Length > 1 || notices.Any(n => n.NotificationType != "TASK_SERIES_STOPPED" || n.CreatedBy != row.ActorId || n.CreatedAt != row.At))
            throw new InvalidOperationException("Series notification receipt conflict.");
        var notice = notices.SingleOrDefault();
        if (notice == null)
        {
            notice = new Notification { NotificationType = "TASK_SERIES_STOPPED", SourceType = "TASK_SERIES", SourceId = id,
                Title = "Recurring task series stopped", Message = "Future occurrences of this task series have stopped. Your existing task remains unchanged.",
                Priority = "NORMAL", CreatedBy = row.ActorId, CreatedAt = row.At };
            db.Notifications.Add(notice); await db.SaveChangesAsync(ct);
        }
        var receipts = await db.NotificationRecipients.AsNoTracking().Where(r => r.NotificationId == notice.NotificationId && r.UserId == row.RecipientId).ToArrayAsync(ct);
        if (receipts.Length > 0)
        {
            if (receipts.Length != 1 || receipts[0].DeliveryChannel != "IN_APP" || receipts[0].DeliveryStatus != "DELIVERED" || receipts[0].DeliveredAt == null)
                throw new InvalidOperationException("Series delivery receipt conflict.");
        }
        else
        {
            db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notice.NotificationId, UserId = row.RecipientId, DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct); db.ChangeTracker.Clear();
        return "DELIVERED";
    }
}
