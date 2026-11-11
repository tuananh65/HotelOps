namespace HotelOps.Api.Services.WorkTime;

/// <summary>A dated shift interval; not authorization, a persisted roster or payroll effort.</summary>
public sealed record PlannedShiftRange(DateOnly ShiftDate, DateTimeOffset StartUtc, DateTimeOffset EndUtc);

/// <summary>
/// UC138/GBR-141: materialize template wall-clock times on a roster date before checking overlap.
/// The caller supplies a verified branch timezone and must authorize and lock sources separately.
/// Ambiguous or nonexistent local times require explicit resolution; never guess an offset.
/// </summary>
public static class ShiftSchedulePlanner
{
    public static PlannedShiftRange Plan(DateOnly date, TimeOnly start, TimeOnly end,
        bool crossesMidnight, TimeZoneInfo timezone)
    {
        ArgumentNullException.ThrowIfNull(timezone);
        var endDate = crossesMidnight ? date.AddDays(1) : date;
        var localStart = date.ToDateTime(start, DateTimeKind.Unspecified);
        var localEnd = endDate.ToDateTime(end, DateTimeKind.Unspecified);
        if (localEnd <= localStart)
            throw new ArgumentException("The template must produce a positive shift interval.");
        var startUtc = Convert(localStart, timezone);
        var endUtc = Convert(localEnd, timezone);
        if (endUtc <= startUtc)
            throw new ArgumentException("The resolved shift must have positive duration.");
        return new(date, startUtc, endUtc);
    }

    private static DateTimeOffset Convert(DateTime local, TimeZoneInfo timezone)
    {
        if (timezone.IsInvalidTime(local) || timezone.IsAmbiguousTime(local))
            throw new ArgumentException("The shift time requires an unambiguous timezone resolution.");
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, timezone);
        if (TimeZoneInfo.ConvertTimeFromUtc(utc, timezone) != local)
            throw new ArgumentException("The shift time is outside the supported UTC range.");
        return new DateTimeOffset(utc);
    }
}