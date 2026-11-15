using System.Text.Json;
using System.Text.Json.Nodes;
using HotelOps.Api.Services.Notifications;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class ReinspectionReassignmentNotificationTests
{
    private static JsonNode Receipt()=>JsonSerializer.SerializeToNode(new
    {
        contract="FE05_REINSPECTION_REASSIGNMENT_V1",issuer="FE05.UC041.REINSPECTION_REASSIGN",issuerVersion=1,
        actorUserId=11,branchId=12,inspectionId=13,reworkId=14,originalInspectorId=17,auditId=20,
        operationId="01111111-1111-4111-8111-111111111111",writerTransactionId=15,
        entity=new {id=13,branchId=12,taskId=21,roomId=18,versionId=19,previousInspectorId=17,inspectorId=16,
            reworkId=14,reworkETag="\"0123456789ABCDEF\"",bindingETag="\"0123456789ABCDE0\"",state="ASSIGNED",sourceETag="\"0123456789ABCDE1\"",
            operationId="01111111-1111-4111-8111-111111111111",notification="PENDING_DELIVERY",contract="FE05_REINSPECTION_REASSIGNMENT_V1"},
        notification=new {schemaVersion=1,kind="REINSPECTION_ASSIGNED",recipientUserId=16}
    })!;

    [Fact]
    public void Transfer_retains_original_inspector_and_current_recipient_separately()
    {
        var receipt=ReinspectionReassignmentSource.Parse(Receipt().ToJsonString());
        Assert.NotNull(receipt);Assert.Equal(17,receipt.OriginalInspectorId);Assert.Equal(16,receipt.Notification.RecipientUserId);
        Assert.Equal(20,receipt.AuditId);Assert.Equal(14,receipt.Entity.ReworkId);
    }
    [Theory]
    [InlineData("entity.state","INSPECTING")]
    [InlineData("entity.state","PASSED")]
    [InlineData("entity.state","FAILED")]
    [InlineData("entity.contract","FE05_INSPECTION_REASSIGNMENT_V1")]
    [InlineData("entity.bindingETag","*")]
    [InlineData("entity.reworkETag","W/\"0123456789ABCDEF\"")]
    [InlineData("entity.sourceETag","\"0123456789abcdef\"")]
    [InlineData("notification.kind","INSPECTION_ASSIGNED")]
    [InlineData("issuer","FE05.UC041.REASSIGN")]
    [InlineData("operationId","00000000-0000-0000-0000-000000000000")]
    public void Other_workflow_or_invalid_revision_is_denied(string path,string value)
    {
        var node=Receipt();var parts=path.Split('.');
        if(parts.Length==1)node[parts[0]]=value;else node[parts[0]]![parts[1]]=value;
        Assert.Null(ReinspectionReassignmentSource.Parse(node.ToJsonString()));
    }
    [Theory]
    [InlineData("auditId",0)]
    [InlineData("originalInspectorId",0)]
    [InlineData("reworkId",99)]
    [InlineData("entity.id",99)]
    [InlineData("entity.branchId",99)]
    [InlineData("entity.reworkId",99)]
    [InlineData("entity.previousInspectorId",16)]
    [InlineData("notification.recipientUserId",17)]
    public void Substituted_identity_is_denied(string path,int value)
    {
        var node=Receipt();var parts=path.Split('.');
        if(parts.Length==1)node[parts[0]]=value;else node[parts[0]]![parts[1]]=value;
        Assert.Null(ReinspectionReassignmentSource.Parse(node.ToJsonString()));
    }
    [Theory]
    [InlineData("null")][InlineData("[]")][InlineData("{}")] [InlineData("broken")]
    public void Malformed_receipt_is_denied(string value)=>Assert.Null(ReinspectionReassignmentSource.Parse(value));
}
