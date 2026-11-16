using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

internal static class MaintenanceExecutionAccess
{
    internal static bool Allows(string role, PermissionGrantPath grant, long branch, DateTime now)
        => role is "MAINTENANCE_STAFF" or "MAINTENANCE_LEAD" && grant.PermissionCode == "MAINTENANCE_EXECUTE"
            && grant.BranchId == branch && PermissionGrantRules.IsEligible(grant, now)
            && PermissionGrantRules.EffectiveScope(grant) is "OWN" or "BRANCH" or "GLOBAL";

    internal static async Task<bool> AllowsAsync(HotelOpsDbContext db, long actor, long branch, long work, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var paths = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == actor && assignment.BranchId == branch
                && grant.Permission.PermissionCode == "MAINTENANCE_EXECUTE"
                && assignment.User.AccountStatus == "ACTIVE" && assignment.User.EmploymentStatus == "ACTIVE"
                && assignment.User.DeletedAt == null
            select new MaintenanceReadAccess.Grant(assignment.Role.RoleCode, new PermissionGrantPath(grant.Permission.PermissionCode,
                grant.DataScope, assignment.BranchId, assignment.IsActive, assignment.Role.IsActive, assignment.AssignedAt, assignment.ExpiresAt,
                assignment.Branch != null && assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(a => a.UserId == actor && a.BranchId == branch && a.RevokedAt == null && a.GrantedAt <= now))))
            .ToArrayAsync(ct);
        if (!paths.Any(p => Allows(p.Role, p.Path, branch, now))) return false;
        var read = await MaintenanceReadAccess.ReadAsync(db, actor, branch, ct);
        return await read.Apply(db.MaintenanceWorks.AsNoTracking()).AnyAsync(w => w.MaintenanceWorkId == work
            && w.AssigneeId == actor && w.IncidentId != null && !w.PreventiveMaintenanceRuns.Any(), ct);
    }
}
