using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class OperationsWindowTests
{
    [Theory]
    [InlineData("day", "2026-11-23T17:00:00Z", "2026-11-24T17:00:00Z")]
    [InlineData("week", "2026-11-22T17:00:00Z", "2026-11-29T17:00:00Z")]
    [InlineData("month", "2026-10-31T17:00:00Z", "2026-11-30T17:00:00Z")]
    public void PeriodUsesBranchCalendarAndExclusiveUpperBound(string period,string first,string end)
    {
        var window=InsightService.TaskWindow.For(new DateTime(2026,11,23,18,0,0,DateTimeKind.Utc),"Asia/Ho_Chi_Minh",period);
        Assert.Equal(DateTime.Parse(first).ToUniversalTime(),window.FromUtc);
        Assert.Equal(DateTime.Parse(end).ToUniversalTime(),window.ToUtc);
    }

    [Fact]
    public void WeekCanCrossYearAndDayAccountsForDst()
    {
        var week=InsightService.TaskWindow.For(new DateTime(2027,1,1,0,0,0,DateTimeKind.Utc),"Asia/Ho_Chi_Minh","week");
        Assert.Equal(new DateOnly(2026,12,28),week.FromDate);
        Assert.Equal(new DateOnly(2027,1,3),week.ToDate);
        var day=InsightService.TaskWindow.For(new DateTime(2026,3,8,12,0,0,DateTimeKind.Utc),"America/New_York","day");
        Assert.Equal(23,(day.ToUtc-day.FromUtc).TotalHours);
    }

    [Fact]
    public void InvalidPeriodIsRejected() => Assert.Throws<ApiException>(()=>
        InsightService.TaskWindow.For(DateTime.UtcNow,"Asia/Ho_Chi_Minh","year"));
}
