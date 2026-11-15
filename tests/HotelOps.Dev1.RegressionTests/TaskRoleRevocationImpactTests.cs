using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Users;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class TaskRoleRevocationImpactTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);
    private static RevokeGrant Grant(long assignment = 1, long branch = 7) => new(assignment,
        new PermissionGrantPath("TASK_EXECUTE", "OWN", branch, true, true, Now.AddDays(-1), null, true, true));

    [Theory]
    [InlineData("ASSIGNED")]
    [InlineData("ACCEPTED")]
    [InlineData("IN_PROGRESS")]
    [InlineData("PAUSED")]
    [InlineData("PENDING_VERIFICATION")]
    public void RemovingLastExecutorGrantBlocksEveryOpenCanonicalState(string state)
        => Assert.Equal(RevokeCapabilityImpact.Lost, RevokeRoleGrantImpact.OperationalTask(state, true, [Grant()], [], 7, Now));

    [Theory]
    [InlineData("TODO")]
    [InlineData("BLOCKED")]
    [InlineData("COMPLETED")]
    [InlineData("CLOSED")]
    [InlineData("CANCELLED")]
    [InlineData("PENDING_ASSIGNMENT")]
    [InlineData(null)]
    public void PhysicalOrContradictoryStateCannotCertifyCurrentAssignment(string? state)
        => Assert.Equal(RevokeCapabilityImpact.Unknown, RevokeRoleGrantImpact.OperationalTask(state, true, [Grant()], [], 7, Now));

    [Fact]
    public void AnotherCurrentOwnGrantPreservesCapabilityButForeignOrExpiredDoesNot()
    {
        RevokeGrant[] before = [Grant(), Grant(2)];
        Assert.Equal(RevokeCapabilityImpact.Preserved, RevokeRoleGrantImpact.OperationalTask("PAUSED", true,
            before, RevokeRoleGrantImpact.Without(before, 1), 7, Now));
        Assert.Equal(RevokeCapabilityImpact.Lost, RevokeRoleGrantImpact.OperationalTask("PAUSED", true,
            before, [Grant(2, 8)], 7, Now));
        Assert.Equal(RevokeCapabilityImpact.Lost, RevokeRoleGrantImpact.OperationalTask("PAUSED", true,
            before, [Grant(2) with { Path = Grant(2).Path with { ExpiresAt = Now } }], 7, Now));
    }

    [Fact]
    public void HistoricalExecutorIsNotCurrentResponsibilityAndUnknownPriorCapabilityCannotBeCleared()
    {
        Assert.Equal(RevokeCapabilityImpact.Preserved, RevokeRoleGrantImpact.OperationalTask("IN_PROGRESS", false, [Grant()], [], 7, Now));
        Assert.Equal(RevokeCapabilityImpact.Unknown, RevokeRoleGrantImpact.OperationalTask("IN_PROGRESS", true, [], [], 7, Now));
        Assert.Equal(RevokeCapabilityImpact.Unknown, RevokeRoleGrantImpact.OperationalTask("IN_PROGRESS", true, [Grant()], [], null, Now));
    }
}
