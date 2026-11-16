using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Notifications;

internal sealed class IncidentRoomReleaseDelivery(HotelOpsDbContext db, TimeProvider clock, IOptions<NotificationCenterOptions> retention)
{
    internal async Task<long[]> PendingAsync(long after, CancellationToken ct)
    {
        var window = NotificationRetention.At(clock.GetUtcNow().UtcDateTime, retention.Value);
        return await db.Database.SqlQuery<long>($"""
            SELECT TOP(100) audit_id AS Value FROM dbo.audit_logs
            WHERE entity_type='rooms' AND action='UPDATE' AND audit_id>{after}
              AND occurred_at>={window.Cutoff} AND occurred_at<={window.Now}
              AND JSON_VALUE(CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END,'$.notification_kind')='INCIDENT_ROOM_RELEASE'
            ORDER BY audit_id
            """).ToArrayAsync(ct);
    }

    internal async Task<string> DeliverAsync(long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Independent clean notification context required.");
        var hint = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a => a.AuditId == id, ct);
        var sourceHint = hint == null ? null : IncidentRoomReleaseSource.Parse(hint);
        if (sourceHint == null) return "SOURCE_DENIED";
        var candidates = await db.UserRoles.AsNoTracking().Where(r => r.BranchId == sourceHint.BranchId
            && r.IsActive && r.Role.IsActive && r.Role.RoleCode == "HK_SUPERVISOR")
            .Select(r => r.UserId).Distinct().OrderBy(x => x).ToArrayAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync(candidates.Append(sourceHint.ActorId), ct);
        var resource = $"HotelOps:IncidentRoomRelease:{id}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
            IF @result<0 THROW 51843,'Room release notification lock unavailable.',1;
            """, ct);
        var source = await IncidentRoomReleaseSource.ReadAsync(db, id, ct);
        if (source == null || source != sourceHint) return "SOURCE_DENIED";
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!NotificationRetention.At(now, retention.Value).Contains(source.At)) return "EVENT_OUTSIDE_RETENTION";
        var eligible = new List<long>();
        foreach (var user in candidates)
            if (await IncidentRoomReleaseSource.CanReadAsync(db, user, source, ct)) eligible.Add(user);
        if (eligible.Count == 0) return "NO_ELIGIBLE_RECIPIENT"; // Intent remains discoverable for retry.
        var notices = await db.Notifications.Where(n => n.NotificationType == IncidentRoomReleaseSource.Kind
            && n.SourceType == IncidentRoomReleaseSource.SourceType && n.SourceId == id).ToArrayAsync(ct);
        if (notices.Length > 1 || notices.Any(n => n.CreatedAt != source.At || n.CreatedBy != source.ActorId))
            throw new InvalidOperationException("Room release notification identity conflict.");
        var notice = notices.SingleOrDefault();
        if (notice == null)
        {
            var readiness = await new HotelOps.Api.Services.Rooms.EffectiveRoomReadiness(db).ReadAsync([source.RoomId], now, ct);
            if (!readiness.TryGetValue(source.RoomId, out var room)) return "ROOM_SOURCE_UNAVAILABLE";
            notice = new Notification { NotificationType = IncidentRoomReleaseSource.Kind, SourceType = IncidentRoomReleaseSource.SourceType,
                SourceId = id, Title = "Incident room block released", Message = room.Readiness == "BLOCKED"
                    ? "An Incident block was released. The room was still Blocked at notification delivery. Open Room Status for current readiness."
                    : "An Incident block was released. This does not mean the room is Ready. Open Room Status for current readiness.",
                Priority = "NORMAL", CreatedBy = source.ActorId, CreatedAt = source.At };
            db.Notifications.Add(notice); await db.SaveChangesAsync(ct);
        }
        foreach (var user in eligible)
        {
            var existing = await db.NotificationRecipients.AsNoTracking().Where(r => r.NotificationId == notice.NotificationId && r.UserId == user).ToArrayAsync(ct);
            if (existing.Length > 0)
            {
                if (existing.Length != 1 || existing[0].DeliveryChannel != "IN_APP" || existing[0].DeliveryStatus != "DELIVERED" || existing[0].DeliveredAt == null)
                    throw new InvalidOperationException("Room release notification delivery conflict.");
                continue;
            }
            db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notice.NotificationId, UserId = user,
                DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
        }
        await db.SaveChangesAsync(ct);
        foreach (var user in eligible)
            if (!await IncidentRoomReleaseSource.CanReadAsync(db, user, source, ct)) return "RECIPIENT_CHANGED";
        if (await IncidentRoomReleaseSource.ReadAsync(db, id, ct) != source) return "SOURCE_CHANGED";
        await tx.CommitAsync(ct); db.ChangeTracker.Clear();
        return "DELIVERED";
    }
}
