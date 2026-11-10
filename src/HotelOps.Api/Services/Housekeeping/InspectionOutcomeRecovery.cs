using System.Text.Json;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

// Observation only. Absence/incomplete does NOT prove rollback and MUST NOT trigger mutation retry.
internal sealed record InspectionRecoveredOutcome(string Status,string? DomainJson)
{
    internal bool AutomaticMutationRetryAllowed=>false;
    internal bool AuthoritativeCommitAccepted=>false;
}
internal static class InspectionOutcomeObservation
{
    internal static Fe05OperationReceipt? Read(Fe05StoredOperation? stored,long actor,Fe05OperationRequest request)
    {
        var fingerprint=Fe05OperationIdentity.Fingerprint(request);
        if(stored==null)return null;
        if(stored.Fingerprint!=fingerprint||!StringComparer.OrdinalIgnoreCase.Equals(stored.DomainFingerprint,request.RequestFingerprint))
            throw new NotificationConflictException();
        if(!stored.Completed||stored.DomainResultJson==null||stored.N01ReceiptJson==null)return null;
        var receipt=JsonSerializer.Deserialize<Fe05OperationReceipt>(stored.N01ReceiptJson)??throw new NotificationConflictException();
        Fe05OperationIdentity.RequireReplay(receipt,actor,request);
        using var document=JsonDocument.Parse(stored.DomainResultJson);var root=document.RootElement;
        var kind=request.RequiredEvents.Single();
        if(root.GetProperty("operationId").GetGuid()!=request.OperationId||root.GetProperty("actorUserId").GetInt64()!=actor
            ||root.GetProperty("branchId").GetInt64()!=request.BranchId||root.GetProperty("inspectionId").GetInt64()!=request.InspectionId
            ||!StringComparer.OrdinalIgnoreCase.Equals(root.GetProperty("requestFingerprint").GetString(),request.RequestFingerprint)
            ||root.GetProperty("contract").GetString()!=(kind==Fe05NotificationKind.InspectionFailed?"FE05_INSPECTION_CONCLUSION_V1":"FE05_REWORK_AUTHORITY_V1"))
            throw new NotificationConflictException();
        return receipt;
    }
}

public sealed partial class RoomTypeCreationService
{
    // Fresh caller scope/retained roster/transaction required after an uncertain COMMIT.
    // Reuses the existing N01 graph and owner assertions. No Reserve/Complete/domain writes,
    // new ledger, SQL retry loop, independent connection, COMMIT or production activation.
    internal async Task<InspectionRecoveredOutcome> ObserveInspectionOutcomeInCallerTransactionAsync(SessionReference ticket,long sid,
        Fe05OperationRequest request,IFe05OperationStore store,IFe05SourceCoordination coordination,NotificationCommitGuard guard,CancellationToken ct,string? privateEvidenceRoot=null)
    {
        var tx=Fe05CallerTransaction.Require(db);guard.RequireAttached(db,tx);
        try
        {
            if(inspectionOwner==null||inspectionUsers==null)throw new Fe05NotificationUnavailableException("FE05_OWNER_RECOVERY_REQUIRED");
            // Caller reconstructs the ORIGINAL canonical request, including old expected ETag,
            // operation key, assignee, selected defects and due time. Never replace these with
            // a collection response/current version/new operation id to force a fresh write.
            Fe05OperationIdentity.Fingerprint(request);
            request=request with{RequiredEvents=Array.AsReadOnly(request.RequiredEvents.Order().ToArray()),RequestFingerprint=request.RequestFingerprint.ToUpperInvariant()};
            await LockInspectionCompositionSourceAsync(ticket,sid,request.BranchId,request.InspectionId,ct);
            await inspectionUsers.ConfirmAfterSourcesAsync(false,ct);
            inspectionOwner.Bind(db,ticket,sid,request);
            if(request.RequiredEvents.Single()==Fe05NotificationKind.CleaningReworkRequested)
            {
                if(privateEvidenceRoot==null)throw new Fe05NotificationUnavailableException("FE05_RECOVERY_EVIDENCE_ROOT_AND_IMMUTABILITY_REQUIRED");
                inspectionOwner.VerifySnapshot=async(rework,sourceDigest,evidenceDigest,token)=>
                {
                    var snapshot=await BuildReworkAuthoritySnapshotAsync(db.Database.GetDbConnection(),db.Database.CurrentTransaction!.GetDbTransaction(),rework,privateEvidenceRoot,token);
                    if(snapshot==null||Convert.ToHexString(snapshot.SourceDigest)!=sourceDigest||Convert.ToHexString(snapshot.EvidenceDigest)!=evidenceDigest)
                        throw new NotificationConflictException();
                };
            }
            var actor=new HotelOps.Api.Security.Authorization.CurrentUserInfo(ticket.UserId,sid,false);
            await coordination.RequireReservationAsync(db,request,actor,ct);
            await store.RequireAvailableAsync(ct); // Existing accepted-lane gate remains mandatory.
            var saved=await store.LockAsync(actor.UserId,request.OperationId,ct);
            var receipt=InspectionOutcomeObservation.Read(saved,actor.UserId,request);
            if(receipt==null)
            {
                guard.Fail(db,tx);
                return new("OUTCOME_UNCONFIRMED_ROLLBACK_REQUIRED",null);
            }
            async Task Verify(CancellationToken token)
            {
                var observed=await store.LockAsync(actor.UserId,request.OperationId,token);
                var replay=InspectionOutcomeObservation.Read(observed,actor.UserId,request)??throw new NotificationConflictException();
                if(observed!.DomainResultJson!=saved!.DomainResultJson||observed.N01ReceiptJson!=saved.N01ReceiptJson)throw new NotificationConflictException();
                await store.VerifyAsync(actor.UserId,request.OperationId,Fe05OperationIdentity.Fingerprint(request),replay,token);
                await coordination.RequireCompletedOperationAsync(db,request,actor,replay,token);
            }
            guard.BindGraph(db,tx,"FE05:Observe:"+actor.UserId+":"+request.OperationId,Verify);
            await guard.VerifyBeforeCommitAsync(db,ct);
            return new("COMPLETED_GRAPH_OBSERVED_IN_CALLER_TRANSACTION",saved!.DomainResultJson);
        }
        catch{guard.Fail(db,tx);throw;}
    }
}
