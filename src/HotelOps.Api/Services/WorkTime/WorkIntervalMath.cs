namespace HotelOps.Api.Services.WorkTime;

/// <summary>UTC time values only; this value object neither persists nor authorizes work.</summary>
public readonly record struct RecordedWorkRange(DateTimeOffset StartUtc, DateTimeOffset? EndUtc);

/// <summary>GBR-140: closed effort and provisional open time remain separate.</summary>
public readonly record struct WorkEffort(TimeSpan Closed, TimeSpan ProvisionalOpen);

/// <summary>
/// Time arithmetic for GBR-014/137/139/140. Callers must supply authoritative SQL records.
/// These checks do not replace database uniqueness, transaction locks or source authorization.
/// </summary>
public static class WorkIntervalMath
{
    public static void Validate(RecordedWorkRange range)
    {
        RequireUtc(range.StartUtc);
        if (range.EndUtc is not { } end) return;
        RequireUtc(end);
        if (end < range.StartUtc) throw new ArgumentException("An interval cannot end before it starts.", nameof(range));
    }

    public static bool Overlaps(RecordedWorkRange left, RecordedWorkRange right)
    {
        Validate(left);
        Validate(right);
        // Half-open ranges permit adjacent work. Zero-duration closed events consume no time.
        if (left.EndUtc == left.StartUtc || right.EndUtc == right.StartUtc) return false;
        return (right.EndUtc is null || left.StartUtc < right.EndUtc.Value)
            && (left.EndUtc is null || right.StartUtc < left.EndUtc.Value);
    }

    public static WorkEffort Summarize(IEnumerable<RecordedWorkRange> source, DateTimeOffset asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(source);
        RequireUtc(asOfUtc);
        var ranges = source.ToArray();
        foreach (var range in ranges) Validate(range);
        if (ranges.Count(range => range.EndUtc is null) > 1)
            throw new ArgumentException("A user cannot have multiple open intervals.", nameof(source));

        var occupied = ranges.Where(range => range.EndUtc != range.StartUtc)
            .OrderBy(range => range.StartUtc).ToArray();
        for (var i = 1; i < occupied.Length; i++)
            if (Overlaps(occupied[i - 1], occupied[i]))
                throw new ArgumentException("Overlapping intervals cannot be summed as independent effort.", nameof(source));

        long closedTicks = 0;
        long openTicks = 0;
        foreach (var range in ranges)
        {
            if (range.EndUtc is { } end)
                closedTicks = checked(closedTicks + (end - range.StartUtc).Ticks);
            else
            {
                if (asOfUtc < range.StartUtc)
                    throw new ArgumentOutOfRangeException(nameof(asOfUtc), "The snapshot cannot precede an open interval.");
                openTicks = (asOfUtc - range.StartUtc).Ticks;
            }
        }
        return new(TimeSpan.FromTicks(closedTicks), TimeSpan.FromTicks(openTicks));
    }

    private static void RequireUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("An explicit UTC value is required.", nameof(value));
    }
}
