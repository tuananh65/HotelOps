using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class TaskDashboardAccessTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);
    private static PermissionGrantPath Grant() => new("DASHBOARD_VIEW", "BRANCH", 7, true, true,
        Now.AddDays(-1), null, true, true);

    [Theory]
    [InlineData("OPERATIONS_MANAGER", true)]
    [InlineData("OWNER", true)]
    [InlineData("DEPT_MANAGER", false)]
    [InlineData("SYS_ADMIN", false)]
    [InlineData("HK_STAFF", false)]
    public void ActorAndPermissionAreBothRequired(string role, bool expected)
        => Assert.Equal(expected, TaskDashboardAccess.Allows(role, Grant(), 7, Now));

    [Fact]
    public void SameAssignmentMustSupplyRolePermissionAndScope()
    {
        Assert.False(TaskDashboardAccess.Allows("OWNER", Grant() with { PermissionCode = "AUDIT_VIEW" }, 7, Now));
        Assert.False(TaskDashboardAccess.Allows("OPERATIONS_MANAGER", Grant(), 8, Now));
        Assert.False(TaskDashboardAccess.Allows("DEPT_MANAGER", Grant() with { DataScope = "ALL", BranchId = null }, 8, Now));
        Assert.False(TaskDashboardAccess.Allows("OPERATIONS_MANAGER", Grant(), null, Now));
        Assert.True(TaskDashboardAccess.Allows("OWNER", Grant() with { DataScope = "ALL", BranchId = null }, 8, Now));
    }

    [Fact]
    public void InactiveExpiredFutureOrRevokedPathsCannotGrant()
    {
        foreach (var path in new[] { Grant() with { AssignmentActive = false }, Grant() with { RoleActive = false },
            Grant() with { BranchActive = false }, Grant() with { BranchAccessActive = false },
            Grant() with { AssignedAt = Now.AddSeconds(1) }, Grant() with { ExpiresAt = Now },
            Grant() with { DataScope = "OWN" }, Grant() with { DataScope = "DEPARTMENT" } })
            Assert.False(TaskDashboardAccess.Allows("OPERATIONS_MANAGER", path, 7, Now));
    }
}
