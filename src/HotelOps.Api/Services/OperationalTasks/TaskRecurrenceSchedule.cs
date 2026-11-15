using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.OperationalTasks;

namespace HotelOps.Api.Services.OperationalTasks;

internal static class TaskRecurrenceSchedule
{
    // GBR014/SYS-PAR10: recurrence weekdays and calendar end dates use hotel time.
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    internal static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    internal static TaskRecurrenceRequest Normalize(TaskRecurrenceRequest input, DateTime firstDueUtc)
    {
        if (input.DefaultAssigneeId <= 0 || input.Rule is not ("DAILY" or "WEEKLY" or "MONTHLY"))
            throw new ValidationException("A supported recurrence rule and default executor are required.");
        var days = input.Weekdays?.ToArray() ?? [];
        if (days.Length > 7 || days.Any(d => d is < 0 or > 6) || days.Distinct().Count() != days.Length
            || input.Rule == "WEEKLY" && (days.Length == 0 || input.MonthDay != null)
            || input.Rule != "WEEKLY" && days.Length != 0
            || input.Rule == "MONTHLY" && input.MonthDay is not (>= 1 and <= 31)
            || input.Rule != "MONTHLY" && input.MonthDay != null)
            throw new ValidationException("Weekly recurrence needs distinct weekdays; monthly recurrence needs a calendar day.");
        var first = Local(firstDueUtc);
        if (input.Rule == "WEEKLY" && !days.Contains((int)first.DayOfWeek)
            || input.Rule == "MONTHLY" && first.Day != input.MonthDay
            || input.EndDate is { } end && end < DateOnly.FromDateTime(first))
            throw new ValidationException("The first deadline must match the recurrence rule and precede its end date.");
        return input with { Weekdays = days.Order().ToArray() };
    }

    internal static DateTime? Next(DateTime currentDueUtc, TaskRecurrenceRequest rule)
    {
        var current = Local(currentDueUtc);
        DateTime? next = null;
        if (rule.Rule == "MONTHLY")
        {
            // A calendar month without the selected day has no such occurrence.
            // Never silently move a 31st-day task to another calendar day.
            var month = new DateTime(current.Year, current.Month, 1);
            while (month.Year < 9999 || month.Month < 12)
            {
                month = month.AddMonths(1);
                if (DateTime.DaysInMonth(month.Year, month.Month) < rule.MonthDay!.Value) continue;
                next = new DateTime(month.Year, month.Month, rule.MonthDay.Value).Add(current.TimeOfDay);
                break;
            }
        }
        else
        {
            for (var offset = 1; offset <= 7; offset++)
            {
                if (current.Date > DateTime.MaxValue.Date.AddDays(-offset)) break;
                var candidate = current.AddDays(offset);
                if (rule.Rule == "DAILY" || rule.Weekdays!.Contains((int)candidate.DayOfWeek)) { next = candidate; break; }
            }
        }
        if (next == null || rule.EndDate is { } end && DateOnly.FromDateTime(next.Value) > end) return null;
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(next.Value, DateTimeKind.Unspecified), Zone);
    }

    // JOB05 runs daily at 00:45 hotel time. SYS-PAR17 is one calendar day before due.
    internal static DateOnly GenerationHorizon(DateTime nowUtc)
    {
        var today = DateOnly.FromDateTime(Local(nowUtc));
        return today == DateOnly.MaxValue ? today : today.AddDays(1);
    }
}
