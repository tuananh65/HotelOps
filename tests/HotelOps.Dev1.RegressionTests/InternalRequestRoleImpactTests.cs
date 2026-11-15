using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Users;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;
public sealed class InternalRequestRoleImpactTests
{
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
}
