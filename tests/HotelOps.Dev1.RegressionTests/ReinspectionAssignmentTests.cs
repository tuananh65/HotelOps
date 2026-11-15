using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.HotelStructure;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class ReinspectionAssignmentTests
{
    [Theory]
    [InlineData("inspection")]
    [InlineData("rework")]
    [InlineData("branch")]
    [InlineData("operation")]
    [InlineData("contract")]
    [InlineData("state")]
    [InlineData("notification")]
    [InlineData("weak-tag")]
    [InlineData("wildcard-tag")]
    [InlineData("missing-tag")]
    public void Replay_does_not_acknowledge_another_source_or_malformed_writer_result(string changed)
    {
        var operation = Guid.NewGuid();
        var receipt = new ReinspectionAssignmentResult(10, 20, 30, "ASSIGNED", "\"0000000000000001\"", operation);
        Assert.True(RoomTypeCreationService.ValidReinspectionAssignmentReceipt(receipt, 30, 10, 20, operation));
        var invalid = changed switch
        {
            "inspection" => receipt with { Id = 11 },
            "rework" => receipt with { ReworkId = 21 },
            "branch" => receipt with { BranchId = 31 },
            "operation" => receipt with { OperationId = Guid.NewGuid() },
            "contract" => receipt with { Contract = "FE04_REWORK_SUBMISSION_V1" },
            "state" => receipt with { State = "PASSED" },
            "notification" => receipt with { Notification = "DELIVERED" },
            "weak-tag" => receipt with { SourceETag = "W/\"0000000000000001\"" },
            "wildcard-tag" => receipt with { SourceETag = "*" },
            _ => receipt with { SourceETag = "" }
        };
        Assert.False(RoomTypeCreationService.ValidReinspectionAssignmentReceipt(invalid, 30, 10, 20, operation));
        Assert.False(RoomTypeCreationService.ValidReinspectionAssignmentReceipt(null, 30, 10, 20, operation));
    }
}
