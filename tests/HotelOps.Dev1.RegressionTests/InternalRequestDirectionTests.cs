using HotelOps.Api.Entities;
using HotelOps.Api.Services.InternalRequests;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;
public sealed class InternalRequestDirectionTests
{
    [Fact]
    public void ManagedDepartmentCannotClassifyAnotherBranchRequestAsReceived()
    {
        var now=DateTime.UtcNow;
        var request=new InternalRequest { InternalRequestId=1, BranchId=20,
            ReceivingDepartmentId=11, ReceivingDepartment=new(){DepartmentId=11,BranchId=10},
            Branch=new(){IsActive=true} };
        var read=new InternalRequestReadAccess.Scope(7,[10,20],[10,20],false);
        var processing=new InternalRequestDirection.Scope(7,[11]);
        var rows=read.Apply(new[]{request}.AsQueryable(),now);
        Assert.Single(rows); // Read scope cannot substitute for processing authority in this branch.
        Assert.Empty(processing.Apply(rows,"RECEIVED",now));
        request.BranchId=10;
        Assert.Single(processing.Apply(rows,"RECEIVED",now));
        request.BranchId=20;
        request.InternalRequestAssignments.Add(new(){AssigneeId=7,Status="ACTIVE",AssignedAt=now});
        Assert.Single(processing.Apply(rows,"RECEIVED",now)); // Independent current-handler path remains.
    }

    [Theory]
    [InlineData("SENT")]
    [InlineData("RECEIVED")]
    public void DirectionCannotExpandReadScope(string direction)
    {
        var now=new DateTime(2026,11,16,0,0,0,DateTimeKind.Utc);
        var rows=new[]
        {
            new InternalRequest{InternalRequestId=1,RequesterId=7,BranchId=10,Branch=new(){IsActive=true},ReceivingDepartmentId=11,ReceivingDepartment=new(){BranchId=10}},
            new InternalRequest{InternalRequestId=2,RequesterId=7,BranchId=20,Branch=new(){IsActive=true},ReceivingDepartmentId=21,ReceivingDepartment=new(){BranchId=20}},
            new InternalRequest{InternalRequestId=3,RequesterId=7,BranchId=10,Branch=new(){IsActive=false},ReceivingDepartmentId=11,ReceivingDepartment=new(){BranchId=10}}
        }.AsQueryable();
        // Even processing authority in both branches cannot replace a missing read grant.
        var processing=new InternalRequestDirection.Scope(7,[11,21]);
        var read=new InternalRequestReadAccess.Scope(7,[10],[10],false);
        Assert.Equal(new long[]{1},processing.Apply(read.Apply(rows,now),direction,now).Select(r=>r.InternalRequestId));
        var revokedRead=new InternalRequestReadAccess.Scope(7,[],[],false);
        Assert.Empty(processing.Apply(revokedRead.Apply(rows,now),direction,now));
    }

    [Fact]
    public void ReceivedReflectsHandlerReplacementWithoutDepartmentAuthority()
    {
        var now=new DateTime(2026,11,16,0,0,0,DateTimeKind.Utc);
        var request=new InternalRequest{InternalRequestId=1,RequesterId=8,ReceivingDepartmentId=11,ReceivingDepartment=new(){BranchId=10}};
        var assignment=new InternalRequestAssignment{AssigneeId=7,Status="ACTIVE",AssignedAt=now.AddMinutes(1)};
        request.InternalRequestAssignments.Add(assignment);
        var rows=new[]{request}.AsQueryable();
        var scope=new InternalRequestDirection.Scope(7,[]);
        Assert.Empty(scope.Apply(rows,"RECEIVED",now));
        assignment.AssignedAt=now;
        Assert.Single(scope.Apply(rows,"RECEIVED",now));
        assignment.AssigneeId=8;
        Assert.Empty(scope.Apply(rows,"RECEIVED",now));
        assignment.AssigneeId=7;
        assignment.Status="ENDED";
        Assert.Empty(scope.Apply(rows,"RECEIVED",now));
    }

    [Fact]
    public void DirectionUsesCurrentResponsibilityAndDoesNotDuplicateOverlap()
    {
        var now=DateTime.UtcNow;
        var sent=new InternalRequest{InternalRequestId=1,RequesterId=7,BranchId=10,ReceivingDepartmentId=11,ReceivingDepartment=new(){BranchId=10}};
        // The same request qualifies as requester, current handler and mapped manager.
        // Each direction must still return one row before counting/pagination.
        sent.InternalRequestAssignments.Add(new(){AssigneeId=7,Status="ACTIVE",AssignedAt=now.AddMinutes(-1)});
        var managed=new InternalRequest{InternalRequestId=2,RequesterId=8,BranchId=10,ReceivingDepartmentId=11,ReceivingDepartment=new(){BranchId=10}};
        var handled=new InternalRequest{InternalRequestId=3,RequesterId=8,ReceivingDepartmentId=12};
        handled.InternalRequestAssignments.Add(new(){AssigneeId=7,Status="ACTIVE",AssignedAt=now.AddMinutes(-1)});
        var scope=new InternalRequestDirection.Scope(7,[11]);var rows=new[]{sent,managed,handled}.AsQueryable();
        Assert.Equal(new long[]{1},scope.Apply(rows,"SENT",now).Select(r=>r.InternalRequestId));
        Assert.Equal(new long[]{1,2,3},scope.Apply(rows,"RECEIVED",now).Select(r=>r.InternalRequestId));
        handled.InternalRequestAssignments.Single().EndedAt=now;
        managed.ReceivingDepartmentId=12;
        Assert.Equal(new long[]{1},scope.Apply(rows,"RECEIVED",now).Select(r=>r.InternalRequestId));
        handled.InternalRequestAssignments.Single().EndedAt=null;
        handled.InternalRequestAssignments.Add(new(){AssigneeId=8,Status="ACTIVE",AssignedAt=now.AddMinutes(-1)});
        Assert.Equal(new long[]{1},scope.Apply(rows,"RECEIVED",now).Select(r=>r.InternalRequestId));
        Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(()=>scope.Apply(rows,"OTHER",now));
    }
}
