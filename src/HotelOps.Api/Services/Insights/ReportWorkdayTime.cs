using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

internal sealed record EmployeeWorkdayTime(long EmployeeId, long BranchId, long ClosedWorkdaySeconds,
    long ProvisionalWorkdaySeconds, long OpenWorkdays);
internal sealed record ReportWorkdayTimeResult(EmployeeWorkdayTime[]? Rows, string? MissingReason);

// UC135 aggregates actual workday time. Report admission owns role/scope/session;
// this never exposes the UC113 workday-detail endpoint or substitutes task effort.
internal static class ReportWorkdayTime
{
    internal static async Task<ReportWorkdayTimeResult> ReadAsync(HotelOpsDbContext db,
        InsightFilters filters, DateTime now, CancellationToken ct)
    {
        if (await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.workdays','U') IS NOT NULL THEN 1 ELSE 0 END AS Value").SingleAsync(ct) != 1)
            return new(null, "workday storage unavailable");
        var period = new WorkTimePeriod(filters.From, filters.To, InsightAccess.Start(filters), InsightAccess.End(filters));
        var days = await db.Workdays.AsNoTracking().Where(d => filters.BranchIds.Contains(d.BranchId)
            && (filters.DepartmentId == null || d.DepartmentId == filters.DepartmentId)
            && d.StartedAt < period.End && (d.EndedAt == null && now > period.Start || d.EndedAt > period.Start))
            .OrderBy(d => d.WorkdayId).Take(50001).ToArrayAsync(ct);
        if (days.Length > 50000) return new(null, "workday validation limit exceeded; narrow the period");
        var departments = days.Select(d => d.DepartmentId).Distinct().ToArray();
        var scopes = await db.Departments.AsNoTracking().Where(d => departments.Contains(d.DepartmentId))
            .Select(d => new { d.DepartmentId, d.BranchId }).ToDictionaryAsync(d => d.DepartmentId, ct);
        var assignmentIds = days.Where(d => d.ShiftAssignmentId != null).Select(d => d.ShiftAssignmentId!.Value).Distinct().ToArray();
        var assignments = await db.ShiftAssignments.AsNoTracking().Where(a => assignmentIds.Contains(a.ShiftAssignmentId))
            .Select(a => new { a.ShiftAssignmentId, a.UserId, a.Shift.BranchId, a.Shift.DepartmentId })
            .ToDictionaryAsync(a => a.ShiftAssignmentId, ct);
        if (days.Any(d => !scopes.TryGetValue(d.DepartmentId, out var scope) || scope.BranchId != d.BranchId
            || d.StartedAt > now || d.EndedAt < d.StartedAt || d.EndedAt > now
            || d.Status is not ("WORKING" or "ENDED") || (d.Status == "WORKING") != (d.EndedAt == null)
            || d.IsUnscheduled != (d.ShiftAssignmentId == null)
            || d.ShiftAssignmentId is {} id && (!assignments.TryGetValue(id, out var assignment)
                || assignment.UserId != d.UserId || assignment.BranchId != d.BranchId || assignment.DepartmentId != d.DepartmentId)))
            return new(null, "inconsistent workday time or scope provenance");
        try
        {
            foreach (var user in days.GroupBy(d => d.UserId))
                WorkIntervalMath.Summarize(user.Select(d => new RecordedWorkRange(Utc(d.StartedAt),
                    d.EndedAt is {} end ? Utc(end) : null)), Utc(now));
        }
        catch (ArgumentException) { return new(null, "overlapping workdays"); }
        return new(days.GroupBy(d => new { d.UserId, d.BranchId }).OrderBy(g => g.Key.UserId).ThenBy(g => g.Key.BranchId)
            .Select(g => new EmployeeWorkdayTime(g.Key.UserId, g.Key.BranchId,
                g.Where(d => d.EndedAt != null).Sum(d => period.Seconds(d.StartedAt, d.EndedAt, now)),
                g.Where(d => d.EndedAt == null).Sum(d => period.Seconds(d.StartedAt, null, now)),
                g.LongCount(d => d.EndedAt == null))).ToArray(), null);
    }

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
