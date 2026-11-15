using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.InternalRequests;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class InternalRequestProcessAuthorityTests
{
    private static readonly DateTime Now = new(2026,11,16,0,0,0,DateTimeKind.Utc);
    private static PermissionGrantPath Path() => new("IR_PROCESS","BRANCH",7,true,true,Now.AddDays(-1),null,true,true);

    [Fact]
    public void ProcessingRequiresDepartmentManagerOnCurrentBranchGrantPath()
    {
        Assert.True(InternalRequestProcessAuthority.Eligible("DEPT_MANAGER",Path(),7,Now));
        Assert.True(InternalRequestProcessAuthority.Eligible("DEPT_MANAGER",Path() with {DataScope="ALL"},7,Now));
        foreach(var role in new[]{"SYS_ADMIN","OPERATIONS_MANAGER","HK_SUPERVISOR","OWNER","HK_STAFF"})
            Assert.False(InternalRequestProcessAuthority.Eligible(role,Path(),7,Now));
        Assert.False(InternalRequestProcessAuthority.Eligible("DEPT_MANAGER",Path(),8,Now));
    }

    [Fact]
    public void ReadOwnGlobalExpiredRevokedAndInactiveDoNotAuthorizeProcessing()
    {
        foreach(var path in new[]{Path() with {PermissionCode="IR_VIEW"}, Path() with {DataScope="OWN"},
            Path() with {DataScope="ALL",BranchId=null},Path() with {AssignmentActive=false},
            Path() with {RoleActive=false},Path() with {BranchActive=false},Path() with {BranchAccessActive=false},
            Path() with {ExpiresAt=Now},Path() with {AssignedAt=Now.AddTicks(1)}})
            Assert.False(InternalRequestProcessAuthority.Eligible("DEPT_MANAGER",path,7,Now));
    }
}
