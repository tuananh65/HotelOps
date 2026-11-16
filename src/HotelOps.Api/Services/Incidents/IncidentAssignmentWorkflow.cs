using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

internal static class IncidentAssignmentWorkflow
{
    // Projection only. Physical OPEN/PENDING is never accepted as assignment evidence.
    internal static async Task<string?> ReadAsync(HotelOpsDbContext db, Incident incident, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Owning transaction required.");
        if (incident.Status != "OPEN") return null;
        if (incident.ClosedAt != null || incident.ResolvedAt != null) return "UNVERIFIED";
        var work = await db.MaintenanceWorks.AsNoTracking().Where(w => w.IncidentId == incident.IncidentId)
            .Select(w => w.MaintenanceWorkId).Take(2).ToArrayAsync(ct);
        if (work.Length > 1) return "UNVERIFIED";
        var triaged = await db.IncidentEvents.AsNoTracking().AnyAsync(e => e.IncidentId == incident.IncidentId && e.EventType == "TRIAGED", ct);
        if (!triaged)
        {
            var sla = await IncidentSlaRead.ReadAsync(db, incident.IncidentId, ct);
            return work.Length == 0 && sla.UnavailableReason == "INCIDENT_SLA_NOT_RECORDED"
                && sla.ResponseDeadlineAt == null && sla.ResolutionDeadlineAt == null
                && await db.IncidentEvents.AsNoTracking().CountAsync(e => e.IncidentId == incident.IncidentId
                    && e.EventType == "CREATED" && e.ToStatus == "OPEN" && e.ActorId == incident.ReportedBy && e.OccurredAt == incident.ReportedAt, ct) == 1
                ? "PENDING_TRIAGE" : "UNVERIFIED";
        }
        if (!await MaintenanceAssignmentProof.HasTriageAsync(db, incident, ct)) return "UNVERIFIED";
        return work.Length == 0 ? "PENDING_ASSIGNMENT" : await MaintenanceAssignmentProof.ReadAsync(db, work[0], ct);
    }
}
