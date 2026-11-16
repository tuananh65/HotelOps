using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

internal static class TaskDashboardQueries
{
    internal static async Task AppendAsync(HotelOpsDbContext db, InsightFilters filters, DateTime now,
        List<MetricRow> metrics, List<DistributionRow> groups, List<string> missing, CancellationToken ct)
    {
        if (!await ProgressSchema.InstalledAsync(db, ct))
        {
            missing.Add("KPI-04: canonical task lifecycle storage unavailable");
            AddMetrics(null, null, null);
            return;
        }
        var tasks = db.OperationalTasks.AsNoTracking().Where(t => filters.BranchIds.Contains(t.BranchId)
            && (filters.DepartmentId == null || t.DepartmentId == filters.DepartmentId));
        var workflow = TaskWorkflowRead.Query(db, now);
        var rows = from task in tasks
            join state in workflow on task.TaskId equals state.TaskId into states
            from state in states.DefaultIfEmpty()
            select new {
                State = task.Department.BranchId != task.BranchId
                    || task.RoomId != null && task.Room!.BranchId != task.BranchId
                    || task.AreaId != null && task.Area!.BranchId != task.BranchId ? null : state.State,
                task.PriorityId, task.DueAt, task.CompletedAt
            };
        var distribution = await rows.GroupBy(r => new { r.State, r.PriorityId })
            .Select(g => new { g.Key.State, g.Key.PriorityId, Count = g.LongCount() }).ToArrayAsync(ct);
        groups.AddRange(distribution.Select(g => new DistributionRow("operational_tasks.workflow_status/priority_id",
            g.State ?? "UNKNOWN", g.Count, g.PriorityId?.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        if (distribution.Any(g => g.State == null))
        {
            missing.Add("KPI-04: task lifecycle or location is inconsistent; open/overdue/closed totals withheld, UNKNOWN rows shown separately");
            AddMetrics(null, null, null);
            return;
        }
        var open = rows.Where(r => r.State != "CLOSED" && r.State != "CANCELLED");
        var start = InsightAccess.Start(filters);
        var end = InsightAccess.End(filters);
        AddMetrics(await open.LongCountAsync(ct), await open.LongCountAsync(r => r.DueAt != null && r.DueAt < now, ct),
            await rows.LongCountAsync(r => r.State == "CLOSED" && r.CompletedAt >= start && r.CompletedAt < end && r.CompletedAt <= now, ct));

        void AddMetrics(long? open, long? overdue, long? closed)
        {
            metrics.Add(new("openTasks", "Open tasks", open, "count", "KPI-04 canonical lifecycle at snapshot"));
            metrics.Add(new("overdueTasks", "Overdue tasks", overdue, "count", "KPI-04 due before snapshot; excludes Closed/Cancelled"));
            metrics.Add(new("closedTasksInPeriod", "Tasks closed in period", closed, "count", "Canonical Closed; completion timestamp in UTC period"));
        }
    }
}
