using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Notifications;

internal sealed class InternalRequestAlertDelivery(HotelOpsDbContext db, IOptions<NotificationCenterOptions> retention)
{
    internal const string Kind = "WORK_ALERT", Source = "WORK_ALERT_EPISODE";

    internal async Task<int> DeliverAsync(long episodeId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Clean independent alert delivery context required.");
        if (!await WorkAlertEpisodeStore.InstalledAsync(db, ct)) return 0;
        var hint = await WorkAlertEpisodeStore.SourcesQuery(db).SingleOrDefaultAsync(e => e.Id == episodeId, ct);
        if (hint == null || hint.ClosedAt != null || hint.SourceType is not ("INTERNAL_REQUEST" or "TASK" or "CLEANING" or "REWORK" or "MAINTENANCE_OCCURRENCE" or "INCIDENT")) return 0;
        // Discovery is a superset only. Exact current responsibility and read
        // authorization are checked under recipient/source locks below.
        var candidates = await db.UserBranchAccesses.AsNoTracking().Where(a => a.BranchId == hint.BranchId && a.RevokedAt == null)
            .Select(a => a.UserId).Distinct().Order().ToArrayAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await new UserEligibilityLock(db).LockAsync(candidates, ct);
            var resource = $"HotelOps:WorkAlertDelivery:{episodeId}";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
                IF @result<0 THROW 51974,'Alert delivery lock unavailable.',1;
                """, ct);
            var episode = await WorkAlertEpisodeStore.SourcesQuery(db).SingleOrDefaultAsync(e => e.Id == episodeId, ct);
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            if (episode == null || episode.ClosedAt != null || episode.SourceType != hint.SourceType
                || episode.SourceId != hint.SourceId || episode.BranchId != hint.BranchId
                || !NotificationRetention.At(now, retention.Value).Contains(episode.OpenedAt)) return 0;
            var parameters = await WorkAlertParameters.ReadAsync(db, ct);
            var expectedCondition = episode.InitialCondition == "AT_RISK" ? WorkAlertCondition.AtRisk : WorkAlertCondition.Overdue;
            if (await WorkAlertSourceAccess.ConditionAsync(db,episode,now,parameters.ThresholdPercent,ct) != expectedCondition) return 0;
            var eligible = new List<long>();
            foreach (var user in candidates)
                if (await WorkAlertSourceAccess.CanReadAsync(db, user, episode, ct)) eligible.Add(user);
            if (eligible.Count == 0) return 0;
            var existing = await db.Notifications.Where(n => n.NotificationType == Kind && n.SourceType == Source && n.SourceId == episodeId).ToArrayAsync(ct);
            if (existing.Length > 1 || existing.Any(n => n.CreatedAt != episode.OpenedAt || n.CreatedBy != null))
                throw new InvalidOperationException("Alert notification identity conflict.");
            var notice = existing.SingleOrDefault();
            if (notice == null)
            {
                notice = new Notification { NotificationType = Kind, SourceType = Source, SourceId = episodeId,
                    Title = episode.InitialCondition == "OVERDUE" ? "Work overdue" : "Work at risk",
                    Message = "A work item needs attention. Open it with your current source permissions.",
                    Priority = "HIGH", CreatedAt = episode.OpenedAt };
                db.Notifications.Add(notice); await db.SaveChangesAsync(ct);
            }
            var count = 0;
            foreach (var user in eligible)
            {
                if (await db.NotificationRecipients.AnyAsync(r => r.NotificationId == notice.NotificationId && r.UserId == user, ct)) continue;
                db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notice.NotificationId, UserId = user,
                    DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
                count++;
            }
            await db.SaveChangesAsync(ct);
            now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            if (await WorkAlertSourceAccess.ConditionAsync(db,episode,now,parameters.ThresholdPercent,ct) != expectedCondition)
                throw new InvalidOperationException("Alert source condition changed before delivery.");
            foreach (var user in eligible)
                if (!await WorkAlertSourceAccess.CanReadAsync(db, user, episode, ct))
                    throw new InvalidOperationException("Alert recipient authority changed.");
            await tx.CommitAsync(ct);
            return count;
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
