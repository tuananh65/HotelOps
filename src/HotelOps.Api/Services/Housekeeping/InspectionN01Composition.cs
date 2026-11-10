using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

// Production-source composition; activation remains closed by service dependency gates.
// Requires approved Closure05 foundation, concrete owner coordination/recipient
// authority, SQL-accepted store, pinned issuer registration and caller user locks.
public sealed partial class RoomTypeCreationService
{
    internal async Task<string> ComposeRejectInspectionAsync(SessionReference ticket,long sid,long branch,long inspection,
        string expected,Guid key,RejectInspectionRequest input,Fe05OperationStaging staging,
        NotificationCommitGuard guard,CancellationToken ct)
    {
        var domainInput=new RejectInspectionInput(input.Reason,input.Confirmed,expected);
        var identity=InspectionIssuerOperation.Reject(key,ticket.UserId,branch,inspection,expected,domainInput);
        var request=new Fe05OperationRequest(key,branch,inspection,Convert.ToHexString(identity.Fingerprint),[Fe05NotificationKind.InspectionFailed]);
        // N01 validates the retained pre-mutation protocol and its completed domain
        // receipt before returning Replay. Do not rerun source preflight on replay.
        await LockInspectionCompositionSourceAsync(ticket,sid,branch,inspection,ct);
        if(inspectionOwner!=null)
        {
            if(inspectionUsers==null)throw new InvalidOperationException("Complete retained Users roster required");
            await inspectionUsers.ConfirmAfterSourcesAsync(false,ct);
            inspectionOwner.Bind(db,ticket,sid,request);
            inspectionOwner.VerifySnapshot=async(rework,sourceDigest,evidenceDigest,token)=>
            {
                var snapshot=await BuildReworkAuthoritySnapshotAsync(db.Database.GetDbConnection(),db.Database.CurrentTransaction!.GetDbTransaction(),rework,null,token);
                if(snapshot==null||Convert.ToHexString(snapshot.SourceDigest)!=sourceDigest||Convert.ToHexString(snapshot.EvidenceDigest)!=evidenceDigest)
                    throw new NotificationConflictException();
            };
        }
        var lease=await Fe05CallerIntegration.ReserveOperationAsync(db,staging,request,ct);
        if(lease.Replay!=null)return await ReadInspectionReplay(ticket,sid,request,"FE05_INSPECTION_CONCLUSION_V1",guard,ct);
        var changed=await ExecuteTrustedRejectDomainInCallerTransactionAsync(ticket,sid,new(branch,inspection,domainInput),identity,ct);
        var revision=WorkflowTag(changed.Revision??throw new InvalidOperationException("FE05_POST_REVISION_REQUIRED"));
        inspectionOwner?.Written(revision);
        await Fe05CallerIntegration.StageOperationAsync(db,staging,lease,[new(Fe05NotificationKind.InspectionFailed,key,branch,inspection,revision)],ct);
        var completed=await CompleteTrustedRejectInCallerTransactionAsync(ticket,sid,branch,inspection,revision,identity,ct);
        var physical=db.Database.CurrentTransaction!.GetDbTransaction();
        await ProjectFailedConclusionInCallerTransactionAsync(ticket,sid,db.Database.GetDbConnection(),physical,branch,inspection,revision,key,ct);
        await guard.VerifyBeforeCommitAsync(db,ct);
        return completed.ResultJson??throw new InvalidOperationException("FE05_DOMAIN_RECEIPT_REQUIRED");
    }

    internal async Task<string> ComposeInspectionReworkAsync(SessionReference ticket,long sid,long branch,long inspection,
        string expected,Guid key,RequestReworkRequest input,string? evidenceRoot,Fe05OperationStaging staging,
        NotificationCommitGuard guard,CancellationToken ct)
    {
        var identity=InspectionIssuerOperation.Rework(key,ticket.UserId,branch,inspection,expected,input);
        var request=new Fe05OperationRequest(key,branch,inspection,Convert.ToHexString(identity.Fingerprint),[Fe05NotificationKind.CleaningReworkRequested]);
        await LockInspectionCompositionSourceAsync(ticket,sid,branch,inspection,ct);
        if(inspectionOwner!=null)
        {
            if(inspectionUsers==null)throw new InvalidOperationException("Complete retained Users roster required");
            await inspectionUsers.ConfirmAfterSourcesAsync(false,ct);
            inspectionOwner.Bind(db,ticket,sid,request);
            inspectionOwner.VerifySnapshot=async(rework,sourceDigest,evidenceDigest,token)=>
            {
                var snapshot=await BuildReworkAuthoritySnapshotAsync(db.Database.GetDbConnection(),db.Database.CurrentTransaction!.GetDbTransaction(),rework,evidenceRoot,token);
                if(snapshot==null||Convert.ToHexString(snapshot.SourceDigest)!=sourceDigest||Convert.ToHexString(snapshot.EvidenceDigest)!=evidenceDigest)
                    throw new NotificationConflictException();
            };
        }
        var lease=await Fe05CallerIntegration.ReserveOperationAsync(db,staging,request,ct);
        if(lease.Replay!=null)return await ReadInspectionReplay(ticket,sid,request,"FE05_REWORK_AUTHORITY_V1",guard,ct);
        var changed=await ExecuteTrustedReworkDomainInCallerTransactionAsync(ticket,sid,branch,inspection,expected,input,identity,ct);
        var rework=changed.ReworkId??throw new InvalidOperationException("FE05_PERSISTED_REWORK_REQUIRED");
        inspectionOwner?.Written(expected,rework);
        var physical=db.Database.CurrentTransaction!.GetDbTransaction();
        var snapshot=await PrepareReworkAuthorityInCallerTransactionAsync(ticket,sid,db.Database.GetDbConnection(),physical,
            branch,inspection,rework,expected,key,input.DefectIds,evidenceRoot,ct);
        await Fe05CallerIntegration.StageOperationAsync(db,staging,lease,[new(Fe05NotificationKind.CleaningReworkRequested,key,branch,inspection,expected)],ct);
        var completed=await CompleteTrustedReworkInCallerTransactionAsync(ticket,sid,branch,inspection,rework,expected,
            identity,input.DefectIds,evidenceRoot,ct);
        await ProjectFailedConclusionInCallerTransactionAsync(ticket,sid,db.Database.GetDbConnection(),physical,
            branch,inspection,expected,key,ct);
        // This detects changes during preparation, but does NOT guarantee immutable bytes.
        // Production stays closed until storage protects the entire authority window.
        // Re-read persisted evidence AND private bytes just before the caller commit gate.
        var observed=await PrepareReworkAuthorityInCallerTransactionAsync(ticket,sid,db.Database.GetDbConnection(),physical,
            branch,inspection,rework,expected,key,input.DefectIds,evidenceRoot,ct);
        if(snapshot.Receipt.SourceDigest!=observed.Receipt.SourceDigest || snapshot.Receipt.EvidenceDigest!=observed.Receipt.EvidenceDigest)
            throw new InvalidOperationException("FE05_EVIDENCE_CHANGED_BEFORE_COMMIT");
        await guard.VerifyBeforeCommitAsync(db,ct);
        return completed.ResultJson??throw new InvalidOperationException("FE05_DOMAIN_RECEIPT_REQUIRED");
    }

    private async Task<(RoomTypeBatchStatus? Denied,InspectionRecipientRoster? Roster)> DiscoverAuthorizedRosterAsync(
        SessionReference ticket,long sid,long branch,long inspection,long? assignee,Fe05NotificationKind kind,CancellationToken ct)
    {
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sid||actor.IsRestricted||ticket.IsRestricted)
            return(RoomTypeBatchStatus.SessionInvalid,null);
        // Authentication supplied the current principal. Discovery is advisory only;
        // existing BatchAsync rechecks the SQL session inside the caller transaction after Users locks.
        var access=await permissions.EvaluateBranchAsync("INSPECTION_PERFORM",branch,ct);
        if(!access.IsGranted)return(access.Decision==HotelOps.Api.Security.Authorization.PermissionDecision.Unavailable?RoomTypeBatchStatus.Unavailable:RoomTypeBatchStatus.Forbidden,null);
        (inspectionRoster??throw new InvalidOperationException("Complete roster required")).BindCleanerCaller(ticket,sid);
        return(null,await inspectionRoster.DiscoverAsync(ticket.UserId,branch,inspection,assignee,kind,ct));
    }

    private async Task LockInspectionCompositionSourceAsync(SessionReference ticket,long sid,long branch,long inspection,CancellationToken ct)
    {
        // Reuse the persisted reader's exact authorization/branch/catalog/root/task/cycle locks.
        // Do not run Pending Conclusion or expected-revision eligibility before completed replay.
        var reader=new RejectReadSession(this,db,ticket,sid);
        reader.RequireCallerTransaction();
        if(await reader.AuthorizeAsync(branch,ct) is {} denied)
            throw new InvalidOperationException("FE05_RESERVATION_"+denied);
        if(await reader.ReadLockedAsync(branch,inspection,ct) is null)
            throw new InvalidOperationException("FE05_RESERVATION_SOURCE_UNAVAILABLE");
    }

    private async Task<string> ReadInspectionReplay(SessionReference ticket,long sid,Fe05OperationRequest request,
        string contract,NotificationCommitGuard guard,CancellationToken ct)
    {
        if(await AuthorizeRejectLoadAsync(ticket,sid,request.BranchId,ct) is {} denied)
            throw new InvalidOperationException("FE05_REPLAY_"+denied);
        // This supplements N01 RequireCompletedOperationAsync; it does not replace
        // owner verification of real proof, recipient/read authority or projection.
        var json=await db.Database.SqlQuery<string>($"SELECT result_json AS Value FROM dbo.notification_operation_reservations WITH(UPDLOCK,HOLDLOCK) WHERE actor_user_id={ticket.UserId} AND operation_key={request.OperationId} AND completed=1 AND fingerprint={Convert.FromHexString(request.RequestFingerprint)}").SingleAsync(ct);
        using var result=JsonDocument.Parse(json);var root=result.RootElement;
        if(root.GetProperty("contract").GetString()!=contract || root.GetProperty("branchId").GetInt64()!=request.BranchId
            || root.GetProperty("inspectionId").GetInt64()!=request.InspectionId)throw new InvalidOperationException("FE05_REPLAY_RECEIPT_MISMATCH");
        await guard.VerifyBeforeCommitAsync(db,ct);
        return json;
    }
    // No Commit/Begin/Rollback here. Caller must roll back on any failure and use
    // guarded EF CommitAsync only after this preparation. No provider/raw COMMIT.
}
