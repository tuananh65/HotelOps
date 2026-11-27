using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class RoomIssueQuantityTests
{
    [Fact] public void ReportCannotTurnOneActualMissingItemIntoOneHundred()
    {
        var remaining=RoomIssueQuantityRules.Outstanding(2,1,0);
        var error=Assert.Throws<ApiException>(()=>RoomIssueQuantityRules.RequireWithin(100,remaining,"ROOM_ISSUE_EXCEEDS_ASSESSED_MISSING"));
        Assert.Equal("ROOM_ISSUE_EXCEEDS_ASSESSED_MISSING",error.Code);
        RoomIssueQuantityRules.RequireWithin(1,remaining,"ROOM_ISSUE_EXCEEDS_ASSESSED_MISSING");
    }
    [Theory]
    [InlineData("APPROVED",2,0,0,2)]
    [InlineData("PARTIALLY_ISSUED",2,1,0,2)]
    [InlineData("REMAINDER_CLOSED",2,1,0,1)]
    [InlineData("CANCELLED",2,1,0,1)]
    [InlineData("FULLY_ISSUED",2,2,1,1)]
    [InlineData("FULLY_ISSUED",2,2,2,0)]
    [InlineData("REJECTED",2,0,0,0)]
    public void RetryCountsDeliveriesReturnsAndUnissuedCommitments(string status,decimal requested,decimal issued,decimal returned,decimal expected)
        => Assert.Equal(expected,RoomIssueQuantityRules.Commitment(status,requested,issued,returned));

    [Fact] public void ReusableRequestCannotExceedReportedQuantityOrNetUnresolvedRemainder()
    {
        var committed=RoomIssueQuantityRules.Commitment("FULLY_ISSUED",2,2,1);
        Assert.Throws<ApiException>(()=>RoomIssueQuantityRules.RequireWithin(2,2-committed,"REMEDIATION_EXCEEDS_REPORTED_MISSING"));
        RoomIssueQuantityRules.RequireWithin(1,2-committed,"REMEDIATION_EXCEEDS_REPORTED_MISSING");
    }
    [Fact] public void FullyReplenishedAssessmentDoesNotAllowAnotherReport()
        => Assert.Throws<ApiException>(()=>RoomIssueQuantityRules.RequireWithin(1,RoomIssueQuantityRules.Outstanding(1,1,0),"ROOM_ISSUE_EXCEEDS_ASSESSED_MISSING"));
    [Theory]
    [InlineData(4, 4, 0, 4)]
    [InlineData(4, 4, 4, 0)]
    [InlineData(4, 2, 1, 1)]
    [InlineData(4, 0, 0, 0)]
    public void MissingBoundUsesReservedNormLessRecordedConsumption(decimal norm, decimal reserved, decimal consumed, decimal expected)
        => Assert.Equal(expected, RoomIssueQuantityRules.ReservationOutstanding(norm, reserved, consumed));

    [Theory]
    [InlineData(4, 5, 0)]
    [InlineData(4, 2, 3)]
    [InlineData(4, 2, -1)]
    [InlineData(4, -1, 0)]
    [InlineData(4, 2, 0.00001)]
    public void InconsistentEvidenceCannotIncreaseMissingQuantity(decimal norm, decimal reserved, decimal consumed)
        => Assert.Equal("SUPPLY_BALANCE_INCONSISTENT", Assert.Throws<ApiException>(
            () => RoomIssueQuantityRules.ReservationOutstanding(norm, reserved, consumed)).Code);

    [Fact]
    public void PriorReportsAndCommitmentsStillReduceTheCanonicalBound()
    {
        var bound = RoomIssueQuantityRules.ReservationOutstanding(4, 3, 1);
        RoomIssueQuantityRules.RequireWithin(1, bound - 1, "ROOM_ISSUE_EXCEEDS_ASSESSED_MISSING");
        Assert.Throws<ApiException>(() => RoomIssueQuantityRules.RequireWithin(2, bound - 1, "ROOM_ISSUE_EXCEEDS_ASSESSED_MISSING"));
        var committed = RoomIssueQuantityRules.Commitment("PARTIALLY_ISSUED", 2, 1, 0);
        Assert.Throws<ApiException>(() => RoomIssueQuantityRules.RequireWithin(1, bound - committed, "REMEDIATION_EXCEEDS_REPORTED_MISSING"));
    }
    [Theory]
    [InlineData(4, null, 0, 4)]
    [InlineData(4, null, 1, 3)]
    [InlineData(4, null, 4, 0)]
    [InlineData(4, 2, 0, 2)]
    [InlineData(4, 0, 0, 0)]
    public void PhysicalReportBeforeConfirmationUsesNormWithoutInventingCustody(
        decimal norm, int? held, decimal replenished, decimal expected)
        => Assert.Equal(expected, RoomIssueQuantityRules.PhysicalReportOutstanding(norm, held, replenished));

    [Fact]
    public void UnreservedReportAndRemediationCannotExceedNormLessExistingEvidence()
    {
        var bound = RoomIssueQuantityRules.PhysicalReportOutstanding(4, null, 1);
        RoomIssueQuantityRules.RequireWithin(2, bound - 1, "ROOM_ISSUE_EXCEEDS_ASSESSED_MISSING");
        Assert.Throws<ApiException>(() => RoomIssueQuantityRules.RequireWithin(3, bound - 1, "ROOM_ISSUE_EXCEEDS_ASSESSED_MISSING"));
        var committed = RoomIssueQuantityRules.Commitment("PARTIALLY_ISSUED", 2, 1, 0);
        RoomIssueQuantityRules.RequireWithin(1, bound - committed, "REMEDIATION_EXCEEDS_ASSESSED_MISSING");
        Assert.Throws<ApiException>(() => RoomIssueQuantityRules.RequireWithin(2, bound - committed, "REMEDIATION_EXCEEDS_ASSESSED_MISSING"));
    }

    [Fact]
    public void UnreservedReportRejectsInvalidConsumptionEvidence()
        => Assert.Equal("SUPPLY_BALANCE_INCONSISTENT", Assert.Throws<ApiException>(() =>
            RoomIssueQuantityRules.PhysicalReportOutstanding(4, null, 5)).Code);
}
