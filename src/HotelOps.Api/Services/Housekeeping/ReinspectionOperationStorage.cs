using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private const string ReinspectionAssignmentContract="FE05_REINSPECTION_ASSIGNMENT_V1";
    private const string ReinspectionConclusionContract="FE05_REINSPECTION_CONCLUSION_V1";
    private async Task<bool> ReinspectionWorkflowInstalled(CancellationToken ct)
        =>await ReworkSubmissionInstalled(ct)&&await db.Database.SqlQueryRaw<int>("""
            SELECT CASE WHEN OBJECT_ID('dbo.fe05_reinspection_bindings_v1','U') IS NOT NULL
              AND OBJECT_ID('dbo.fe05_reinspection_defect_results_v1','U') IS NOT NULL
              AND OBJECT_ID('dbo.fe05_reinspection_defect_attachments_v1','U') IS NOT NULL
              AND OBJECT_ID('dbo.fe05_bind_reinspection','P') IS NOT NULL AND OBJECT_ID('dbo.fe05_commit_reinspection','P') IS NOT NULL
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.Reinspection.V1' AND CONVERT(nvarchar(128),value)='FE05_REINSPECTION_V1')
              AND (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_reinspection_binding_immutable_v1','TR_reinspection_result_revision_v1','TR_reinspection_photo_revision_v1') AND is_disabled=0)=3
              AND HAS_PERMS_BY_NAME('dbo.fe05_reinspection_bindings_v1','OBJECT','INSERT')=0
              AND HAS_PERMS_BY_NAME('dbo.fe05_reinspection_bindings_v1','OBJECT','UPDATE')=0
              AND HAS_PERMS_BY_NAME('dbo.fe05_reinspection_bindings_v1','OBJECT','DELETE')=0 THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)==1;

    private async Task<bool> CheckReinspectionWriter(string issuer,string writer,bool execute,CancellationToken ct)
        =>await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.fe04_rework_authority_issuers x JOIN sys.sql_modules m ON m.object_id=x.writer_object_id
            WHERE x.issuer={issuer} AND x.version=1 AND x.is_active=1 AND x.writer_object_id=OBJECT_ID({writer})
              AND x.source_commit LIKE REPLICATE('[0-9a-f]',40) AND m.execute_as_principal_id=-2
              AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition))=x.module_digest
              AND({execute}=0 OR HAS_PERMS_BY_NAME({writer},'OBJECT','EXECUTE')=1)
            """).SingleAsync(ct)==1;

    private sealed record ReinspectionOperationReceipt<T>(string Contract,string Issuer,int IssuerVersion,long ActorUserId,
        long BranchId,long InspectionId,long ReworkId,Guid OperationId,long WriterTransactionId,T Entity);

    // Historical acknowledgement only. The caller still owns current session/role/branch admission.
    private async Task<(bool Exists,T? Value)> ReadReinspectionOperation<T>(long actor,long branch,long source,Guid operation,
        byte[] fingerprint,string contract,CancellationToken ct) where T:class
    {
        if (contract is not (ReinspectionAssignmentContract or ReinspectionConclusionContract)) return (true, null);
        var row=await db.Database.SqlQuery<SubmissionLedgerRow>($"""
            SELECT result_json AS Json,fingerprint AS Fingerprint,operation_key AS OperationId FROM dbo.notification_operation_reservations WITH(HOLDLOCK)
            WHERE actor_user_id={actor} AND operation_key={operation} AND completed=1 AND fingerprint_version=1
            """).SingleOrDefaultAsync(ct);
        if(row==null)return(false,null);
        ReinspectionOperationReceipt<T>? receipt;
        try{receipt=JsonSerializer.Deserialize<ReinspectionOperationReceipt<T>>(row.Json,Json);}catch(JsonException){return(true,null);}
        var assignment=contract==ReinspectionAssignmentContract;
        var issuer=assignment?"FE05.UC041":"FE05.UC047";var writer=assignment?"dbo.fe05_bind_reinspection":"dbo.fe05_commit_reinspection";
        if(receipt==null||receipt.Entity==null||receipt.Contract!=contract||receipt.Issuer!=issuer||receipt.IssuerVersion!=1
            ||receipt.ActorUserId!=actor||receipt.BranchId!=branch||receipt.OperationId!=operation||receipt.WriterTransactionId<=0
            ||receipt.InspectionId<=0||receipt.ReworkId<=0||(assignment?receipt.ReworkId:receipt.InspectionId)!=source
            ||!CryptographicOperations.FixedTimeEquals(row.Fingerprint,fingerprint)||!await CheckReinspectionWriter(issuer,writer,false,ct))return(true,null);
        if (assignment && (receipt.Entity is not ReinspectionAssignmentResult entity
            || !ValidReinspectionAssignmentReceipt(entity, branch, receipt.InspectionId, receipt.ReworkId, operation))) return (true, null);
        if (!assignment && (receipt.Entity is not ReinspectionConclusionResult concluded
            || !ValidReinspectionConclusionReceipt(concluded, branch, receipt.InspectionId, receipt.ReworkId, operation))) return (true, null);
        if(await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_bindings_v1 b WITH(HOLDLOCK)
            JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id AND i.cleaning_task_id=b.cleaning_task_id AND i.room_id=b.room_id AND i.version_id=b.version_id
            JOIN dbo.cleaning_reworks w ON w.rework_id=b.rework_id AND w.inspection_id=b.original_inspection_id AND w.cleaning_task_id=b.cleaning_task_id
            WHERE b.inspection_id={receipt.InspectionId} AND b.rework_id={receipt.ReworkId} AND b.branch_id={branch}
              AND({assignment}=0 OR b.bound_by={actor}) AND({assignment}=1 OR b.reinspector_user_id={actor})
              AND {receipt.WriterTransactionId}<>CURRENT_TRANSACTION_ID()
              AND(SELECT COUNT(*) FROM dbo.audit_logs a WHERE a.actor_user_id={actor} AND a.branch_id={branch}
                AND a.entity_type='REINSPECTION' AND a.entity_id=CONVERT(nvarchar(50),b.inspection_id) AND a.action={(assignment?"CREATE":"UPDATE")}
                AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),a.new_values))=HASHBYTES('SHA2_256',CONVERT(varbinary(max),{row.Json})))=1
            """).SingleAsync(ct)!=1)return(true,null);
        return(true,receipt.Entity);
    }

    public static bool ValidReinspectionAssignmentReceipt(ReinspectionAssignmentResult? result, long branch,
        long inspection, long rework, Guid operation)
        => result != null && branch > 0 && inspection > 0 && rework > 0 && operation != Guid.Empty
            && result.Id == inspection && result.ReworkId == rework && result.BranchId == branch
            && result.OperationId == operation && result.Contract == ReinspectionAssignmentContract
            && result.State == "ASSIGNED" && result.Notification == "PENDING_DELIVERY" && WorkflowStrongTag(result.SourceETag);

    public static bool ValidReinspectionConclusionReceipt(ReinspectionConclusionResult? result, long branch,
        long inspection, long rework, Guid operation)
        => result != null && branch > 0 && inspection > 0 && rework > 0 && operation != Guid.Empty
            && result.Id == inspection && result.ReworkId == rework && result.BranchId == branch
            && result.CleaningTaskId > 0 && result.RoomId > 0 && result.OperationId == operation
            && result.Contract == ReinspectionConclusionContract && result.Notification == "PENDING_DELIVERY"
            && WorkflowStrongTag(result.SourceETag) && WorkflowStrongTag(result.ReworkETag)
            && (result.State == "PASSED" && result.ReworkState == "COMPLETED" && result.EffectiveReadiness is "READY" or "BLOCKED"
                || result.State == "FAILED" && result.ReworkState == "FAILED" && result.EffectiveReadiness is "REWORK_REQUIRED" or "BLOCKED")
            && result.MessageCode == (result.EffectiveReadiness == "BLOCKED" ? "MSG17" : "MSG08");

    private async Task<T> ReadWrittenReinspectionResult<T>(long actor,Guid operation,CancellationToken ct) where T:class
    {
        var json=await db.Database.SqlQuery<string>($"""
            SELECT result_json AS Value FROM dbo.notification_operation_reservations
            WHERE actor_user_id={actor} AND operation_key={operation} AND completed=1
              AND TRY_CONVERT(bigint,JSON_VALUE(result_json,'$.writerTransactionId'))=CURRENT_TRANSACTION_ID()
            """).SingleAsync(ct);
        using var document=JsonDocument.Parse(json);
        return document.RootElement.GetProperty("entity").Deserialize<T>(Json)??throw new InvalidOperationException("REINSPECTION_WRITER_RESULT_INVALID");
    }
}
