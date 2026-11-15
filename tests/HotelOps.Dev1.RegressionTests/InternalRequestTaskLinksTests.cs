using System.Text.Json;
using HotelOps.Api.Services.InternalRequests;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class InternalRequestTaskLinksTests
{
    [Fact]
    public void CompletionRequirementCannotDefaultWhenOmitted()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<InternalRequestTaskLinkInput>("{\"taskId\":1}", options));
        Assert.False(JsonSerializer.Deserialize<InternalRequestTaskLinkInput>("{\"taskId\":1,\"mandatory\":false}", options)!.Mandatory);
        Assert.True(JsonSerializer.Deserialize<InternalRequestTaskLinkInput>("{\"taskId\":1,\"mandatory\":true}", options)!.Mandatory);
    }

    [Theory]
    [InlineData(null)] [InlineData("TODO")] [InlineData("COMPLETED")] [InlineData("CLOSED")] [InlineData("CANCELLED")]
    public void PhysicalUnknownAndTerminalStatesCannotBeLinked(string? state) => Assert.False(InternalRequestTaskLinks.Open(state));

    [Theory]
    [InlineData("PENDING_ASSIGNMENT")] [InlineData("ASSIGNED")] [InlineData("ACCEPTED")]
    [InlineData("IN_PROGRESS")] [InlineData("PAUSED")] [InlineData("PENDING_VERIFICATION")]
    public void VerifiedOpenWorkflowStatesCanBeLinked(string state) => Assert.True(InternalRequestTaskLinks.Open(state));
}
