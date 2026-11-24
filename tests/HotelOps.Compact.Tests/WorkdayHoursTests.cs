using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class WorkdayHoursTests
{
    private static readonly DateOnly Monday = new(2026, 11, 23);
    [Fact]
    public void ExactlyEightPerDayAndFortyPerWeekNeedsNoOverride() => Assert.Empty(
        WorkdayService.HourWarnings([], Enumerable.Range(0, 5).Select(Monday.AddDays).ToArray(), 480));
    [Fact]
    public void SixthEightHourDayWarnsWeeklyWithoutDailyWarning()
    {
        var warning = Assert.Single(WorkdayService.HourWarnings([], Enumerable.Range(0, 6).Select(Monday.AddDays).ToArray(), 480));
        Assert.Equal("WEEK", warning.Period); Assert.Equal(2880, warning.TotalMinutes); Assert.Equal(2400, warning.LimitMinutes);
    }
    [Fact]
    public void ExistingDifferentShiftsCountTowardDailyAndWeeklyLimits()
    {
        var existing = Enumerable.Range(0, 5).Select(d => new ScheduledMinutes(Monday.AddDays(d), 480)).ToArray();
        var warnings = WorkdayService.HourWarnings(existing, [Monday.AddDays(4)], 60);
        Assert.Equal(2, warnings.Length); Assert.Equal(540, warnings[0].TotalMinutes); Assert.Equal(2460, warnings[1].TotalMinutes);
    }
    [Fact]
    public void AdjacentWeeksAreNotCombined()
    {
        var existing = Enumerable.Range(0, 5).Select(d => new ScheduledMinutes(Monday.AddDays(d), 480)).ToArray();
        Assert.Empty(WorkdayService.HourWarnings(existing, [Monday.AddDays(7)], 480));
    }
    [Theory]
    [InlineData(2026, 11, 30, 2026, 12, 6)]
    [InlineData(2026, 12, 28, 2027, 1, 3)]
    public void WeekAcrossMonthOrYearIsAccepted(int y,int m,int d,int endY,int endM,int endD)
    {
        var first = new DateOnly(y,m,d); var last = new DateOnly(endY,endM,endD);
        Assert.Equal(new[]{first,last}, WorkdayService.SchedulingDates([first,last], first));
        Assert.Equal(first, WorkdayService.WeekStart(last));
    }
    [Fact]
    public void UnrelatedMonthsStillRejected()
    {
        var error = Assert.Throws<ApiException>(() => WorkdayService.SchedulingDates([new(2026,11,23),new(2026,12,7)], Monday));
        Assert.Equal("SHIFT_ONE_MONTH_OR_WEEK_REQUIRED", error.Code);
    }
}
