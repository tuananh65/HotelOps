using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

// UC131/UC132 share the approved Operations Manager / Owner actor contract. Keep the role and DASHBOARD_VIEW grant on the same assignment path.
public sealed class TaskDashboardAccess(HotelOpsDbContext db, InsightAccess access, TimeProvider clock)
{
    public long UserId => access.UserId;

    internal static bool Allows(string roleCode, PermissionGrantPath path, long? branch, DateTime now)
        => roleCode is "OPERATIONS_MANAGER" or "OWNER"
        && path.PermissionCode == "DASHBOARD_VIEW" && PermissionGrantRules.IsEligible(path, now)
        && (path.DataScope == "ALL" && path.BranchId == null
            || branch != null && path.BranchId == branch && path.DataScope is "ALL" or "BRANCH");

    internal async Task<bool> AllowedAsync(long? branch, CancellationToken ct)
    {
        var user = UserId;
        var now = clock.GetUtcNow().UtcDateTime;
        var paths = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == user && grant.Permission.PermissionCode == "DASHBOARD_VIEW"
                && assignment.User.AccountStatus == "ACTIVE" && assignment.User.DeletedAt == null
                && (assignment.Role.RoleCode == "OPERATIONS_MANAGER" || assignment.Role.RoleCode == "OWNER")
            select new { assignment.Role.RoleCode, Path = new PermissionGrantPath(grant.Permission.PermissionCode,
                grant.DataScope, assignment.BranchId, assignment.IsActive, assignment.Role.IsActive,
                assignment.AssignedAt, assignment.ExpiresAt, assignment.Branch != null && assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(a => a.UserId == user && a.BranchId == assignment.BranchId
                    && a.RevokedAt == null && a.GrantedAt <= now)) }).ToArrayAsync(ct);
        return paths.Any(p => Allows(p.RoleCode, p.Path, branch, now));
    }
}
