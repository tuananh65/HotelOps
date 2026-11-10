using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

// FE05 call-site seam into the existing durable operation/staging participants, NOT a
// new ledger, notification graph or a receipt issuer. No accepted implementation exists.
internal interface IInspectionApproveAcceptedBinding
{
    Task RequireAcceptedAsync(HotelOpsDbContext db, InspectionIssuerOperation operation, CancellationToken ct);
    Task<string?> ReserveOrReadCompletedAsync(HotelOpsDbContext db, InspectionIssuerOperation operation, CancellationToken ct);
    Task StageRequiredAsync(HotelOpsDbContext db, InspectionIssuerOperation operation, string revision, CancellationToken ct);
    Task<string> CompleteWithTrustedSqlReceiptAsync(HotelOpsDbContext db, InspectionIssuerOperation operation, string revision, CancellationToken ct);
    // Must revalidate the registered SQL issuer, durable N01 graph, exact recipient
    // policies, retained Users roster, current source and persisted projection on replay.
    // JSON structural validation alone MUST NOT implement this method.
    Task VerifyCompletedAsync(HotelOpsDbContext db, InspectionIssuerOperation operation, string receipt, CancellationToken ct);
}
internal sealed class ClosedInspectionApproveBinding : IInspectionApproveAcceptedBinding
{
    private static Task Closed(CancellationToken ct) { ct.ThrowIfCancellationRequested(); throw new Fe05NotificationUnavailableException("UC044_PASSED_STAGING_AND_DURABLE_BINDING_UNACCEPTED"); }
    public Task RequireAcceptedAsync(HotelOpsDbContext db,InspectionIssuerOperation op,CancellationToken ct)=>Closed(ct);
    public async Task<string?> ReserveOrReadCompletedAsync(HotelOpsDbContext db,InspectionIssuerOperation op,CancellationToken ct){await Closed(ct);return null;}
    public Task StageRequiredAsync(HotelOpsDbContext db,InspectionIssuerOperation op,string tag,CancellationToken ct)=>Closed(ct);
    public async Task<string> CompleteWithTrustedSqlReceiptAsync(HotelOpsDbContext db,InspectionIssuerOperation op,string tag,CancellationToken ct){await Closed(ct);return "";}
    public Task VerifyCompletedAsync(HotelOpsDbContext db,InspectionIssuerOperation op,string receipt,CancellationToken ct)=>Closed(ct);
}

// Structural receipt validation is not proof that SQL issued or committed the receipt.
internal static class InspectionPassedReceipt
{
    internal static void Require(string json, InspectionIssuerOperation operation, long actor, long branch,
        long inspection, long room, long task, long version, string revision, long? currentWriterTransaction = null)
    {
        using var document=JsonDocument.Parse(json);var r=document.RootElement;
        if(operation.UseCase!="UC044" || r.GetProperty("contract").GetString()!="FE05_INSPECTION_CONCLUSION_V1"
            ||r.GetProperty("issuer").GetString()!="FE05.UC044.UC045"||r.GetProperty("issuerVersion").GetInt32()!=1
            ||r.GetProperty("actorUserId").GetInt64()!=actor||r.GetProperty("operationId").GetGuid()!=operation.Id
            ||r.GetProperty("requestFingerprint").GetString()!=Convert.ToHexString(operation.Fingerprint)
            ||r.GetProperty("branchId").GetInt64()!=branch||r.GetProperty("inspectionId").GetInt64()!=inspection
            ||r.GetProperty("roomId").GetInt64()!=room||r.GetProperty("cleaningTaskId").GetInt64()!=task
            ||r.GetProperty("versionId").GetInt64()!=version||r.GetProperty("inspectionETag").GetString()!=revision||r.GetProperty("conclusion").GetString()!="PASSED"
            ||r.GetProperty("writerTransactionId").GetInt64()<=0
            ||currentWriterTransaction is {} tx && r.GetProperty("writerTransactionId").GetInt64()!=tx)
            throw new NotificationConflictException();
    }
}

public sealed partial class RoomTypeCreationService
{
    // Caller retains Begin/Commit/Rollback. No public dispatch, DI activation or configurable bypass.
    internal async Task<string> ComposeApproveInCallerTransactionAsync(SessionReference ticket,long sid,long branch,
        long inspection,string expected,Guid key,IInspectionApproveAcceptedBinding binding,NotificationCommitGuard guard,CancellationToken ct)
    {
        if(branch<=0||inspection<=0||key==Guid.Empty||!WorkflowStrongTag(expected))throw new ArgumentException("UC044_REQUEST_IDENTITY_INVALID");
        InspectionConclusionSql.RequireTransaction(db);
        var tx=db.Database.CurrentTransaction!;var physical=tx.GetDbTransaction();var connection=db.Database.GetDbConnection();
        guard.RequireAttached(db,tx.TransactionId);
        var operation=InspectionIssuerOperation.Approve(key,ticket.UserId,branch,inspection,expected);
        void SameTransaction() {
            if(db.Database.CurrentTransaction?.TransactionId!=tx.TransactionId
                ||!ReferenceEquals(db.Database.CurrentTransaction.GetDbTransaction(),physical)
                ||!ReferenceEquals(connection,db.Database.GetDbConnection())||!ReferenceEquals(physical.Connection,connection))
                throw new InvalidOperationException("UC044_PARTICIPANT_CHANGED_CALLER_TRANSACTION");
        }
        try
        {
            // Fail closed before domain mutation until Passed notification/receipt policy exists.
            await binding.RequireAcceptedAsync(db,operation,ct);SameTransaction();
            await LockInspectionCompositionSourceAsync(ticket,sid,branch,inspection,ct);
            // Existing completed replay is checked before Pending Conclusion/stale eligibility.
            var replay=await binding.ReserveOrReadCompletedAsync(db,operation,ct);SameTransaction();
            if(replay!=null) {
                await binding.VerifyCompletedAsync(db,operation,replay,ct);SameTransaction();
                await RequirePassedReceiptSourceAsync(replay,operation,ticket,branch,inspection,null,ct);
                guard.BindGraph(db,tx.TransactionId,"FE05:UC044:"+key,async token=>{
                    SameTransaction();if(await AuthorizeRejectLoadAsync(ticket,sid,branch,token) is not null)throw new InvalidOperationException("UC044_COMMIT_AUTHORIZATION_REVOKED");
                    await binding.VerifyCompletedAsync(db,operation,replay,token);
                    await RequirePassedReceiptSourceAsync(replay,operation,ticket,branch,inspection,null,token);SameTransaction();
                });
                await guard.VerifyBeforeCommitAsync(db,ct);SameTransaction();return replay;
            }
            var domain=await ApproveDomainInCallerTransactionAsync(ticket,sid,branch,inspection,expected,ct);SameTransaction();
            await binding.StageRequiredAsync(db,operation,domain.Revision,ct);SameTransaction();
            var result=await binding.CompleteWithTrustedSqlReceiptAsync(db,operation,domain.Revision,ct);SameTransaction();
            var writer=await db.Database.SqlQuery<long>($"SELECT CURRENT_TRANSACTION_ID() AS Value").SingleAsync(ct);
            await RequirePassedReceiptSourceAsync(result,operation,ticket,branch,inspection,writer,ct);
            // Original FE04 adapter alone owns Ready/Blocked projection and registered SQL receipt checks.
            await new InspectionRoomProjectionAdapter(db).ApplyAsync(connection,physical,ticket.UserId,branch,inspection,
                Convert.FromHexString(domain.Revision.Trim('"')),key,ct);SameTransaction();
            guard.BindGraph(db,tx.TransactionId,"FE05:UC044:"+key,async token=>{
                SameTransaction();
                if(await AuthorizeRejectLoadAsync(ticket,sid,branch,token) is not null)throw new InvalidOperationException("UC044_COMMIT_AUTHORIZATION_REVOKED");
                await binding.VerifyCompletedAsync(db,operation,result,token);
                await RequirePassedReceiptSourceAsync(result,operation,ticket,branch,inspection,writer,token);SameTransaction();
            });
            await guard.VerifyBeforeCommitAsync(db,ct);return result;
        }
        catch {guard.Fail(db,tx.TransactionId);throw;}
    }
    private async Task RequirePassedReceiptSourceAsync(string receipt,InspectionIssuerOperation operation,
        SessionReference ticket,long branch,long inspection,long? writer,CancellationToken ct)
    {
        var rows=await db.Database.SqlQuery<PassedReceiptSource>($"""
            SELECT i.room_id AS RoomId,i.cleaning_task_id AS TaskId,i.version_id AS VersionId,i.row_version AS Revision
            FROM dbo.room_inspections i WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.rooms r ON r.room_id=i.room_id AND r.branch_id={branch}
            JOIN dbo.cleaning_tasks t WITH(UPDLOCK,HOLDLOCK) ON t.cleaning_task_id=i.cleaning_task_id AND t.room_id=i.room_id
            JOIN dbo.room_cleaning_cycle_state c WITH(UPDLOCK,HOLDLOCK) ON c.room_id=i.room_id AND c.cleaning_task_id=i.cleaning_task_id
            WHERE i.inspection_id={inspection} AND i.inspector_user_id={ticket.UserId} AND i.workflow_status='PASSED'
                AND i.status='COMPLETED' AND i.result='PASS' AND t.workflow_status='COMPLETED' AND t.status='COMPLETED'
            """).ToArrayAsync(ct);
        if(rows.Length!=1)throw new NotificationConflictException();
        InspectionPassedReceipt.Require(receipt,operation,ticket.UserId,branch,inspection,rows[0].RoomId,rows[0].TaskId,rows[0].VersionId,WorkflowTag(rows[0].Revision),writer);
    }
    private sealed class PassedReceiptSource { public long RoomId {get;set;} public long TaskId {get;set;} public long VersionId {get;set;} public byte[] Revision {get;set;}=[]; }
}
