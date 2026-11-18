using System.Data;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

public sealed record WorkTimeDepartmentOption(long Id, string Name);
public sealed record WorkTimeBranchOption(long BranchId, string Name, bool IsPrimary, bool CanReadShifts,
    bool CanReadOwnTime, IReadOnlyList<WorkTimeDepartmentOption> CorrectionDepartments);

public sealed partial class WorkdayService
{
    // Discover existing authority for each screen; subsequent reads/writes still authorize independently.
    public async Task<WorkdayResult> WorkspaceOptionsAsync(CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.IsRestricted
            || http.HttpContext is not { } context || !HotelOpsCookieTicket.TryRead(context.User, out var ticket, out var sid)
            || ticket is null || ticket.UserId != actor.UserId || sid != actor.SessionId)
            return new(401, Code: "SESSION_INVALID");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await participant.LockUsersAsync([actor.UserId], ct);
            async Task<bool> Valid()
            {
                var value = await sessions.ValidateSessionAsync(ticket, ct);
                return value.IsValid && value.Session is { IsRestricted: false } s && s.UserId == actor.UserId && s.SessionId == sid;
            }
            if (!await Valid()) return new(401, Code: "SESSION_INVALID");
            var now = await CorrectionNow(ct);
            var primary = await db.Users.AsNoTracking().Where(u => u.UserId == actor.UserId).Select(u => u.PrimaryBranchId).SingleAsync(ct);
            var branches = await db.UserBranchAccesses.AsNoTracking().Where(a => a.UserId == actor.UserId && a.RevokedAt == null
                && a.GrantedAt <= now && a.Branch.IsActive).Select(a => new { a.BranchId, a.Branch.BranchName })
                .Distinct().OrderBy(b => b.BranchName).ToArrayAsync(ct);
            var options = new List<WorkTimeBranchOption>();
            foreach (var b in branches)
            {
                var own = await Authorize("ATTENDANCE_RECORD", new("workday_self_branch", b.BranchId), ct);
                var shifts = await Authorize("SHIFT_VIEW", new("workday_self_branch", b.BranchId), ct);
                if (own is { Status: not 403 }) return own;
                if (shifts is { Status: not 403 }) return shifts;
                var departments = new List<WorkTimeDepartmentOption>();
                foreach (var d in await db.Departments.AsNoTracking().Where(d => d.BranchId == b.BranchId && d.IsActive)
                    .OrderBy(d => d.DepartmentName).Select(d => new { d.DepartmentId, d.DepartmentName }).ToArrayAsync(ct))
                    if (await TimeCorrectionAccess.DecideAsync(db, actor.UserId, b.BranchId, d.DepartmentId, now, ct))
                        departments.Add(new(d.DepartmentId, d.DepartmentName));
                if (own is null || shifts is null || departments.Count > 0)
                    options.Add(new(b.BranchId, b.BranchName, b.BranchId == primary, shifts is null, own is null, departments));
            }
            if (!await Valid()) return new(401, Code: "SESSION_INVALID");
            var finalNow = await CorrectionNow(ct);
            foreach (var b in options)
            {
                if (b.CanReadOwnTime && await Authorize("ATTENDANCE_RECORD", new("workday_self_branch", b.BranchId), ct) is { } deniedOwn) return deniedOwn;
                if (b.CanReadShifts && await Authorize("SHIFT_VIEW", new("workday_self_branch", b.BranchId), ct) is { } deniedShifts) return deniedShifts;
                foreach (var d in b.CorrectionDepartments)
                    if (!await TimeCorrectionAccess.DecideAsync(db, actor.UserId, b.BranchId, d.Id, finalNow, ct)) return new(403, Code: "CORRECTION_FORBIDDEN");
            }
            await tx.CommitAsync(ct);
            return new(200, options);
        }
        catch (SessionStoreUnavailableException) { return new(503, Code: "SESSION_STORE_UNAVAILABLE"); }
        catch (SqlException) when (!ct.IsCancellationRequested) { return new(503, Code: "WORKDAY_STORE_UNAVAILABLE"); }
        catch (WorkTimeContractException) { return new(503, Code: "WORKDAY_STORE_UNAVAILABLE"); }
    }
}
