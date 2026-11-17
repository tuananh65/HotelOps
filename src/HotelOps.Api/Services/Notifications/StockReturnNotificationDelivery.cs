using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Notifications;

internal sealed class StockReturnNotificationDelivery(HotelOpsDbContext db, TimeProvider clock, IOptions<NotificationCenterOptions> retention)
{
    internal async Task<long[]> PendingAsync(long after, CancellationToken ct)
    {
        var window = NotificationRetention.At(clock.GetUtcNow().UtcDateTime, retention.Value);
        return await db.AuditLogs.AsNoTracking().Where(a => a.AuditId > after && a.Action == "CREATE"
            && (a.EntityType == "stock_return_request_intents" || a.EntityType == "stock_return_notification_intents")
            && a.OccurredAt >= window.Cutoff && a.OccurredAt <= window.Now).OrderBy(a => a.AuditId).Select(a => a.AuditId).Take(100).ToArrayAsync(ct);
    }

    internal async Task<string> DeliverAsync(long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Independent notification context required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var source = await StockReturnNotificationSource.ReadAsync(db, id, ct);
        if (source == null) return "SOURCE_DENIED";
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!NotificationRetention.At(now, retention.Value).Contains(source.At)) return "EVENT_OUTSIDE_RETENTION";
        var candidates = source.Kind == StockReturnNotificationSource.Posted ? new[] { source.RequesterId }
            : await db.UserRoles.AsNoTracking().Where(r => r.Role.RoleCode == "STOREKEEPER" && r.IsActive && r.Role.IsActive
                && (r.BranchId == null || r.BranchId == source.BranchId) && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now))
                .Select(r => r.UserId).Distinct().ToArrayAsync(ct);
        await new UserEligibilityLock(db).LockAsync(candidates.Append(source.ActorId), ct);
        var resource = $"HotelOps:StockReturnNotification:{id}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
            IF @result<0 THROW 51844,'Return notification lock unavailable.',1;
            """, ct);
        var eligible = new List<long>();
        foreach (var user in candidates)
            if (await StockReturnNotificationSource.CanReadAsync(db, user, source, ct)) eligible.Add(user);
        if (eligible.Count == 0) return "NO_ELIGIBLE_RECIPIENT";
        var notices = await db.Notifications.Where(n => n.SourceType == StockReturnNotificationSource.SourceType && n.SourceId == id && n.NotificationType == source.Kind).ToArrayAsync(ct);
        if (notices.Length > 1 || notices.Any(n => n.CreatedBy != source.ActorId || n.CreatedAt != source.At)) throw new InvalidOperationException("Return notification identity conflict.");
        var notice = notices.SingleOrDefault();
        if (notice == null)
        {
            notice = new Notification { SourceType = StockReturnNotificationSource.SourceType, SourceId = id, NotificationType = source.Kind,
                Title = source.Kind == StockReturnNotificationSource.Submitted ? "Material return submitted" : "Material return posted",
                Message = "Open the return with your current permissions to review its recorded state and quantities.",
                Priority = "NORMAL", CreatedBy = source.ActorId, CreatedAt = source.At };
            db.Notifications.Add(notice); await db.SaveChangesAsync(ct);
        }
        foreach (var user in eligible)
        {
            var existing = await db.NotificationRecipients.AsNoTracking().Where(r => r.NotificationId == notice.NotificationId && r.UserId == user).ToArrayAsync(ct);
            if (existing.Length != 0)
            {
                if (existing.Length != 1 || existing[0].DeliveryChannel != "IN_APP" || existing[0].DeliveryStatus != "DELIVERED" || existing[0].DeliveredAt == null)
                    throw new InvalidOperationException("Return delivery identity conflict.");
                continue;
            }
            db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notice.NotificationId, UserId = user,
                DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
        }
        await db.SaveChangesAsync(ct);
        foreach (var user in eligible)
            if (!await StockReturnNotificationSource.CanReadAsync(db, user, source, ct)) return "RECIPIENT_CHANGED";
        if (await StockReturnNotificationSource.ReadAsync(db, id, ct) != source) return "SOURCE_CHANGED";
        await tx.CommitAsync(ct); db.ChangeTracker.Clear(); return "DELIVERED";
    }
}
