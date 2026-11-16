using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Housekeeping;
using Xunit;
namespace HotelOps.Dev1.RegressionTests;
public sealed class CleaningMonitorActorTests
{
    private static readonly DateTime Now = new(2026,11,16,0,0,0,DateTimeKind.Utc);
    private static PermissionGrantPath Grant() => new("CLEANING_VIEW","BRANCH",7,true,true,Now.AddDays(-1),null,true,true);
    [Fact] public void CurrentSupervisorBranchOrGlobalGrantRequiresBranchAccess()
    {
        Assert.True(CleaningMonitorService.SupervisorCanMonitor([Grant()],7,Now));
        Assert.True(CleaningMonitorService.SupervisorCanMonitor([Grant() with {BranchId=null,DataScope="ALL"}],7,Now));
        Assert.False(CleaningMonitorService.SupervisorCanMonitor([Grant() with {BranchId=null,DataScope="ALL",BranchAccessActive=false}],7,Now));
    }
    [Fact] public void OwnForeignRevokedOrExpiredGrantsNeverOpenSupervisorMonitor()
    {
        foreach(var grant in new[]{Grant() with {DataScope="OWN"},Grant() with {BranchId=8},Grant() with {RoleActive=false},
            Grant() with {AssignmentActive=false},Grant() with {AssignedAt=Now.AddTicks(1)},Grant() with {ExpiresAt=Now},
            Grant() with {BranchActive=false},Grant() with {BranchAccessActive=false},Grant() with {PermissionCode="CLEANING_EXECUTE"}})
            Assert.False(CleaningMonitorService.SupervisorCanMonitor([grant],7,Now));
    }
}
