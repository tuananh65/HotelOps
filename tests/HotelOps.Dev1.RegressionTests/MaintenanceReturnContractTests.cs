using HotelOps.Api.Controllers;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Services.Dev2Authorization;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class MaintenanceReturnContractTests
{
    [Fact]
    public async Task Return_requires_both_permissions_and_exact_action_root_input_binding()
    {
        var contract = Assert.Single(MaintenanceReturnContract.Contracts);
        var input = new ReturnMaintenanceWorkRequest { ActorUserId = 10, Reason = "Repair is incomplete" };
        var action = new Dev2BoundAction(new("A004A-032", typeof(MaintenanceWorksController), "ReturnWork", "POST", "api/maintenance-works/{id:long}/return"),
            new DefaultHttpContext(), new(10,20,false), new Dictionary<string,object?> { ["id"] = 1L, ["request"] = input });
        Assert.True(contract.RequiresActionTransaction);
        Assert.Equal(Dev2PermissionCombination.All, contract.Combination);
        Assert.Equal(2, (await contract.Requirements(action, CancellationToken.None)).Count);
        Assert.True(contract.MatchesServiceInvocation(action, new("MaintenanceWorks","ReturnWork",1,input)));
        Assert.False(contract.MatchesServiceInvocation(action, new("MaintenanceWorks","VerifyWork",1,input)));
        Assert.False(contract.MatchesServiceInvocation(action, new("MaintenanceWorks","ReturnWork",2,input)));
        Assert.False(contract.MatchesServiceInvocation(action, new("MaintenanceWorks","ReturnWork",1,
            new ReturnMaintenanceWorkRequest { ActorUserId=10, Reason=input.Reason })));
        Assert.Throws<Dev2AccessDenied>(() => contract.MatchesServiceInvocation(action with
        { Arguments = new Dictionary<string,object?> { ["id"]=1L,["request"]=new VerifyMaintenanceWorkRequest { ActorUserId=10 } } },
            new("MaintenanceWorks","ReturnWork",1,input)));
        Assert.False(await contract.ValidateFactsAndWorkflow(action,Dev2AuthorizationPhase.Admission,CancellationToken.None)); // HTTP denied before SQL.
        Assert.DoesNotContain(MaintenanceReturnContract.Contracts, c => c.DecisionId == "A004A-031");
    }
}
