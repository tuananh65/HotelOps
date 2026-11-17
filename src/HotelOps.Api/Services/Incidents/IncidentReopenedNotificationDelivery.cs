using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Incidents;

internal sealed class IncidentReopenedNotificationDelivery(HotelOpsDbContext db, IOptions<NotificationCenterOptions> retention)
{
    internal const string Kind = "INCIDENT_REOPENED";
    internal sealed record Target(long IncidentId, long? PreviousWorkId);
    internal sealed record Source
    {
        public long EventId { get; init; }
        public long IncidentId { get; init; }
        public long PreviousWorkId { get; init; }
        public long BranchId { get; init; }
        public long ActorId { get; init; }
        public long TechnicianId { get; init; }
        public DateTime At { get; init; }
    }
    private static IQueryable<Source> Sources(HotelOpsDbContext db) => db.Database.SqlQueryRaw<Source>("""
        SELECT r.reopen_event_id AS EventId,r.incident_id AS IncidentId,r.previous_work_id AS PreviousWorkId,i.branch_id AS BranchId,
          e.actor_id AS ActorId,w.assignee_id AS TechnicianId,e.occurred_at AS At
        FROM dbo.incident_reopen_cycles r JOIN dbo.incident_events e ON e.event_id=r.reopen_event_id AND e.incident_id=r.incident_id
        JOIN dbo.incidents i ON i.incident_id=r.incident_id
        JOIN dbo.maintenance_works w ON w.maintenance_work_id=r.previous_work_id AND w.incident_id=r.incident_id
        WHERE e.event_type='REOPENED' AND w.assignee_id IS NOT NULL
        """);

    internal async Task<long[]> PendingAsync(long after, CancellationToken ct)
    {
        if (!await IncidentReopenStore.InstalledAsync(db, ct)) return [];
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var cutoff = NotificationRetention.At(now, retention.Value).Cutoff;
        return await Sources(db).Where(s => s.EventId > after && s.At >= cutoff && s.At <= now)
            .OrderBy(s => s.EventId).Select(s => s.EventId).Take(100).ToArrayAsync(ct);
    }

    internal static async Task<Target?> TargetAsync(HotelOpsDbContext db, long id, long user, long? actor, DateTime at, CancellationToken ct)
    {
        await new UserEligibilityLock(db).LockAsync([user], ct);
        if (!await IncidentReopenStore.InstalledAsync(db, ct)) return null;
        var source = await Sources(db).SingleOrDefaultAsync(s => s.EventId == id, ct);
        if (source == null || source.TechnicianId != user || source.ActorId != actor || source.At != at
            || await IncidentReopenStore.ReadAsync(db, source.IncidentId, ct, id) == null) return null;
        var read = await IncidentReadAccess.ReadAsync(db, user, source.BranchId, ct, sourceRecord: true);
        if (await read.Apply(db.Incidents.AsNoTracking()).AnyAsync(i => i.IncidentId == source.IncidentId, ct))
            return new(source.IncidentId, null);
        // Reopen removes the previous assignment from the new cycle. The technician
        // may still read their historical work through existing MAINTENANCE_VIEW;
        // this does not confer Incident access or expose the new cycle's details.
        var workScope = await MaintenanceReadAccess.ReadAsync(db, user, source.BranchId, ct);
        return await workScope.Apply(db.MaintenanceWorks.AsNoTracking()).AnyAsync(w =>
            w.MaintenanceWorkId == source.PreviousWorkId && w.IncidentId == source.IncidentId
            && w.AssigneeId == user && w.Status == "CLOSED", ct)
            ? new(source.IncidentId, source.PreviousWorkId) : null;
    }

    internal async Task DeliverAsync(long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Clean notification context required.");
        if (!await IncidentReopenStore.InstalledAsync(db, ct)) return;
        var hint = await Sources(db).SingleOrDefaultAsync(s => s.EventId == id, ct);
        if (hint == null) return;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([hint.TechnicianId], ct);
        var resource = $"HotelOps:IncidentReopened:{id}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
            IF @r<0 THROW 51846,'Incident reopen notification lock unavailable.',1;
            """, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!NotificationRetention.At(now, retention.Value).Contains(hint.At)) return;
        var target = await TargetAsync(db, id, hint.TechnicianId, hint.ActorId, hint.At, ct);
        if (target?.IncidentId != hint.IncidentId) return;
        var notices = await db.Notifications.Where(n => n.SourceType == Kind && n.SourceId == id).ToArrayAsync(ct);
        if (notices.Length > 1 || notices.Any(n => n.NotificationType != Kind || n.CreatedBy != hint.ActorId || n.CreatedAt != hint.At))
            throw new InvalidOperationException("Reopen notice identity conflict.");
        var notice = notices.SingleOrDefault();
        if (notice == null)
        {
            notice = new Notification { NotificationType = Kind, SourceType = Kind, SourceId = id, Title = "Incident reopened",
                Message = "An incident from your previous maintenance work was reopened for a new processing cycle. Open the source available within your current access.",
                Priority = "NORMAL", CreatedBy = hint.ActorId, CreatedAt = hint.At };
            db.Notifications.Add(notice); await db.SaveChangesAsync(ct);
        }
        var receipts = await db.NotificationRecipients.AsNoTracking().Where(r => r.NotificationId == notice.NotificationId).ToArrayAsync(ct);
        if (receipts.Length > 1 || receipts.Any(r => r.UserId != hint.TechnicianId || r.DeliveryChannel != "IN_APP"
            || r.DeliveryStatus != "DELIVERED" || r.DeliveredAt == null)) throw new InvalidOperationException("Reopen delivery receipt conflict.");
        if (receipts.Length == 0) db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notice.NotificationId,
            UserId = hint.TechnicianId, DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
        await db.SaveChangesAsync(ct);
        if (await TargetAsync(db, id, hint.TechnicianId, hint.ActorId, hint.At, ct) != target)
            throw new InvalidOperationException("Reopen recipient authority changed.");
        await tx.CommitAsync(ct); db.ChangeTracker.Clear();
    }
}
