using HotelOps.Api.Controllers.Filters;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Dev2Authorization;
using HotelOps.Api.Services.Incidents;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class IncidentReadAccessTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
    private static PermissionGrantPath Path() => new("INCIDENT_VIEW", "BRANCH", 7, true, true, Now.AddDays(-1), null, true, true);
    private static Incident Row(long id, long reporter = 99, long branch = 7) => new()
        { IncidentId = id, ReportedBy = reporter, BranchId = branch, Branch = new() { IsActive = true } };

    [Fact]
    public void StaffBranchGrantNeverExpandsBeyondReportedOrAssigned()
    {
        var own = Row(1, 10); var assigned = Row(2); assigned.MaintenanceWorks.Add(new() { AssigneeId = 10 });
        var rows = new[] { own, assigned, Row(3), Row(4, 10, 8) }.AsQueryable();
        var scope = IncidentReadAccess.Resolve(10, 7, [new("MAINTENANCE_STAFF", Path())], Now);
        Assert.Equal(new long[] { 1, 2 }, scope.Apply(rows).Select(r => r.IncidentId));
        assigned.MaintenanceWorks.Single().AssigneeId = 11;
        Assert.Single(scope.Apply(rows));
    }

    [Fact]
    public void LeadGrantRequiresSameCurrentRolePathAndCoherentBranch()
    {
        var rows = new[] { Row(1), Row(2, branch: 8), Row(3) };
        rows[2].Area = new() { BranchId = 8 };
        Assert.Single(IncidentReadAccess.Resolve(10, 7, [new("MAINTENANCE_LEAD", Path())], Now).Apply(rows.AsQueryable()));
        foreach (var path in new[] { Path() with { AssignmentActive = false }, Path() with { RoleActive = false },
            Path() with { BranchAccessActive = false }, Path() with { ExpiresAt = Now }, Path() with { AssignedAt = Now.AddTicks(1) },
            Path() with { PermissionCode = "IR_VIEW" }, Path() with { BranchId = 8 }, Path() with { DataScope = "OWN" } })
            Assert.Empty(IncidentReadAccess.Resolve(10, 7, [new("MAINTENANCE_LEAD", path)], Now).Apply(rows.AsQueryable()));
        Assert.Empty(IncidentReadAccess.Resolve(10, 7, [new("DEPT_MANAGER", Path()), new("MAINTENANCE_LEAD", Path() with { PermissionCode = "IR_VIEW" })], Now).Apply(rows.AsQueryable()));
    }

    [Fact]
    public void IncidentReadContractsCannotMatchMutationAndTriageIsSeparate()
    {
        string[] reads = ["A004A-022", "A004A-023", "INCIDENT-CATEGORIES-READ", "INCIDENT-EVIDENCE-LIST", "INCIDENT-EVIDENCE-DOWNLOAD"];
        Assert.Equal(reads.Order(), Dev2ActionAdmissionRegistry.KnownActions
            .Where(i => i.ControllerType == typeof(HotelOps.Api.Controllers.IncidentsController) && i.HttpMethod == "GET" && Dev2ActionContracts.Find(i) != null)
            .Select(i => i.DecisionId).Order());
        foreach (var identity in Dev2ActionAdmissionRegistry.KnownActions.Where(i => i.ControllerType == typeof(HotelOps.Api.Controllers.IncidentsController)))
        {
            var contract = Dev2ActionContracts.Find(identity);
            if (reads.Contains(identity.DecisionId))
            {
                Assert.NotNull(contract); Assert.True(contract.IsComplete(identity)); Assert.True(contract.RequiresReadTransaction);
                Assert.False(contract.MatchesServiceInvocation(null!, null!));
            }
            else if (identity.DecisionId is "A004A-020" or "A004A-021")
            {
                Assert.NotNull(contract);
                Assert.True(contract.RequiresActionTransaction);
                Assert.False(contract.RequiresReadTransaction);
            }
            else Assert.Null(contract);
        }
    }
}
