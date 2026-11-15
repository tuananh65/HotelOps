using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Services.HotelStructure;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class TaskReopenContractTests
{
    [Fact]
    public void Remediation_accepts_multiline_text_and_trims_boundaries()
    {
        var result = RoomTypeCreationService.NormalizeTaskReopen(new(7, "  Result is unsatisfactory  ", "  Repair first\nThen inspect\tresult  "));
        Assert.Equal("Result is unsatisfactory", result.Reason);
        Assert.Equal("Repair first\nThen inspect\tresult", result.Instructions);
    }
    [Theory]
    [InlineData(0, "Result is unsatisfactory", "Repair")]
    [InlineData(7, "short", "Repair")]
    [InlineData(7, "          ", "Repair")]
    [InlineData(7, "Result is unsatisfactory", " \n\t ")]
    [InlineData(7, "Result\u0001is unsatisfactory", "Repair")]
    [InlineData(7, "Result is unsatisfactory", "Repair\u0085")]
    public void Invalid_reopening_is_rejected(long assignee, string reason, string instructions) =>
        Assert.Throws<ValidationException>(() => RoomTypeCreationService.NormalizeTaskReopen(new(assignee, reason, instructions)));
    [Fact]
    public void Both_fields_obey_actual_nvarchar4000_boundary()
    {
        RoomTypeCreationService.NormalizeTaskReopen(new(7, new('r', 4000), new('i', 4000)));
        Assert.Throws<ValidationException>(() => RoomTypeCreationService.NormalizeTaskReopen(new(7, new('r', 4001), "Repair")));
        Assert.Throws<ValidationException>(() => RoomTypeCreationService.NormalizeTaskReopen(new(7, "Result is unsatisfactory", new('i', 4001))));
    }
}
