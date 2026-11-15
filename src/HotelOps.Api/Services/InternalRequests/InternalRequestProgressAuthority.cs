using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.InternalRequests;

// GBR149: assignment is independent of Department Head management authority.
public sealed class InternalRequestProgressAuthority(HotelOpsDbContext db, InternalRequestProcessAuthority managers)
{
    internal static bool EligibleHandler(string role, PermissionGrantPath path, long branch, DateTime now) =>
        role is "HK_SUPERVISOR" or "HK_STAFF" or "MAINTENANCE_LEAD" or "MAINTENANCE_STAFF" or "STOREKEEPER" or "DEPT_MANAGER"
        && path.PermissionCode == "IR_PROCESS" && path.BranchId == branch
        && PermissionGrantRules.IsEligible(path, now)
        && PermissionGrantRules.EffectiveScope(path) is "OWN" or "BRANCH";

    internal async Task<(bool CanUpdate, bool CanChangeDeadline)> ResolveAsync(long user, long request, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Progress authority requires a serializable transaction.");
        var users = await new UserEligibilityLock(db).LockAsync([user], ct);
        if (!users.TryGetValue(user, out var actor) || actor is null || actor.DeletedAt != null
            || actor.AccountStatus != "ACTIVE" || actor.EmploymentStatus != "ACTIVE") return (false, false);
        var row = await db.InternalRequests.FromSqlInterpolated($"SELECT * FROM dbo.internal_requests WITH(UPDLOCK,HOLDLOCK) WHERE internal_request_id={request}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (row?.ReceivingDepartmentId is not {} department || row.Status != "IN_PROGRESS") return (false, false);
        if (await managers.AllowsDepartmentAsync(user, row.BranchId, department, ct)) return (true, true);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        // Home department is relevant to handler membership, never management authority.
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.UserId == user && u.DepartmentId == department, ct)
            || !await db.Departments.AsNoTracking().AnyAsync(d => d.DepartmentId == department
            && d.BranchId == row.BranchId && d.IsActive && d.Branch.IsActive, ct)) return (false, false);
        var assignments = await db.InternalRequestAssignments.AsNoTracking().Where(a => a.InternalRequestId == request && a.Status == "ACTIVE")
            .Select(a => new { a.AssigneeId, a.AssignedAt, a.EndedAt }).ToArrayAsync(ct);
        if (assignments.Length != 1 || assignments[0].AssigneeId != user || assignments[0].EndedAt != null
            || assignments[0].AssignedAt > now) return (false, false);
        var grants = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == user && grant.Permission.PermissionCode == "IR_PROCESS"
            select new { Role = assignment.Role.RoleCode, Path = new PermissionGrantPath(grant.Permission.PermissionCode,
                grant.DataScope, assignment.BranchId, assignment.IsActive, assignment.Role.IsActive,
                assignment.AssignedAt, assignment.ExpiresAt, assignment.Branch != null && assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(a => a.UserId == user && a.BranchId == assignment.BranchId
                    && a.RevokedAt == null && a.GrantedAt <= now)) }).ToArrayAsync(ct);
        return (grants.Any(g => EligibleHandler(g.Role, g.Path, row.BranchId, now)), false);
    }
}
