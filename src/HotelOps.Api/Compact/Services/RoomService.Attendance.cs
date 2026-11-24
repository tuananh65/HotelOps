using Microsoft.EntityFrameworkCore;
using HotelOps.Api.Compact.Infrastructure;

namespace HotelOps.Api.Compact.Services;

public sealed partial class RoomService
{
    private async Task<bool> CurrentReceptionShift(long branchId, CancellationToken ct)
    {
        var timezone = await db.Branches.AsNoTracking().Where(b => b.BranchId == branchId).Select(b => b.Timezone).SingleAsync(ct);
        var instant = clock.GetUtcNow();
        var local = TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(timezone));
        var day = DateOnly.FromDateTime(local.DateTime);
        var minute = local.Hour * 60 + local.Minute;
        var now = instant.UtcDateTime;
        var working = await (from a in db.WorkdayAssignments.AsNoTracking()
            join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            where a.UserId == actor.Id && s.BranchId == branchId && s.IsActive
                && a.WorkDate == day && a.Status == "WORKING" && a.StartedAt != null
                && a.StartedAt <= now && a.EndedAt == null
                && s.StartMinute <= minute && minute < s.EndMinute
            select a.AssignmentId).AnyAsync(ct);
        return working;
    }
}
