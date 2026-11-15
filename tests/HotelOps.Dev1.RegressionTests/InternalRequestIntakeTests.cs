using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.InternalRequests;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class InternalRequestIntakeTests
{
    private static readonly DateTime Now=new(2026,11,16,0,0,0,DateTimeKind.Utc);
    private static PermissionGrantPath Grant()=>new("IR_CREATE","BRANCH",7,true,true,Now.AddDays(-1),null,true,true);
    private static InternalRequestIntakeInput Input()=>new(Guid.NewGuid(),7,1,2," Service "," Description ",3,new DateTimeOffset(Now.AddHours(1)));
    [Fact]
    public void ActorPermissionAndCurrentBranchAreAllRequired()
    {
        foreach(var role in new[]{"SYS_ADMIN","OPERATIONS_MANAGER","HK_SUPERVISOR","HK_STAFF","MAINTENANCE_LEAD","MAINTENANCE_STAFF","STOREKEEPER","DEPT_MANAGER"})
            Assert.True(InternalRequestIntakeAccess.Eligible(role,Grant(),7,Now));
        foreach(var role in new[]{"OWNER","RECEPTION","UNKNOWN"})Assert.False(InternalRequestIntakeAccess.Eligible(role,Grant(),7,Now));
        foreach(var grant in new[]{Grant() with{PermissionCode="IR_VIEW"},Grant() with{PermissionCode="IR_PROCESS"},Grant() with{BranchAccessActive=false},
            Grant() with{AssignmentActive=false},Grant() with{RoleActive=false},Grant() with{ExpiresAt=Now},Grant() with{AssignedAt=Now.AddMinutes(1)},Grant() with{BranchId=8}})
            Assert.False(InternalRequestIntakeAccess.Eligible("DEPT_MANAGER",grant,7,Now));
    }
    [Fact]
    public void IntakeValidatesRequiredFieldsAndPersistenceLimits()
    {
        var valid=InternalRequestIntakeService.Validate(Input(),Now);
        Assert.Equal("Service",valid.Subject);Assert.Equal("Description",valid.Description);
        foreach(var input in new[]{Input() with{SubmissionId=Guid.Empty},Input() with{Subject=" "},Input() with{Subject=new string('a',201)},
            Input() with{Description=new string('a',4001)},Input() with{DesiredDeadline=new DateTimeOffset(Now)},Input() with{PriorityId=0},
            Input() with{RoomId=1,AreaId=2},Input() with{DepartmentId=0},Input() with{TypeId=0}})
            Assert.Throws<ValidationException>(()=>InternalRequestIntakeService.Validate(input,Now));
    }
}
