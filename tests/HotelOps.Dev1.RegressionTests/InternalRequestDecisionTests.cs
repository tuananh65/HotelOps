using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Services.InternalRequests;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class InternalRequestDecisionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectNeedsReason(string? reason) => Assert.Throws<ValidationException>(() =>
        InternalRequestDecisionService.Validate(new(false,reason,null)));

    [Fact]
    public void RejectCannotSmuggleDeadlineChange() => Assert.Throws<ValidationException>(() =>
        InternalRequestDecisionService.Validate(new(false,"Unable to serve",DateTimeOffset.UtcNow)));

    [Fact]
    public void ReasonIsTrimmedButNeverInvented()
    {
        Assert.Equal("Unable to serve",InternalRequestDecisionService.Validate(new(false,"  Unable to serve  ",null)));
        Assert.Null(InternalRequestDecisionService.Validate(new(true,"  ",null)));
    }

    [Theory]
    [InlineData("bad\u0000reason")]
    [InlineData("bad\nreason")]
    public void ControlsAreRejected(string reason) => Assert.Throws<ValidationException>(() =>
        InternalRequestDecisionService.Validate(new(false,reason,null)));
}
