using System.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

public sealed partial class ProvisionalCleaningDelivery
{
    private sealed class CancellationIntent
    {
        public long Id { get; set; }
        public long User { get; set; }
        public Guid Publication { get; set; }
    }

    internal async Task<string> DeliverCancellationOnlyAsync(CancellationToken ct)
    {
        if (!await candidate.CancellationAvailableAsync(ct)) return "DISABLED";
        var selected = await db.Database.SqlQuery<CancellationIntent>($"""
            SELECT TOP(1) i.intent_id AS Id,i.user_id AS [User],i.publication_id AS Publication
            FROM dbo.notification_delivery_intents i JOIN dbo.notification_publications p ON p.publication_id=i.publication_id
            WHERE p.event_type='CLEANING_TASK_CANCELLED' AND i.status='PENDING'
              AND i.delivery_channel='IN_APP' AND i.next_attempt_at<=SYSUTCDATETIME() ORDER BY i.intent_id
            """).SingleOrDefaultAsync(ct);
        if (selected == null) return "EMPTY";
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await new UserEligibilityLock(db).LockAsync([selected.User], ct);
            if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.notification_delivery_intents WITH(UPDLOCK,HOLDLOCK) WHERE intent_id={selected.Id} AND status='PENDING'").SingleAsync(ct) != 1) return "EMPTY";
            var proven = await CleaningCancellationNotificationSource.LoadAsync(db, selected.Publication, selected.User, ct);
            if (proven == null || !await CleaningCancellationRecipientAccess.AllowsAsync(db, selected.User,
                proven.Value.Receipt, clock.GetUtcNow().UtcDateTime, ct))
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_delivery_intents SET status='TERMINAL',failure_code='RECIPIENT_OR_SOURCE_DENIED',attempt_count=attempt_count+1 WHERE intent_id={selected.Id}", ct);
                await tx.CommitAsync(ct); return "RECIPIENT_OR_SOURCE_DENIED";
            }
            var r = proven.Value.Receipt;
            var notice = new Notification { NotificationType="CLEANING_TASK_CANCELLED",Title="Cleaning task cancelled",
                Message="Your cleaning assignment was cancelled.",Priority="NORMAL",SourceType="CLEANING_TASK",SourceId=r.Id,
                ActionUrl="/notifications",
                CreatedBy=proven.Value.Actor,CreatedAt=clock.GetUtcNow().UtcDateTime };
            db.Notifications.Add(notice); await db.SaveChangesAsync(ct);
            db.NotificationRecipients.Add(new() { NotificationId=notice.NotificationId,UserId=selected.User,
                DeliveryChannel="IN_APP",DeliveryStatus="DELIVERED",DeliveredAt=notice.CreatedAt });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_delivery_intents SET status='DELIVERED',inbox_notification_id={notice.NotificationId},attempt_count=attempt_count+1,failure_code=NULL WHERE intent_id={selected.Id}",ct);
            await tx.CommitAsync(ct); return "DELIVERED";
        }
        catch (Exception error) when (!ct.IsCancellationRequested && error is System.Data.Common.DbException or DbUpdateException)
        {
            db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_delivery_intents SET attempt_count=attempt_count+1,next_attempt_at=DATEADD(second,5,SYSUTCDATETIME()),failure_code='TRANSIENT_DELIVERY_ERROR' WHERE intent_id={selected.Id} AND status='PENDING'",ct);
            return "RETRY_SCHEDULED";
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
