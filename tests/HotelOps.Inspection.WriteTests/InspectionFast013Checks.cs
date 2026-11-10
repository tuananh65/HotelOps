using System.Text.Json;
using System.Text.Json.Nodes;
using HotelOps.Api.Controllers;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Notifications;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

internal static class InspectionFast013Checks
{
    internal static async Task RunAsync()
    {
        int n=0;
        void Check(string name,bool ok){if(!ok)throw new Exception(name);n++;Console.WriteLine("PASS "+name);}
        void Deny(string name,Action run){try{run();}catch(Exception e)when(e is NotificationConflictException or InvalidOperationException or KeyNotFoundException or JsonException){Check(name,true);return;}throw new Exception("Unexpected acceptance: "+name);}
        const string tag="\"0000000000000001\"";var operation=InspectionIssuerOperation.Approve(Guid.NewGuid(),1,2,3,tag);
        var json=JsonSerializer.Serialize(new{contract="FE05_INSPECTION_CONCLUSION_V1",issuer="FE05.UC044.UC045",issuerVersion=1,
            actorUserId=1,operationId=operation.Id,requestFingerprint=Convert.ToHexString(operation.Fingerprint),branchId=2,
            inspectionId=3,roomId=4,cleaningTaskId=5,versionId=8,inspectionETag=tag,conclusion="PASSED",writerTransactionId=6});
        void Validate(string j,long? writer=6)=>InspectionPassedReceipt.Require(j,operation,1,2,3,4,5,8,tag,writer);
        Validate(json);Check("PASSED receipt exact FE04 structural contract",true);
        foreach(var (field,value) in new (string,JsonNode?)[]{("contract",JsonValue.Create("FAKE")),("issuer",JsonValue.Create("OTHER")),
            ("issuerVersion",JsonValue.Create(2)),("actorUserId",JsonValue.Create(9)),("operationId",JsonValue.Create(Guid.NewGuid())),
            ("requestFingerprint",JsonValue.Create(new string('0',64))),("branchId",JsonValue.Create(9)),("inspectionId",JsonValue.Create(9)),
            ("roomId",JsonValue.Create(9)),("versionId",JsonValue.Create(9)),("cleaningTaskId",JsonValue.Create(9)),("inspectionETag",JsonValue.Create("\"0000000000000002\"")),
            ("conclusion",JsonValue.Create("FAILED")),("writerTransactionId",JsonValue.Create(0)),("writerTransactionId",JsonValue.Create(7))})
        {var node=JsonNode.Parse(json)!;node[field]=value;Deny("Receipt rejects changed "+field+"="+value,()=>Validate(node.ToJsonString()));}
        Validate(json,null);Check("Completed replay allows original writer transaction, not current transaction substitution",true);
        var noTag=JsonNode.Parse(json)!;noTag.AsObject().Remove("inspectionETag");Deny("Missing receipt field denied",()=>Validate(noTag.ToJsonString()));
        Deny("Wrong UC operation cannot claim PASSED receipt",()=>InspectionPassedReceipt.Require(json,
            InspectionIssuerOperation.Reject(operation.Id,1,2,3,tag,new("reason",true,tag)),1,2,3,4,5,8,tag));
        var fixedPlan=ReinspectionLifecycleCandidate.From(new([],true));
        Check("All Fixed follows SRS completed plan",fixedPlan is {InspectionState:"PASSED",ReworkState:"COMPLETED",CleaningState:"COMPLETED",SchemaAndLifecycleAccepted:false});
        Check("Not Fixed follows SRS failed plan only",ReinspectionLifecycleCandidate.From(new([],false)) is {InspectionState:"FAILED",ReworkState:"FAILED",CleaningState:"REWORK_REQUIRED",SchemaAndLifecycleAccepted:false});
        Deny("Incomplete decisions cannot prepare lifecycle",()=>ReinspectionLifecycleCandidate.From(new(["EVERY_DEFECT_DECISION_REQUIRED"],null)));
        Check("Candidate stale maps 412 class",ReinspectionReviewErrors.Status("REINSPECTION_REWORK_STALE")==RoomTypeBatchStatus.Stale);
        Check("Candidate SOD maps forbidden",ReinspectionReviewErrors.Status("INSPECTOR_CLEANER_CONFLICT")==RoomTypeBatchStatus.Forbidden);
        Check("Candidate missing schema unavailable",ReinspectionReviewErrors.Status("REINSPECTION_REMEDIATION_SCHEMA_REQUIRED")==RoomTypeBatchStatus.SchemaRequired);
        Check("Candidate invalid identity maps bad request",ReinspectionReviewErrors.Status("REINSPECTION_IDENTITY_INVALID")==RoomTypeBatchStatus.InvalidReference);
        Check("Candidate invalid phase maps conflict",ReinspectionReviewErrors.Status("REINSPECTION_REWORK_SOURCE_REQUIRED")==RoomTypeBatchStatus.Inactive);
        Check("Unexpected error is not silently remapped",ReinspectionReviewErrors.Status("UNKNOWN_BUG")==null);
        var manager=new ApplicationPartManager();manager.ApplicationParts.Add(new AssemblyPart(typeof(ReinspectionReviewCandidateController).Assembly));manager.FeatureProviders.Add(new ControllerFeatureProvider());
        var controllers=new ControllerFeature();manager.PopulateFeature(controllers);
        Check("UC047 candidate has no MVC production dispatch",!controllers.Controllers.Any(c=>c.AsType()==typeof(ReinspectionReviewCandidateController)));
        Check("Existing Reject controller remains discoverable",controllers.Controllers.Any(c=>c.AsType()==typeof(InspectionConclusionController)));
        try{await new ClosedInspectionApproveBinding().RequireAcceptedAsync(null!,operation,default);throw new Exception("Approve binding activated");}
        catch(Fe05NotificationUnavailableException e){Check("Missing Passed staging closes before writer",e.Message=="UC044_PASSED_STAGING_AND_DURABLE_BINDING_UNACCEPTED");}
        Check("14 owner cases unique/stable",InspectionOwnerFourteenDriver.Cases.Count==14&&InspectionOwnerFourteenDriver.Cases.Select(c=>c.Id).Distinct().Count()==14);
        var committed=InspectionOwnerFourteenDriver.Cases[0];
        Deny("Owner driver rejects synthetic authority",()=>InspectionOwnerFourteenDriver.RequireEvidence(committed,new("O01","receipt",true,true,true,false,true)));
        Deny("Owner driver rejects missing committed source",()=>InspectionOwnerFourteenDriver.RequireEvidence(committed,new("O01","receipt",true,false,false,false,true)));
        Deny("Owner driver rejects interception/non-SQL",()=>InspectionOwnerFourteenDriver.RequireEvidence(committed,new("O01","receipt",false,false,true,false,true)));
        Deny("Owner driver rejects wrong case evidence",()=>InspectionOwnerFourteenDriver.RequireEvidence(committed,new("O02","receipt",true,false,true,false,true)));
        Deny("Owner driver requires positive different-transaction consumer",()=>InspectionOwnerFourteenDriver.RequireEvidence(InspectionOwnerFourteenDriver.Cases[13],new("O14","receipt",true,false,true,false,true)));
        foreach(var role in Enum.GetValues<InspectionRecipientRelationship>())Check(role+" acceptance/bytes unchanged",InspectionRecipientPermissionContract.For(role) is {AcceptedConcreteBinding:null,PrivateEvidenceAllowed:false});
        Console.WriteLine($"{n}/{n} PASS FAST013 focused unit/discovery checks; no authority acceptance");
    }
}
