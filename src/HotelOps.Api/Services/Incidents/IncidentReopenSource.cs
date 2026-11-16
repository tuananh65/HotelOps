using System.Data;
using System.Globalization;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Incidents;

// UC060 transaction participant. The owning workflow must separately establish
// current actor/permission, validate recurrence evidence, and persist a new cycle.
// This reader neither grants access nor changes any historical work or deadline.
internal static class IncidentReopenSource
{
    internal sealed record Closure(long IncidentId, long BranchId, long WorkId, long AuditId,
        long EventId, long TechnicianId, long ReviewerId, DateTime ClosedAtUtc,
        DateTime EvaluatedAtUtc, string Note, string ETag, IncidentSlaDto PreviousSla);

    // windowDays is the resolved SYS-PAR-19 policy, never a request-body value.
    internal static async Task<Closure> ReadAsync(HotelOpsDbContext db, long incidentId,
        int windowDays, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(incidentId);
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Reopen source requires the owning serializable transaction.");
        var incident = await db.Incidents.FromSqlInterpolated($"SELECT * FROM dbo.incidents WITH(UPDLOCK,HOLDLOCK) WHERE incident_id={incidentId}")
            .AsNoTracking().SingleOrDefaultAsync(ct)
            ?? throw new InternalRequestReadException(404, "INCIDENT_UNAVAILABLE");
        if (incident.Status != "CLOSED") throw new InternalRequestReadException(409, "INCIDENT_NOT_CLOSED");
        if (incident.ClosedAt is not { } closed || incident.ResolvedAt is not { } resolved
            || resolved > closed || resolved < incident.DiscoveredAt || string.IsNullOrWhiteSpace(incident.ClosureNote))
            throw new InternalRequestReadException(409, "INCIDENT_CLOSURE_INCONSISTENT");
        if (windowDays <= 0) throw new InternalRequestReadException(503, "INCIDENT_REOPEN_WINDOW_UNAVAILABLE");
        var now = DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct), DateTimeKind.Utc);
        if (closed > now || (now - closed).TotalDays > windowDays)
            throw new InternalRequestReadException(409, "INCIDENT_REOPEN_WINDOW_EXPIRED");
        var tag = await IncidentTriageStore.TagAsync(db, incidentId, ct)
            ?? throw new InternalRequestReadException(503, "INCIDENT_VERSION_UNAVAILABLE");
        // Bind to the current closure, not the largest work ID or an old assignee.
        var works = await db.MaintenanceWorks.AsNoTracking().Where(w => w.IncidentId == incidentId
            && w.Status == "CLOSED" && w.ReviewedAt == closed).Take(2).ToArrayAsync(ct);
        if (works.Length != 1 || works[0].AssigneeId is not > 0 || works[0].ReviewedBy is not > 0
            || await db.MaintenanceWorks.AsNoTracking().AnyAsync(w => w.IncidentId == incidentId
                && w.Status != "CLOSED" && w.Status != "CANCELLED", ct)
            || await db.WorkIntervals.AsNoTracking().AnyAsync(i => i.WorkItemKind == "MAINTENANCE" && i.ClosedAt == null
                && db.MaintenanceWorks.Any(w => w.MaintenanceWorkId == i.WorkItemId && w.IncidentId == incidentId), ct))
            throw new InternalRequestReadException(409, "INCIDENT_CLOSURE_INCONSISTENT");
        var work = works[0];
        var workKey = work.MaintenanceWorkId.ToString(CultureInfo.InvariantCulture);
        var audits = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "maintenance_works"
            && a.EntityId == workKey && a.Action == "UPDATE" && a.BranchId == incident.BranchId
            && a.ActorUserId == work.ReviewedBy && a.OccurredAt == closed).ToArrayAsync(ct);
        var matching = audits.Select(MaintenanceClosureSource.Parse).Where(s => s != null
            && s.IncidentId == incidentId && s.WorkId == work.MaintenanceWorkId).ToArray();
        if (matching.Length != 1 || await MaintenanceClosureSource.ReadAsync(db, matching[0]!.AuditId, ct) is not { } source)
            throw new InternalRequestReadException(409, "INCIDENT_CLOSURE_RECEIPT_REQUIRED");
        var events = await db.IncidentEvents.AsNoTracking().Where(e => e.IncidentId == incidentId
            && e.EventType == "STATUS_CHANGED" && e.FromStatus == "RESOLVED" && e.ToStatus == "CLOSED"
            && e.ActorId == source.ActorId && e.OccurredAt == closed && e.Note == source.Note)
            .Select(e => e.EventId).Take(2).ToArrayAsync(ct);
        if (events.Length != 1) throw new InternalRequestReadException(409, "INCIDENT_CLOSURE_RECEIPT_REQUIRED");
        var sla = await IncidentSlaRead.ReadAsync(db, incidentId, ct);
        // A new cycle must not erase malformed or unavailable previous SLA evidence.
        if (sla.UnavailableReason != null)
            throw new InternalRequestReadException(409, "INCIDENT_PREVIOUS_SLA_UNAVAILABLE");
        return new(incidentId, incident.BranchId, work.MaintenanceWorkId, source.AuditId,
            events[0], source.TechnicianId, source.ActorId, DateTime.SpecifyKind(closed, DateTimeKind.Utc),
            now, source.Note, tag, sla);
    }
}
