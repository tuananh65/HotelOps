using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Services.InternalRequests;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class InternalRequestAssignmentTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void HandlerMustExist(long handler) => Assert.Throws<ValidationException>(() =>
        InternalRequestAssignmentService.Validate(new(handler, null)));

    [Theory]
    [InlineData("line\nbreak")]
    [InlineData("null\0byte")]
    public void NoteCannotContainControls(string note) => Assert.Throws<ValidationException>(() =>
        InternalRequestAssignmentService.Validate(new(1, note)));

    [Fact]
    public void NoteIsOptionalAndBounded()
    {
        Assert.Null(InternalRequestAssignmentService.Validate(new(1, "  ")));
        Assert.Equal("Context", InternalRequestAssignmentService.Validate(new(1, " Context ")));
        Assert.Equal(4000, InternalRequestAssignmentService.Validate(new(1, new string('x', 4000)))!.Length);
        Assert.Throws<ValidationException>(() => InternalRequestAssignmentService.Validate(new(1, new string('x', 4001))));
    }
}
