using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Security;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class SupervisorHandoverPolicyTests
{
    [Theory]
    [InlineData("CLEANING","ASSIGNED",false,false,false,true)]
    [InlineData("REWORK","PENDING_ACCEPTANCE",false,false,false,true)]
    [InlineData("CLEANING","ASSIGNED",true,false,false,false)]
    [InlineData("REWORK","PENDING_ACCEPTANCE",true,false,false,false)]
    [InlineData("CLEANING","ASSIGNED",false,true,false,false)]
    [InlineData("REWORK","PENDING_ACCEPTANCE",false,true,false,false)]
    [InlineData("CLEANING","ASSIGNED",false,false,true,false)]
    [InlineData("REWORK","PENDING_ACCEPTANCE",false,false,true,false)]
    [InlineData("CLEANING","IN_PROGRESS",false,false,false,false)]
    [InlineData("REWORK","ON_HOLD",false,false,false,false)]
    [InlineData("REWORK","ACCEPTED",false,false,false,false)]
    [InlineData("CLEANING","PENDING_INSPECTION",false,false,false,false)]
    [InlineData("REWORK","PENDING_REINSPECTION",false,false,false,false)]
    [InlineData("CLEANING","COMPLETED",false,false,false,false)]
    [InlineData("MAINTENANCE","ASSIGNED",false,false,false,false)]
    [InlineData("OPERATIONAL","ASSIGNED",false,false,false,false)]
    public void HandoverRequiresAnUnacceptedAssignmentWithoutAnyExecutionHistory(string kind,string status,bool accepted,bool actualStart,bool executionHistory,bool expected)
    {
        var now=new DateTime(2026,11,27,8,0,0,DateTimeKind.Utc);
        var work=new WorkItem{Kind=kind,Status=status,ActualStartAt=actualStart?now:null};
        var assignment=new WorkAssignment{AssignmentStatus="ACTIVE",AcceptedAt=accepted?now:null};
        Assert.Equal(expected,CleaningContinuationAccess.IsBeforeAcceptance(work,assignment,executionHistory));
    }

    [Theory]
    [InlineData("CLEANING","ASSIGNED")]
    [InlineData("REWORK","PENDING_ACCEPTANCE")]
    public void MissingOrReassignedAssignmentCannotRequestHandover(string kind,string status)
    {
        var work=new WorkItem{Kind=kind,Status=status};
        Assert.False(CleaningContinuationAccess.IsBeforeAcceptance(work,null,false));
        Assert.False(CleaningContinuationAccess.IsBeforeAcceptance(work,new WorkAssignment{AssignmentStatus="REASSIGNED"},false));
    }
}
