using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.InternalRequests;
using Xunit;
namespace HotelOps.Dev1.RegressionTests;
public sealed class InternalRequestEscalationAuthorityTests
{
    private static readonly DateTime Now = new(2026,11,16,0,0,0,DateTimeKind.Utc);
    private static PermissionGrantPath Path() => new("IR_OVERDUE_HANDLE","BRANCH",7,true,true,Now.AddDays(-1),null,true,true);
    [Fact]
    public void OnlyOperationsManagerOnMatchingCurrentBranchPathQualifies()
    {
        Assert.True(InternalRequestEscalationAuthority.Eligible("OPERATIONS_MANAGER",Path(),7,Now));
        Assert.True(InternalRequestEscalationAuthority.Eligible("OPERATIONS_MANAGER",Path() with {DataScope="ALL"},7,Now));
        foreach(var role in new[]{"DEPT_MANAGER","OWNER","SYS_ADMIN","MAINTENANCE_LEAD"})
            Assert.False(InternalRequestEscalationAuthority.Eligible(role,Path(),7,Now));
        Assert.False(InternalRequestEscalationAuthority.Eligible("OPERATIONS_MANAGER",Path(),8,Now));
    }
    [Fact]
    public void ReadProcessingGlobalOwnExpiredAndRevokedPathsDoNotQualify()
    {
        foreach(var path in new[]{Path() with {PermissionCode="IR_VIEW"},Path() with {PermissionCode="IR_PROCESS"},
            Path() with {DataScope="OWN"},Path() with {DataScope="ALL",BranchId=null},
            Path() with {AssignmentActive=false},Path() with {RoleActive=false},Path() with {BranchActive=false},
            Path() with {BranchAccessActive=false},Path() with {ExpiresAt=Now},Path() with {AssignedAt=Now.AddTicks(1)}})
            Assert.False(InternalRequestEscalationAuthority.Eligible("OPERATIONS_MANAGER",path,7,Now));
    }
}
