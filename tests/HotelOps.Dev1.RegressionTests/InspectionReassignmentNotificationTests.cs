using System.Text.Json;
using System.Text.Json.Nodes;
using HotelOps.Api.Services.Notifications;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class InspectionReassignmentNotificationTests
{
    [Theory]
    [InlineData("ASSIGNED")]
    [InlineData("INSPECTING")]
    [InlineData("PENDING_CONCLUSION")]
    public void Open_lifecycle_is_retained_but_same_inspector_is_not_an_event(string state)
    {
        var node=Receipt(); node["entity"]!["state"]=state;
        Assert.NotNull(InspectionReassignmentSource.Parse(node.ToJsonString()));
        node["entity"]!["previousInspectorId"]=16;
        Assert.Null(InspectionReassignmentSource.Parse(node.ToJsonString()));
    }
    private static JsonNode Receipt() => JsonSerializer.SerializeToNode(new
    {
        contract = "FE05_INSPECTION_REASSIGNMENT_V1", issuer = "FE05.UC041.REASSIGN", issuerVersion = 1,
        actorUserId = 11, branchId = 12, inspectionId = 13, taskId = 14,
        operationId = "11111111-1111-1111-1111-111111111111", writerTransactionId = 15,
        entity = new { id = 13, branchId = 12, taskId = 14, roomId = 18, versionId = 19, previousInspectorId = 17, inspectorId = 16, state = "ASSIGNED", sourceETag = "\"0123456789ABCDEF\"",
            operationId = "11111111-1111-1111-1111-111111111111", notification = "PENDING_DELIVERY", contract = "FE05_INSPECTION_REASSIGNMENT_V1" },
        notification = new { schemaVersion = 1, kind = "INSPECTION_ASSIGNED", recipientUserId = 16 }
    })!;

    [Fact]
    public void Receipt_retains_assigned_recipient_separately_from_assigner()
    {
        var receipt = InspectionReassignmentSource.Parse(Receipt().ToJsonString());
        Assert.NotNull(receipt);
        Assert.Equal(11, receipt.ActorUserId);
        Assert.Equal(16, receipt.Notification.RecipientUserId);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("broken")]
    public void Malformed_receipt_is_denied(string json) => Assert.Null(InspectionReassignmentSource.Parse(json));

    [Theory]
    [InlineData("contract", "OTHER")]
    [InlineData("issuer", "FE05.UC047")]
    [InlineData("entity.state", "PASSED")]
    [InlineData("entity.sourceETag", "*")]
    [InlineData("notification.kind", "REINSPECTION_PASSED")]
    public void Substituted_contract_or_source_is_denied(string path, string value)
    {
        var node = Receipt(); var parts = path.Split('.');
        if (parts.Length == 1) node[parts[0]] = value; else node[parts[0]]![parts[1]] = value;
        Assert.Null(InspectionReassignmentSource.Parse(node.ToJsonString()));
    }
}
