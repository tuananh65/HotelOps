using HotelOps.Api.Services.InternalRequests;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class InternalRequestReopenSourceTests
{
    [Fact]
    public void ReopenWindowIncludesExactDeadlineButNotFollowingTick()
    {
        var closed = new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
        Assert.True(InternalRequestReopenSource.WithinWindow(closed, closed, 7));
        Assert.True(InternalRequestReopenSource.WithinWindow(closed, closed.AddDays(7), 7));
        Assert.False(InternalRequestReopenSource.WithinWindow(closed, closed.AddDays(7).AddTicks(1), 7));
        Assert.False(InternalRequestReopenSource.WithinWindow(closed, closed.AddTicks(-1), 7));
    }

    [Fact]
    public void InvalidWindowCannotEnableReopenAndDatesCannotOverflow()
    {
        Assert.False(InternalRequestReopenSource.WithinWindow(DateTime.MinValue, DateTime.MinValue, 0));
        Assert.False(InternalRequestReopenSource.WithinWindow(DateTime.MinValue, DateTime.MinValue, -1));
        Assert.False(InternalRequestReopenSource.WithinWindow(DateTime.MinValue, DateTime.MaxValue, 7));
        Assert.True(InternalRequestReopenSource.WithinWindow(DateTime.MaxValue.AddDays(-7), DateTime.MaxValue, 7));
    }
}
