using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Maintenance;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class MaintenanceExecutionAccessTests
{
    [Fact]
    public void Resume_contract_is_bound_to_exact_action_root_and_request()
    {
        var contract = Assert.Single(HotelOps.Api.Services.Dev2Authorization.MaintenanceResumeContract.Contracts);
        var request = new HotelOps.Api.DTOs.Maintenance.ResumeMaintenanceWorkRequest { ActorUserId = 10 };
        var action = new HotelOps.Api.Services.Dev2Authorization.Dev2BoundAction(
            new("A004A-028", typeof(HotelOps.Api.Controllers.MaintenanceWorksController), "ResumeWork", "POST", "api/maintenance-works/{id:long}/resume"),
            new Microsoft.AspNetCore.Http.DefaultHttpContext(), new(10, 20, false), new Dictionary<string, object?> { ["id"] = 1L, ["request"] = request });
        Assert.True(contract.RequiresActionTransaction);
        Assert.True(contract.MatchesServiceInvocation(action, new("MaintenanceWorks", "ResumeWork", 1, request)));
        Assert.False(contract.MatchesServiceInvocation(action, new("MaintenanceWorks", "StartWork", 1, request)));
        Assert.False(contract.MatchesServiceInvocation(action, new("MaintenanceWorks", "ResumeWork", 2, request)));
        Assert.False(contract.MatchesServiceInvocation(action, new("MaintenanceWorks", "ResumeWork", 1,
            new HotelOps.Api.DTOs.Maintenance.ResumeMaintenanceWorkRequest { ActorUserId = 10 })));
    }

    private static readonly DateTime Now = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
    private static PermissionGrantPath Path(string scope = "OWN")
        => new("MAINTENANCE_EXECUTE", scope, 7, true, true, Now.AddDays(-1), null, true, true);

    [Theory]
    [InlineData("OWN", true)]
    [InlineData("BRANCH", true)]
    [InlineData("GLOBAL", false)]
    public void Current_staff_execution_path_is_required(string scope, bool allowed)
        => Assert.Equal(allowed, MaintenanceExecutionAccess.Allows("MAINTENANCE_STAFF", Path(scope), 7, Now));

    [Fact]
    public void Other_role_read_permission_revocation_and_foreign_branch_do_not_authorize_pause()
    {
        foreach (var role in new[] { "MAINTENANCE_LEAD", "DEPT_MANAGER", "ADMIN", "" })
            Assert.False(MaintenanceExecutionAccess.Allows(role, Path(), 7, Now));
        foreach (var path in new[] { Path() with { PermissionCode = "MAINTENANCE_VIEW" },
            Path() with { AssignmentActive = false }, Path() with { RoleActive = false },
            Path() with { BranchAccessActive = false }, Path() with { BranchActive = false },
            Path() with { ExpiresAt = Now }, Path() with { AssignedAt = Now.AddMinutes(1) },
            Path() with { BranchId = 8 }, Path("DEPARTMENT") })
            Assert.False(MaintenanceExecutionAccess.Allows("MAINTENANCE_STAFF", path, 7, Now));
    }
}
