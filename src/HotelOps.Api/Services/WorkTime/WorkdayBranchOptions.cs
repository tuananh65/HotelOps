using System.Data;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

public sealed record OwnWorkdayBranch(long BranchId, string Name, bool IsPrimary);

public sealed partial class WorkdayService
{
    // Discovery only: start/end and all subsequent reads retain their own authorization.
    public async Task<WorkdayResult> BranchOptionsAsync(CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId <= 0 || actor.SessionId <= 0)
            return new(401, Code: "SESSION_INVALID");
        if (actor.IsRestricted) return new(403, Code: "PASSWORD_CHANGE_REQUIRED");
        if (http.HttpContext is not { } context || !HotelOpsCookieTicket.TryRead(context.User, out var ticket, out var sid)
            || ticket is null || ticket.UserId != actor.UserId || sid != actor.SessionId)
            return new(401, Code: "SESSION_INVALID");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await participant.LockUsersAsync([actor.UserId], ct);
            async Task<bool> ValidSession()
            {
                var valid = await sessions.ValidateSessionAsync(ticket, ct);
                return valid.IsValid && valid.Session?.SessionId == actor.SessionId
                    && valid.Session.UserId == actor.UserId && !valid.Session.IsRestricted;
            }
            if (!await ValidSession()) return new(401, Code: "SESSION_INVALID");
            var user = await db.Users.AsNoTracking().Where(u => u.UserId == actor.UserId && u.DeletedAt == null
                && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE")
                .Select(u => new { u.PrimaryBranchId }).SingleOrDefaultAsync(ct);
            if (user is null) return new(403, Code: "TIME_ACTOR_INVALID");
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            var branches = await db.UserBranchAccesses.AsNoTracking().Where(a => a.UserId == actor.UserId
                && a.RevokedAt == null && a.GrantedAt <= now && a.Branch.IsActive)
                .Select(a => new { a.BranchId, Name = a.Branch.BranchName, IsPrimary = a.BranchId == user.PrimaryBranchId })
                .Distinct().OrderByDescending(b => b.IsPrimary).ThenBy(b => b.Name).ToArrayAsync(ct);
            var allowed = new List<OwnWorkdayBranch>();
            foreach (var branch in branches)
            {
                var resource = new PermissionResource("workday_self_branch", branch.BranchId);
                var read = await Authorize("SHIFT_VIEW", resource, ct);
                var record = await Authorize("ATTENDANCE_RECORD", resource, ct);
                if (read is { Status: not 403 }) return read;
                if (record is { Status: not 403 }) return record;
                if (read is null && record is null) allowed.Add(new(branch.BranchId, branch.Name, branch.IsPrimary));
            }
            if (!await ValidSession()) return new(401, Code: "SESSION_INVALID");
            foreach (var branch in allowed)
                foreach (var permission in new[] { "SHIFT_VIEW", "ATTENDANCE_RECORD" })
                    if (await Authorize(permission, new("workday_self_branch", branch.BranchId), ct) is { } denied)
                        return denied;
            await tx.CommitAsync(ct);
            return new(200, allowed);
        }
        catch (SessionStoreUnavailableException) { return new(503, Code: "SESSION_STORE_UNAVAILABLE"); }
        catch (SqlException) when (!ct.IsCancellationRequested) { return new(503, Code: "WORKDAY_STORE_UNAVAILABLE"); }
        catch (WorkTimeContractException) { return new(503, Code: "WORKDAY_STORE_UNAVAILABLE"); }
    }
}
