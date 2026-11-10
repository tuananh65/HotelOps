using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;

internal static class ApproveReinspectionChecks
{
    internal static async Task RunAsync()
    {
        int n=0;
        void Check(string name,bool ok){if(!ok)throw new Exception(name);n++;Console.WriteLine("PASS "+name);}
        byte[] revision=[0,0,0,0,0,0,0,1];const string tag="\"0000000000000001\"";
        ReinspectionDecisionResult Decide(long[] source,ReinspectionDefectDecision[] decisions,
            string? wt=tag,string? it=tag)=>ReinspectionDecisionValidator.Validate(source,decisions,revision,wt,revision,it);
        Check("All exact persisted defects fixed",Decide([1,2],[new(1,true),new(2,true)]) is {IsComplete:true,AllFixed:true,AuthoritativeCommitAccepted:false});
        Check("Not Fixed remains distinct from incomplete",Decide([1,2],[new(1,false),new(2,true)]) is {IsComplete:true,AllFixed:false});
        Check("All not fixed is complete failure evaluation",Decide([1,2],[new(1,false),new(2,false)]) is {IsComplete:true,AllFixed:false});
        Check("Decision order independent",Decide([1,2],[new(2,true),new(1,true)]).AllFixed==true);
        Check("Missing defect blocks conclusion",!Decide([1,2],[new(1,true)]).IsComplete);
        Check("Null Fixed blocks conclusion",!Decide([1],[new(1,null)]).IsComplete);
        Check("Foreign defect rejected",!Decide([1],[new(2,true)]).IsComplete);
        Check("Duplicate decisions rejected",!Decide([1],[new(1,true),new(1,false)]).IsComplete);
        Check("Empty persisted source rejected",!Decide([],[]).IsComplete);
        Check("Duplicate persisted source rejected",!Decide([1,1],[new(1,true)]).IsComplete);
        Check("Nonpositive persisted source rejected",!Decide([0],[new(0,true)]).IsComplete);
        Check("Stale Rework revision rejected",!Decide([1],[new(1,true)],"\"0000000000000002\"").IsComplete);
        Check("Stale Inspection revision rejected",!Decide([1],[new(1,true)],tag,"\"0000000000000002\"").IsComplete);
        Check("Weak revision rejected",!Decide([1],[new(1,true)],"W/"+tag).IsComplete);
        Check("Missing revision rejected",!Decide([1],[new(1,true)],null).IsComplete);
        Check("Invalid source revision rejected",!ReinspectionDecisionValidator.Validate([1],[new(1,true)],[],tag,revision,tag).IsComplete);
        Check("Incomplete decision never reports PASS",Decide([1,2],[new(1,true)]).AllFixed==null);
        var op=Guid.NewGuid();
        var approve=InspectionIssuerOperation.Approve(op,1,2,3,tag);
        Check("UC044 canonical identity reproducible",approve.Canonical==InspectionIssuerOperation.Approve(op,1,2,3,tag).Canonical);
        Check("UC044 fingerprint changes with branch",!approve.Fingerprint.SequenceEqual(InspectionIssuerOperation.Approve(op,1,4,3,tag).Fingerprint));
        Check("UC044 cannot collide with UC045 identity",!approve.Fingerprint.SequenceEqual(InspectionIssuerOperation.Reject(op,1,2,3,tag,new("reason",true,tag)).Fingerprint));
        var candidate=new ReinspectionSourceCandidate(2,3,1,4,5,tag,tag,[new(1,"Remediated",6,DateTime.UtcNow)]);
        Check("Same-task candidate cannot assert persisted binding",!candidate.AuthoritativeBindingAccepted);
        Check("Pinned version difference is visible rather than silently replaced",candidate.AssignedChecklistVersionId==4&&candidate.OriginalChecklistVersionId==5);
        Check("UC047 explicit missing binding gate",ReinspectionSourceCandidate.MissingContracts.Contains("EXACT_REINSPECTION_REWORK_BINDING_REQUIRED"));
        Check("UC047 explicit missing per-defect storage gate",ReinspectionSourceCandidate.MissingContracts.Contains("PER_DEFECT_REINSPECTION_RESULT_STORAGE_REQUIRED"));
        Check("UC044 core result never claims authoritative commit",!new InspectionApproveDomainResult(1,tag).AuthoritativeCommitAccepted);
        Check("Approve mandatory participants still closed",RoomTypeCreationService.ApproveWriteDependencies.Length>0);
        Check("Reject mandatory participants still closed",RoomTypeCreationService.RejectWriteDependencies.Length>0);
        Check("Rework immutable storage gate still closed",RoomTypeCreationService.ReworkWriteDependencies.Contains("PRIVATE_EVIDENCE_IMMUTABILITY_UNACCEPTED"));
        foreach(var role in Enum.GetValues<InspectionRecipientRelationship>())
            Check(role+" permission acceptance stays null",InspectionRecipientPermissionContract.For(role).AcceptedConcreteBinding==null);
        try{await new InspectionClosedTrialExecution().RequireAsync(default);throw new Exception("N01 lane activated");}
        catch(Fe05NotificationUnavailableException e){Check("Owner driver cannot activate N01 lane",e.Message=="N01_SQL_LANE_NOT_ACCEPTED");}
        Check("Versioned digest still not durable storage acceptance",!InspectionVersionedEvidenceCandidate.DurableImmutabilityAccepted);
        Console.WriteLine($"{n}/{n} PASS FAST012 focused domain/gate checks; no SQL acceptance");
    }
}
