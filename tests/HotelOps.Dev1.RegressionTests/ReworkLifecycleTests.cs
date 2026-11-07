using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.HotelStructure;
using System.Text.Json;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class ReworkLifecycleTests
{
    [Theory]
    [InlineData("PENDING_ACCEPTANCE", "accept", "ACCEPTED")]
    [InlineData("ACCEPTED", "start", "IN_PROGRESS")]
    [InlineData("IN_PROGRESS", "complete", "PENDING_REINSPECTION")]
    [InlineData("PENDING_ACCEPTANCE", "start", null)]
    [InlineData("ACCEPTED", "complete", null)]
    [InlineData("PENDING_REINSPECTION", "complete", null)]
    [InlineData("COMPLETED", "start", null)]
    [InlineData(null, "accept", null)]
    public void Receive_and_submit_require_each_SRS_transition(string? state, string action, string? expected)
        => Assert.Equal(expected, ReworkLifecycleRules.Next(state, action));

    [Fact]
    public void Remediation_is_required_for_exact_source_defects_without_substitution_or_duplicates()
    {
        ReworkLifecycleRules.ValidateRemediations([1, 2], [new(2, "Mirror cleaned"), new(1, "Floor cleaned")]);
        foreach (var rows in new ReworkRemediation[][] {
            [new(1, "Fixed")], [new(1, "Fixed"), new(3, "Foreign defect")],
            [new(1, "Fixed"), new(1, "Duplicate")], [new(1, "Fixed"), new(2, " ")],
            [new(1, "Fixed"), new(2, new string('x', 501))] })
            Assert.Throws<ValidationException>(() => ReworkLifecycleRules.ValidateRemediations([1, 2], rows));
        Assert.Throws<ValidationException>(() => ReworkLifecycleRules.ValidateRemediations([], []));
        Assert.Throws<ValidationException>(() => ReworkLifecycleRules.ValidateRemediations([1], null));
    }

    [Fact]
    public void Evidence_limit_applies_across_all_defects()
    {
        var files = Enumerable.Repeat(new EvidenceUpload("synthetic.png", "image/png", "AAAA"), 3).ToArray();
        Assert.Throws<ValidationException>(() => ReworkLifecycleRules.ValidateRemediations([1, 2], [new(1, "Fixed", files), new(2, "Fixed", files)]));
    }
    [Fact]
    public void Fingerprint_binds_original_actor_branch_rework_action_etag_defect_notes_and_bytes()
    {
        var body=new ReworkActionRequest("Accept",[new(3,"Fixed")]);
        var hash=RoomTypeCreationService.ReworkFingerprint(1,2,3,"complete","tag",body);
        Assert.Equal(hash,RoomTypeCreationService.ReworkFingerprint(1,2,3,"complete","tag",body));
        Assert.NotEqual(hash,RoomTypeCreationService.ReworkFingerprint(2,2,3,"complete","tag",body));
        Assert.NotEqual(hash,RoomTypeCreationService.ReworkFingerprint(1,4,3,"complete","tag",body));
        Assert.NotEqual(hash,RoomTypeCreationService.ReworkFingerprint(1,2,4,"complete","tag",body));
        Assert.NotEqual(hash,RoomTypeCreationService.ReworkFingerprint(1,2,3,"start","tag",body));
        Assert.NotEqual(hash,RoomTypeCreationService.ReworkFingerprint(1,2,3,"complete","newtag",body));
        Assert.NotEqual(hash,RoomTypeCreationService.ReworkFingerprint(1,2,3,"complete","tag",body with{Remediations=[new(3,"Other remedy")]}));
        Assert.NotEqual(hash,RoomTypeCreationService.ReworkFingerprint(1,2,3,"complete","tag",body with{Remediations=[new(3,"Fixed",[new("proof.png","image/png","AA==")])]}));
    }
    [Theory]
    [InlineData("ready")][InlineData("actor")][InlineData("source")][InlineData("event")][InlineData("etag")][InlineData("accepted")]
    public void Replay_receipt_rejects_forged_source_state_event_and_contract(string changed)
    {
        var op=Guid.NewGuid();var receipt=new ReworkCommandResult(1,2,3,4,5,"complete","PENDING_REINSPECTION",op,"\"0000000000000001\"",DateTime.UtcNow,6,Guid.NewGuid(),"PENDING_INSPECTION","PENDING_DELIVERY");
        Assert.True(RoomTypeCreationService.ValidReworkReceipt(receipt,5,2,1,3,4,op,"complete"));
        var bad=changed switch{"ready"=>receipt with{Readiness="READY"},"actor"=>receipt with{AssigneeId=9},"source"=>receipt with{InspectionId=9},"event"=>receipt with{PublicationId=null},"etag"=>receipt with{SourceETag="*"},_=>receipt with{Contract="ACCEPTED"}};
        Assert.False(RoomTypeCreationService.ValidReworkReceipt(bad,5,2,1,3,4,op,"complete"));
    }
    [Theory]
    [InlineData("inspectionResult")][InlineData("branchId")][InlineData("assigneeId")]
    public void Client_cannot_supply_Inspection_result_or_server_owned_source_fields(string field)
        => Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<ReworkActionRequest>("{\"note\":\"Accept\",\""+field+"\":1}",new JsonSerializerOptions(JsonSerializerDefaults.Web)));
}
