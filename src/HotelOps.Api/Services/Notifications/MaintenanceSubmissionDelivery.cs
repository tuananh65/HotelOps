using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Notifications;

internal sealed class MaintenanceSubmissionDelivery(HotelOpsDbContext db, TimeProvider clock, IOptions<NotificationCenterOptions> retention)
{
    internal async Task<long[]> PendingAsync(long after, CancellationToken ct)
    {
        var window = NotificationRetention.At(clock.GetUtcNow().UtcDateTime, retention.Value);
        return await db.Database.SqlQuery<long>($"""
            SELECT TOP(100) audit_id AS Value FROM dbo.audit_logs
            WHERE entity_type='maintenance_works' AND action='UPDATE' AND audit_id>{after}
              AND occurred_at>={window.Cutoff} AND occurred_at<={window.Now}
              AND JSON_VALUE(CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END,'$.notification_kind')='MAINTENANCE_SUBMITTED'
            ORDER BY audit_id
            """).ToArrayAsync(ct);
    }

    internal async Task<string> DeliverAsync(long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Independent clean notification context required.");
        var hint = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a => a.AuditId == id, ct);
        var sourceHint = hint == null ? null : MaintenanceSubmissionSource.Parse(hint);
        if (sourceHint == null) return "SOURCE_DENIED";
        var candidates = await db.UserRoles.AsNoTracking().Where(r => r.BranchId == sourceHint.BranchId
            && r.Role.RoleCode == "MAINTENANCE_LEAD").Select(r => r.UserId).Distinct().OrderBy(u => u).ToArrayAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync(candidates.Append(sourceHint.ActorId), ct);
        var resource = $"HotelOps:MaintenanceSubmission:{id}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
            IF @result<0 THROW 51843,'Maintenance notification lock unavailable.',1;
            """, ct);
        var source = await MaintenanceSubmissionSource.ReadAsync(db, id, ct);
        if (source == null || source != sourceHint) return "SOURCE_DENIED";
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!NotificationRetention.At(now, retention.Value).Contains(source.At)) return "EVENT_OUTSIDE_RETENTION";
        var eligible = new List<long>();
        foreach (var user in candidates)
            if (await MaintenanceSubmissionSource.CanReviewAsync(db, user, source, ct)) eligible.Add(user);
        if (eligible.Count == 0) return "NO_ELIGIBLE_RECIPIENT"; // Intent remains discoverable for retry.
        var notices = await db.Notifications.Where(n => n.NotificationType == MaintenanceSubmissionSource.Kind
            && n.SourceType == MaintenanceSubmissionSource.SourceType && n.SourceId == id).ToArrayAsync(ct);
        if (notices.Length > 1 || notices.Any(n => n.CreatedAt != source.At || n.CreatedBy != source.ActorId))
            throw new InvalidOperationException("Maintenance notification identity conflict.");
        var notice = notices.SingleOrDefault();
        if (notice == null)
        {
            notice = new Notification { NotificationType = MaintenanceSubmissionSource.Kind, SourceType = MaintenanceSubmissionSource.SourceType,
                SourceId = id, Title = "Maintenance awaiting verification", Message = "A maintenance result is ready for review.",
                Priority = "NORMAL", CreatedBy = source.ActorId, CreatedAt = source.At };
            db.Notifications.Add(notice); await db.SaveChangesAsync(ct);
        }
        foreach (var user in eligible)
        {
            var existing = await db.NotificationRecipients.AsNoTracking().Where(r => r.NotificationId == notice.NotificationId && r.UserId == user).ToArrayAsync(ct);
            if (existing.Length > 0)
            {
                if (existing.Length != 1 || existing[0].DeliveryChannel != "IN_APP" || existing[0].DeliveryStatus != "DELIVERED" || existing[0].DeliveredAt == null)
                    throw new InvalidOperationException("Maintenance notification delivery conflict.");
                continue;
            }
            db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notice.NotificationId, UserId = user,
                DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
        }
        await db.SaveChangesAsync(ct);
        foreach (var user in eligible)
            if (!await MaintenanceSubmissionSource.CanReviewAsync(db, user, source, ct)) return "RECIPIENT_CHANGED";
        if (await MaintenanceSubmissionSource.ReadAsync(db, id, ct) != source) return "SOURCE_CHANGED";
        await tx.CommitAsync(ct); db.ChangeTracker.Clear();
        return "DELIVERED";
    }
}
