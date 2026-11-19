using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

internal static class IncidentEscalationHandlingAccess
{
    internal static bool Allows(long branch, IEnumerable<IncidentReadAccess.Grant> grants, DateTime now)
        => grants.Any(g => g.Role == "OPERATIONS_MANAGER"
            && g.Path.PermissionCode == "INCIDENT_ESCALATION_HANDLE" && g.Path.BranchId == branch
            && PermissionGrantRules.IsEligible(g.Path, now)
            && PermissionGrantRules.EffectiveScope(g.Path) == "BRANCH");

    internal static async Task<bool> AllowsAsync(HotelOpsDbContext db, long user, long branch, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var grants = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == user && assignment.BranchId == branch
                && grant.Permission.PermissionCode == "INCIDENT_ESCALATION_HANDLE"
                && assignment.User.AccountStatus == "ACTIVE" && assignment.User.EmploymentStatus == "ACTIVE"
                && assignment.User.DeletedAt == null
            select new IncidentReadAccess.Grant(assignment.Role.RoleCode,
                new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, assignment.BranchId,
                    assignment.IsActive, assignment.Role.IsActive, assignment.AssignedAt, assignment.ExpiresAt,
                    assignment.Branch != null && assignment.Branch.IsActive,
                    db.UserBranchAccesses.Any(a => a.UserId == user && a.BranchId == branch
                        && a.RevokedAt == null && a.GrantedAt <= now)))).ToArrayAsync(ct);
        return Allows(branch, grants, now);
    }
}
