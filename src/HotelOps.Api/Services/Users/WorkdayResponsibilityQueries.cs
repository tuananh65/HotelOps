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
            Status = w.Status,
            StartedAt = w.StartedAt,
            EndedAt = w.EndedAt
        }).ToArrayAsync(ct);
        return Assess(targetId, rows);
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
