using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.HotelStructure;

internal static class InspectionFast014Checks
{
    internal static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(output);var cases=new List<object>();
        void Check(string name,bool ok){if(!ok)throw new Exception(name);cases.Add(new{name,result="PASS"});Console.WriteLine("PASS "+name);}
        InspectionProjectionFacts Facts(bool blocked=false)=>new("PASSED","COMPLETED","PASS","COMPLETED","COMPLETED","INSPECTION",true,true,true,false,false,true,blocked);
        var ready=InspectionRoomProjectionRules.Evaluate(Facts());Check("Original FE04 PASSED rule yields READY only with trusted receipt",ready.OperationalStatus=="READY"&&ready.Readiness=="READY");
        var blocked=InspectionRoomProjectionRules.Evaluate(Facts(true));Check("Original FE04 block precedence preserves operational projection",blocked.OperationalStatus=="INSPECTION"&&blocked.EffectiveReadiness=="BLOCKED");
        Check("Optional FAIL flag alone does not override active mandatory PASS rule",InspectionRoomProjectionRules.Evaluate(Facts() with{HasFailedCriterion=true}).Readiness=="READY");
        foreach(var (name,facts) in new[]{("untrusted receipt",Facts() with{TrustedConclusionReceipt=false}),("wrong cycle",Facts() with{CurrentCycle=false}),
            ("missing mandatory PASS",Facts() with{RequiredCriteriaPassed=false}),("open Rework",Facts() with{HasOpenRework=true}),
            ("task not completed",Facts() with{CleaningState="PENDING_INSPECTION",CleaningStatus="IN_PROGRESS"})}){
            try{InspectionRoomProjectionRules.Evaluate(facts);throw new Exception("Unexpected projection acceptance");}catch(InvalidOperationException){Check("Original FE04 projection rejects "+name,true);}
        }
        // Actual file-write negative control: no WORM/ACL acceptance inferred from an open lease.
        var root=Path.Combine(Path.GetFullPath(output),"owned-evidence-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var bytes=new byte[]{1,2,3,4};
        await using(var prepared=await InspectionVersionedEvidenceCandidate.PrepareAsync(root,bytes,default)){
            await using(var lease=await InspectionEvidenceReadLease.OpenAsync(root,prepared.Version,default)){
                var denied=false;try{await File.WriteAllBytesAsync(Path.Combine(root,prepared.Version.Key),new byte[]{4,3,2,1});}catch(IOException){denied=true;}
                Check("Actual file sharing denies overwrite while private-byte lease is held",denied);
            }
            await File.WriteAllBytesAsync(Path.Combine(root,prepared.Version.Key),new byte[]{4,3,2,1});
            Check("Actual post-lease overwrite succeeds: deployment lacks durable immutable enforcement",true);
            try{await using var changed=await InspectionEvidenceReadLease.OpenAsync(root,prepared.Version,default);throw new Exception("Changed bytes accepted");}
            catch(HotelOps.Api.Services.Notifications.Fe05NotificationUnavailableException){Check("Version/content digest detects post-lease replacement but does not prevent it",true);}
        }
        Check("Known rollback removes candidate-owned file",Directory.GetFiles(root).Length==0);Directory.Delete(root);
        Check("Storage acceptance still false",!InspectionVersionedEvidenceCandidate.DurableImmutabilityAccepted);
        foreach(var role in Enum.GetValues<InspectionRecipientRelationship>())Check(role+" policy remains unaccepted/private bytes denied",InspectionRecipientPermissionContract.For(role) is {AcceptedConcreteBinding:null,PrivateEvidenceAllowed:false});
        await File.WriteAllTextAsync(Path.Combine(output,"fast014-unit-results.json"),System.Text.Json.JsonSerializer.Serialize(new{success=true,componentOnly=true,originalFe04RuleUnchanged=true,storageWormAccepted=false,cases},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"{cases.Count}/{cases.Count} PASS new FAST014 rule/storage checks; not authoritative SQL acceptance");
    }
}
