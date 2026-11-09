using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

// Internal request identity, separate from FE04 source/evidence authority digests.
// ASCII fields and UTF-16LE reason hex have an independently reproducible SQL encoding.
internal sealed record InspectionIssuerOperation(Guid Id, string UseCase, byte[] Fingerprint, string Canonical)
{
    internal static InspectionIssuerOperation Reject(Guid id,long actor,long branch,long inspection,string tag,RejectInspectionInput input)
        => Create(id,"UC045",actor,branch,inspection,tag,input.Reason,
            input.Confirmed is null ? "null" : input.Confirmed.Value ? "1" : "0");
    internal static InspectionIssuerOperation Rework(Guid id,long actor,long branch,long inspection,string tag,RequestReworkRequest input)
        => Create(id,"UC046",actor,branch,inspection,tag,input.Reason,
            Number(input.AssigneeId),Number(input.DueAt.Ticks),string.Join(',',input.DefectIds.Order().Select(Number)));
    private static string Number(long value)=>value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static InspectionIssuerOperation Create(Guid id,string uc,long actor,long branch,long inspection,string tag,string? reason,params string[] tail)
    {
        if(id==Guid.Empty)throw new ArgumentException("Operation identity required");
        var canonical=string.Join('|',new[]{"FE05","1",uc,Number(actor),Number(branch),Number(inspection),tag.Trim('"').ToUpperInvariant(),
            reason is null ? "NULL" : Convert.ToHexString(System.Text.Encoding.Unicode.GetBytes(reason.Trim()))}.Concat(tail));
        return new(id,uc,SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(canonical)),canonical);
    }
}
internal sealed record InspectionIssuerStep(long? ReworkId=null,byte[]? Revision=null,string? ResultJson=null);

public sealed partial class RoomTypeCreationService
{
    internal async Task<InspectionIssuerStep> ExecuteTrustedRejectDomainInCallerTransactionAsync(SessionReference ticket,long sid,
        RejectInspectionLoadCommand command,InspectionIssuerOperation operation,CancellationToken ct,bool validateOnly=false)
    {
        RequireIssuerOperation(operation,InspectionIssuerOperation.Reject(operation.Id,ticket.UserId,command.BranchId,
            command.InspectionId,command.Input.ExpectedETag!,command.Input));
        var source=await PrepareRejectInCallerTransactionAsync(ticket,sid,command,ct);
        if(source.Status!=RoomTypeBatchStatus.Success)throw new InvalidOperationException("FE05_REJECT_SOURCE_"+source.Status);
        return await CallInspectionIssuerAsync(ticket,sid,command.BranchId,command.InspectionId,command.Input.ExpectedETag!,
            operation,validateOnly?"VALIDATE_REJECT":"REJECT",command.Input.Reason,command.Input.Confirmed==true,null,null,null,null,null,ct);
    }

    internal async Task<InspectionIssuerStep> ExecuteTrustedReworkDomainInCallerTransactionAsync(SessionReference ticket,long sid,
        long branch,long inspection,string tag,RequestReworkRequest input,InspectionIssuerOperation operation,CancellationToken ct)
    {
        RequireIssuerOperation(operation,InspectionIssuerOperation.Rework(operation.Id,ticket.UserId,branch,inspection,tag,input));
        // Existing eligibility/user lock protocol; no copied Work-Time implementation.
        await new UserEligibilityLock(db).LockAsync([ticket.UserId,input.AssigneeId],ct);
        var source=await PrepareReworkInCallerTransactionAsync(ticket,sid,branch,inspection,tag,ct);
        if(source.Status!=RoomTypeBatchStatus.Success)throw new InvalidOperationException("FE05_REWORK_SOURCE_"+source.Status);
        if(source.Value is not ReworkSourcePreview value || input.DefectIds.Any(id=>!value.DefectIds.Contains(id))
            || !await DueHousekeeper(input.AssigneeId,branch,input.DueAt,ct))throw new InvalidOperationException("FE05_REWORK_INPUT_INVALID");
        WorkflowReason(input.Reason);WorkflowDue(input.DueAt,clock.GetUtcNow().UtcDateTime);
        return await CallInspectionIssuerAsync(ticket,sid,branch,inspection,tag,operation,"REWORK",input.Reason,false,
            input.AssigneeId,input.DueAt,input.DefectIds,null,null,ct);
    }

    internal async Task<InspectionIssuerStep> CompleteTrustedRejectInCallerTransactionAsync(SessionReference ticket,long sid,
        long branch,long inspection,string tag,InspectionIssuerOperation operation,CancellationToken ct)
    {
        if(operation.UseCase!="UC045")throw new InvalidOperationException("FE05_OPERATION_MISMATCH");
        var tx=db.Database.CurrentTransaction??throw new InvalidOperationException("Caller transaction required");
        await PrepareFailedConclusionReceiptInCallerTransactionAsync(ticket,sid,db.Database.GetDbConnection(),tx.GetDbTransaction(),branch,inspection,tag,ct);
        return await CallInspectionIssuerAsync(ticket,sid,branch,inspection,tag,operation,"COMPLETE_REJECT",null,false,null,null,null,null,null,ct);
    }

    internal async Task<InspectionIssuerStep> CompleteTrustedReworkInCallerTransactionAsync(SessionReference ticket,long sid,
        long branch,long inspection,long rework,string tag,InspectionIssuerOperation operation,IReadOnlyList<long> selected,
        string? evidenceRoot,CancellationToken ct)
    {
        if(operation.UseCase!="UC046")throw new InvalidOperationException("FE05_OPERATION_MISMATCH");
        var tx=db.Database.CurrentTransaction??throw new InvalidOperationException("Caller transaction required");
        // Exact FE04 snapshot/digest implementation at pinned R03; full composition remains unaccepted.
        var prepared=await PrepareReworkAuthorityInCallerTransactionAsync(ticket,sid,db.Database.GetDbConnection(),tx.GetDbTransaction(),
            branch,inspection,rework,tag,operation.Id,selected,evidenceRoot,ct);
        return await CallInspectionIssuerAsync(ticket,sid,branch,inspection,tag,operation,"COMPLETE_REWORK",null,false,
            null,null,selected,rework,prepared.Snapshot,ct);
    }

    private static void RequireIssuerOperation(InspectionIssuerOperation supplied,InspectionIssuerOperation expected)
    {
        if(supplied.UseCase!=expected.UseCase || supplied.Fingerprint.Length!=32
            || !CryptographicOperations.FixedTimeEquals(supplied.Fingerprint,expected.Fingerprint))
            throw new InvalidOperationException("FE05_OPERATION_MISMATCH");
    }

    internal async Task<InspectionIssuerStep> CallInspectionIssuerAsync(SessionReference ticket,long sid,long branch,long inspection,
        string tag,InspectionIssuerOperation operation,string action,string? reason,bool confirmed,long? assignee,DateTime? due,
        IReadOnlyList<long>? selected,long? rework,ReworkAuthoritySnapshot? snapshot,CancellationToken ct)
    {
        InspectionConclusionSql.RequireTransaction(db);
        if(System.Transactions.Transaction.Current!=null || operation.Id==Guid.Empty || operation.Fingerprint.Length!=32 || !WorkflowStrongTag(tag))
            throw new InvalidOperationException("FE05_ISSUER_INPUT_INVALID");
        if(await AuthorizeRejectLoadAsync(ticket,sid,branch,ct) is {} denied)throw new InvalidOperationException("FE05_ISSUER_"+denied);
        var connection=db.Database.GetDbConnection();var transaction=db.Database.CurrentTransaction!.GetDbTransaction();
        await using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="dbo.fe05_inspection_issuer_v1";command.CommandType=CommandType.StoredProcedure;
        void Add(string name,SqlDbType type,object? value,int size=0){var p=new SqlParameter(name,type){Value=value??DBNull.Value};if(size!=0)p.Size=size;command.Parameters.Add(p);}
        Add("@action",SqlDbType.VarChar,action,24);Add("@actor",SqlDbType.BigInt,ticket.UserId);Add("@session",SqlDbType.BigInt,sid);
        Add("@session_hash",SqlDbType.VarChar,ticket.KeyHash,71);Add("@session_created",SqlDbType.DateTime2,ticket.CreatedAtUtc);
        Add("@branch",SqlDbType.BigInt,branch);Add("@inspection",SqlDbType.BigInt,inspection);
        Add("@expected",SqlDbType.Binary,Convert.FromHexString(tag.Trim('"')),8);Add("@operation",SqlDbType.UniqueIdentifier,operation.Id);
        Add("@fingerprint",SqlDbType.Binary,operation.Fingerprint,32);Add("@canonical",SqlDbType.VarChar,operation.Canonical,-1);Add("@reason",SqlDbType.NVarChar,reason?.Trim(),-1);Add("@confirmed",SqlDbType.Bit,confirmed);
        Add("@assignee",SqlDbType.BigInt,assignee);Add("@due",SqlDbType.DateTime2,due);
        Add("@selected",SqlDbType.NVarChar,selected==null?null:JsonSerializer.Serialize(selected),-1);Add("@rework",SqlDbType.BigInt,rework);
        Add("@source_digest",SqlDbType.Binary,snapshot?.SourceDigest,32);Add("@evidence_digest",SqlDbType.Binary,snapshot?.EvidenceDigest,32);
        InspectionIssuerStep result;
        await using(var reader=await command.ExecuteReaderAsync(ct))
        {
            if(!await reader.ReadAsync(ct))throw new InvalidOperationException("FE05_ISSUER_RESULT_REQUIRED");
            var fields=Enumerable.Range(0,reader.FieldCount).ToDictionary(reader.GetName,i=>i);
            result=new(fields.TryGetValue("ReworkId",out var rw)?reader.GetInt64(rw):null,
                fields.TryGetValue("Revision",out var rev)?(byte[])reader.GetValue(rev):null,
                fields.TryGetValue("ResultJson",out var json)?reader.GetString(json):null);
        }
        if(!ReferenceEquals(transaction,db.Database.CurrentTransaction?.GetDbTransaction()))throw new InvalidOperationException("FE05_TRANSACTION_REPLACED");
        if(await AuthorizeRejectLoadAsync(ticket,sid,branch,ct) is {} revoked)throw new InvalidOperationException("FE05_ISSUER_FINAL_"+revoked);
        return result; // Never commits; N01/projection/receipt checks still belong to caller composition.
    }
}
