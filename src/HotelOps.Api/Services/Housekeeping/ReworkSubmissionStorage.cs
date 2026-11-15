using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.DTOs.HotelStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private const string ReworkSubmissionContract="FE04_REWORK_SUBMISSION_V1";
    private sealed record ReworkSubmissionReceipt(string Contract,string Issuer,int IssuerVersion,long ActorUserId,long BranchId,long ReworkId,
        Guid OperationId,long WriterTransactionId,Guid AuthorityOperationId,string SourceDigest,string RemediationDigest,ReworkCommandResult Entity);
    private sealed class SubmissionLedgerRow
    {public string Json{get;set;}="";public byte[] Fingerprint{get;set;}=[];}
    private sealed class SubmissionRemediationRow
    {public long DefectId{get;set;}public string? Note{get;set;}public long? Actor{get;set;}public DateTime? OccurredAt{get;set;}}
    private sealed class SubmissionFileRow
    {
        public long DefectId{get;set;}public long AttachmentId{get;set;}public string EntityType{get;set;}="";public long EntityId{get;set;}
        public string StorageKey{get;set;}="";public string Name{get;set;}="";public string? Mime{get;set;}public long? Size{get;set;}
        public long Actor{get;set;}public DateTime OccurredAt{get;set;}public DateTime? DeletedAt{get;set;}
    }

    private async Task<bool> ReworkSubmissionInstalled(CancellationToken ct)
        =>await db.Database.SqlQueryRaw<int>("""
            SELECT CASE WHEN COL_LENGTH('dbo.cleaning_rework_defects','remediation_note')=1000
              AND COL_LENGTH('dbo.cleaning_rework_defects','remediated_by')=8 AND COL_LENGTH('dbo.cleaning_rework_defects','remediated_at')=8
              AND OBJECT_ID('dbo.fs016_rework_evidence','U') IS NOT NULL AND OBJECT_ID('dbo.fe04_commit_rework_submission','P') IS NOT NULL
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.ReworkSubmission.V1' AND CONVERT(nvarchar(128),value)='FE04_REWORK_SUBMISSION_V1')
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_rework_remediation_revision_v1') AND is_disabled=0)
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_rework_evidence_revision_v1') AND is_disabled=0)
              AND HAS_PERMS_BY_NAME('dbo.fs016_rework_source_proofs','OBJECT','INSERT')=0
              AND HAS_PERMS_BY_NAME('dbo.fs016_rework_source_proofs','OBJECT','UPDATE')=0
              AND HAS_PERMS_BY_NAME('dbo.fs016_rework_source_proofs','OBJECT','DELETE')=0
              AND HAS_PERMS_BY_NAME('dbo.notification_operation_reservations','OBJECT','INSERT')=0
              AND HAS_PERMS_BY_NAME('dbo.notification_operation_reservations','OBJECT','UPDATE')=0
              AND HAS_PERMS_BY_NAME('dbo.notification_operation_reservations','OBJECT','DELETE')=0
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)==1;

    private async Task<byte[]?> ReworkRemediationDigest(long rework,long task,long actor,string? root,CancellationToken ct)
    {
        var rows=await db.Database.SqlQuery<SubmissionRemediationRow>($"""
            SELECT defect_id AS DefectId,remediation_note AS Note,remediated_by AS Actor,remediated_at AS OccurredAt
            FROM dbo.cleaning_rework_defects WITH(HOLDLOCK) WHERE rework_id={rework} ORDER BY defect_id
            """).ToArrayAsync(ct);
        if(rows.Length is <1 or >200||rows.Any(r=>string.IsNullOrWhiteSpace(r.Note)||r.Note.Length>500||r.Actor!=actor||r.OccurredAt==null))return null;
        var files=await db.Database.SqlQuery<SubmissionFileRow>($"""
            SELECT e.defect_id AS DefectId,a.attachment_id AS AttachmentId,a.entity_type AS EntityType,a.entity_id AS EntityId,
              a.storage_key AS StorageKey,COALESCE(a.original_file_name,a.file_name) AS Name,a.mime_type AS Mime,a.file_size_bytes AS Size,
              a.uploaded_by AS Actor,a.uploaded_at AS OccurredAt,a.deleted_at AS DeletedAt
            FROM dbo.fs016_rework_evidence e WITH(HOLDLOCK) JOIN dbo.attachments a WITH(HOLDLOCK) ON a.attachment_id=e.attachment_id
            WHERE e.rework_id={rework} ORDER BY e.defect_id,a.attachment_id
            """).ToArrayAsync(ct);
        if(files.Length>5||files.Any(f=>f.DeletedAt!=null||f.EntityType!="CLEANING_TASK"||f.EntityId!=task||f.Actor!=actor||!rows.Any(r=>r.DefectId==f.DefectId)))return null;
        var content=new List<object>();
        foreach(var file in files)
        {
            var metadata=new HistoryEvidenceDto(file.AttachmentId,file.EntityType,file.EntityId,file.Name,file.Mime,file.Size,file.OccurredAt,"");
            var read=await HistoryFile([metadata],file.AttachmentId,root,ct);if(read==null)return null;
            content.Add(new{file.DefectId,file.AttachmentId,file.StorageKey,file.Name,file.Mime,file.Size,file.Actor,
                occurredAt=DateTime.SpecifyKind(file.OccurredAt,DateTimeKind.Utc),digest=Convert.ToHexString(SHA256.HashData(read.Content))});
        }
        return SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{version=1,rework,task,actor,
            defects=rows.Select(r=>new{r.DefectId,r.Note,r.Actor,occurredAt=DateTime.SpecifyKind(r.OccurredAt!.Value,DateTimeKind.Utc)}),files=content},Json));
    }

    private async Task<(bool Exists,ReworkSubmissionReceipt? Receipt)> ReadReworkSubmissionReceipt(long actor,long branch,long rework,Guid operation,
        byte[] fingerprint,string? root,CancellationToken ct)
    {
        var stored=await db.Database.SqlQuery<SubmissionLedgerRow>($"""
            SELECT result_json AS Json,fingerprint AS Fingerprint FROM dbo.notification_operation_reservations WITH(HOLDLOCK)
            WHERE actor_user_id={actor} AND operation_key={operation} AND completed=1 AND fingerprint_version=1
            """).SingleOrDefaultAsync(ct);
        if(stored==null)return(false,null);
        ReworkSubmissionReceipt? receipt;
        try{receipt=JsonSerializer.Deserialize<ReworkSubmissionReceipt>(stored.Json,Json);}catch(JsonException){receipt=null;}
        if(receipt?.Entity is not {} entity||!stored.Fingerprint.SequenceEqual(fingerprint)||receipt.Contract!=ReworkSubmissionContract
            ||receipt.Issuer!="FE04.UC036"||receipt.IssuerVersion!=1||receipt.ActorUserId!=actor||receipt.BranchId!=branch||receipt.ReworkId!=rework
            ||receipt.OperationId!=operation||receipt.WriterTransactionId<=0||receipt.AuthorityOperationId==Guid.Empty
            ||!ValidReworkReceipt(entity,actor,branch,rework,entity.CleaningTaskId,entity.InspectionId,operation,"complete",ReworkSubmissionContract)
            ||entity.CleaningTaskId<=0||entity.InspectionId<=0||entity.PublicationId!=operation)return(true,null);
        if(await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.fe04_rework_authority_issuers x JOIN sys.sql_modules m ON m.object_id=x.writer_object_id
            WHERE x.issuer='FE04.UC036' AND x.version=1 AND x.is_active=1 AND x.writer_object_id=OBJECT_ID('dbo.fe04_commit_rework_submission')
              AND x.source_commit LIKE REPLICATE('[0-9a-f]',40) AND m.execute_as_principal_id=-2
              AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition))=x.module_digest
              AND {receipt.WriterTransactionId}<>CURRENT_TRANSACTION_ID()
            """).SingleAsync(ct)!=1)return(true,null);
        if(await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.fs016_rework_source_proofs p
            JOIN dbo.cleaning_reworks w ON w.rework_id=p.rework_id AND w.inspection_id=p.inspection_id AND w.cleaning_task_id=p.cleaning_task_id
            JOIN dbo.fe04_rework_authority_issuers x ON x.issuer=p.issuer AND x.version=p.issuer_version AND x.is_active=1
            JOIN sys.sql_modules m ON m.object_id=x.writer_object_id AND m.execute_as_principal_id=-2
            JOIN dbo.work_intervals wi ON wi.interval_id={entity.IntervalId} AND wi.user_id={actor} AND wi.branch_id={branch}
              AND wi.work_item_kind='REWORK' AND wi.work_item_id=w.rework_id AND wi.closed_by={actor} AND wi.closed_at={entity.OccurredAtUtc}
            WHERE p.rework_id={rework} AND p.branch_id={branch} AND w.assignee_user_id={actor}
              AND p.inspection_id={entity.InspectionId} AND p.cleaning_task_id={entity.CleaningTaskId}
              AND p.origin='FE05_COMMITTED_V1' AND p.issuer='FE05.UC045.UC046' AND p.contract_version=1
              AND p.operation_key={receipt.AuthorityOperationId} AND CONVERT(varchar(64),p.source_digest,2)={receipt.SourceDigest}
              AND x.source_commit LIKE REPLICATE('[0-9a-f]',40)
              AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition))=x.module_digest
            """).SingleAsync(ct)!=1)return(true,null);
        if(await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.audit_logs WHERE actor_user_id={actor} AND branch_id={branch}
              AND entity_type='cleaning_reworks' AND entity_id=CONVERT(nvarchar(50),{rework}) AND action='UPDATE'
              AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),new_values))=HASHBYTES('SHA2_256',CONVERT(varbinary(max),{stored.Json}))
            """).SingleAsync(ct)!=1)return(true,null);
        // A historical confirmation is not a new mutation or permission to use a room.
        // Original immutable source/evidence must still match even after a later inspection is assigned.
        var source=await BuildReworkAuthoritySnapshotAsync(db.Database.GetDbConnection(),db.Database.CurrentTransaction!.GetDbTransaction(),rework,root,ct);
        var remediation=await ReworkRemediationDigest(rework,entity.CleaningTaskId,actor,root,ct);
        return source!=null&&Convert.ToHexString(source.SourceDigest)==receipt.SourceDigest&&remediation!=null&&Convert.ToHexString(remediation)==receipt.RemediationDigest
            ?(true,receipt):(true,null);
    }
}
