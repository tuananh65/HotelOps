using HotelOps.Api.Compact.Contracts;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkdayService
{
    public static AttendancePeriod AttendanceTotal(string period, DateOnly from, DateOnly to, AttendanceEntry[] entries)
    {
        var completed = entries.Where(x => x.WorkDate >= from && x.WorkDate <= to && x.Status == "ENDED"
            && x.StartedAt != null && x.EndedAt != null && x.EndedAt >= x.StartedAt).ToArray();
        var seconds = completed.Sum(x => (x.EndedAt!.Value - x.StartedAt!.Value).TotalSeconds);
        return new(period, from, to, completed.Length, (long)Math.Floor(seconds), Math.Round((decimal)seconds / 3600, 2),
            entries.Count(x => x.WorkDate >= from && x.WorkDate <= to && x.Status == "WORKING" && x.StartedAt != null));
    }

    public async Task<AttendanceSummaryDto> PersonalSummary(long branch, CancellationToken ct)
    {
        await access.Require("ATTENDANCE_RECORD", branch, true, ct);
        // Reuse the existing expiry workflow so a finished shift is not left out until the next worker tick.
        await expiry.CloseExpired(branch, ct);
        var timezone = await db.Branches.AsNoTracking().Where(b => b.BranchId == branch).Select(b => b.Timezone).SingleAsync(ct);
        var today = LocalDate(clock.GetUtcNow(), timezone);
        var week = WeekStart(today); var month = new DateOnly(today.Year, today.Month, 1);
        var firstDay = week < month ? week : month;
        var entries = await (from a in db.WorkdayAssignments.AsNoTracking()
            join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            where a.UserId == actor.Id && s.BranchId == branch && a.WorkDate >= firstDay && a.WorkDate <= today
                && (a.Status == "ENDED" || a.Status == "WORKING")
            select new AttendanceEntry(a.WorkDate, a.Status, a.StartedAt, a.EndedAt)).ToArrayAsync(ct);
        return new(branch, timezone, Now, [AttendanceTotal("WEEK", week, week.AddDays(6), entries),
            AttendanceTotal("MONTH", month, month.AddMonths(1).AddDays(-1), entries)]);
    }
}
