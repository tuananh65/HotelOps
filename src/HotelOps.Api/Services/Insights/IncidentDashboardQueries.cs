using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.Incidents;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

internal static class IncidentDashboardQueries
{
    internal static async Task AppendAsync(HotelOpsDbContext db, InsightFilters filters, DateTime now,
        List<MetricRow> metrics, List<DistributionRow> groups, List<string> missing, CancellationToken ct)
    {
        var start = InsightAccess.Start(filters); var end = InsightAccess.End(filters);
        var incidents = db.Incidents.AsNoTracking().Where(i => filters.BranchIds.Contains(i.BranchId)
            && (i.RoomId == null || i.Room!.BranchId == i.BranchId)
            && (i.AreaId == null || i.Area!.BranchId == i.BranchId)
            && (i.AssetId == null || i.Asset!.BranchId == i.BranchId));
        if (filters.DepartmentId is {} department)
        {
            var current = IncidentCurrentAssignment.Query(db);
            if (await incidents.AnyAsync(i => !current.Any(w => w.IncidentId == i.IncidentId && w.DepartmentId != null), ct))
                missing.Add("UC133: incidents without a proven current work department are excluded from the department filter.");
            incidents = incidents.Where(i => current.Any(w => w.IncidentId == i.IncidentId && w.DepartmentId == department));
        }
        var open = await incidents.Where(i => i.Status != "CLOSED" && i.Status != "CANCELLED")
            .GroupBy(i => new { i.Status, i.SeverityId }).Select(g => new { g.Key.Status, g.Key.SeverityId, Count = g.LongCount() }).ToArrayAsync(ct);
        groups.AddRange(open.Select(g => new DistributionRow("incidents.status/severity_id", g.Status, g.Count, g.SeverityId?.ToString())));
        metrics.Add(new("openIncidents", "Open incidents", open.Sum(g => g.Count), "count", "KPI-07"));
        var ids = incidents.Select(i => i.IncidentId);
        var blocks = db.RoomBlocks.AsNoTracking().Where(b => b.IncidentId != null && ids.Contains(b.IncidentId.Value)
            && b.Room.BranchId == b.Incident!.BranchId && b.Room.IsActive && b.BlockedAt <= now
            && (b.ReleasedAt == null || b.ReleasedAt > now));
        metrics.Add(new("incidentBlockedRooms", "Rooms with effective incident blocks", await blocks.Select(b => b.RoomId).Distinct().LongCountAsync(ct),
            "rooms", "Effective Incident blocks; no readiness inferred"));
        var cycles = await ReportIncidentCycles.ReadAsync(db, incidents, now, ct);
        var currentCycles = cycles.GroupBy(c => c.IncidentId).SelectMany(g =>
        {
            var latest = g.Max(c => c.ReopenEventId ?? 0);
            return g.Where(c => (c.ReopenEventId ?? 0) == latest);
        }).ToArray();
        var response = currentCycles.Where(c => c.AcceptedAt >= start && c.AcceptedAt < end && c.ResponseSeconds != null).ToArray();
        var resolution = currentCycles.Where(c => c.ResolvedAt >= start && c.ResolvedAt < end && c.ResolutionSeconds != null).ToArray();
        metrics.Add(new("responseHours", "Average response time", response.Length == 0 ? null : response.Average(c => c.ResponseSeconds) / 3600m,
            "hours", "KPI-08 current cycles accepted in period; missing timestamps excluded"));
        metrics.Add(new("resolutionHours", "Average resolution time", resolution.Length == 0 ? null : resolution.Average(c => c.ResolutionSeconds) / 3600m,
            "hours", "KPI-08 current cycles resolved in period; missing timestamps excluded"));
        var resolved = cycles.Where(c => c.ResolvedAt >= start && c.ResolvedAt < end).ToArray();
        metrics.Add(new("slaCompliance", "SLA compliance", resolved.Length == 0 || resolved.Any(c => c.WithinResolutionSla == null)
            ? null : 100m * resolved.Count(c => c.WithinResolutionSla == true) / resolved.Length,
            "percent", "KPI-09 proven cycles resolved in period; missing SLA is N/A"));
        if (cycles.Any(c => c.MissingReason != null)) missing.Add("UC133: incomplete cycle timestamps or SLA evidence; available measurements exclude unknown timestamps and never substitute planned dates.");
        var reopened = await db.IncidentEvents.AsNoTracking().Where(e => ids.Contains(e.IncidentId)
            && e.EventType == "REOPENED" && e.OccurredAt >= start && e.OccurredAt < end && e.OccurredAt <= now)
            .Select(e => new { e.EventId, e.IncidentId }).ToArrayAsync(ct);
        var proven = cycles.Where(c => c.ReopenEventId != null && c.DiscoveredAt != null).Select(c => c.ReopenEventId!.Value).ToHashSet();
        metrics.Add(new("reopenedIncidents", "Reopened incidents", reopened.Any(e => !proven.Contains(e.EventId)) ? null
            : reopened.Select(e => e.IncidentId).Distinct().Count(), "count", "KPI-10 distinct incidents with proven reopen events in period"));
        if (reopened.Any(e => !proven.Contains(e.EventId))) missing.Add("KPI-10: one or more reopen boundaries lack complete immutable evidence.");
        var occurrences = await ReportPreventiveOccurrences.ReadAsync(db, filters, now, ct);
        groups.AddRange(occurrences.GroupBy(o => o.WorkflowStatus ?? "UNKNOWN")
            .Select(g => new DistributionRow("preventive_occurrences.workflow_status", g.Key, g.LongCount())));
        metrics.Add(new("preventiveDue", "Preventive occurrences due in period", occurrences.Length, "occurrences", "Stored occurrence due; selected UTC period"));
        metrics.Add(new("preventiveOverdue", "Overdue preventive occurrences", occurrences.Any(o => o.Overdue == null) ? null
            : occurrences.Count(o => o.Overdue == true), "occurrences", "Open occurrences due in period before snapshot"));
        if (occurrences.Any(o => o.MissingReason != null)) missing.Add("UC133: preventive lifecycle evidence is incomplete; overdue is N/A.");
    }
}
