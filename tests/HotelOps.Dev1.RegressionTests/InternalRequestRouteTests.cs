using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Services.InternalRequests;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class InternalRequestRouteTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("123456789")]
    [InlineData("long reason\nwith control")]
    public void RoutingRequiresGbr012Reason(string? reason) => Assert.Throws<ValidationException>(() => InternalRequestRouteService.Validate(new(1, reason!)));

    [Fact]
    public void ValidReasonAndDepartmentAreRequired()
    {
        Assert.Equal("valid route reason", InternalRequestRouteService.Validate(new(1, " valid route reason ")));
        Assert.Throws<ValidationException>(() => InternalRequestRouteService.Validate(new(0, "valid route reason")));
        Assert.Throws<ValidationException>(() => InternalRequestRouteService.Validate(new(1, new string('x', 4001))));
    }
}
