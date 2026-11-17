using HotelOps.Api.Data;
using HotelOps.Api.Services.Incidents;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

public sealed record MaintenanceDeadlineView(DateTime? DueAt, bool? IsOverdue,
    string Source, string? UnavailableReason, DateTime EvaluatedAt);

// UC144: Incident resolution SLA or the linked preventive occurrence's scheduled due.
// PlannedEndAt is deliberately not a fallback for either source.
internal static class MaintenanceDeadlineRead
{
    internal static async Task<MaintenanceDeadlineView> ReadAsync(HotelOpsDbContext db,
        long user, long branch, long work, DateTime evaluatedAt, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Deadline reads require an owning read transaction.");
        var scope = await MaintenanceReadAccess.ReadAsync(db, user, branch, ct);
        var row = await scope.Apply(db.MaintenanceWorks.AsNoTracking())
            .Where(w => w.MaintenanceWorkId == work)
            .Select(w => new { w.IncidentId, w.Status }).SingleOrDefaultAsync(ct);
        if (row == null) return new(null, null, "UNAVAILABLE", "WORK_ACCESS_UNAVAILABLE", evaluatedAt);
        if (row.IncidentId != null)
        {
            var sla = await IncidentSlaRead.ForWorkAsync(db, user, branch, work, ct);
            if (sla.ResolutionDeadlineAt is not { } due)
                return new(null, null, "INCIDENT_RESOLUTION_SLA", sla.UnavailableReason ?? "RESOLUTION_DEADLINE_UNAVAILABLE", evaluatedAt);
            // Completed work awaits verification; it is not Closed.
            var known = row.Status is "PENDING" or "IN_PROGRESS" or "ON_HOLD" or "COMPLETED" or "CLOSED" or "CANCELLED";
            return new(due, known ? row.Status is not ("CLOSED" or "CANCELLED") && evaluatedAt > due : null,
                "INCIDENT_RESOLUTION_SLA", known ? null : "WORK_STATUS_UNAVAILABLE", evaluatedAt);
        }
        var runs = await db.PreventiveMaintenanceRuns.AsNoTracking()
            .Where(r => r.MaintenanceWorkId == work)
            .Select(r => new { r.PlannedAt, r.PerformedAt }).Take(2).ToArrayAsync(ct);
        if (runs.Length != 1 || runs[0].PlannedAt == default)
            return new(null, null, "PREVENTIVE_OCCURRENCE", "OCCURRENCE_DUE_UNAVAILABLE", evaluatedAt);
        var scheduledDue = DateTime.SpecifyKind(runs[0].PlannedAt, DateTimeKind.Utc);
        var knownStatus = row.Status is "PENDING" or "IN_PROGRESS" or "ON_HOLD" or "COMPLETED" or "CLOSED" or "CANCELLED";
        return new(scheduledDue, knownStatus ? runs[0].PerformedAt == null
            && row.Status is not ("CLOSED" or "CANCELLED") && evaluatedAt > scheduledDue : null,
            "PREVENTIVE_OCCURRENCE", knownStatus ? null : "WORK_STATUS_UNAVAILABLE", evaluatedAt);
    }
}
