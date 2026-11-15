using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.OperationalTasks;

internal static class TaskAssignmentEligibility
{
    internal static IQueryable<User> Candidates(HotelOpsDbContext db, long branch, long department, DateTime now) =>
        db.Users.AsNoTracking().Where(u => u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE" && u.DeletedAt == null
            && u.DepartmentId == department && u.Department != null && u.Department.BranchId == branch && u.Department.IsActive && u.Department.Branch.IsActive
            && db.UserBranchAccesses.Any(a => a.UserId == u.UserId && a.BranchId == branch && a.GrantedAt <= now && a.RevokedAt == null)
            && db.UserRoles.Any(r => r.UserId == u.UserId && r.IsActive && r.Role.IsActive
                && (r.Role.RoleCode == "HK_STAFF" || r.Role.RoleCode == "MAINTENANCE_STAFF" || r.Role.RoleCode == "STOREKEEPER")
                && (r.BranchId == branch || r.BranchId == null) && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)));

    internal static IQueryable<OperationalTask> ScopedTasks(HotelOpsDbContext db, long branch) =>
        db.OperationalTasks.AsNoTracking().Where(t => t.BranchId == branch && t.Department.BranchId == branch && t.Department.IsActive
            && (t.RoomId == null || t.Room!.BranchId == branch) && (t.AreaId == null || t.Area!.BranchId == branch));

    // SYS-PAR-10, matching the workday service. Only persisted, closed effective
    // intervals count as logged time; live elapsed time is not committed effort.
    internal static DateTime StartOfDayUtc(DateTime now)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var date = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(now, DateTimeKind.Utc), zone).Date;
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), zone);
    }
}
