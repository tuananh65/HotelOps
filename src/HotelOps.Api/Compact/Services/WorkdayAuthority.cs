using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

// Domain handlers must be working in the role that owns the queue, not merely hold that role.
internal static class WorkdayAuthority
{
    internal static async Task Require(HotelOpsCompactDbContext db,Access access,Actor actor,TimeProvider clock,long branch,string role,CancellationToken ct)
    {
        await access.Eligible(actor.Id,branch,ct);
        Output.Require(await access.HasRole(role,branch,ct),"REQUEST_ACTOR_ROLE_REQUIRED",403);
        var now=clock.GetUtcNow().UtcDateTime;
        var zone=TimeZoneInfo.FindSystemTimeZoneById(await db.Branches.Where(b=>b.BranchId==branch&&b.IsActive).Select(b=>b.Timezone).SingleAsync(ct));
        var shifts=await (from a in db.WorkdayAssignments.AsNoTracking()
            join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            join r in db.Roles on s.RoleId equals r.RoleId
            where a.UserId==actor.Id&&a.Status=="WORKING"&&a.StartedAt!=null&&a.StartedAt<=now&&a.EndedAt==null
                &&s.BranchId==branch&&s.IsActive&&r.IsActive&&r.Code==role
                &&db.Memberships.Any(m=>m.UserId==actor.Id&&m.BranchId==branch&&m.Kind=="EMPLOYMENT"&&m.DepartmentId==s.DepartmentId&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now))
            select new{a.WorkDate,s.StartMinute,s.EndMinute}).ToArrayAsync(ct);
        Output.Require(shifts.Any(s=>TimeZoneInfo.ConvertTimeToUtc(s.WorkDate.ToDateTime(TimeOnly.MinValue).AddMinutes(s.StartMinute),zone)<=now
            &&now<TimeZoneInfo.ConvertTimeToUtc(s.WorkDate.ToDateTime(TimeOnly.MinValue).AddMinutes(s.EndMinute),zone)),"ACTIVE_SHIFT_REQUIRED",403);
    }
}
