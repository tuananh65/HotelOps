using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed class AssetConditionAuthority(HotelOpsDbContext db, AssetProfileAccess profile, InsightAccess access)
{
    internal async Task RequireAsync(long branch, long? department, CancellationToken ct)
    {
        await profile.RequireAsync(branch, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var paths = await (from role in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on role.RoleId equals grant.RoleId
            where role.UserId == access.UserId && grant.Permission.PermissionCode == "ASSET_MANAGE"
                && (role.Role.RoleCode == "OPERATIONS_MANAGER" || role.Role.RoleCode == "DEPT_MANAGER")
            select new { Role = role.Role.RoleCode, Path = new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope,
                role.BranchId, role.IsActive, role.Role.IsActive, role.AssignedAt, role.ExpiresAt, role.Branch != null && role.Branch.IsActive,
                db.UserBranchAccesses.Any(a => a.UserId == access.UserId && a.BranchId == branch && a.RevokedAt == null && a.GrantedAt <= now)) }).ToArrayAsync(ct);
        var eligible = paths.Where(p => PermissionGrantRules.IsEligible(p.Path, now)
            && (PermissionGrantRules.EffectiveScope(p.Path) == "GLOBAL" || p.Path.BranchId == branch && PermissionGrantRules.EffectiveScope(p.Path) == "BRANCH")).ToArray();
        if (eligible.Any(p => p.Role == "OPERATIONS_MANAGER")) return;
        if (department != null && eligible.Any(p => p.Role == "DEPT_MANAGER")
            && await new InternalRequestProcessAuthority(db).InstalledAsync(ct)
            && await db.Departments.AnyAsync(d => d.DepartmentId == department && d.BranchId == branch && d.IsActive, ct)
            && await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM dbo.department_management_access WITH(HOLDLOCK)
                WHERE user_id={access.UserId} AND branch_id={branch} AND department_id={department}
                  AND granted_at<={now} AND revoked_at IS NULL
                """).SingleAsync(ct) == 1) return;
        throw new InsightException(403, "ACCESS_DENIED", "Current Operations Manager or mapped Department Head with ASSET_MANAGE required.");
    }
}
