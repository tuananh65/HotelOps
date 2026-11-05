using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

public sealed record OwnOpenWorkInterval(long IntervalId, string WorkItemKind, long WorkItemId,
    DateTimeOffset OpenedAtUtc, string RowVersion);

public sealed record OwnWorkingDaySnapshot(long WorkdayId, long BranchId, long DepartmentId,
    DateOnly WorkDate, long? ShiftAssignmentId, bool IsUnscheduled, DateTimeOffset StartedAtUtc,
    string RowVersion, OwnOpenWorkInterval? OpenInterval);

/// <summary>
/// Read-only presentation query, not a permission boundary or a transition precondition.
/// The eventual API service must authorize its approved UC139 binding and revalidate the
/// SQL session before returning this result. No route or DI activation is included here.
/// Actor identity comes only from the authenticated request context, never request DTOs.
/// Writes must independently re-read state under the A1 transaction/locking protocol.
/// </summary>
public sealed class OwnWorkdaySnapshotReader(HotelOpsDbContext db, ICurrentUserAccessor current)
{
    public async Task<OwnWorkingDaySnapshot?> ReadAsync(long branchId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId <= 0 || actor.SessionId <= 0)
            throw new WorkTimeContractException("TIME_SESSION_INVALID");
        if (actor.IsRestricted) throw new WorkTimeContractException("TIME_RESTRICTED");
        if (branchId <= 0) throw new WorkTimeContractException("TIME_SCOPE_DENIED");

        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct);
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.UserId == actor.UserId
                && u.DeletedAt == null && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE", ct))
            throw new WorkTimeContractException("TIME_ACTOR_INVALID");
        if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == actor.UserId
                && a.BranchId == branchId && a.GrantedAt <= now && a.RevokedAt == null && a.Branch.IsActive, ct))
            throw new WorkTimeContractException("TIME_SCOPE_DENIED");

        var days = await db.Workdays.AsNoTracking().Where(d => d.UserId == actor.UserId
                && d.BranchId == branchId && d.Status == "WORKING")
            .Select(d => new { d.WorkdayId, d.BranchId, d.DepartmentId, d.WorkDate,
                d.ShiftAssignmentId, d.IsUnscheduled, d.StartedAt, d.RowVersion })
            .Take(2).ToArrayAsync(ct);
        if (days.Length > 1) throw new WorkTimeContractException("TIME_STATE_CONFLICT");
        if (days.Length == 0) return null;
        var day = days[0];
        var intervals = await db.WorkIntervals.AsNoTracking().Where(i => i.UserId == actor.UserId
                && i.BranchId == branchId && i.WorkdayId == day.WorkdayId && i.ClosedAt == null)
            .Select(i => new { i.IntervalId, i.WorkItemKind, i.WorkItemId, i.OpenedAt, i.RowVersion })
            .Take(2).ToArrayAsync(ct);
        if (intervals.Length > 1) throw new WorkTimeContractException("TIME_STATE_CONFLICT");
        var open = intervals.SingleOrDefault();
        return new(day.WorkdayId, day.BranchId, day.DepartmentId, day.WorkDate, day.ShiftAssignmentId,
            day.IsUnscheduled, Utc(day.StartedAt), Convert.ToBase64String(day.RowVersion),
            open is null ? null : new(open.IntervalId, open.WorkItemKind, open.WorkItemId,
                Utc(open.OpenedAt), Convert.ToBase64String(open.RowVersion)));
    }

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
