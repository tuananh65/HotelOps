using System.Text.Json;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;

// Executable FE05 owner harness. The independent dispatcher/gates/evidence contract are
// implemented; accepted fixture actions are deliberately NOT fabricated or substituted.
internal static class InspectionOwnerFourteenDriver
{
    internal sealed record Case(string Id,string Name,string EntryPoint,bool RequiresCommittedSource=false,bool RequiresDifferentTransactionConsumer=false);
    internal static IReadOnlyList<Case> Cases {get;}=Array.AsReadOnly(new[] {
        new Case("O01","Reject committed source","ComposeRejectInspectionAsync",true),
        new Case("O02","Rework committed proof","ComposeInspectionReworkAsync",true),
        new Case("O03","Completed replay preserves domain/audit/graph","ComposeRejectInspectionAsync / ComposeInspectionReworkAsync",true),
        new Case("O04","Conflicting operation identity","Fe05OperationStaging.ReserveAsync"),
        new Case("O05","Revoked real session/grant","AuthorizeRejectLoadAsync"),
        new Case("O06","Wrong branch","LockInspectionCompositionSourceAsync"),
        new Case("O07","Wrong persisted current cycle","ExecuteTrustedRejectDomainInCallerTransactionAsync"),
        new Case("O08","Stale revision and D4 invalidation","PrepareRejectInCallerTransactionAsync"),
        new Case("O09","Changed immutable evidence version/bytes","PrepareReworkAuthorityInCallerTransactionAsync"),
        new Case("O10","Wrong selected defect set","ExecuteTrustedReworkDomainInCallerTransactionAsync"),
        new Case("O11","Retained Users/revoked recipient/reassignment","InspectionUsersPrelock.ConfirmAfterSourcesAsync"),
        new Case("O12","Mandatory participant failure rolls back all","NotificationCommitGuard.VerifyBeforeCommitAsync"),
        new Case("O13","Concurrent Reject/Rework serialization","ComposeRejectInspectionAsync / ComposeInspectionReworkAsync"),
        new Case("O14","Unknown outcome and positive FE04 consumer","ObserveInspectionOutcomeInCallerTransactionAsync",true,true)
    });
    internal sealed record Evidence(string CaseId,string ReceiptReference,bool RealSql,bool SyntheticAuthority,
        bool CommittedSource,bool DifferentTransactionConsumer,bool ExpectedAssertionsPassed);
    // External fixture methods must invoke the exact owner entry points with real scoped
    // SQL session/prelock/staging/proof/projection participants on the approved lane.
    // No this-is-accepted boolean or environment/config flag exists in this driver.
    internal static async Task<bool> RunAsync(string output,IFe05TrialExecutionAuthorization lane,
        IReadOnlyDictionary<string,Func<CancellationToken,Task<Evidence>>>? boundFixtures,CancellationToken ct)
    {
        Directory.CreateDirectory(output);
        var results=new List<object>();var completed=false;
        try {
            await lane.RequireAsync(ct); // BEFORE fixture construction, SQL connection or mutation.
            foreach(var role in Enum.GetValues<InspectionRecipientRelationship>())
                InspectionRecipientPermissionContract.For(role).RequireBusinessAcceptance();
            if(!StorageAccepted())
                throw new Fe05NotificationUnavailableException("FE05_IMMUTABLE_STORAGE_UNACCEPTED");
            if(boundFixtures==null||boundFixtures.Count!=Cases.Count||Cases.Any(c=>!boundFixtures.ContainsKey(c.Id)))
                throw new Fe05NotificationUnavailableException("FE05_OWNER_ACCEPTED_FIXTURE_BINDING_REQUIRED");
            foreach(var c in Cases) {
                var evidence=await boundFixtures[c.Id](ct);
                RequireEvidence(c,evidence);
                results.Add(new{id=c.Id,c.Name,result="PASS",evidence.ReceiptReference});
            }
            completed=true;
        }
        catch(Fe05NotificationUnavailableException e) {
            foreach(var c in Cases.Skip(results.Count))results.Add(new{id=c.Id,c.Name,result="NOT_RUN",blocker=e.Message});
        }
        catch(Exception e) {
            var next=Cases[results.Count];results.Add(new{id=next.Id,next.Name,result="FAIL",error=e.GetType().Name});
            foreach(var c in Cases.Skip(results.Count))results.Add(new{id=c.Id,c.Name,result="NOT_RUN",blocker="EARLIER_CASE_FAILED"});
            throw;
        }
        finally {
            await File.WriteAllTextAsync(Path.Combine(output,"owner-14-results.json"),JsonSerializer.Serialize(new{
                driver="FE05_OWNER_14_INDEPENDENT_PORTION",fixtureBindingsAvailable=boundFixtures!=null,
                accepted=false,results},new JsonSerializerOptions{WriteIndented=true}),ct);
        }
        return completed;
    }
    private static bool StorageAccepted()=>InspectionVersionedEvidenceCandidate.DurableImmutabilityAccepted;
    internal static void RequireEvidence(Case c,Evidence e)
    {
        if(e.CaseId!=c.Id||!e.RealSql||e.SyntheticAuthority||!e.ExpectedAssertionsPassed||string.IsNullOrWhiteSpace(e.ReceiptReference)
            ||c.RequiresCommittedSource&&!e.CommittedSource||c.RequiresDifferentTransactionConsumer&&!e.DifferentTransactionConsumer)
            throw new InvalidOperationException("OWNER_CASE_REAL_EVIDENCE_REQUIRED:"+c.Id);
    }
}
