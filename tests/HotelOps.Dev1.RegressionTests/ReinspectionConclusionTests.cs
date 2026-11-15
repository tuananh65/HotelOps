using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.HotelStructure;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class ReinspectionConclusionTests
{
    [Theory]
    [InlineData("PASSED", "COMPLETED", "READY", "MSG08")]
    [InlineData("PASSED", "COMPLETED", "BLOCKED", "MSG17")]
    [InlineData("FAILED", "FAILED", "REWORK_REQUIRED", "MSG08")]
    [InlineData("FAILED", "FAILED", "BLOCKED", "MSG17")]
    public void Acknowledgement_requires_coherent_outcome_and_exact_source(string state, string reworkState, string readiness, string message)
    {
        var op = Guid.NewGuid();
        var valid = new ReinspectionConclusionResult(10, 20, 30, 40, 50, state, reworkState,
            "\"0000000000000001\"", "\"0000000000000002\"", op, readiness, message);
        Assert.True(RoomTypeCreationService.ValidReinspectionConclusionReceipt(valid, 30, 10, 20, op));
        ReinspectionConclusionResult[] invalid = [
            valid with { Id = 11 }, valid with { ReworkId = 21 }, valid with { BranchId = 31 },
            valid with { CleaningTaskId = 0 }, valid with { RoomId = 0 }, valid with { OperationId = Guid.NewGuid() },
            valid with { Contract = "FE05_REINSPECTION_ASSIGNMENT_V1" }, valid with { State = "ASSIGNED" },
            valid with { ReworkState = "PENDING_REINSPECTION" }, valid with { EffectiveReadiness = "UNKNOWN" },
            valid with { Notification = "DELIVERED" }, valid with { SourceETag = "*" }, valid with { ReworkETag = "" },
            valid with { MessageCode = message == "MSG17" ? "MSG08" : "MSG17" },
        ];
        foreach (var receipt in invalid)
            Assert.False(RoomTypeCreationService.ValidReinspectionConclusionReceipt(receipt, 30, 10, 20, op));
        Assert.False(RoomTypeCreationService.ValidReinspectionConclusionReceipt(null, 30, 10, 20, op));
    }
}
