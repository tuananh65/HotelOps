using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class ShiftExpiryTests
{
    [Theory]
    [InlineData(0, "2026-11-22T17:00:00Z")]
    [InlineData(480, "2026-11-23T01:00:00Z")]
    [InlineData(1440, "2026-11-23T17:00:00Z")]
    public void VietnameseShiftBoundariesIncludeMidnight(int minute, string expected)
    {
        var actual = ShiftExpiryService.Boundary(new DateOnly(2026, 11, 23), minute, "Asia/Ho_Chi_Minh");
        Assert.Equal(DateTime.Parse(expected).ToUniversalTime(), actual);
        Assert.Equal(DateTimeKind.Utc, actual.Kind);
    }
}
