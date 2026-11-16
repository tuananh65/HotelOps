using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Maintenance;
using Xunit;
namespace HotelOps.Dev1.RegressionTests;
public sealed class MaintenanceReadAccessTests
{
    private static readonly DateTime Now = new(2026,11,16,0,0,0,DateTimeKind.Utc);
    private static PermissionGrantPath Path(string scope = "BRANCH") => new("MAINTENANCE_VIEW",scope,7,true,true,Now.AddDays(-1),null,true,true);
    private static MaintenanceWork Row(long id, long assignee=10, long branch=7) => new()
    { MaintenanceWorkId=id, AssigneeId=assignee, Incident=new() { BranchId=branch, Branch=new() { IsActive=true } } };
    [Fact] public void StaffIsAssignedOnlyAndReassignmentRemovesVisibility()
    {
        var own=Row(1); var rows=new[] {own,Row(2,11),Row(3,10,8)}.AsQueryable();
        var scope=MaintenanceReadAccess.Resolve(10,7,[new("MAINTENANCE_STAFF",Path())],Now);
        Assert.Equal(new long[]{1},scope.Apply(rows).Select(w=>w.MaintenanceWorkId));
        own.AssigneeId=11; Assert.Empty(scope.Apply(rows));
    }
    [Fact] public void LeadRequiresEligibleSameRolePathAndOwnCannotExpand()
    {
        var rows=new[]{Row(1,11)}.AsQueryable();
        Assert.Single(MaintenanceReadAccess.Resolve(10,7,[new("MAINTENANCE_LEAD",Path())],Now).Apply(rows));
        foreach(var path in new[]{Path("OWN"),Path() with {AssignmentActive=false},Path() with {RoleActive=false},
            Path() with {BranchAccessActive=false},Path() with {ExpiresAt=Now},Path() with {BranchId=8}})
            Assert.Empty(MaintenanceReadAccess.Resolve(10,7,[new("MAINTENANCE_LEAD",path)],Now).Apply(rows));
        Assert.Empty(MaintenanceReadAccess.Resolve(10,7,[new("DEPT_MANAGER",Path()),
            new("MAINTENANCE_LEAD",Path() with {PermissionCode="IR_VIEW"})],Now).Apply(rows));
    }
    [Fact] public void PreventiveSourceMustBeUniqueAndCoherent()
    {
        var run=new PreventiveMaintenanceRun {Schedule=new() {RoomId=1,Room=new() {BranchId=7,Branch=new() {IsActive=true}}}};
        var work=new MaintenanceWork {AssigneeId=10};work.PreventiveMaintenanceRuns.Add(run);
        var scope=MaintenanceReadAccess.Resolve(10,7,[new("MAINTENANCE_LEAD",Path())],Now);
        var rows=new[]{work}.AsQueryable(); Assert.Single(scope.Apply(rows));
        run.Schedule.AreaId=2; Assert.Empty(scope.Apply(rows));run.Schedule.AreaId=null;
        run.Schedule.Room!.BranchId=8;Assert.Empty(scope.Apply(rows));run.Schedule.Room.BranchId=7;
        work.Incident=Row(2).Incident;Assert.Empty(scope.Apply(rows));work.Incident=null;
        work.PreventiveMaintenanceRuns.Add(new(){Schedule=run.Schedule});Assert.Empty(scope.Apply(rows));
    }
    [Fact] public void ReadAdmissionDoesNotAuthorizeMaintenanceMutations()
    {
        var reads = HotelOps.Api.Services.Dev2Authorization.MaintenanceReadContract.Contracts.ToArray();
        Assert.Equal(new[] { "A004A-035", "A004A-036" }, reads.Select(c => c.DecisionId));
        foreach(var contract in reads)
        {
            Assert.True(contract.RequiresReadTransaction);
            Assert.False(contract.MatchesServiceInvocation(null!, null!));
        }
    }
}
