using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Maintenance;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class MaintenanceMaterialAccessTests
{
    [Fact]
    public void Inventory_read_requires_current_independent_branch_grant()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        var grant = new PermissionGrantPath("INVENTORY_VIEW", "BRANCH", 7, true, true, now.AddDays(-1), null, true, true);
        Assert.True(MaintenanceMaterialAccess.AllowsInventory(grant, 7, now));
        foreach (var invalid in new[] { grant with { PermissionCode = "MAINTENANCE_VIEW" },
            grant with { PermissionCode = "MAINTENANCE_EXECUTE" }, grant with { PermissionCode = "MR_VIEW" },
            grant with { DataScope = "OWN" }, grant with { BranchId = 8 }, grant with { RoleActive = false },
            grant with { AssignmentActive = false }, grant with { BranchAccessActive = false },
            grant with { BranchActive = false }, grant with { ExpiresAt = now } })
            Assert.False(MaintenanceMaterialAccess.AllowsInventory(invalid, 7, now));
    }
}
