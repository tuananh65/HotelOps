using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed record PersonnelRow(long UserId, string EmployeeCode, string FullName, string? Phone,
    string? Email, string Department, string[] Roles, bool Working);

public sealed partial class WorkdayService
{
    public async Task<PersonnelRow[]> Personnel(long branchId, CancellationToken ct)
    {
        await access.Require("SHIFT_MANAGE", branchId, ct: ct);
        var now = Now;
        var memberships = db.Memberships.AsNoTracking().Where(m => m.BranchId == branchId
            && m.Kind == "EMPLOYMENT" && m.ValidFrom <= now && (m.ValidTo == null || m.ValidTo > now));
        var roles = await (from ur in db.UserRoles.AsNoTracking()
            join r in db.Roles on ur.RoleId equals r.RoleId
            where (ur.BranchId == branchId || ur.BranchId == null) && ur.AssignedAt <= now
                && ur.RevokedAt == null && (ur.ExpiresAt == null || ur.ExpiresAt > now) && r.IsActive
            select new { ur.UserId, r.Code, r.Name }).ToArrayAsync(ct);
        var excluded = roles.Where(r => r.Code is "OPERATION_MANAGER" or "OWNER" or "SYS_ADMIN")
            .Select(r => r.UserId).ToArray();
        var users = await db.Users.AsNoTracking().Where(u => u.DeletedAt == null
            && memberships.Any(m => m.UserId == u.UserId) && !excluded.Contains(u.UserId))
            .OrderBy(u => u.FullName).Select(u => new { u.UserId, u.EmployeeCode, u.FullName, u.Phone, u.Email }).ToArrayAsync(ct);
        var departments = await (from m in memberships join d in db.Departments on m.DepartmentId equals d.DepartmentId
            where d.BranchId == branchId select new { m.UserId, d.Name }).ToArrayAsync(ct);
        var zone = await db.Branches.AsNoTracking().Where(b => b.BranchId == branchId).Select(b => b.Timezone).SingleAsync(ct);
        var sessions = await (from a in db.WorkdayAssignments.AsNoTracking()
            join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            where s.BranchId == branchId && s.IsActive && a.Status == "WORKING" && a.StartedAt != null
                && a.StartedAt <= now && a.EndedAt == null
                && db.Users.Any(u=>u.UserId==a.UserId&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null)
                && db.UserRoles.Any(ur=>ur.UserId==a.UserId&&ur.RoleId==s.RoleId&&ur.BranchId==branchId&&ur.AssignedAt<=now&&ur.RevokedAt==null&&(ur.ExpiresAt==null||ur.ExpiresAt>now))
                && db.Roles.Any(r=>r.RoleId==s.RoleId&&r.IsActive)
                && db.Departments.Any(d=>d.DepartmentId==s.DepartmentId&&d.BranchId==branchId&&d.IsActive)
                && db.Memberships.Any(m=>m.UserId==a.UserId&&m.BranchId==branchId&&m.Kind=="BRANCH_ACCESS"&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now))
                && memberships.Any(m=>m.UserId==a.UserId&&m.DepartmentId==s.DepartmentId)
            select new { a.UserId, a.WorkDate, s.StartMinute, s.EndMinute }).ToArrayAsync(ct);
        var working = sessions.Where(a => HotelOps.Api.Compact.Security.ShiftCommandAccess.Covers(a.WorkDate,a.StartMinute,a.EndMinute,zone,now))
            .Select(a => a.UserId).ToHashSet();
        return users.Select(u => new PersonnelRow(u.UserId, u.EmployeeCode, u.FullName, u.Phone, u.Email,
            string.Join(", ", departments.Where(d => d.UserId == u.UserId).Select(d => d.Name).Distinct()),
            roles.Where(r => r.UserId == u.UserId).Select(r => r.Name).Distinct().ToArray(), working.Contains(u.UserId))).ToArray();
    }
}
