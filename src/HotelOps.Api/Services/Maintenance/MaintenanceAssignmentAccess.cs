using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

using HotelOps.Api.Services.Incidents;

internal static class MaintenanceAssignmentAccess
{
    internal static bool Allows(string role, PermissionGrantPath grant, long branch, DateTime now)
        => role == "MAINTENANCE_LEAD" && grant.PermissionCode == "MAINTENANCE_ASSIGN" && grant.BranchId == branch
            && PermissionGrantRules.IsEligible(grant, now) && PermissionGrantRules.EffectiveScope(grant) is "BRANCH" or "GLOBAL";

    internal static async Task<bool> AllowsAsync(HotelOpsDbContext db, long user, long branch, long incident, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var paths = await (from role in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on role.RoleId equals grant.RoleId
            where role.UserId == user && role.BranchId == branch && role.Role.RoleCode == "MAINTENANCE_LEAD"
                && grant.Permission.PermissionCode == "MAINTENANCE_ASSIGN" && role.User.AccountStatus == "ACTIVE"
                && role.User.EmploymentStatus == "ACTIVE" && role.User.DeletedAt == null
            select new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, role.BranchId,
                role.IsActive, role.Role.IsActive, role.AssignedAt, role.ExpiresAt, role.Branch != null && role.Branch.IsActive,
                db.UserBranchAccesses.Any(b => b.UserId == user && b.BranchId == branch && b.RevokedAt == null && b.GrantedAt <= now)))
            .ToArrayAsync(ct);
        if (!paths.Any(p => Allows("MAINTENANCE_LEAD", p, branch, now))) return false;
        var read = await IncidentReadAccess.ReadAsync(db, user, branch, ct, sourceRecord: true);
        var maintenance = await MaintenanceReadAccess.ReadAsync(db, user, branch, ct);
        if (!maintenance.Lead) return false;
        return await read.Apply(db.Incidents.AsNoTracking()).AnyAsync(i => i.IncidentId == incident, ct);
    }
}
