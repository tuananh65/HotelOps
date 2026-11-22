using HotelOps.Api.Compact.Services;
using Xunit;
namespace HotelOps.Compact.Tests;
public sealed class WorkdayTests
{
    [Theory]
    [InlineData("2026-11-22T16:59:59Z","2026-11-22")]
    [InlineData("2026-11-22T17:00:00Z","2026-11-23")]
    [InlineData("2026-12-31T17:00:00Z","2027-01-01")]
    public void CurrentDayUsesBranchTimezoneRatherThanUtc(string instant,string expected)
        => Assert.Equal(DateOnly.Parse(expected),WorkdayService.LocalDate(DateTimeOffset.Parse(instant),"Asia/Ho_Chi_Minh"));
}
