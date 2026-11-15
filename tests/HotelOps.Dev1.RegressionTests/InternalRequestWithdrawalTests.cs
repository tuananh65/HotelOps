using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Services.InternalRequests;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class InternalRequestWithdrawalTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\u0000reason")]
    [InlineData("bad\nreason")]
    public void WithdrawalRequiresNonblankReasonWithoutControls(string? reason) =>
        Assert.Throws<ValidationException>(() => InternalRequestWithdrawalService.Validate(new(reason!)));

    [Fact]
    public void WithdrawalReasonIsTrimmedAndBounded()
    {
        Assert.Equal("No longer needed", InternalRequestWithdrawalService.Validate(new("  No longer needed  ")));
        Assert.Equal(4000, InternalRequestWithdrawalService.Validate(new(new string('x', 4000))).Length);
        Assert.Throws<ValidationException>(() => InternalRequestWithdrawalService.Validate(new(new string('x', 4001))));
    }
}
