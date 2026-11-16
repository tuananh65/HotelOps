using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.Services.WorkTime;

// Inclusive hotel dates become a half-open UTC range. Never use a client clock
// for provisional time, and never mix workday duration into task effort.
internal readonly record struct WorkTimePeriod(DateOnly From, DateOnly To, DateTime Start, DateTime End)
{
    // Dashboard UTC ranges do not have the interactive workday editor's one-year
    // limit. Workload reads remain SQL-paginated; other callers keep Create guards.
    internal static WorkTimePeriod ForWorkload(DateOnly? from, DateOnly? to, DateTime now, string calendar)
    {
        if (calendar != "UTC") return Create(from, to, now, calendar);
        var first = from ?? DateOnly.FromDateTime(now);
        var last = to ?? first;
        if (last < first || last == DateOnly.MaxValue)
            throw new ValidationException("Select an ordered UTC period ending before 9999-12-31.");
        return new(first, last, first.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            last.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }

    internal static WorkTimePeriod Create(DateOnly? from, DateOnly? to, DateTime now, string calendar = "Asia/Ho_Chi_Minh")
    {
        if (calendar is not ("UTC" or "Asia/Ho_Chi_Minh"))
            throw new ValidationException("Unsupported work-time calendar.");
        var zone = calendar == "UTC" ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(now, DateTimeKind.Utc), zone));
        var first = from ?? today;
        var last = to ?? first;
        if (last < first || last == DateOnly.MaxValue || first.Year < 1900 || last.DayNumber - first.DayNumber > 365)
            throw new ValidationException("Select an ordered period of at most 366 days, from 1900 onwards.");
        return new(first, last, TimeZoneInfo.ConvertTimeToUtc(first.ToDateTime(TimeOnly.MinValue), zone),
            TimeZoneInfo.ConvertTimeToUtc(last.AddDays(1).ToDateTime(TimeOnly.MinValue), zone));
    }

    internal long Seconds(DateTime start, DateTime? end, DateTime asOf)
    {
        if (end < start || !end.HasValue && start > asOf)
            throw new ValidationException("Inconsistent recorded work interval.");
        var left = start > Start ? start : Start;
        var right = end ?? asOf;
        if (right > End) right = End;
        // Match the existing SQL DATEDIFF_BIG(second) effort contract.
        return right <= left ? 0 : right.Ticks / TimeSpan.TicksPerSecond - left.Ticks / TimeSpan.TicksPerSecond;
    }
}
