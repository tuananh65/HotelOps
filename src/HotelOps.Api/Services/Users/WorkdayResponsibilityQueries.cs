using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

/// <summary>FE01 D05: Working requires employee End, even without intervals. Never ends work on the employee's behalf.</summary>
public sealed class WorkdayResponsibilityQueries(HotelOpsDbContext db)
{
    public static IQueryable<Workday> ForTarget(IQueryable<Workday> source, long targetId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetId);
        return source.Where(w => w.UserId == targetId);
    }

    internal async Task<UserResponsibilityAssessment> ReadAsync(long targetId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Owner SQL transaction and retained User locks required.");
        // Across every branch, including inactive branches and unscheduled records.
        // A missing/unverified deployed table must throw/rollback, never mean empty.
        var rows = await ForTarget(db.Workdays.AsNoTracking(), targetId).Select(w => new Workday
        {
            WorkdayId = w.WorkdayId,
            UserId = w.UserId,
            BranchId = w.BranchId,
            DepartmentId = w.DepartmentId,
            ShiftAssignmentId = w.ShiftAssignmentId,
            IsUnscheduled = w.IsUnscheduled,
            UnscheduledReason = w.UnscheduledReason,
            Status = w.Status,
            StartedAt = w.StartedAt,
            EndedAt = w.EndedAt
        }).ToArrayAsync(ct);
        var assessment = Assess(targetId, rows);
        var blockers = new SortedSet<string>(assessment.Blockers, StringComparer.Ordinal);
        var unresolved = new SortedSet<string>(assessment.Unresolved, StringComparer.Ordinal);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (rows.Any(w => w.StartedAt > now || w.EndedAt > now))
            unresolved.Add("WORKDAY_DATA_INCONSISTENCY");
        var assignmentIds = rows.Where(w => w.ShiftAssignmentId != null).Select(w => w.ShiftAssignmentId!.Value).Distinct().ToArray();
        var assignments = await db.ShiftAssignments.AsNoTracking().Where(a => assignmentIds.Contains(a.ShiftAssignmentId))
            .Select(a => new { a.ShiftAssignmentId, a.UserId, a.Shift.BranchId, a.Shift.DepartmentId, a.AssignmentStatus })
            .ToDictionaryAsync(a => a.ShiftAssignmentId, ct);
        foreach (var day in rows)
        {
            if (day.IsUnscheduled != (day.ShiftAssignmentId == null)
                || day.IsUnscheduled && (string.IsNullOrWhiteSpace(day.UnscheduledReason)
                    || day.UnscheduledReason.Length > 1000 || day.UnscheduledReason.Contains('\0')))
                unresolved.Add("WORKDAY_ASSIGNMENT_DATA_INCONSISTENCY");
            if (day.ShiftAssignmentId is { } assignment && (!assignments.TryGetValue(assignment, out var assigned)
                || assigned.UserId != targetId || assigned.BranchId != day.BranchId || assigned.DepartmentId != day.DepartmentId
                || assigned.AssignmentStatus == "CANCELLED"))
                unresolved.Add("WORKDAY_ASSIGNMENT_DATA_INCONSISTENCY");
        }
        // Include both ownership directions: an orphaned interval or an interval attributed to
        // someone else inside this user's day must not disappear from the responsibility review.
        var intervals = await (from interval in db.WorkIntervals.AsNoTracking()
            join day in db.Workdays.AsNoTracking() on interval.WorkdayId equals day.WorkdayId into days
            from day in days.DefaultIfEmpty()
            where interval.UserId == targetId || day != null && day.UserId == targetId
            select new { interval.UserId, interval.BranchId, interval.OpenedAt, interval.ClosedAt,
                interval.ClosedBy, interval.EffectiveStartedAt, interval.EffectiveEndedAt,
                DayUser = day == null ? (long?)null : day.UserId,
                DayBranch = day == null ? (long?)null : day.BranchId,
                DayStatus = day == null ? null : day.Status,
                DayStart = day == null ? (DateTime?)null : day.StartedAt,
                DayEnd = day == null ? (DateTime?)null : day.EndedAt }).ToArrayAsync(ct);
        foreach (var interval in intervals)
        {
            if (interval.ClosedAt == null) blockers.Add("OPEN_WORK_INTERVAL_END_REQUIRED");
            if (interval.DayUser != targetId || interval.UserId != targetId || interval.BranchId != interval.DayBranch
                || interval.OpenedAt < interval.DayStart || interval.OpenedAt > now
                || interval.ClosedAt < interval.OpenedAt || interval.ClosedAt > now
                || interval.EffectiveStartedAt < interval.DayStart || interval.EffectiveStartedAt > now
                || interval.EffectiveEndedAt < interval.EffectiveStartedAt || interval.EffectiveEndedAt > now
                || (interval.ClosedAt == null) != (interval.ClosedBy == null)
                || (interval.ClosedAt == null) != (interval.EffectiveEndedAt == null)
                || interval.DayStatus is not ("WORKING" or "ENDED")
                || interval.DayStatus == "ENDED" && (interval.DayEnd == null || interval.ClosedAt == null
                    || interval.ClosedAt > interval.DayEnd || interval.EffectiveEndedAt > interval.DayEnd)
                || interval.DayStatus == "WORKING" && interval.DayEnd != null)
                unresolved.Add("WORK_INTERVAL_DATA_INCONSISTENCY");
        }
        if (intervals.Count(i => i.ClosedAt == null) > 1)
            unresolved.Add("WORK_INTERVAL_DATA_INCONSISTENCY");
        return new(blockers.ToArray(), unresolved.ToArray());
    }

    public static UserResponsibilityAssessment Assess(long targetId, IReadOnlyList<Workday> rows)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetId);
        ArgumentNullException.ThrowIfNull(rows);
        var blockers = new SortedSet<string>(StringComparer.Ordinal);
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row.UserId != targetId || row.WorkdayId <= 0 || row.BranchId <= 0 || row.DepartmentId <= 0
                || row.StartedAt == default || row.EndedAt < row.StartedAt)
            { unresolved.Add("WORKDAY_DATA_INCONSISTENCY"); continue; }
            if (row.Status == "WORKING")
            {
                if (row.EndedAt is not null) unresolved.Add("WORKDAY_DATA_INCONSISTENCY");
                else blockers.Add("WORKING_WORKDAY_END_REQUIRED");
            }
            else if (row.Status != "ENDED") unresolved.Add("WORKDAY_STATUS_UNRESOLVED");
            else if (row.EndedAt is null) unresolved.Add("WORKDAY_DATA_INCONSISTENCY");
        }
        if (rows.Count(w => w.Status == "WORKING") > 1 || rows.Select(w => w.WorkdayId).Distinct().Count() != rows.Count)
            unresolved.Add("WORKDAY_DATA_INCONSISTENCY");
        // Empty/Ended results do not certify assignment, interval, writer or model coverage.
        return new(Array.AsReadOnly(blockers.ToArray()), Array.AsReadOnly(unresolved.ToArray()));
    }
}
