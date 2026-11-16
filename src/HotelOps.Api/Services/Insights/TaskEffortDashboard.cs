using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

internal static class TaskEffortDashboard
{
    internal static async Task AppendAsync(HotelOpsDbContext db, InsightFilters filters, DateTime now,
        List<MetricRow> metrics, List<string> missing, CancellationToken ct)
    {
        var installed = await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.work_intervals','U') IS NOT NULL AND OBJECT_ID('dbo.workdays','U') IS NOT NULL THEN 1 ELSE 0 END AS Value").SingleAsync(ct);
        if (installed != 1) { Unavailable("work interval storage unavailable"); return; }
        var period = new WorkTimePeriod(filters.From, filters.To, InsightAccess.Start(filters), InsightAccess.End(filters));
        var tasks = db.OperationalTasks.AsNoTracking().Where(t => filters.BranchIds.Contains(t.BranchId)
            && (filters.DepartmentId == null || t.DepartmentId == filters.DepartmentId));
        var intervals = await db.WorkIntervals.AsNoTracking().Where(i => i.WorkItemKind == "TASK"
            && filters.BranchIds.Contains(i.BranchId)
            && (filters.DepartmentId == null || tasks.Any(t => t.TaskId == i.WorkItemId)
                || db.Workdays.Any(d => d.WorkdayId == i.WorkdayId && d.DepartmentId == filters.DepartmentId))
            && i.EffectiveStartedAt < period.End && (i.EffectiveEndedAt == null && now > period.Start || i.EffectiveEndedAt > period.Start))
            .OrderBy(i => i.IntervalId).Take(50001).ToArrayAsync(ct);
        // Bounded materialization keeps consistency/overlap checks independent of provider translation.
        if (intervals.Length > 50000) { Unavailable("interval validation limit exceeded; narrow the period"); return; }
        var dayIds = intervals.Select(i => i.WorkdayId).Distinct().ToArray();
        var days = await db.Workdays.AsNoTracking().Where(d => dayIds.Contains(d.WorkdayId)).ToDictionaryAsync(d => d.WorkdayId, ct);
        var taskIds = intervals.Select(i => i.WorkItemId).Distinct().ToArray();
        var sources = await tasks.Where(t => taskIds.Contains(t.TaskId) && t.Department.BranchId == t.BranchId
            && (t.RoomId == null || t.Room!.BranchId == t.BranchId) && (t.AreaId == null || t.Area!.BranchId == t.BranchId))
            .Select(t => new { t.TaskId, t.BranchId, t.DepartmentId }).ToDictionaryAsync(t => t.TaskId, ct);
        if (intervals.Any(i => !days.TryGetValue(i.WorkdayId, out var day)
            || !sources.TryGetValue(i.WorkItemId, out var task) || task.BranchId != i.BranchId || task.DepartmentId != day.DepartmentId
            || day.UserId != i.UserId || day.BranchId != i.BranchId || i.EffectiveStartedAt < day.StartedAt
            || day.EndedAt != null && (i.EffectiveEndedAt == null || i.EffectiveEndedAt > day.EndedAt)
            || i.EffectiveStartedAt > now || i.EffectiveEndedAt < i.EffectiveStartedAt || i.EffectiveEndedAt > now
            || (i.ClosedAt == null) != (i.EffectiveEndedAt == null)))
        { Unavailable("inconsistent interval, workday or task provenance"); return; }
        try
        {
            foreach (var user in intervals.GroupBy(i => i.UserId)) OwnTimeLogReader.ValidateNonOverlap(user.ToArray(), now);
        }
        catch (WorkTimeContractException) { Unavailable("overlapping task work intervals"); return; }
        var closed = intervals.Where(i => i.ClosedAt != null).Sum(i => period.Seconds(i.EffectiveStartedAt, i.EffectiveEndedAt, now));
        var provisional = intervals.Where(i => i.ClosedAt == null).Sum(i => period.Seconds(i.EffectiveStartedAt, null, now));
        Add(closed / 3600m, provisional / 3600m, intervals.LongCount(i => i.ClosedAt == null));

        void Unavailable(string reason) { missing.Add("KPI-06: " + reason); Add(null, null, null); }
        void Add(decimal? closedHours, decimal? provisionalHours, long? openCount)
        {
            metrics.Add(new("taskEffortHours", "Task effort", closedHours, "hours", "Closed effective TASK intervals clipped to selected UTC period"));
            metrics.Add(new("taskProvisionalHours", "Task effort in open intervals", provisionalHours, "hours", "Provisional only; clipped at server snapshot and UTC period"));
            metrics.Add(new("taskOpenIntervals", "Open task intervals", openCount, "count", "Open intervals overlapping selected UTC period"));
        }
    }
}
