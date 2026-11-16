using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Security.Authorization;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class MaintenanceAssignmentEligibilityTests
{
    [Fact]
    public void AssignmentGrantRequiresCurrentScopedLead()
    {
        var now=new DateTime(2026,11,16);
        var grant=new PermissionGrantPath("MAINTENANCE_ASSIGN","BRANCH",3,true,true,now.AddDays(-1),null,true,true);
        Assert.True(MaintenanceAssignmentAccess.Allows("MAINTENANCE_LEAD",grant,3,now));
        Assert.False(MaintenanceAssignmentAccess.Allows("MAINTENANCE_STAFF",grant,3,now));
        foreach(var bad in new[]{grant with {BranchId=4},grant with {RoleActive=false},grant with {AssignmentActive=false},
            grant with {BranchAccessActive=false},grant with {ExpiresAt=now},grant with {DataScope="OWN"},grant with {PermissionCode="MAINTENANCE_VIEW"}})
            Assert.False(MaintenanceAssignmentAccess.Allows("MAINTENANCE_LEAD",bad,3,now));
    }

    [Fact]
    public void FormerAssigneeReceiptDoesNotGrantWorkAccessOrCrossBranchAccess()
    {
        var receipt=new MaintenanceAssignmentReceipt {PreviousAssigneeId=10,AssigneeId=11,BranchId=3};
        var own=new MaintenanceReadAccess.Scope(10,3,false,true);
        Assert.True(MaintenanceAssignmentStore.AllowsReceipt(10,receipt,own));
        Assert.False(MaintenanceAssignmentStore.AllowsReceipt(11,receipt,new(11,3,true,true)));
        Assert.False(MaintenanceAssignmentStore.AllowsReceipt(10,receipt,own with {Branch=4}));
        Assert.False(MaintenanceAssignmentStore.AllowsReceipt(10,receipt,own with {User=11}));
        Assert.False(MaintenanceAssignmentStore.AllowsReceipt(10,receipt,own with {Staff=false}));
    }
    [Fact]
    public void PlannedEndRequiresUtcAndCannotPrecedeStart()
    {
        var start = new DateTime(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc);
        Assert.Equal(start, MaintenanceAssignmentEligibility.RequirePlannedEnd(start, start));
        Assert.Equal(start.AddHours(2), MaintenanceAssignmentEligibility.RequirePlannedEnd(null, start.AddHours(2)));
        Assert.Throws<ValidationException>(() => MaintenanceAssignmentEligibility.RequirePlannedEnd(start, null));
        Assert.Throws<ValidationException>(() => MaintenanceAssignmentEligibility.RequirePlannedEnd(start, start.AddTicks(-1)));
        Assert.Throws<ValidationException>(() => MaintenanceAssignmentEligibility.RequirePlannedEnd(start, DateTime.SpecifyKind(start, DateTimeKind.Unspecified)));
        Assert.Throws<ValidationException>(() => MaintenanceAssignmentEligibility.RequirePlannedEnd(start, DateTime.SpecifyKind(start, DateTimeKind.Local)));
    }
}
