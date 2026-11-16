using HotelOps.Api.Controllers;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Dev2Authorization;
using HotelOps.Api.Services.Incidents;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class IncidentTriageAuthorizationTests
{
    [Fact]
    public void OnlyCurrentScopedLeadGrantAllowsTriage()
    {
        var now = new DateTime(2026, 11, 16);
        var path = new PermissionGrantPath("INCIDENT_TRIAGE", "BRANCH", 7, true, true, now.AddDays(-1), null, true, true);
        Assert.True(IncidentTriageAccess.Allows("MAINTENANCE_LEAD", path, 7, now));
        foreach (var role in new[] { "MAINTENANCE_STAFF", "OPERATIONS_MANAGER", "SYS_ADMIN" })
            Assert.False(IncidentTriageAccess.Allows(role, path, 7, now));
        foreach (var denied in new[] { path with { BranchId = 8 }, path with { AssignmentActive = false },
            path with { RoleActive = false }, path with { BranchAccessActive = false }, path with { ExpiresAt = now },
            path with { DataScope = "OWN" }, path with { PermissionCode = "INCIDENT_VIEW" } })
            Assert.False(IncidentTriageAccess.Allows("MAINTENANCE_LEAD", denied, 7, now));
    }

    [Fact]
    public async Task ExactBindingRequiresBothPermissionsAndRejectsUntrustedActorBeforeSql()
    {
        var contract = Assert.Single(IncidentTriageContract.Contracts);
        var request = new TriageIncidentRequest { ActorUserId = 10, SeverityId = 2, PriorityId = 3 };
        var http = new DefaultHttpContext();
        var action = new Dev2BoundAction(new("A004A-020", typeof(IncidentsController), "TriageIncident", "PATCH", "api/incidents/{id:long}/triage"),
            http, new(10, 20, false), new Dictionary<string, object?> { ["id"] = 7L, ["request"] = request });
        Assert.Equal(Dev2PermissionCombination.All, contract.Combination);
        Assert.Equal(2, (await contract.Requirements(action, CancellationToken.None)).Count);
        Assert.True(contract.RequiresActionTransaction);
        Assert.True(contract.MatchesServiceInvocation(action, new("Incidents", "TriageIncident", 7, request)));
        Assert.False(contract.MatchesServiceInvocation(action, new("Incidents", "TriageIncident", 8, request)));
        Assert.False(contract.MatchesServiceInvocation(action, new("Incidents", "ChangeIncidentStatus", 7, request)));
        Assert.False(contract.MatchesServiceInvocation(action, new("Incidents", "TriageIncident", 7, new TriageIncidentRequest())));
        Assert.False(await contract.ValidateFactsAndWorkflow(action, Dev2AuthorizationPhase.Admission, CancellationToken.None));
        http.Request.Scheme = "https";
        Assert.False(await contract.ValidateFactsAndWorkflow(action with { Actor = new(11, 20, false) }, Dev2AuthorizationPhase.Admission, CancellationToken.None));
        Assert.Contains("A004A-020", Dev2ActionContracts.DecisionIds);
    }
}
