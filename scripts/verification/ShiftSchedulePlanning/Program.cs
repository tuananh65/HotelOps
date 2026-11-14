using HotelOps.Api.Entities;
using HotelOps.Api.Services.WorkTime;

var count = 0;
void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); count++; Console.WriteLine("PASS: " + label); }
void Reject(Action action, string label) { try { action(); } catch (ArgumentException) { Check(true, label); return; } throw new Exception("FAIL: " + label); }
var date = new DateOnly(2026, 11, 9);
var zone = TimeZoneInfo.CreateCustomTimeZone("fixture+7", TimeSpan.FromHours(7), "fixture", "fixture");
var day = ShiftSchedulePlanner.Plan(date, new(8, 0), new(16, 0), false, zone);
Check(day.StartUtc == new DateTimeOffset(2026, 11, 9, 1, 0, 0, TimeSpan.Zero), "branch wall-clock start converts to UTC");
Check(day.EndUtc == new DateTimeOffset(2026, 11, 9, 9, 0, 0, TimeSpan.Zero), "branch wall-clock end converts to UTC");
Check(day.StartUtc.Offset == TimeSpan.Zero && day.EndUtc.Offset == TimeSpan.Zero, "both offsets are UTC");
Check(day.ShiftDate == date, "roster date retained separately from UTC date");
var night = ShiftSchedulePlanner.Plan(date, new(22, 0), new(6, 0), true, zone);
Check(night.EndUtc - night.StartUtc == TimeSpan.FromHours(8), "overnight template produces next-day end");
Check(night.EndUtc.UtcDateTime.Day == 9 && night.EndUtc.UtcDateTime.Hour == 23, "overnight UTC date may differ from local end date");
var leap = ShiftSchedulePlanner.Plan(new(2028, 2, 29), new(22, 0), new(6, 0), true, TimeZoneInfo.Utc);
Check(leap.EndUtc.UtcDateTime == new DateTime(2028, 3, 1, 6, 0, 0, DateTimeKind.Utc), "leap-day rollover");
var year = ShiftSchedulePlanner.Plan(new(2026, 12, 31), new(22, 0), new(6, 0), true, TimeZoneInfo.Utc);
Check(year.EndUtc.Year == 2027, "year rollover");
Reject(() => ShiftSchedulePlanner.Plan(date, new(8, 0), new(8, 0), false, zone), "zero interval rejected");
Reject(() => ShiftSchedulePlanner.Plan(date, new(22, 0), new(6, 0), false, zone), "end before start without overnight rejected");
Reject(() => ShiftSchedulePlanner.Plan(DateOnly.MaxValue, new(22, 0), new(6, 0), true, zone), "date overflow rejected");
Reject(() => ShiftSchedulePlanner.Plan(date, new(8, 0), new(16, 0), false, null!), "missing timezone rejected");
var dst = TimeZoneInfo.CreateCustomTimeZone("fixture-dst", TimeSpan.FromHours(-5), "fixture-dst", "standard", "daylight", [TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new(2026, 1, 1), new(2026, 12, 31), TimeSpan.FromHours(1), TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday), TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday))]);
Reject(() => ShiftSchedulePlanner.Plan(new(2026, 3, 8), new(2, 30), new(4, 0), false, dst), "DST nonexistent start rejected");
Reject(() => ShiftSchedulePlanner.Plan(new(2026, 11, 1), new(1, 30), new(4, 0), false, dst), "DST ambiguous start rejected");
Reject(() => ShiftSchedulePlanner.Plan(new(2026, 3, 8), new(0, 30), new(2, 30), false, dst), "DST nonexistent end rejected");
Reject(() => ShiftSchedulePlanner.Plan(new(2026, 11, 1), new(0, 30), new(1, 30), false, dst), "DST ambiguous end rejected");
Reject(() => ShiftSchedulePlanner.Plan(DateOnly.MinValue, new(0, 0), new(8, 0), false, zone), "UTC underflow cannot be silently clamped");
var negativeZone = TimeZoneInfo.CreateCustomTimeZone("fixture-7", TimeSpan.FromHours(-7), "fixture", "fixture");
Reject(() => ShiftSchedulePlanner.Plan(DateOnly.MaxValue, new(8, 0), new(23, 0), false, negativeZone), "UTC overflow cannot be silently clamped");
var adjacent = ShiftSchedulePlanner.Plan(date, new(16, 0), new(20, 0), false, zone);
Check(!WorkIntervalMath.Overlaps(new(day.StartUtc, day.EndUtc), new(adjacent.StartUtc, adjacent.EndUtc)), "adjacent planned shifts do not overlap");
var overlap = ShiftSchedulePlanner.Plan(date, new(15, 0), new(20, 0), false, zone);
Check(WorkIntervalMath.Overlaps(new(day.StartUtc, day.EndUtc), new(overlap.StartUtc, overlap.EndUtc)), "overlapping planned shifts conflict");
var reader = new ShiftAssignmentConflictReader(null!);
ShiftTemplate Template() => new() { ShiftTemplateId = 1, BranchId = 2, IsActive = true, StartTime = new(8, 0), EndTime = new(16, 0) };
foreach (var scenario in new[] { "wrong-branch", "inactive", "invalid-employee" }) {
    var template = Template(); if (scenario == "inactive") template.IsActive = false;
    Reject(() => reader.HasOverlapAsync(scenario == "invalid-employee" ? 0 : 7, scenario == "wrong-branch" ? 3 : 2, template, date, zone, null, default), "reader rejects " + scenario + " before SQL");
}
try { _ = reader.HasOverlapAsync(7, 2, Template(), date, zone, null, new CancellationToken(true)); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { Check(true, "reader cancellation before SQL"); }
Console.WriteLine($"RESULT: {count} PASS; no SQL, roster writes, authorization or notification acceptance executed.");