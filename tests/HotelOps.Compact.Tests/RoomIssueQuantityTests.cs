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
}
