using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Services;

internal static class WorkdaySchedulingChecks
{
    private static int passed;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    private static void Reject(DateOnly[]? dates, DateOnly today, string code)
    {
        try { WorkdayService.SchedulingDates(dates, today); throw new Exception($"Expected {code}"); }
        catch (ApiException e) { Check(e.Code == code, code); }
    }
    public static void Main()
    {
        var today = new DateOnly(2026, 11, 23);
        Check(WorkdayService.SchedulingDates([today.AddDays(2), today], today).SequenceEqual(new[] { today, today.AddDays(2) }), "sort valid selected dates");
        Reject(null, today, "SHIFT_DATES_REQUIRED");
        Reject([], today, "SHIFT_DATES_REQUIRED");
        Reject(Enumerable.Repeat(today, 32).ToArray(), today, "SHIFT_DATES_REQUIRED");
        Reject([today, today], today, "SHIFT_DUPLICATE_DATES");
        Reject([today.AddDays(-1)], today, "SHIFT_DATE_OUT_OF_RANGE");
        Reject([today.AddDays(367)], today, "SHIFT_DATE_OUT_OF_RANGE");
        Reject([today, new(2026, 12, 1)], today, "SHIFT_ONE_MONTH_REQUIRED");
        Check(WorkdayService.SchedulingDates([today.AddDays(366)], today).Length == 1, "inclusive 366 day horizon");
        Check(WorkdayService.LocalDate(DateTimeOffset.Parse("2026-11-22T17:00:00Z"), "Asia/Ho_Chi_Minh") == today, "Vietnam midnight boundary");
        Check(WorkdayCalendar.Markers(2026).Any(m => m.Date == new DateOnly(2026, 11, 24)), "2026 Culture Day marker");
        Check(!WorkdayCalendar.Markers(2025).Any(m => m.Date.Month == 11 && m.Date.Day == 24), "do not backdate Culture Day");
        Check(WorkdayCalendar.Markers(2027).Any(m => m.Date == new DateOnly(2027, 2, 6) && m.Kind == "HOLIDAY"), "2027 Tet anchor");
        Check(WorkdayCalendar.Markers(2026).Any(m => m.Date == new DateOnly(2026, 2, 17) && m.Kind == "HOLIDAY"), "2026 Tet anchor");
        Check(WorkdayCalendar.Markers(2026).Any(m => m.Date == new DateOnly(2026, 4, 26) && m.Kind == "HOLIDAY"), "2026 Hung Kings anchor");
        Check(WorkdayCalendar.Markers(2027).Where(m => m.Date.Month == 9 && m.Date.Day != 2).All(m => m.Kind == "REVIEW"), "adjacent National Day requires employer review");
        Check(WorkdayCalendar.Notice(2028).Contains("Chưa xác minh"), "unverified lunar year disclosed");
        Console.WriteLine($"PASS {passed} pure checks; current Workday service/controller/DTO compiled; no database or API mutation.");
    }
}
