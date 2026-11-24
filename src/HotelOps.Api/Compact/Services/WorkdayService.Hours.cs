using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed record ScheduledMinutes(DateOnly Date, int Minutes);

public sealed partial class WorkdayService
{
    public static DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    public static WorkdayHourWarning[] HourWarnings(ScheduledMinutes[] existing, DateOnly[] dates, int duration)
    {
        var warnings = new List<WorkdayHourWarning>();
        foreach (var day in dates.Order())
        {
            var minutes = existing.Where(x => x.Date == day).Sum(x => x.Minutes);
            if (minutes + duration > 480) warnings.Add(new("DAY", day, day, minutes, duration, 480));
        }
        foreach (var week in dates.GroupBy(WeekStart).OrderBy(g => g.Key))
        {
            var end = week.Key.AddDays(6);
            var minutes = existing.Where(x => x.Date >= week.Key && x.Date <= end).Sum(x => x.Minutes);
            var added = week.Count() * duration;
            if (minutes + added > 2400) warnings.Add(new("WEEK", week.Key, end, minutes, added, 2400));
        }
        return warnings.ToArray();
    }

    private async Task<Dictionary<long, WorkdayHoursReview>> HoursReviews(WorkdayShift shift, DateOnly[] dates, long[] userIds, CancellationToken ct)
    {
        var first = WeekStart(dates.Min()); var last = WeekStart(dates.Max()).AddDays(6);
        // Sum the employee's schedule across departments/branches, without exposing other branch records.
        // ENDED still counts toward assigned hours; CANCELLED does not.
        var existing = await (from a in db.WorkdayAssignments.AsNoTracking()
            join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            where userIds.Contains(a.UserId) && a.Status != "CANCELLED" && a.WorkDate >= first && a.WorkDate <= last
            orderby a.AssignmentId
            select new { a.AssignmentId, a.UserId, a.WorkDate, a.RowVersion, minutes = s.EndMinute - s.StartMinute }).ToArrayAsync(ct);
        return userIds.Distinct().ToDictionary(id => id, id =>
        {
            var rows = existing.Where(x => x.UserId == id).ToArray();
            var warnings = HourWarnings(rows.Select(x => new ScheduledMinutes(x.WorkDate, x.minutes)).ToArray(), dates, shift.EndMinute - shift.StartMinute);
            var fingerprint = JsonSerializer.Serialize(new { userId = id, shift.ShiftId, shift.RowVersion, shift.StartMinute, shift.EndMinute, dates = dates.Order().ToArray(), rows });
            return new WorkdayHoursReview(warnings, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint))));
        });
    }

    private static void ConfirmHours(WorkdayHoursReview review, string? confirmation) =>
        Output.Require(review.Warnings.Length == 0 || string.Equals(review.Token, confirmation, StringComparison.Ordinal),
            "SHIFT_HOURS_CONFIRMATION_REQUIRED");

    // Keep each event within the existing reason column; full-batch evidence remains in audit.
    private static string? HoursReason(WorkdayHoursReview review, DateOnly date) => review.Warnings.Length == 0 ? null :
        "Đã xác nhận vượt giới hạn phân ca 8 giờ/ngày hoặc 40 giờ/tuần. " + string.Join("; ", review.Warnings.Where(w => date >= w.From && date <= w.To).Select(w =>
            $"{(w.Period == "DAY" ? "Ngày" : "Tuần")} {w.From:dd/MM/yyyy}: {w.TotalMinutes} phút / {w.LimitMinutes} phút"));
}
