using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

internal static class TimeCorrectionAccess
{
    internal static async Task<bool> OwnAsync(HotelOpsDbContext db, long user, long branch, DateTime now, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.UserId == user && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE" && u.DeletedAt == null, ct)
            || !await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == user && a.BranchId == branch
                && a.GrantedAt <= now && a.RevokedAt == null && a.Branch.IsActive, ct)) return false;
        return (await new EfPermissionDataSource(db).ReadPathsAsync(user, "ATTENDANCE_RECORD", now, ct))
            .Any(p => p.PermissionCode == "ATTENDANCE_RECORD" && PermissionGrantRules.IsEligible(p, now)
                && (PermissionGrantRules.EffectiveScope(p) == "GLOBAL" || p.BranchId == branch && PermissionGrantRules.EffectiveScope(p) is "BRANCH" or "OWN"));
    }

    internal static async Task<bool> DecideAsync(HotelOpsDbContext db, long user, long branch, long department, DateTime now, CancellationToken ct)
    {
        if (!await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == user && r.IsActive && r.Role.IsActive
            && r.Role.RoleCode == "DEPT_MANAGER" && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && (r.BranchId == null || r.BranchId == branch) && r.User.AccountStatus == "ACTIVE"
            && r.User.EmploymentStatus == "ACTIVE" && r.User.DeletedAt == null, ct)
            || !await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == user && a.BranchId == branch
                && a.GrantedAt <= now && a.RevokedAt == null && a.Branch.IsActive, ct)
            || !await db.Departments.AsNoTracking().AnyAsync(d => d.DepartmentId == department && d.BranchId == branch && d.IsActive, ct)) return false;
        if (!await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.department_management_access','U') IS NULL THEN 0 ELSE 1 END AS Value").AnyAsync(x => x == 1, ct)) return false;
        if (!await db.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM dbo.department_management_access WITH(HOLDLOCK)
            WHERE user_id={user} AND branch_id={branch} AND department_id={department}
                AND granted_at<={now} AND revoked_at IS NULL
            """).AnyAsync(ct)) return false;
        var paths = await new EfPermissionDataSource(db).ReadPathsAsync(user, "TIME_CORRECTION_DECIDE", now, ct);
        return paths.Any(p => p.PermissionCode == "TIME_CORRECTION_DECIDE" && PermissionGrantRules.IsEligible(p, now)
            && (PermissionGrantRules.EffectiveScope(p) == "GLOBAL"
                || p.BranchId == branch && PermissionGrantRules.EffectiveScope(p) == "BRANCH"));
    }
}
