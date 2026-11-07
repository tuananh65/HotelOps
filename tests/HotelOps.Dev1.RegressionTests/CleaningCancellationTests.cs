using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Housekeeping;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class CleaningCancellationTests
{
    [Theory]
    [InlineData("PENDING_ASSIGNMENT", "PENDING", 0, true)]
    [InlineData("ASSIGNED", "PENDING", 1, true)]
    [InlineData("ASSIGNED", "PENDING", 0, false)]
    [InlineData("PENDING_ASSIGNMENT", "PENDING", 1, false)]
    [InlineData("ASSIGNED", "IN_PROGRESS", 1, false)]
    [InlineData("IN_PROGRESS", "IN_PROGRESS", 1, false)]
    [InlineData("ON_HOLD", "IN_PROGRESS", 1, false)]
    [InlineData("PENDING_INSPECTION", "IN_PROGRESS", 1, false)]
    [InlineData("COMPLETED", "COMPLETED", 0, false)]
    [InlineData("CANCELLED", "CANCELLED", 0, false)]
    [InlineData(null, "PENDING", 0, false)]
    public void State_requires_unstarted_consistent_assignment(string? state, string status, int owners, bool expected)
    {
        Assert.Equal(expected, CleaningCancellationRules.CanCancel(state, status, null, null, owners));
        Assert.False(CleaningCancellationRules.CanCancel(state, status, DateTime.UtcNow, null, owners));
        Assert.False(CleaningCancellationRules.CanCancel(state, status, null, DateTime.UtcNow, owners));
    }
    [Theory]
    [InlineData("confirmation")][InlineData("reason")][InlineData("length")][InlineData("control")]
    [InlineData("weak-tag")][InlineData("multiple-tag")][InlineData("key")][InlineData("branch")]
    public void Mandatory_request_validation(string scenario)
    {
        var reason = scenario switch { "reason" => " ", "length" => new string('x',501), "control" => "a\0b", _ => "Change of plan" };
        Assert.Throws<ValidationException>(() => CleaningCancellationRules.Validate(scenario == "branch" ? 0 : 1, 2,
            scenario switch { "weak-tag" => "W/\"0000000000000001\"", "multiple-tag" => "\"0000000000000001\",\"0000000000000002\"", _ => "\"0000000000000001\"" },
            scenario == "key" ? "invalid" : Guid.NewGuid().ToString(), new(scenario != "confirmation", reason)));
    }
    [Fact]
    public void Replay_fingerprint_binds_actor_branch_task_tag_and_reason()
    {
        var hash = CleaningCancellationRules.Fingerprint(1,2,3,"tag","reason");
        Assert.Equal(hash, CleaningCancellationRules.Fingerprint(1,2,3,"tag"," reason "));
        Assert.NotEqual(hash, CleaningCancellationRules.Fingerprint(2,2,3,"tag","reason"));
        Assert.NotEqual(hash, CleaningCancellationRules.Fingerprint(1,3,3,"tag","reason"));
        Assert.NotEqual(hash, CleaningCancellationRules.Fingerprint(1,2,4,"tag","reason"));
        Assert.NotEqual(hash, CleaningCancellationRules.Fingerprint(1,2,3,"new","reason"));
        Assert.NotEqual(hash, CleaningCancellationRules.Fingerprint(1,2,3,"tag","other"));
    }
    [Fact]
    public void Receipt_requires_atomic_readiness_and_mandatory_assignee_publication()
    {
        var operation = Guid.NewGuid();
        var r = new CleaningCancellationResult(3,2,1,null,null,operation,null,1,DateTime.UtcNow,"\"0000000000000001\"");
        Assert.True(CleaningCancellationRules.ValidReceipt(r,2,3,operation));
        Assert.False(CleaningCancellationRules.ValidReceipt(r with { AssignmentId=1 },2,3,operation));
        Assert.False(CleaningCancellationRules.ValidReceipt(r with { Readiness="READY" },2,3,operation));
        Assert.False(CleaningCancellationRules.ValidReceipt(r with { ReadinessEventId=0 },2,3,operation));
        Assert.False(CleaningCancellationRules.ValidReceipt(r,3,3,operation));
        Assert.False(CleaningCancellationRules.ValidReceipt(r,2,3,Guid.NewGuid()));
    }
}
