using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

// ISOLATED COMPATIBILITY CANDIDATE ONLY; not included in the production API.
// Requires approved Closure05 foundation, concrete owner coordination/recipient
// authority, SQL-accepted store, pinned issuer registration and caller user locks.
public sealed partial class RoomTypeCreationService
{
    internal async Task<string> ComposeRejectCandidateAsync(SessionReference ticket,long sid,long branch,long inspection,
        string expected,Guid key,RejectInspectionRequest input,Fe05OperationStaging staging,
        NotificationCommitGuard guard,CancellationToken ct)
    {
        var domainInput=new RejectInspectionInput(input.Reason,input.Confirmed,expected);
        var identity=InspectionIssuerOperation.Reject(key,ticket.UserId,branch,inspection,expected,domainInput);
        var request=new Fe05OperationRequest(key,branch,inspection,Convert.ToHexString(identity.Fingerprint),[Fe05NotificationKind.InspectionFailed]);
        // N01 validates the retained pre-mutation protocol and its completed domain
        // receipt before returning Replay. Do not rerun source preflight on replay.
        var lease=await Fe05CallerIntegration.ReserveOperationAsync(db,staging,request,ct);
        if(lease.Replay!=null)return await ReadCandidateReplay(ticket,sid,request,"FE05_INSPECTION_CONCLUSION_V1",guard,ct);
        var changed=await ExecuteTrustedRejectDomainInCallerTransactionAsync(ticket,sid,new(branch,inspection,domainInput),identity,ct);
        var revision=WorkflowTag(changed.Revision??throw new InvalidOperationException("FE05_POST_REVISION_REQUIRED"));
        await Fe05CallerIntegration.StageOperationAsync(db,staging,lease,[new(Fe05NotificationKind.InspectionFailed,key,branch,inspection,revision)],ct);
        var completed=await CompleteTrustedRejectInCallerTransactionAsync(ticket,sid,branch,inspection,revision,identity,ct);
        var physical=db.Database.CurrentTransaction!.GetDbTransaction();
        await ProjectFailedConclusionInCallerTransactionAsync(ticket,sid,db.Database.GetDbConnection(),physical,branch,inspection,revision,key,ct);
        await guard.VerifyBeforeCommitAsync(db,ct);
        return completed.ResultJson??throw new InvalidOperationException("FE05_DOMAIN_RECEIPT_REQUIRED");
    }

    internal async Task<string> ComposeReworkCandidateAsync(SessionReference ticket,long sid,long branch,long inspection,
        string expected,Guid key,RequestReworkRequest input,string? evidenceRoot,Fe05OperationStaging staging,
        NotificationCommitGuard guard,CancellationToken ct)
    {
        var identity=InspectionIssuerOperation.Rework(key,ticket.UserId,branch,inspection,expected,input);
        var request=new Fe05OperationRequest(key,branch,inspection,Convert.ToHexString(identity.Fingerprint),[Fe05NotificationKind.CleaningReworkRequested]);
        var lease=await Fe05CallerIntegration.ReserveOperationAsync(db,staging,request,ct);
        if(lease.Replay!=null)return await ReadCandidateReplay(ticket,sid,request,"FE05_REWORK_AUTHORITY_V1",guard,ct);
        var changed=await ExecuteTrustedReworkDomainInCallerTransactionAsync(ticket,sid,branch,inspection,expected,input,identity,ct);
        var rework=changed.ReworkId??throw new InvalidOperationException("FE05_PERSISTED_REWORK_REQUIRED");
        var physical=db.Database.CurrentTransaction!.GetDbTransaction();
        var snapshot=await PrepareReworkAuthorityInCallerTransactionAsync(ticket,sid,db.Database.GetDbConnection(),physical,
            branch,inspection,rework,expected,key,input.DefectIds,evidenceRoot,ct);
        await Fe05CallerIntegration.StageOperationAsync(db,staging,lease,[new(Fe05NotificationKind.CleaningReworkRequested,key,branch,inspection,expected)],ct);
        var completed=await CompleteTrustedReworkInCallerTransactionAsync(ticket,sid,branch,inspection,rework,expected,
            identity,input.DefectIds,evidenceRoot,ct);
        // Re-read persisted evidence AND private bytes just before the caller commit gate.
        var observed=await PrepareReworkAuthorityInCallerTransactionAsync(ticket,sid,db.Database.GetDbConnection(),physical,
            branch,inspection,rework,expected,key,input.DefectIds,evidenceRoot,ct);
        if(snapshot.Receipt.SourceDigest!=observed.Receipt.SourceDigest || snapshot.Receipt.EvidenceDigest!=observed.Receipt.EvidenceDigest)
            throw new InvalidOperationException("FE05_EVIDENCE_CHANGED_BEFORE_COMMIT");
        await guard.VerifyBeforeCommitAsync(db,ct);
        return completed.ResultJson??throw new InvalidOperationException("FE05_DOMAIN_RECEIPT_REQUIRED");
    }

    private async Task<string> ReadCandidateReplay(SessionReference ticket,long sid,Fe05OperationRequest request,
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
