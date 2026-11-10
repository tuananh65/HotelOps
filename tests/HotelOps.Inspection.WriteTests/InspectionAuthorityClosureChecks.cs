using System.Text;
using System.Text.Json;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;

internal static class InspectionAuthorityClosureChecks
{
    internal static async Task RunAsync()
    {
        var results=new List<object>();
        void Check(string title,bool condition){if(!condition)throw new Exception(title);results.Add(new{title,result="PASS"});Console.WriteLine("PASS "+title);}
        void Deny(string title,Action run){try{run();}catch(Exception e)when(e is Fe05NotificationUnavailableException or NotificationConflictException or ArgumentException or JsonException or InvalidOperationException){Check(title,true);return;}throw new Exception("Expected denial: "+title);}
        async Task DenyFile(string title,Func<Task> run){try{await run();}catch(Exception e)when(e is IOException or Fe05NotificationUnavailableException or OperationCanceledException){Check(title,true);return;}throw new Exception("Expected file rejection: "+title);}
        var now=DateTime.UtcNow;
        foreach(var role in Enum.GetValues<InspectionRecipientRelationship>())
        {
            var contract=InspectionRecipientPermissionContract.For(role);
            PermissionGrantPath Grant(string scope,long? branch=2)=>new(contract.ExistingPermission,scope,branch,true,true,now.AddMinutes(-1),null,true,true);
            Check(role+" exact existing code",contract.ExistingPermission==(role==InspectionRecipientRelationship.ResponsibleSupervisor?"INSPECTION_PERFORM":"CLEANING_VIEW"));
            Check(role+" own grant candidate matches exact relation branch",contract.MatchesExistingGrant(Grant("OWN"),2,now));
            Check(role+" existing branch grant matches",contract.MatchesExistingGrant(Grant("BRANCH"),2,now));
            Check(role+" existing global grant still requires separate exact relationship",contract.MatchesExistingGrant(Grant("ALL",null),2,now));
            Check(role+" unrelated branch rejected",!contract.MatchesExistingGrant(Grant("OWN",3),2,now));
            Check(role+" revoked grant rejected",!contract.MatchesExistingGrant(Grant("BRANCH") with{AssignmentActive=false},2,now));
            Check(role+" inactive role rejected",!contract.MatchesExistingGrant(Grant("BRANCH") with{RoleActive=false},2,now));
            Check(role+" expired grant rejected",!contract.MatchesExistingGrant(Grant("BRANCH") with{ExpiresAt=now.AddSeconds(-1)},2,now));
            Check(role+" legacy department scope rejected",!contract.MatchesExistingGrant(Grant("DEPARTMENT"),2,now));
            Check(role+" different permission rejected",!contract.MatchesExistingGrant(Grant("BRANCH") with{PermissionCode="MASTER_DATA_MANAGE"},2,now));
            Check(role+" no source-read or byte acceptance",contract.AcceptedConcreteBinding==null&&!contract.PrivateEvidenceAllowed);
            Check(role+" summary fields exclude notes and bytes",!contract.SummaryFields.Any(f=>f.Contains("note",StringComparison.OrdinalIgnoreCase)||f.Contains("evidence",StringComparison.OrdinalIgnoreCase)));
            Deny(role+" completed/replay acceptance closed",()=>contract.RequireBusinessAcceptance());
        }
        var request=new Fe05OperationRequest(Guid.NewGuid(),2,3,new string('A',64),[Fe05NotificationKind.InspectionFailed]);
        var receipt=new Fe05OperationReceipt(Fe05OperationReceipt.ContractName,1,request.OperationId,request.RequestFingerprint,
            [new(Guid.NewGuid(),"inspection.failed",1,new string('B',64),[4,5])]);
        var domain=JsonSerializer.Serialize(new{contract="FE05_INSPECTION_CONCLUSION_V1",operationId=request.OperationId,actorUserId=1,branchId=2,inspectionId=3,requestFingerprint=request.RequestFingerprint});
        var stored=new Fe05StoredOperation(Fe05OperationIdentity.Fingerprint(request),request.RequestFingerprint,true,domain,JsonSerializer.Serialize(receipt));
        Check("Absent operation stays unconfirmed",InspectionOutcomeObservation.Read(null,1,request)==null);
        Check("Incomplete operation stays unconfirmed",InspectionOutcomeObservation.Read(stored with{Completed=false},1,request)==null);
        Check("Missing domain receipt stays unconfirmed",InspectionOutcomeObservation.Read(stored with{DomainResultJson=null},1,request)==null);
        Check("Completed typed receipt is structurally observable without commit claim",InspectionOutcomeObservation.Read(stored,1,request)?.OperationId==request.OperationId);
        Deny("Recovery rejects changed request fingerprint",()=>InspectionOutcomeObservation.Read(stored with{DomainFingerprint=new string('C',64)},1,request));
        Deny("Recovery rejects changed graph fingerprint",()=>InspectionOutcomeObservation.Read(stored with{Fingerprint=new string('C',64)},1,request));
        Deny("Recovery rejects wrong actor",()=>InspectionOutcomeObservation.Read(stored,7,request));
        Deny("Recovery rejects wrong operation",()=>InspectionOutcomeObservation.Read(stored,1,request with{OperationId=Guid.NewGuid()}));
        Deny("Recovery rejects domain retarget",()=>InspectionOutcomeObservation.Read(stored with{DomainResultJson=domain.Replace("\"branchId\":2","\"branchId\":9")},1,request));
        Deny("Recovery rejects wrong domain UC contract",()=>InspectionOutcomeObservation.Read(stored with{DomainResultJson=domain.Replace("FE05_INSPECTION_CONCLUSION_V1","FE05_REWORK_AUTHORITY_V1")},1,request));
        Check("Unknown commit never authorizes automatic mutation retry",!new InspectionRecoveredOutcome("OUTCOME_UNCONFIRMED_ROLLBACK_REQUIRED",null).AutomaticMutationRetryAllowed);
        Check("Observed receipt never self-attests production commit acceptance",!new InspectionRecoveredOutcome("COMPLETED_GRAPH_OBSERVED_IN_CALLER_TRANSACTION",domain).AuthoritativeCommitAccepted);
        var root=Path.GetFullPath(Path.Combine("TestResults","fast011","files",Guid.NewGuid().ToString("N")));Directory.CreateDirectory(root);
        var bytes=Encoding.ASCII.GetBytes("%PDF-1.4 Synthetic evidence only");
        var version=InspectionEvidenceVersion.New(bytes);
        Check("Version identity round trips exact key/digest/length",InspectionEvidenceVersion.Parse(version.Key,version.Length)==version);
        Check("New uploads have distinct version IDs even for same bytes",InspectionEvidenceVersion.New(bytes).Key!=version.Key);
        Deny("Path traversal version rejected",()=>InspectionEvidenceVersion.Parse("../escape",1));
        Deny("Legacy mutable key rejected",()=>InspectionEvidenceVersion.Parse("fixture.pdf",1));
        Deny("Unbounded evidence rejected",()=>InspectionEvidenceVersion.New(new byte[10*1024*1024+1]));
        await using(var prepared=await InspectionVersionedEvidenceCandidate.PrepareAsync(root,bytes,default))
        {
            var file=Path.Combine(root,prepared.Version.Key);
            await using(var lease=await InspectionEvidenceReadLease.OpenAsync(root,prepared.Version,default))
            {
                await lease.VerifyAsync(default);Check("Prepared file matches version digest",true);
                if(OperatingSystem.IsWindows())
                {
                    await DenyFile("Concurrent byte replacement blocked while Windows read lease held",()=>File.WriteAllBytesAsync(file,bytes.Reverse().ToArray()));
                    await DenyFile("Concurrent deletion blocked while Windows read lease held",()=>{File.Delete(file);return Task.CompletedTask;});
                }
            }
            await File.WriteAllBytesAsync(file,bytes.Reverse().ToArray());
            await DenyFile("Replacement after lease release detected by same persisted version",async()=>{await using var bad=await InspectionEvidenceReadLease.OpenAsync(root,prepared.Version,default);});
        }
        Check("Rollback candidate cleans invocation-owned file",Directory.GetFiles(root).Length==0);
        string retained;
        await using(var prepared=await InspectionVersionedEvidenceCandidate.PrepareAsync(root,bytes,default))
        {retained=Path.Combine(root,prepared.Version.Key);prepared.RetainAfterCommitOrUnknownOutcome();}
        Check("Unknown commit preserves file rather than risking committed attachment deletion",File.Exists(retained));
        using(var cancelled=new CancellationTokenSource())
        {
            cancelled.Cancel();await DenyFile("Cancelled preparation cleans incomplete candidate",async()=>{await using var file=await InspectionVersionedEvidenceCandidate.PrepareAsync(root,bytes,cancelled.Token);});
        }
        Check("Cancellation does not delete unrelated retained version",Directory.GetFiles(root).Length==1&&File.Exists(retained));
        var concurrent=await Task.WhenAll(InspectionVersionedEvidenceCandidate.PrepareAsync(root,bytes,default),InspectionVersionedEvidenceCandidate.PrepareAsync(root,bytes,default));
        await using(var a=concurrent[0])
        await using(var b=concurrent[1])
            Check("Parallel versions never overwrite same content key",a.Version.Key!=b.Version.Key&&File.Exists(Path.Combine(root,a.Version.Key))&&File.Exists(Path.Combine(root,b.Version.Key)));
        Check("Local candidate never claims durable WORM acceptance",!InspectionVersionedEvidenceCandidate.DurableImmutabilityAccepted);
        Directory.CreateDirectory("TestResults/fast011");
        await File.WriteAllTextAsync("TestResults/fast011/closure-unit-files.json",JsonSerializer.Serialize(new{success=true,sql=false,productionAcceptance=false,filesRoot=root,cases=results},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(results.Count+"/"+results.Count+" PASS; file leases and fixture receipt observations are not durable production authority");
    }
}
