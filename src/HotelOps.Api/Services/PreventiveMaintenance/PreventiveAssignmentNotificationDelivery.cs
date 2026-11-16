using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.PreventiveMaintenance;

internal sealed class PreventiveAssignmentNotificationDelivery(HotelOpsDbContext db, IOptions<NotificationCenterOptions> retention)
{
    internal const string Kind = "PREVENTIVE_ASSIGNED";
    internal sealed record Source
    {
        public long EventId { get; init; }
        public long RunId { get; init; }
        public long BranchId { get; init; }
        public long? ActorId { get; init; }
        public long TechnicianId { get; init; }
        public DateTime At { get; init; }
    }
    private static IQueryable<Source> Sources(HotelOpsDbContext db) => db.Database.SqlQueryRaw<Source>("""
        SELECT a.audit_id AS EventId,d.run_id AS RunId,d.branch_id AS BranchId,
          a.actor_user_id AS ActorId,TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.assigneeId')) AS TechnicianId,a.occurred_at AS At
        FROM dbo.audit_logs a
        CROSS APPLY (SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'{}' END AS payload) j
        JOIN dbo.preventive_occurrence_details d ON a.entity_id=CONVERT(varchar(30),d.run_id) AND a.branch_id=d.branch_id
        JOIN dbo.preventive_maintenance_runs r ON r.run_id=d.run_id
        JOIN dbo.preventive_maintenance_schedules s ON s.schedule_id=r.schedule_id
        WHERE a.entity_type='preventive_maintenance_runs'
          AND TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.runId'))=d.run_id
          AND TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.assigneeId'))=d.assignee_id
          AND JSON_VALUE(j.payload,'$.notificationPending')='true'
          AND d.workflow_status IN ('ASSIGNED','IN_PROGRESS','COMPLETED')
          AND ((a.action='CREATE' AND a.audit_id=d.generation_audit_id AND a.occurred_at=d.generated_at
                AND a.actor_user_id IS NULL AND JSON_VALUE(j.payload,'$.eventType')='PREVENTIVE_OCCURRENCE_GENERATED')
            OR (a.action='UPDATE' AND a.actor_user_id IS NOT NULL AND JSON_VALUE(j.payload,'$.eventType')='PREVENTIVE_OCCURRENCE_ASSIGNED'))
          AND ((s.asset_id IS NOT NULL AND s.room_id IS NULL AND s.area_id IS NULL
                AND EXISTS(SELECT 1 FROM dbo.assets t WHERE t.asset_id=s.asset_id AND t.branch_id=d.branch_id))
            OR (s.room_id IS NOT NULL AND s.asset_id IS NULL AND s.area_id IS NULL
                AND EXISTS(SELECT 1 FROM dbo.rooms t WHERE t.room_id=s.room_id AND t.branch_id=d.branch_id))
            OR (s.area_id IS NOT NULL AND s.asset_id IS NULL AND s.room_id IS NULL
                AND EXISTS(SELECT 1 FROM dbo.areas t WHERE t.area_id=s.area_id AND t.branch_id=d.branch_id)))
        """);

    internal async Task<long[]> PendingAsync(long after, CancellationToken ct)
    {
        if (!await PreventiveOccurrenceGeneration.InstalledAsync(db, ct)) return [];
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var cutoff = NotificationRetention.At(now, retention.Value).Cutoff;
        return await Sources(db).Where(s => s.EventId > after && s.At >= cutoff && s.At <= now)
            .OrderBy(s => s.EventId).Select(s => s.EventId).Take(100).ToArrayAsync(ct);
    }

    internal static async Task<Source?> TargetAsync(HotelOpsDbContext db, long id, long user, long? actor, DateTime at, CancellationToken ct)
    {
        await new UserEligibilityLock(db).LockAsync([user], ct);
        if (!await PreventiveOccurrenceGeneration.InstalledAsync(db, ct)) return null;
        var source = await Sources(db).SingleOrDefaultAsync(s => s.EventId == id, ct);
        if (source == null || source.TechnicianId != user || source.ActorId != actor || source.At != at) return null;
        var read = await Maintenance.MaintenanceReadAccess.ReadAsync(db, user, source.BranchId, ct);
        return read.Lead || read.Staff ? source : null;
    }

    internal async Task DeliverAsync(long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Clean notification context required.");
        if (!await PreventiveOccurrenceGeneration.InstalledAsync(db, ct)) return;
        var hint = await Sources(db).SingleOrDefaultAsync(s => s.EventId == id, ct);
        if (hint == null) return;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([hint.TechnicianId], ct);
        var resource = $"HotelOps:PreventiveAssigned:{id}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
            IF @r<0 THROW 51846,'Preventive assignment notification lock unavailable.',1;
            """, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!NotificationRetention.At(now, retention.Value).Contains(hint.At)
            || await TargetAsync(db, id, hint.TechnicianId, hint.ActorId, hint.At, ct) == null) return;
        var notices = await db.Notifications.Where(n => n.SourceType == Kind && n.SourceId == id).ToArrayAsync(ct);
        if (notices.Length > 1 || notices.Any(n => n.NotificationType != Kind || n.CreatedBy != hint.ActorId || n.CreatedAt != hint.At))
            throw new InvalidOperationException("Preventive notice identity conflict.");
        var notice = notices.SingleOrDefault();
        if (notice == null)
        {
            notice = new Notification { NotificationType = Kind, SourceType = Kind, SourceId = id, Title = "Preventive maintenance assigned",
                Message = "A preventive maintenance occurrence has been assigned to you. Open it for current instructions and due date.",
                Priority = "NORMAL", CreatedBy = hint.ActorId, CreatedAt = hint.At };
            db.Notifications.Add(notice); await db.SaveChangesAsync(ct);
        }
        var receipts = await db.NotificationRecipients.AsNoTracking().Where(r => r.NotificationId == notice.NotificationId).ToArrayAsync(ct);
        if (receipts.Length > 1 || receipts.Any(r => r.UserId != hint.TechnicianId || r.DeliveryChannel != "IN_APP"
            || r.DeliveryStatus != "DELIVERED" || r.DeliveredAt == null)) throw new InvalidOperationException("Preventive delivery receipt conflict.");
        if (receipts.Length == 0) db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notice.NotificationId,
            UserId = hint.TechnicianId, DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
        await db.SaveChangesAsync(ct);
        if (await TargetAsync(db, id, hint.TechnicianId, hint.ActorId, hint.At, ct) == null)
            throw new InvalidOperationException("Preventive recipient authority changed.");
        await tx.CommitAsync(ct); db.ChangeTracker.Clear();
    }
}
