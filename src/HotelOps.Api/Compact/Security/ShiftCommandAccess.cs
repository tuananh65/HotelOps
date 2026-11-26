using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Security;

public static class ShiftCommandAccess
{
    public static bool IsBusinessCommand(string method, string path) =>
        method.ToUpperInvariant() is not ("GET" or "HEAD" or "OPTIONS") &&
        new[]{"/work", "/rooms", "/documents", "/assets", "/requests", "/reusable-inventory", "/inventory-lifecycle", "/evidence", "/cleaning-reconciliation", "/supplies/room-assessments"}
            .Any(prefix => path.Equals("/api/v2"+prefix,StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/v2"+prefix+"/",StringComparison.OrdinalIgnoreCase)) &&
        !path.TrimEnd('/').Equals("/api/v2/work/room-issue-types",StringComparison.OrdinalIgnoreCase) &&
        !path.TrimEnd('/').Equals("/api/v2/work/internal-request-types",StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/api/v2/work/internal-request-types/",StringComparison.OrdinalIgnoreCase);

    public static bool Covers(DateOnly workDate,int startMinute,int endMinute,string timezone,DateTime now)
    {
        var zone=TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var start=workDate.ToDateTime(TimeOnly.MinValue).AddMinutes(startMinute);
        var end=workDate.ToDateTime(TimeOnly.MinValue).AddMinutes(endMinute);
        if(end<=start)end=end.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(start,zone)<=now && now<TimeZoneInfo.ConvertTimeToUtc(end,zone);
    }

    public static async Task<bool> CanOperate(HotelOpsCompactDbContext db,long userId,long branch,DateTime now,CancellationToken ct,string? permission=null,bool owns=false)
    {
        if(!await db.Users.AsNoTracking().AnyAsync(u=>u.UserId==userId&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null,ct))return false;
        var timezone=await db.Branches.AsNoTracking().Where(b=>b.BranchId==branch&&b.IsActive).Select(b=>b.Timezone).SingleOrDefaultAsync(ct);
        if(timezone==null)return false;
        var roles=await (from ur in db.UserRoles.AsNoTracking() join r in db.Roles on ur.RoleId equals r.RoleId
            where ur.UserId==userId&&ur.AssignedAt<=now&&ur.RevokedAt==null&&(ur.ExpiresAt==null||ur.ExpiresAt>now)&&r.IsActive
                &&(ur.BranchId==branch||ur.BranchId==null)
            select r.Code).ToArrayAsync(ct);
        // Administrative roles have no operational check-in requirement. Their normal permissions still apply.
        if(roles.Any(r=>r is "OWNER" or "SYS_ADMIN" or "OPERATION_MANAGER"))return true;
        if(!await db.Memberships.AsNoTracking().AnyAsync(m=>m.UserId==userId&&m.BranchId==branch&&m.Kind=="BRANCH_ACCESS"
            &&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now),ct))return false;
        var shifts=await (from a in db.WorkdayAssignments.AsNoTracking() join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            where a.UserId==userId&&s.BranchId==branch&&s.IsActive&&a.Status=="WORKING"&&a.StartedAt!=null&&a.StartedAt<=now&&a.EndedAt==null
                &&db.UserRoles.Any(ur=>ur.UserId==userId&&ur.RoleId==s.RoleId&&ur.BranchId==branch&&ur.AssignedAt<=now&&ur.RevokedAt==null&&(ur.ExpiresAt==null||ur.ExpiresAt>now))
                &&db.Roles.Any(r=>r.RoleId==s.RoleId&&r.IsActive)
                &&(permission==null||db.RolePermissions.Any(g=>g.RoleId==s.RoleId&&(g.DataScope=="ALL"||g.DataScope=="BRANCH"||g.DataScope=="OWN"&&owns)
                    &&db.Permissions.Any(p=>p.PermissionId==g.PermissionId&&p.IsActive&&p.Code==permission)))
                &&db.Departments.Any(d=>d.DepartmentId==s.DepartmentId&&d.BranchId==branch&&d.IsActive)
                &&db.Memberships.Any(m=>m.UserId==userId&&m.BranchId==branch&&m.Kind=="EMPLOYMENT"&&m.DepartmentId==s.DepartmentId&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now))
            select new {a.WorkDate,s.StartMinute,s.EndMinute}).ToArrayAsync(ct);
        return shifts.Any(s=>Covers(s.WorkDate,s.StartMinute,s.EndMinute,timezone,now));
    }

    public static async Task Require(HotelOpsCompactDbContext db,long userId,long branch,DateTime now,CancellationToken ct,string? permission=null,bool owns=false) =>
        Output.Require(await CanOperate(db,userId,branch,now,ct,permission,owns),"ACTIVE_SHIFT_REQUIRED",403);
}
