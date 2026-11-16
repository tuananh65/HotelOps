using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Incidents;

// The escalation event, receipt and matching audit form the committed notification intent.
// Delivery failures never roll back escalation; retained events are rescanned.
internal sealed class IncidentEscalatedNotificationDelivery(HotelOpsDbContext db, IOptions<NotificationCenterOptions> retention)
{
    internal const string Kind = "INCIDENT_ESCALATED";
    internal sealed record Report
    {
        public long Id { get; init; }
        public long IncidentId { get; init; }
        public long BranchId { get; init; }
        public long ActorId { get; init; }
        public DateTime At { get; init; }
    }
    internal static IQueryable<Report> Events(HotelOpsDbContext db) => db.Database.SqlQueryRaw<Report>("""
        SELECT e.event_id AS Id,e.incident_id AS IncidentId,i.branch_id AS BranchId,e.actor_id AS ActorId,e.occurred_at AS At
        FROM dbo.incident_events e JOIN dbo.incidents i ON i.incident_id=e.incident_id
        JOIN dbo.audit_logs a ON a.entity_type='incidents' AND TRY_CONVERT(bigint,a.entity_id)=e.incident_id
          AND a.actor_user_id=e.actor_id AND a.branch_id=i.branch_id AND a.occurred_at=e.occurred_at AND a.action='UPDATE'
        WHERE e.event_type='ESCALATED' AND e.from_status=e.to_status
          AND EXISTS(SELECT 1 FROM dbo.incident_escalations x WHERE x.incident_id=e.incident_id AND x.opened_event_id=e.event_id AND x.opened_at=e.occurred_at)
          AND JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'{}' END,'$.notification_kind')='INCIDENT_ESCALATED'
          AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'{}' END,'$.event_id'))=e.event_id
        """);
    internal async Task<long[]> PendingAsync(long after, CancellationToken ct)
    {
        if(!await IncidentEscalationClosure.InstalledAsync(db,ct) || !await NotificationAcknowledgmentStore.InstalledAsync(db,ct)) return [];
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var cutoff = NotificationRetention.At(now, retention.Value).Cutoff;
        return await Events(db).Where(e => e.Id > after && e.At >= cutoff && e.At <= now)
            .OrderBy(e => e.Id).Select(e => e.Id).Take(100).ToArrayAsync(ct);
    }

    internal static async Task<long?> TargetAsync(HotelOpsDbContext db, long id, long user, long? actor, DateTime at, CancellationToken ct)
    {
        await new UserEligibilityLock(db).LockAsync([user], ct);
        var source = await Events(db).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (source == null || source.ActorId != actor || source.At != at) return null;
        var scope = await IncidentReadAccess.ReadAsync(db, user, source.BranchId, ct, sourceRecord: true);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if(!await db.UserRoles.AsNoTracking().AnyAsync(r=>r.UserId==user && r.BranchId==source.BranchId && r.IsActive && r.Role.IsActive
            && r.Role.RoleCode=="OPERATIONS_MANAGER" && r.AssignedAt<=now && (r.ExpiresAt==null || r.ExpiresAt>now),ct)) return null;
        // Current Operations Manager and independent source read authority are both required.
        return await scope.Apply(db.Incidents.AsNoTracking()).AnyAsync(i => i.IncidentId == source.IncidentId, ct)
            ? source.IncidentId : null;
    }

    internal async Task DeliverAsync(long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()
            || System.Transactions.Transaction.Current != null || db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("Independent clean delivery context required.");
        var hint = await Events(db).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (hint == null) return;
        if(!await IncidentEscalationClosure.InstalledAsync(db,ct) || !await NotificationAcknowledgmentStore.InstalledAsync(db,ct)) return;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var candidates = await db.UserRoles.AsNoTracking().Where(r=>r.BranchId==hint.BranchId && r.Role.RoleCode=="OPERATIONS_MANAGER").Select(r=>r.UserId).Distinct().OrderBy(u=>u).ToArrayAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync(candidates, ct);
        var current = await Events(db).SingleOrDefaultAsync(e => e.Id == id, ct);
        now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (current != hint || !NotificationRetention.At(now, retention.Value).Contains(hint.At)) return;
        var eligible = new List<long>();
        foreach (var user in candidates)
            if (await TargetAsync(db, id, user, hint.ActorId, hint.At, ct) == hint.IncidentId) eligible.Add(user);
        if (eligible.Count == 0) return;
        var notices = await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE source_type={Kind} AND source_id={id}")
            .AsNoTracking().ToArrayAsync(ct);
        if (notices.Length > 1 || notices.Any(n => n.NotificationType != Kind || n.CreatedBy != hint.ActorId || n.CreatedAt != hint.At))
            throw new InvalidOperationException("Incident notice provenance conflict.");
        var notice = notices.SingleOrDefault();
        if (notice == null)
        {
            notice = new Notification { NotificationType = Kind, SourceType = Kind, SourceId = id,
                Title = "Incident escalated", Message = "An incident requires your attention. Open it to review the requested support and acknowledge this notification.",
                Priority = "NORMAL", CreatedBy = hint.ActorId, CreatedAt = hint.At };
            db.Notifications.Add(notice);
            await db.SaveChangesAsync(ct);
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notifications SET requires_acknowledgment=1 WHERE notification_id={notice.NotificationId}",ct);
        var receipts = await db.NotificationRecipients.AsNoTracking().Where(r => r.NotificationId == notice.NotificationId).ToArrayAsync(ct);
        if (receipts.GroupBy(r => r.UserId).Any(g => g.Count() > 1) || receipts.Any(r => r.DeliveryChannel != "IN_APP"
            || r.DeliveryStatus != "DELIVERED" || r.DeliveredAt == null))
            throw new InvalidOperationException("Incident notice receipt conflict.");
        foreach (var user in eligible)
            if (!receipts.Any(r => r.UserId == user)) db.NotificationRecipients.Add(new NotificationRecipient {
                NotificationId = notice.NotificationId, UserId = user, DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
        await db.SaveChangesAsync(ct);
        foreach (var user in eligible)
            if (await TargetAsync(db, id, user, hint.ActorId, hint.At, ct) != hint.IncidentId)
                throw new InvalidOperationException("Incident recipient authority changed.");
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }
}
