using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

internal sealed record TaskEmployeeEffort(long EmployeeId, long BranchId, long ClosedEffortSeconds, long ProvisionalEffortSeconds, long OpenIntervals);
internal sealed record TaskEffortReadResult(TaskEmployeeEffort[]? Rows, string? MissingReason);

// Shared validated interval calculation only. Callers retain authorization and snapshot ownership.
internal static class TaskEffortRead
{
    internal static async Task<TaskEffortReadResult> ReadAsync(HotelOpsDbContext db, InsightFilters filters, DateTime now, CancellationToken ct, long? employeeId = null)
    {
        var installed = await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.work_intervals','U') IS NOT NULL AND OBJECT_ID('dbo.workdays','U') IS NOT NULL THEN 1 ELSE 0 END AS Value").SingleAsync(ct);
        if (installed != 1) { return new(null, "work interval storage unavailable"); }
        var period = new WorkTimePeriod(filters.From, filters.To, InsightAccess.Start(filters), InsightAccess.End(filters));
        var tasks = db.OperationalTasks.AsNoTracking().Where(t => filters.BranchIds.Contains(t.BranchId)
            && (filters.DepartmentId == null || t.DepartmentId == filters.DepartmentId));
        var intervals = await db.WorkIntervals.AsNoTracking().Where(i => i.WorkItemKind == "TASK"
            && (employeeId == null || i.UserId == employeeId)
            && filters.BranchIds.Contains(i.BranchId)
            && (filters.DepartmentId == null || tasks.Any(t => t.TaskId == i.WorkItemId)
                || db.Workdays.Any(d => d.WorkdayId == i.WorkdayId && d.DepartmentId == filters.DepartmentId))
            && i.EffectiveStartedAt < period.End && (i.EffectiveEndedAt == null && now > period.Start || i.EffectiveEndedAt > period.Start))
            .OrderBy(i => i.IntervalId).Take(50001).ToArrayAsync(ct);
        // Bounded materialization keeps consistency/overlap checks independent of provider translation.
        if (intervals.Length > 50000) { return new(null, "interval validation limit exceeded; narrow the period"); }
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
        { return new(null, "inconsistent interval, workday or task provenance"); }
        try
        {
            foreach (var user in intervals.GroupBy(i => i.UserId)) OwnTimeLogReader.ValidateNonOverlap(user.ToArray(), now);
        }
        catch (WorkTimeContractException) { return new(null, "overlapping task work intervals"); }
        var rows = intervals.GroupBy(i => new { i.UserId, i.BranchId }).OrderBy(g => g.Key.UserId).ThenBy(g => g.Key.BranchId)
            .Select(g => new TaskEmployeeEffort(g.Key.UserId, g.Key.BranchId,
                g.Where(i => i.ClosedAt != null).Sum(i => period.Seconds(i.EffectiveStartedAt, i.EffectiveEndedAt, now)),
                g.Where(i => i.ClosedAt == null).Sum(i => period.Seconds(i.EffectiveStartedAt, null, now)),
                g.LongCount(i => i.ClosedAt == null))).ToArray();
        return new(rows, null);
    }
}
