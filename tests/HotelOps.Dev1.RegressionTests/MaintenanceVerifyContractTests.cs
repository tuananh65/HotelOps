using HotelOps.Api.Controllers;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Services.Dev2Authorization;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class MaintenanceVerifyContractTests
{
    [Fact]
    public async Task Verify_requires_both_permissions_and_exact_action_root_input_binding()
    {
        var contract = Assert.Single(MaintenanceVerifyContract.Contracts);
        var input = new VerifyMaintenanceWorkRequest { ActorUserId = 10, SubmissionAuditId = 12, Note = "Repair is complete" };
        var action = new Dev2BoundAction(new("A004A-031", typeof(MaintenanceWorksController), "VerifyWork", "POST", "api/maintenance-works/{id:long}/verify"),
            new DefaultHttpContext(), new(10,20,false), new Dictionary<string,object?> { ["id"] = 1L, ["request"] = input });
        Assert.True(contract.RequiresActionTransaction);
        Assert.Equal(Dev2PermissionCombination.All, contract.Combination);
        Assert.Equal(2, (await contract.Requirements(action, CancellationToken.None)).Count);
        Assert.True(contract.MatchesServiceInvocation(action, new("MaintenanceWorks","VerifyWork",1,input)));
        Assert.False(contract.MatchesServiceInvocation(action, new("MaintenanceWorks","ReturnWork",1,input)));
        Assert.False(contract.MatchesServiceInvocation(action, new("MaintenanceWorks","VerifyWork",2,input)));
        Assert.False(contract.MatchesServiceInvocation(action, new("MaintenanceWorks","VerifyWork",1,
            new VerifyMaintenanceWorkRequest { ActorUserId=10, Note=input.Note })));
        Assert.Throws<Dev2AccessDenied>(() => contract.MatchesServiceInvocation(action with
        { Arguments = new Dictionary<string,object?> { ["id"]=1L,["request"]=new ReturnMaintenanceWorkRequest { ActorUserId=10 } } },
            new("MaintenanceWorks","VerifyWork",1,input)));
        Assert.False(await contract.ValidateFactsAndWorkflow(action,Dev2AuthorizationPhase.Admission,CancellationToken.None)); // HTTP denied before SQL.
        Assert.Contains("A004A-031", Dev2ActionContracts.DecisionIds);
    }

    [Theory]
    [InlineData(11, 12)]
    [InlineData(10, 0)]
    [InlineData(10, -1)]
    public async Task Verify_rejects_actor_mismatch_or_missing_snapshot_before_database_access(long actor, long submission)
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        var action = new Dev2BoundAction(new("A004A-031", typeof(MaintenanceWorksController), "VerifyWork", "POST", "api/maintenance-works/{id:long}/verify"),
            http, new(10,20,false), new Dictionary<string,object?> { ["id"] = 1L,
                ["request"] = new VerifyMaintenanceWorkRequest { ActorUserId = actor, SubmissionAuditId = submission, Note = "Repair is complete" } });
        Assert.False(await Assert.Single(MaintenanceVerifyContract.Contracts)
            .ValidateFactsAndWorkflow(action, Dev2AuthorizationPhase.Admission, CancellationToken.None));
    }
}
