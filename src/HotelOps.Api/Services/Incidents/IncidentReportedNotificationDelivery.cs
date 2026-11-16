using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Incidents;

// The CREATED event is the durable intent, committed with the incident and audit.
// Delivery failures never roll back reporting; retained events are rescanned.
internal sealed class IncidentReportedNotificationDelivery(HotelOpsDbContext db, IOptions<NotificationCenterOptions> retention)
{
    internal const string Kind = "INCIDENT_REPORTED";
    internal sealed record Report
    {
        public long Id { get; init; }
        public long IncidentId { get; init; }
        public long BranchId { get; init; }
        public long ActorId { get; init; }
        public DateTime At { get; init; }
    }
    internal static IQueryable<Report> Events(HotelOpsDbContext db) => db.IncidentEvents.AsNoTracking()
        .Where(e => e.EventType == "CREATED" && e.FromStatus == null && e.ToStatus == "OPEN"
            && e.ActorId == e.Incident.ReportedBy && e.OccurredAt == e.Incident.ReportedAt)
        .Select(e => new Report { Id = e.EventId, IncidentId = e.IncidentId, BranchId = e.Incident.BranchId, ActorId = e.ActorId, At = e.OccurredAt });

    private static IQueryable<long> Candidates(HotelOpsDbContext db, long branch, DateTime now) => db.UserRoles.AsNoTracking()
        .Where(r => r.Role.RoleCode == "MAINTENANCE_LEAD" && r.IsActive && r.Role.IsActive
            && r.BranchId == branch && r.Branch!.IsActive && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && r.User.DeletedAt == null && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE"
            && db.UserBranchAccesses.Any(b => b.UserId == r.UserId && b.BranchId == branch && b.GrantedAt <= now && b.RevokedAt == null))
        .Select(r => r.UserId).Distinct();

    internal async Task<long[]> PendingAsync(long after, CancellationToken ct)
    {
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
        var scope = await IncidentReadAccess.ReadAsync(db, user, source.BranchId, ct);
        // UC049 recipients are Maintenance Leads, not every actor who can view a source.
        return scope.Lead && await scope.Apply(db.Incidents.AsNoTracking()).AnyAsync(i => i.IncidentId == source.IncidentId, ct)
            ? source.IncidentId : null;
    }

    internal async Task DeliverAsync(long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()
            || System.Transactions.Transaction.Current != null || db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("Independent clean delivery context required.");
        var hint = await Events(db).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (hint == null) return;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var candidates = await Candidates(db, hint.BranchId, now).OrderBy(u => u).ToArrayAsync(ct);
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
                Title = "Incident reported", Message = "An incident was reported in your branch. Open it to review its current status.",
                Priority = "NORMAL", CreatedBy = hint.ActorId, CreatedAt = hint.At };
            db.Notifications.Add(notice);
            await db.SaveChangesAsync(ct);
        }
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
