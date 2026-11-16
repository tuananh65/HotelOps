using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Users;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class RevokeGrantRereadTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
    private static RevokeGrant Grant() => new(31, new PermissionGrantPath("IR_PROCESS", "BRANCH", 7,
        true, true, Now.AddDays(-1), null, true, true), "DEPT_MANAGER");

    [Fact]
    public void EqualPermissionSummaryCannotHideActorOrAssignmentDrift()
    {
        var expected = Grant();
        foreach (var changed in new[] { expected with { RoleCode = "OPERATIONS_MANAGER" },
            expected with { AssignmentId = 32 }, expected with { RoleCode = null },
            expected with { Path = expected.Path with { ExpiresAt = Now.AddDays(1) } } })
        {
            Assert.Equal(RevokeRoleGrantImpact.Effective([expected], Now), RevokeRoleGrantImpact.Effective([changed], Now));
            Assert.False(RevokeRoleGrantImpact.SameEffectivePaths([expected], [changed], Now));
        }
    }

    [Fact]
    public void ExactSurvivorsAreOrderIndependentAndIgnoreInactiveRevokedAssignment()
    {
        var first = Grant();
        var second = first with { AssignmentId = 32, Path = first.Path with { PermissionCode = "IR_VIEW" } };
        var inactive = first with { AssignmentId = 33, Path = first.Path with { AssignmentActive = false } };
        Assert.True(RevokeRoleGrantImpact.SameEffectivePaths([first, second], [inactive, second, first], Now));
        Assert.False(RevokeRoleGrantImpact.SameEffectivePaths([first, second], [first], Now));
        Assert.False(RevokeRoleGrantImpact.SameEffectivePaths([first], [first, second], Now));
        Assert.False(RevokeRoleGrantImpact.SameEffectivePaths([first], [first], DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
    }
}
