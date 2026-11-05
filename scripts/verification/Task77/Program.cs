using HotelOps.Api.Services.WorkTime;

if (args is ["--di"])
{
    RuntimeDiVerification.Run();
    return;
}
if (args.Length == 3 && args[0] == "--sql")
{
    await WorkTimeSqlAcceptance.RunAsync(args[1], args[2]);
    return;
}
if (args.Length == 3 && args[0] == "--production-host")
{
    await ProductionHostProbe.RunAsync(args[1], args[2]);
    return;
}

var checks = 0;
var zero = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);
RecordedWorkRange Range(int start, int? end) => new(zero.AddMinutes(start), end is int value ? zero.AddMinutes(value) : null);
void Check(bool value, string label)
{
    if (!value) throw new InvalidOperationException("FAIL: " + label);
    checks++;
}
void Reject(Action action, string label)
{
    try { action(); }
    catch (ArgumentException) { checks++; return; }
    throw new InvalidOperationException("FAIL: " + label);
}

Check(WorkIntervalMath.Summarize([], zero) == new WorkEffort(TimeSpan.Zero, TimeSpan.Zero), "Empty time is zero, independent of shift attendance");
Check(WorkIntervalMath.Summarize([Range(0, 60), Range(90, 120)], zero.AddMinutes(150)).Closed == TimeSpan.FromMinutes(90), "A pause is excluded from effort");
var total = WorkIntervalMath.Summarize([Range(0, 60), Range(90, null)], zero.AddMinutes(120));
Check(total.Closed == TimeSpan.FromHours(1) && total.ProvisionalOpen == TimeSpan.FromMinutes(30), "Open time is provisional, not closed effort");
Check(WorkIntervalMath.Summarize([Range(30, 60), Range(0, 30)], zero).Closed == TimeSpan.FromHours(1), "Unordered adjacent intervals");
Check(!WorkIntervalMath.Overlaps(Range(0, 30), Range(30, null)), "Close then resume at same server timestamp");
Check(WorkIntervalMath.Overlaps(Range(0, 30), Range(29, 60)), "Partial overlap");
Check(WorkIntervalMath.Overlaps(Range(0, 60), Range(20, 30)), "Nested correction overlap");
Check(!WorkIntervalMath.Overlaps(Range(20, 20), Range(0, null)), "Zero-duration closed event occupies no time");
Check(WorkIntervalMath.Summarize([Range(10, 10), Range(0, 30)], zero).Closed == TimeSpan.FromMinutes(30), "Zero-duration event does not double count");
Check(WorkIntervalMath.Overlaps(Range(0, null), Range(30, null)), "Two open intervals overlap");
Reject(() => WorkIntervalMath.Summarize([Range(0, null), Range(30, null)], zero.AddHours(1)), "Multiple open intervals");
Reject(() => WorkIntervalMath.Summarize([Range(0, 30), Range(29, 60)], zero), "Do not silently sum overlapping legacy data");
Reject(() => WorkIntervalMath.Summarize([Range(0, 100), Range(20, 30), Range(40, 50)], zero), "Nested intervals cannot escape overlap detection");
Reject(() => WorkIntervalMath.Validate(Range(30, 29)), "Negative duration");
Reject(() => WorkIntervalMath.Summarize([Range(1, null)], zero), "Snapshot before open start");
Reject(() => WorkIntervalMath.Validate(new(zero.ToOffset(TimeSpan.FromHours(7)), null)), "Offset input must be explicitly converted to UTC");
Reject(() => WorkIntervalMath.Validate(new(zero, zero.AddHours(1).ToOffset(TimeSpan.FromHours(7)))), "End offset must be UTC");
Reject(() => WorkIntervalMath.Summarize([], zero.ToOffset(TimeSpan.FromHours(7))), "Snapshot offset must be UTC");
Reject(() => WorkIntervalMath.Summarize(null!, zero), "Null collection");
Check(WorkIntervalMath.Summarize([new(zero, zero.AddTicks(1))], zero).Closed.Ticks == 1, "Preserve SQL datetime2(7) precision without minute rounding");
Check(WorkIntervalMath.Summarize([Range(0, null)], zero).ProvisionalOpen == TimeSpan.Zero, "Start snapshot has zero provisional effort");
Check(WorkIntervalMath.Summarize([new(zero.AddHours(-2), zero.AddHours(2))], zero.AddHours(2)).Closed == TimeSpan.FromHours(4), "UTC midnight crossing");

// Exhaustive discrete closed ranges: compare against occupied one-minute slots,
// independent of the range predicate used by production arithmetic.
for (var a = 0; a <= 4; a++)
for (var b = a; b <= 5; b++)
for (var c = 0; c <= 4; c++)
for (var d = c; d <= 5; d++)
{
    var left = Enumerable.Range(a, b - a).ToHashSet();
    var right = Enumerable.Range(c, d - c).ToHashSet();
    Check(WorkIntervalMath.Overlaps(Range(a, b), Range(c, d)) == left.Overlaps(right), "Overlap matches occupied-slot oracle");
}
Console.WriteLine($"PASS: {checks} WorkIntervalMath checks. Pure UTC/overlap/effort arithmetic only; no SQL, HTTP, persistence, authentication or concurrency acceptance.");
