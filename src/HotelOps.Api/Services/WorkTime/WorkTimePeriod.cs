using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.Services.WorkTime;

// Inclusive hotel dates become a half-open UTC range. Never use a client clock
// for provisional time, and never mix workday duration into task effort.
internal readonly record struct WorkTimePeriod(DateOnly From, DateOnly To, DateTime Start, DateTime End)
{
    internal static WorkTimePeriod Create(DateOnly? from, DateOnly? to, DateTime now)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
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
