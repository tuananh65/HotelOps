using HotelOps.Api.Entities;
using HotelOps.Api.Services.InternalRequests;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;
public sealed class InternalRequestDirectionTests
{
    [Fact]
    public void DirectionUsesCurrentResponsibilityAndDoesNotDuplicateOverlap()
    {
        var now=DateTime.UtcNow;
        var sent=new InternalRequest{InternalRequestId=1,RequesterId=7,ReceivingDepartmentId=11};
        var managed=new InternalRequest{InternalRequestId=2,RequesterId=8,ReceivingDepartmentId=11};
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
