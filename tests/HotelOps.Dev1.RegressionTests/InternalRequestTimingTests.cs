using HotelOps.Api.Services.InternalRequests;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class InternalRequestTimingTests
{
    private static readonly DateTime Created = new(2026,11,16,0,0,0,DateTimeKind.Utc);

    [Theory]
    [InlineData(79, "ON_TIME")]
    [InlineData(80, "AT_RISK")]
    [InlineData(100, "AT_RISK")]
    [InlineData(101, "OVERDUE")]
    public void ApprovedBoundaries(int minutes, string expected) =>
        Assert.Equal(expected, InternalRequestTiming.Classify("IN_PROGRESS",Created,Created.AddMinutes(100),Created.AddMinutes(minutes)));

    [Theory]
    [InlineData("CLOSED")]
    [InlineData("REJECTED")]
    [InlineData("CANCELLED")]
    public void TerminalRequestsNeverQualify(string status) =>
        Assert.Equal("TERMINAL",InternalRequestTiming.Classify(status,Created,Created.AddMinutes(100),Created.AddDays(1)));

    [Fact]
    public void CurrentDeadlineRecalculatesWithoutResettingCreation()
    {
        var now=Created.AddMinutes(80);
        Assert.Equal("AT_RISK",InternalRequestTiming.Classify("IN_PROGRESS",Created,Created.AddMinutes(100),now));
        Assert.Equal("ON_TIME",InternalRequestTiming.Classify("IN_PROGRESS",Created,Created.AddMinutes(200),now));
        Assert.Equal("OVERDUE",InternalRequestTiming.Classify("IN_PROGRESS",Created,Created.AddMinutes(70),now));
    }

    [Fact]
    public void MissingInvalidAndFutureWindowsCannotEscalate()
    {
        foreach(var due in new DateTime?[]{null,Created,Created.AddTicks(-1)})
            Assert.Equal("INVALID_WINDOW",InternalRequestTiming.Classify("IN_PROGRESS",Created,due,Created.AddDays(1)));
        Assert.Equal("ON_TIME",InternalRequestTiming.Classify("IN_PROGRESS",Created,Created.AddDays(1),Created.AddTicks(-1)));
    }

    [Fact]
    public void ExactTickPrecisionAndFullDateRangeDoNotRoundOrOverflow()
    {
        var due=Created.AddTicks(5);
        Assert.Equal("ON_TIME",InternalRequestTiming.Classify("IN_PROGRESS",Created,due,Created.AddTicks(3)));
        Assert.Equal("AT_RISK",InternalRequestTiming.Classify("IN_PROGRESS",Created,due,Created.AddTicks(4)));
        Assert.Equal("AT_RISK",InternalRequestTiming.Classify("IN_PROGRESS",Created,due,due));
        Assert.Equal("OVERDUE",InternalRequestTiming.Classify("IN_PROGRESS",Created,due,due.AddTicks(1)));
        Assert.Equal("AT_RISK",InternalRequestTiming.Classify("IN_PROGRESS",DateTime.MinValue,DateTime.MaxValue,DateTime.MaxValue));
    }
}
