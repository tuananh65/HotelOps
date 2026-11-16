using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;

namespace HotelOps.Api.Services.Insights;

internal static class TaskEffortDashboard
{
    internal static async Task AppendAsync(HotelOpsDbContext db, InsightFilters filters, DateTime now,
        List<MetricRow> metrics, List<string> missing, CancellationToken ct)
    {
        var result = await TaskEffortRead.ReadAsync(db, filters, now, ct);
        if (result.Rows is not {} rows) { Unavailable(result.MissingReason!); return; }
        Add(rows.Sum(r => r.ClosedEffortSeconds) / 3600m, rows.Sum(r => r.ProvisionalEffortSeconds) / 3600m,
            rows.Sum(r => r.OpenIntervals));
        void Unavailable(string reason) { missing.Add("KPI-06: " + reason); Add(null, null, null); }
        void Add(decimal? closedHours, decimal? provisionalHours, long? openCount)
        {
            metrics.Add(new("taskEffortHours", "Task effort", closedHours, "hours", "Closed effective TASK intervals clipped to selected UTC period"));
            metrics.Add(new("taskProvisionalHours", "Task effort in open intervals", provisionalHours, "hours", "Provisional only; clipped at server snapshot and UTC period"));
            metrics.Add(new("taskOpenIntervals", "Open task intervals", openCount, "count", "Open intervals overlapping selected UTC period"));
        }
    }
}
