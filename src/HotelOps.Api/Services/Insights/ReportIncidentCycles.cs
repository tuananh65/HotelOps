using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

// ReportService owns REPORT_VIEW admission and its serializable snapshot. Do not
// substitute the interactive Incident reader's actor set for report authorization.
internal static class ReportIncidentCycles
{
    internal sealed record Row(long IncidentId, long? WorkId, long? ReopenEventId, DateTime? DiscoveredAt,
        DateTime? AcceptedAt, DateTime? ResolvedAt, DateTime? ResponseDeadlineAt, DateTime? ResolutionDeadlineAt,
        decimal? ResponseSeconds, decimal? ResolutionSeconds, bool? WithinResolutionSla, string? MissingReason);

    internal static async Task<Row[]> ReadAsync(HotelOpsDbContext db, IQueryable<Incident> scoped, DateTime now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Owning report transaction required.");
        var incidents = await scoped.OrderBy(i => i.IncidentId).Take(50001).ToArrayAsync(ct);
        if (incidents.Length > 50000) throw new InsightException(422, "REPORT_ROW_LIMIT", "Narrow the report period.");
        var result = new List<Row>();
        foreach (var incident in incidents)
        {
            var works = await db.MaintenanceWorks.AsNoTracking().Where(w => w.IncidentId == incident.IncidentId)
                .OrderBy(w => w.CreatedAt).ThenBy(w => w.MaintenanceWorkId).Take(50001).ToArrayAsync(ct);
            if (result.Count + works.Length > 50000) throw new InsightException(422, "REPORT_ROW_LIMIT", "Narrow the report period.");
            var eventIds = await db.IncidentEvents.AsNoTracking().Where(e => e.IncidentId == incident.IncidentId && e.EventType == "REOPENED")
                .OrderBy(e => e.EventId).Select(e => e.EventId).ToArrayAsync(ct);
            var cycles = new List<IncidentReopenStore.Boundary>();
            string? cycleError = null;
            try
            {
                foreach (var id in eventIds)
                {
                    var boundary = await IncidentReopenStore.ReadAsync(db, incident.IncidentId, ct, id);
                    if (boundary == null || boundary.ReopenedAt > now) { cycleError = "CYCLE_PROVENANCE_UNAVAILABLE"; break; }
                    cycles.Add(boundary);
                }
            }
            catch (InternalRequestReadException) { cycleError = "CYCLE_PROVENANCE_UNAVAILABLE"; }
            if (works.Length == 0)
            {
                var last = cycles.LastOrDefault();
                var sla = cycleError == null ? await IncidentSlaRead.ReadAsync(db, incident.IncidentId, ct) : null;
                var discovered = cycleError == null ? last?.DiscoveredAt ?? incident.DiscoveredAt : (DateTime?)null;
                result.Add(new(incident.IncidentId, null, last?.EventId, discovered, null, null,
                    sla?.ResponseDeadlineAt, sla?.ResolutionDeadlineAt, null, null, null, cycleError ?? "ACCEPT_AND_START_NOT_RECORDED"));
                continue;
            }
            foreach (var work in works)
            {
                var boundary = cycles.LastOrDefault(c => c.ReopenedAt <= work.CreatedAt);
                var next = cycles.FirstOrDefault(c => c.ReopenedAt > work.CreatedAt);
                var errors = new List<string>();
                if (cycleError != null) errors.Add(cycleError);
                var discovered = cycleError == null ? boundary?.DiscoveredAt ?? incident.DiscoveredAt : (DateTime?)null;
                var cycleWorks = works.Count(w => w.CreatedAt >= (boundary?.ReopenedAt ?? DateTime.MinValue)
                    && (next == null || w.CreatedAt < next.ReopenedAt));
                if (cycleWorks != 1) { errors.Add("AMBIGUOUS_CYCLE_WORK"); discovered = null; }
                var key = work.MaintenanceWorkId.ToString(CultureInfo.InvariantCulture);
                var audits = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "maintenance_works" && a.EntityId == key
                    && a.Action == "UPDATE" && a.BranchId == incident.BranchId && a.OccurredAt <= now).ToArrayAsync(ct);
                var starts = audits.Where(a => Start(a, work, incident.IncidentId)).ToArray();
                DateTime? accepted = starts.Length == 1 ? starts[0].OccurredAt : null;
                if (accepted == null) errors.Add("ACCEPT_AND_START_PROOF_UNAVAILABLE");
                var submissions = audits.Select(MaintenanceSubmissionSource.Parse)
                    .Where(s => s != null && s.IncidentId == incident.IncidentId && s.At == work.ResolvedAt).ToArray();
                DateTime? resolved = null;
                if (work.Status is "COMPLETED" or "CLOSED" && submissions.Length == 1)
                {
                    var submitted = submissions[0]!;
                    if (await db.IncidentEvents.AsNoTracking().AnyAsync(e => e.IncidentId == incident.IncidentId
                        && e.EventType == "STATUS_CHANGED" && e.FromStatus == "IN_PROGRESS" && e.ToStatus == "RESOLVED"
                        && e.OccurredAt == submitted.At && e.ActorId == submitted.ActorId, ct)) resolved = submitted.At;
                }
                if (resolved == null) errors.Add("RESOLUTION_NOT_PROVEN_OR_CYCLE_OPEN");
                IncidentSlaDto? sla = null;
                if (next == null && cycleError == null) sla = await IncidentSlaRead.ReadAsync(db, incident.IncidentId, ct);
                else if (work.Status == "CLOSED" && discovered != null)
                {
                    var closures = audits.Where(a => MaintenanceClosureSource.Parse(a) is {} closure
                        && closure.WorkId == work.MaintenanceWorkId && closure.IncidentId == incident.IncidentId
                        && closure.At == work.ReviewedAt).ToArray();
                    if (closures.Length == 1 && await MaintenanceClosureSource.ReadHistoricalAsync(db, closures[0].AuditId, ct) != null)
                        sla = HistoricalSla(closures[0], discovered.Value, boundary?.EventId);
                }
                if (sla == null || sla.UnavailableReason != null) errors.Add(sla?.UnavailableReason ?? "CYCLE_SLA_NOT_RECORDED");
                if (discovered == null || discovered > now || accepted < discovered || accepted > now || accepted < work.CreatedAt
                    || resolved < discovered || resolved > now || resolved < accepted
                    || next != null && (accepted >= next.ReopenedAt || resolved >= next.ReopenedAt))
                { accepted = null; resolved = null; errors.Add("INVALID_CYCLE_TIMESTAMPS"); }
                if (sla != null && discovered != null) sla = IncidentSlaRead.Normalize(discovered.Value, sla.ResponseDeadlineAt, sla.ResolutionDeadlineAt);
                if (discovered == null) sla = null;
                if (sla?.UnavailableReason != null) errors.Add(sla.UnavailableReason);
                result.Add(new(incident.IncidentId, work.MaintenanceWorkId, boundary?.EventId, discovered, accepted, resolved,
                    sla?.ResponseDeadlineAt, sla?.ResolutionDeadlineAt,
                    discovered != null && accepted != null ? (decimal)(accepted.Value - discovered.Value).TotalSeconds : null,
                    discovered != null && resolved != null ? (decimal)(resolved.Value - discovered.Value).TotalSeconds : null,
                    resolved != null && sla?.ResolutionDeadlineAt != null ? resolved < sla.ResolutionDeadlineAt : null,
                    errors.Count == 0 ? null : string.Join(";", errors.Distinct())));
            }
            // A newly reopened cycle can legitimately have no maintenance work
            // until triage/assignment. Do not mistake the previous closed cycle
            // for the current cycle or lose its proven reopen event.
            if (cycleError == null && cycles.LastOrDefault() is {} latest
                && !works.Any(w => w.CreatedAt >= latest.ReopenedAt))
            {
                var sla = await IncidentSlaRead.ReadAsync(db, incident.IncidentId, ct);
                result.Add(new(incident.IncidentId, null, latest.EventId, latest.DiscoveredAt, null, null,
                    sla?.ResponseDeadlineAt, sla?.ResolutionDeadlineAt, null, null, null, "CURRENT_CYCLE_NOT_STARTED"));
            }
        }
        return result.ToArray();
    }

    private static bool Start(AuditLog audit, MaintenanceWork work, long incident)
    {
        try
        {
            using var before = JsonDocument.Parse(audit.OldValues ?? "null");
            using var after = JsonDocument.Parse(audit.NewValues ?? "null");
            var b = before.RootElement; var a = after.RootElement;
            return work.ActualStartAt == audit.OccurredAt && audit.ActorUserId > 0
                && b.GetProperty("status").GetString() == "PENDING" && a.GetProperty("status").GetString() == "IN_PROGRESS"
                && a.GetProperty("maintenance_work_id").GetInt64() == work.MaintenanceWorkId
                && a.GetProperty("incident_id").GetInt64() == incident
                && a.GetProperty("assignee_id").GetInt64() == audit.ActorUserId
                && a.GetProperty("actual_start_at").GetDateTime() == work.ActualStartAt;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException) { return false; }
    }

    private static IncidentSlaDto? HistoricalSla(AuditLog audit, DateTime discovery, long? reopen)
    {
        try
        {
            using var json = JsonDocument.Parse(audit.NewValues!);
            var a = json.RootElement;
            if (a.GetProperty("incident_cycle_discovered_at").GetDateTime() != discovery) return null;
            var version = a.GetProperty("incident_cycle_sla_version").GetInt32();
            if (version == 1 && reopen != null || version is not (1 or 2)) return null;
            if (version == 2)
            {
                var value = a.GetProperty("incident_cycle_reopen_event_id");
                if ((value.ValueKind == JsonValueKind.Null ? (long?)null : value.GetInt64()) != reopen) return null;
            }
            return a.GetProperty("incident_cycle_sla").Deserialize<IncidentSlaDto>();
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException) { return null; }
    }
}
