using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Users;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;
public sealed class InternalRequestRoleImpactTests
{
    [Fact]
    public void MappedManagerNeedsItsOwnBranchProcessPathNotAnotherHandlersOwnGrant()
    {
        var manager = Grant(1, "IR_PROCESS") with { RoleCode = "DEPT_MANAGER" };
        manager = manager with { Path = manager.Path with { DataScope = "BRANCH" } };
        var read = Grant(1, "IR_VIEW") with { RoleCode = "DEPT_MANAGER" };
        var staff = Grant(2, "IR_PROCESS") with { RoleCode = "HK_STAFF" };
        Assert.Equal(RevokeCapabilityImpact.Preserved, RevokeRoleGrantImpact.InternalRequestHandler("IN_PROGRESS",
            [manager, read, staff], [manager, read], 1, Now, false, true));
        Assert.Equal(RevokeCapabilityImpact.Lost, RevokeRoleGrantImpact.InternalRequestHandler("IN_PROGRESS",
            [manager, read, staff], [staff, read], 1, Now, false, true));
        Assert.Equal(RevokeCapabilityImpact.Unknown, RevokeRoleGrantImpact.InternalRequestHandler("IN_PROGRESS",
            [manager, read], [manager, read], 1, Now, false, false));
        var ownManager = manager with { Path = manager.Path with { DataScope = "OWN" } };
        Assert.Equal(RevokeCapabilityImpact.Unknown, RevokeRoleGrantImpact.InternalRequestHandler("IN_PROGRESS",
            [ownManager, read], [ownManager, read], 1, Now, false, true));
    }
    private static readonly DateTime Now=new(2026,11,16,0,0,0,DateTimeKind.Utc);
    private static RevokeGrant Grant(long assignment,string permission,long branch=1)=>new(assignment,
        new PermissionGrantPath(permission,"OWN",branch,true,true,Now.AddDays(-1),null,true,true));

    [Theory]
    [InlineData("IR_PROCESS")]
    [InlineData("IR_VIEW")]
    public void LosingEitherRequiredGrantBlocksCurrentHandler(string removed)
    {
        var before=new[]{Grant(1,"IR_PROCESS"),Grant(2,"IR_VIEW")};
        var after=before.Where(g=>g.Path.PermissionCode!=removed).ToArray();
        Assert.Equal(RevokeCapabilityImpact.Lost,RevokeRoleGrantImpact.InternalRequestHandler("IN_PROGRESS",before,after,1,Now));
    }
    [Fact]
    public void SurvivingGrantDoesNotCertifyActorOrDepartmentAuthority()
    {
        var grants=new[]{Grant(1,"IR_PROCESS"),Grant(2,"IR_VIEW")};
        Assert.Equal(RevokeCapabilityImpact.Unknown,RevokeRoleGrantImpact.InternalRequestHandler("IN_PROGRESS",grants,grants,1,Now));
        Assert.Equal(RevokeCapabilityImpact.Unknown,RevokeRoleGrantImpact.InternalRequestHandler("CLOSED",grants,[],1,Now));
        Assert.Equal(RevokeCapabilityImpact.Unknown,RevokeRoleGrantImpact.InternalRequestHandler("IN_PROGRESS",grants,[],2,Now));
    }

    [Theory]
    [InlineData("OPERATIONS_MANAGER")]
    [InlineData("OWNER")]
    [InlineData("SYS_ADMIN")]
    public void IneligibleActorCannotReplaceHandlerEvenWithBothPermissions(string role)
    {
        var handler = Grant(1, "IR_PROCESS") with { RoleCode = "HK_STAFF" };
        var replacement = Grant(2, "IR_PROCESS") with { RoleCode = role };
        var read = Grant(2, "IR_VIEW") with { RoleCode = role };
        Assert.Equal(RevokeCapabilityImpact.Lost, RevokeRoleGrantImpact.InternalRequestHandler("IN_PROGRESS",
            [handler, replacement, read], [replacement, read], 1, Now));
    }

    [Theory]
    [InlineData("HK_STAFF")]
    [InlineData("HK_SUPERVISOR")]
    [InlineData("MAINTENANCE_STAFF")]
    [InlineData("MAINTENANCE_LEAD")]
    [InlineData("STOREKEEPER")]
    [InlineData("DEPT_MANAGER")]
    public void EligibleSurvivorStillRequiresDepartmentAndResponsibilityProof(string role)
    {
        var handler = Grant(1, "IR_PROCESS") with { RoleCode = "HK_STAFF" };
        var replacement = Grant(2, "IR_PROCESS") with { RoleCode = role };
        var read = Grant(2, "IR_VIEW") with { RoleCode = role };
        Assert.Equal(RevokeCapabilityImpact.Unknown, RevokeRoleGrantImpact.InternalRequestHandler("IN_PROGRESS",
            [handler, replacement, read], [replacement, read], 1, Now));
    }

    [Fact]
    public void ForeignOrExpiredActorGrantCannotPreserveHandlerCapability()
    {
        var handler = Grant(1, "IR_PROCESS") with { RoleCode = "HK_STAFF" };
        var read = Grant(2, "IR_VIEW") with { RoleCode = "HK_STAFF" };
        var foreign = Grant(2, "IR_PROCESS", 2) with { RoleCode = "HK_STAFF" };
        var expired = handler with { AssignmentId = 2, Path = handler.Path with { ExpiresAt = Now } };
        foreach (var alternative in new[] { foreign, expired })
            Assert.Equal(RevokeCapabilityImpact.Lost, RevokeRoleGrantImpact.InternalRequestHandler("IN_PROGRESS",
                [handler, alternative, read], [alternative, read], 1, Now));
    }

    [Theory]
    [InlineData("OWNER", RevokeCapabilityImpact.Lost)]
    [InlineData("OPERATIONS_MANAGER", RevokeCapabilityImpact.Lost)]
    [InlineData("DEPT_MANAGER", RevokeCapabilityImpact.Lost)]
    [InlineData("SYS_ADMIN", RevokeCapabilityImpact.Unknown)]
    public void GlobalReadReplacementUsesUc123ActorRestriction(string role, RevokeCapabilityImpact expected)
    {
        var process = Grant(1, "IR_PROCESS") with { RoleCode = "HK_STAFF" };
        var read = Grant(2, "IR_VIEW") with { RoleCode = "HK_STAFF" };
        var global = new RevokeGrant(3, new PermissionGrantPath("IR_VIEW", "ALL", null, true, true,
            Now.AddDays(-1), null, false, false), role);
        Assert.Equal(expected, RevokeRoleGrantImpact.InternalRequestHandler("IN_PROGRESS",
            [process, read, global], [process, global], 1, Now));
    }

    [Fact]
    public void UnknownReadActorDoesNotCertifyGlobalReplacementOrPositiveLoss()
    {
        var process = Grant(1, "IR_PROCESS") with { RoleCode = "HK_STAFF" };
        var read = Grant(2, "IR_VIEW") with { RoleCode = "HK_STAFF" };
        var unknown = new RevokeGrant(3, new PermissionGrantPath("IR_VIEW", "ALL", null, true, true,
            Now.AddDays(-1), null, false, false));
        Assert.Equal(RevokeCapabilityImpact.Unknown, RevokeRoleGrantImpact.InternalRequestHandler("IN_PROGRESS",
            [process, read, unknown], [process, unknown], 1, Now));
    }

    [Theory]
    [InlineData("HK_STAFF")]
    [InlineData("DEPT_MANAGER")]
    public void VerifiedHandlerMembershipAndEligibleSurvivingPathsPreserveOnlyHandlerCapability(string role)
    {
        var removed = Grant(1, "IR_PROCESS") with { RoleCode = "HK_STAFF" };
        var process = Grant(2, "IR_PROCESS") with { RoleCode = role };
        var read = Grant(2, "IR_VIEW") with { RoleCode = role };
        Assert.Equal(RevokeCapabilityImpact.Preserved, RevokeRoleGrantImpact.InternalRequestHandler("IN_PROGRESS",
            [removed, process, read], [process, read], 1, Now, true));
        Assert.Equal(RevokeCapabilityImpact.Unknown, RevokeRoleGrantImpact.InternalRequestHandler("IN_PROGRESS",
            [removed, process, read], [process, read], 1, Now, false));
        Assert.Equal(RevokeCapabilityImpact.Lost, RevokeRoleGrantImpact.InternalRequestHandler("IN_PROGRESS",
            [removed, process, read], [read], 1, Now, true));
    }
}
