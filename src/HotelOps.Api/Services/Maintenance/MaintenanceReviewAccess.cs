using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.Maintenance;

internal static class MaintenanceReviewAccess
{
    internal static async Task<bool> AllowsAsync(HotelOpsDbContext db, long user, long branch, long work, CancellationToken ct)
    {
        if (await MaintenanceReviewSeparation.PerformedAsync(db, user, work, ct)) return false;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var paths = await (from role in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on role.RoleId equals grant.RoleId
            where role.UserId == user && role.BranchId == branch && role.Role.RoleCode == "MAINTENANCE_LEAD"
                && grant.Permission.PermissionCode == "MAINTENANCE_REVIEW" && role.User.AccountStatus == "ACTIVE"
                && role.User.EmploymentStatus == "ACTIVE" && role.User.DeletedAt == null
            select new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, role.BranchId,
                role.IsActive, role.Role.IsActive, role.AssignedAt, role.ExpiresAt, role.Branch != null && role.Branch.IsActive,
                db.UserBranchAccesses.Any(b => b.UserId == user && b.BranchId == branch && b.RevokedAt == null && b.GrantedAt <= now)))
            .ToArrayAsync(ct);
        if (!paths.Any(p => PermissionGrantRules.IsEligible(p, now) && PermissionGrantRules.EffectiveScope(p) is "BRANCH" or "GLOBAL")) return false;
        var read = await MaintenanceReadAccess.ReadAsync(db, user, branch, ct);
        return await read.Apply(db.MaintenanceWorks.AsNoTracking()).AnyAsync(w => w.MaintenanceWorkId == work, ct);
    }
}
