using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Notifications;

internal sealed class UnscheduledWorkdayDelivery(HotelOpsDbContext db, TimeProvider clock, IOptions<NotificationCenterOptions> retention)
{
    internal async Task<long[]> PendingAsync(long after, CancellationToken ct)
    {
        if (!await WorkdayMembershipStore.InstalledAsync(db, ct)) return [];
        var window = NotificationRetention.At(clock.GetUtcNow().UtcDateTime, retention.Value);
        return await db.Database.SqlQuery<long>($"""
            SELECT TOP(100) workday_id AS Value FROM dbo.unscheduled_workday_notifications
            WHERE status IN('PENDING','NO_RECIPIENT') AND workday_id>{after}
              AND created_at>={window.Cutoff} AND created_at<={window.Now}
            ORDER BY workday_id
            """).ToArrayAsync(ct);
    }

    internal async Task DeliverAsync(long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Independent delivery context required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var pending = await db.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM dbo.unscheduled_workday_notifications WITH(UPDLOCK,HOLDLOCK)
            WHERE workday_id={id} AND status IN('PENDING','NO_RECIPIENT')
            """).AnyAsync(ct);
        if (!pending) return;
        var day = await db.Workdays.AsNoTracking().SingleOrDefaultAsync(d => d.WorkdayId == id && d.IsUnscheduled, ct);
        if (day is null) return;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!NotificationRetention.At(now, retention.Value).Contains(day.StartedAt)) return;
        var head = await UnscheduledWorkdayAccess.HeadAsync(db, day.BranchId, day.DepartmentId, now, ct);
        if (head is null)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.unscheduled_workday_notifications SET status='NO_RECIPIENT',last_attempt_at={now} WHERE workday_id={id}", ct);
            await tx.CommitAsync(ct);
            return;
        }
        var notification = new Notification
        {
            NotificationType = "WORKDAY_UNSCHEDULED", SourceType = "UNSCHEDULED_WORKDAY", SourceId = id,
            Title = "Unscheduled workday started", Message = "An employee started an unscheduled workday. Open the record to review it.",
            Priority = "NORMAL", CreatedBy = day.UserId, CreatedAt = day.StartedAt
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
        db.NotificationRecipients.Add(new NotificationRecipient
        {
            NotificationId = notification.NotificationId, UserId = head.Value, DeliveryChannel = "IN_APP",
            DeliveryStatus = "DELIVERED", DeliveredAt = now
        });
        await db.SaveChangesAsync(ct);
        var finalNow = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (await UnscheduledWorkdayAccess.HeadAsync(db, day.BranchId, day.DepartmentId, finalNow, ct) != head) return;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.unscheduled_workday_notifications SET status='DELIVERED',last_attempt_at={finalNow},notification_id={notification.NotificationId}
            WHERE workday_id={id}
            """, ct);
        await tx.CommitAsync(ct);
    }
}
