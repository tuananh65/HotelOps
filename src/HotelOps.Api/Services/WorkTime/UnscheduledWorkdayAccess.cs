using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

internal static class UnscheduledWorkdayAccess
{
    internal static async Task<long?> HeadAsync(HotelOpsDbContext db, long branch, long department, DateTime now, CancellationToken ct)
    {
        if (!await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.department_management_access','U') IS NULL THEN 0 ELSE 1 END AS Value").AnyAsync(x => x == 1, ct))
            return null;
        var head = await db.Departments.AsNoTracking().Where(d => d.DepartmentId == department && d.BranchId == branch && d.IsActive)
            .Select(d => d.ManagerUserId).SingleOrDefaultAsync(ct);
        if (head is null || !await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == head
            && r.Role.RoleCode == "DEPT_MANAGER" && r.IsActive && r.Role.IsActive && r.AssignedAt <= now
            && (r.ExpiresAt == null || r.ExpiresAt > now) && (r.BranchId == branch || r.BranchId == null)
            && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE" && r.User.DeletedAt == null, ct)) return null;
        if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == head && a.BranchId == branch
            && a.GrantedAt <= now && a.RevokedAt == null && a.Branch.IsActive, ct)) return null;
        if (!await db.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM dbo.department_management_access WITH(HOLDLOCK)
            WHERE user_id={head} AND branch_id={branch} AND department_id={department}
                AND granted_at<={now} AND revoked_at IS NULL
            """).AnyAsync(ct)) return null;
        foreach (var path in await new EfPermissionDataSource(db).ReadPathsAsync(head.Value, "WORKLOAD_VIEW", now, ct))
            if (path.PermissionCode == "WORKLOAD_VIEW" && PermissionGrantRules.IsEligible(path, now)
                && (PermissionGrantRules.EffectiveScope(path) == "GLOBAL"
                    || path.BranchId == branch && PermissionGrantRules.EffectiveScope(path) == "BRANCH")) return head;
        return null;
    }

    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db, long user, long branch, long id, DateTime now, CancellationToken ct)
    {
        var department = await db.Workdays.AsNoTracking().Where(d => d.WorkdayId == id && d.BranchId == branch && d.IsUnscheduled)
            .Select(d => (long?)d.DepartmentId).SingleOrDefaultAsync(ct);
        return department is { } dept && await HeadAsync(db, branch, dept, now, ct) == user;
    }
}
