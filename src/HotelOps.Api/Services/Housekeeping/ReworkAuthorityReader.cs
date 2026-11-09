using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

public sealed record ReworkAuthoritySnapshot(ReworkAuthorityBinding Binding, long CurrentCycleId,
    bool CurrentFailedSource, byte[] SourceDigest, byte[] EvidenceDigest);

public sealed partial class RoomTypeCreationService
{
    private sealed class AuthoritySourceRow
    {
        public long ReworkId { get; set; }
        public long InspectionId { get; set; }
        public long CleaningTaskId { get; set; }
        public long RoomId { get; set; }
        public long BranchId { get; set; }
        public long VersionId { get; set; }
        public byte[] InspectionRevision { get; set; } = [];
        public long CurrentCycleId { get; set; }
        public bool CurrentFailedSource { get; set; }
    }
    private sealed class AuthorityEvidenceRow
    {
        public string Kind { get; set; } = "";
        public long SourceId { get; set; }
        public long AttachmentId { get; set; }
        public string EntityType { get; set; } = "";
        public long EntityId { get; set; }
        public string StorageKey { get; set; } = "";
        public string FileName { get; set; } = "";
        public string? MimeType { get; set; }
        public long? Size { get; set; }
        public long UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
    private sealed class AuthorityProofRow
    {
        public string Origin { get; set; } = "";
        public int ContractVersion { get; set; }
        public string Issuer { get; set; } = "";
        public int IssuerVersion { get; set; }
        public long ActorUserId { get; set; }
        public Guid OperationId { get; set; }
        public long WriterTransactionId { get; set; }
        public long ReworkId { get; set; }
        public long InspectionId { get; set; }
        public long CleaningTaskId { get; set; }
        public long RoomId { get; set; }
        public long BranchId { get; set; }
        public long VersionId { get; set; }
        public byte[] InspectionRevision { get; set; } = [];
        public byte[] SourceDigest { get; set; } = [];
        public byte[] EvidenceDigest { get; set; } = [];
        public bool TrustedIssuer { get; set; }
        public bool ReceiptMatches { get; set; }
        public bool AuditMatches { get; set; }
        public long ReaderTransactionId { get; set; }
    }

    // FE05 may call this after its domain writes, before its own commit. This reads/hashes only;
    // it never issues a proof, creates an operation, publishes an event or commits a transaction.
    public async Task<ReworkAuthoritySnapshot?> BuildReworkAuthoritySnapshotAsync(DbConnection connection,
        DbTransaction transaction, long id, string? evidenceRoot, CancellationToken ct)
    {
        RequireAuthorityTransaction(connection, transaction);
        var source = await db.Database.SqlQuery<AuthoritySourceRow>($"""
            SELECT w.rework_id AS ReworkId,i.inspection_id AS InspectionId,w.cleaning_task_id AS CleaningTaskId,
              i.room_id AS RoomId,r.branch_id AS BranchId,i.version_id AS VersionId,i.row_version AS InspectionRevision,
              COALESCE(c.cleaning_task_id,0) AS CurrentCycleId,
              CONVERT(bit,CASE WHEN i.workflow_status='FAILED' AND i.status='COMPLETED' AND i.result='FAIL'
                AND i.inspected_at IS NOT NULL AND w.requested_at>=i.inspected_at AND w.due_at IS NOT NULL
                AND t.workflow_status='REWORK_REQUIRED' AND w.cleaning_task_id=i.cleaning_task_id AND t.room_id=i.room_id
                AND NOT EXISTS(SELECT 1 FROM dbo.room_inspections newer WITH(HOLDLOCK)
                  WHERE newer.cleaning_task_id=w.cleaning_task_id AND newer.inspection_id>i.inspection_id)
                THEN 1 ELSE 0 END) AS CurrentFailedSource
            FROM dbo.cleaning_reworks w WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.room_inspections i WITH(UPDLOCK,HOLDLOCK) ON i.inspection_id=w.inspection_id
            JOIN dbo.cleaning_tasks t WITH(UPDLOCK,HOLDLOCK) ON t.cleaning_task_id=w.cleaning_task_id
            JOIN dbo.rooms r WITH(UPDLOCK,HOLDLOCK) ON r.room_id=i.room_id
            LEFT JOIN dbo.room_cleaning_cycle_state c WITH(UPDLOCK,HOLDLOCK) ON c.room_id=i.room_id
            WHERE w.rework_id={id}
            """).SingleOrDefaultAsync(ct);
        if (source == null) return null;
        var selected = await db.Database.SqlQuery<long>($"SELECT defect_id AS Value FROM dbo.cleaning_rework_defects WITH(HOLDLOCK) WHERE rework_id={id}").ToArrayAsync(ct);
        if (selected.Length is < 1 or > 200) return null;
        var evidence = await db.Database.SqlQuery<AuthorityEvidenceRow>($"""
            SELECT TOP(201) l.Kind,l.SourceId,a.attachment_id AS AttachmentId,a.entity_type AS EntityType,a.entity_id AS EntityId,
              a.storage_key AS StorageKey,COALESCE(a.original_file_name,a.file_name) AS FileName,a.mime_type AS MimeType,
              a.file_size_bytes AS Size,a.uploaded_by AS UploadedBy,a.uploaded_at AS UploadedAt,a.deleted_at AS DeletedAt
            FROM (
              SELECT 'DEFECT' AS Kind,d.defect_id AS SourceId,da.attachment_id
              FROM dbo.cleaning_rework_defects d WITH(HOLDLOCK)
              JOIN dbo.inspection_defect_attachments da WITH(HOLDLOCK) ON da.defect_id=d.defect_id WHERE d.rework_id={id}
              UNION
              SELECT 'RESULT',ir.result_id,ra.attachment_id FROM dbo.cleaning_rework_defects d WITH(HOLDLOCK)
              JOIN dbo.inspection_defects f WITH(HOLDLOCK) ON f.defect_id=d.defect_id
              JOIN dbo.inspection_results ir WITH(HOLDLOCK) ON ir.inspection_id=f.inspection_id AND ir.checklist_item_id=f.checklist_item_id
              JOIN dbo.inspection_result_attachments ra WITH(HOLDLOCK) ON ra.result_id=ir.result_id WHERE d.rework_id={id}
            ) l JOIN dbo.attachments a WITH(HOLDLOCK) ON a.attachment_id=l.attachment_id
            ORDER BY l.Kind,l.SourceId,a.attachment_id
            """).ToArrayAsync(ct);
        if (evidence.Length > 200 || evidence.Any(e => e.DeletedAt != null || e.EntityType != "INSPECTION" || e.EntityId != source.InspectionId)) return null;
        var digests = new List<ReworkAuthorityEvidence>();
        foreach (var e in evidence.DistinctBy(e => (e.Kind,e.SourceId,e.AttachmentId)))
        {
            var metadata = new HistoryEvidenceDto(e.AttachmentId,e.EntityType,e.EntityId,e.FileName,e.MimeType,e.Size,e.UploadedAt,"");
            var file = await HistoryFile([metadata],e.AttachmentId,evidenceRoot,ct);
            if (file == null) return null;
            digests.Add(new(e.Kind,e.SourceId,e.AttachmentId,e.StorageKey,e.FileName,e.MimeType,e.Size,e.UploadedBy,
                DateTime.SpecifyKind(e.UploadedAt,DateTimeKind.Utc),SHA256.HashData(file.Content)));
        }
        var evidenceDigest = ReworkAuthorityContract.EvidenceDigest(digests);
        // Include selected criterion/result facts as well as the existing FE04 canonical source digest.
        var canonical = await db.Database.SqlQuery<string>($"""
            SELECT (SELECT d.defect_id,d.inspection_id,d.checklist_item_id,d.description,d.severity_id,d.room_location,
              r.result_id,r.outcome,r.note,r.inspected_at,c.version_id,c.is_required,c.is_active
              FROM dbo.cleaning_rework_defects l WITH(HOLDLOCK)
              JOIN dbo.inspection_defects d WITH(HOLDLOCK) ON d.defect_id=l.defect_id
              JOIN dbo.inspection_checklist_items c WITH(HOLDLOCK) ON c.item_id=d.checklist_item_id
              JOIN dbo.inspection_results r WITH(HOLDLOCK) ON r.inspection_id=d.inspection_id AND r.checklist_item_id=d.checklist_item_id
              WHERE l.rework_id={id} ORDER BY d.defect_id,r.result_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value
            """).SingleAsync(ct);
        return new(new(source.ReworkId,source.InspectionId,source.CleaningTaskId,source.RoomId,source.BranchId,source.VersionId,source.InspectionRevision),
            source.CurrentCycleId,source.CurrentFailedSource,
            ReworkAuthorityContract.SourceDigest(canonical,await ReworkSourceDigestAsync(id,ct),evidenceDigest),evidenceDigest);
    }

    private void RequireAuthorityTransaction(DbConnection connection, DbTransaction transaction)
    {
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is not { } tx
            || !ReferenceEquals(connection,db.Database.GetDbConnection()) || !ReferenceEquals(transaction,tx.GetDbTransaction())
            || !ReferenceEquals(transaction.Connection,connection) || transaction.IsolationLevel != IsolationLevel.Serializable
            || System.Transactions.Transaction.Current != null)
            throw new InvalidOperationException("REWORK_AUTHORITY_CALLER_SERIALIZABLE_TRANSACTION_REQUIRED");
    }

    private async Task<ReworkAuthorityProof?> ReadTrustedReworkAuthorityAsync(long actor,long branch,long id,string? root,CancellationToken ct)
    {
        // Missing/old candidate schema is not authority. The original synthetic lane remains separate.
        if (await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE04.ReworkAuthority.V1'
                AND CONVERT(nvarchar(128),value)='FE05_COMMITTED_V1')
              AND COL_LENGTH('dbo.fs016_rework_source_proofs','writer_transaction_id')=8
              AND OBJECT_ID('dbo.fe04_rework_authority_issuers','U') IS NOT NULL
              AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_fe04_authority_origin'
                AND parent_object_id=OBJECT_ID('dbo.fs016_rework_source_proofs') AND is_disabled=0 AND is_not_trusted=0)
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.fs016_rework_source_proofs')
                AND name IN('FK_fe04_authority_issuer','FK_fe04_authority_operation') AND is_disabled=0 AND is_not_trusted=0)=2
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE name='TR_fe04_authority_immutable' AND is_disabled=0)
              AND HAS_PERMS_BY_NAME('dbo.fs016_rework_source_proofs','OBJECT','INSERT')=0
              AND HAS_PERMS_BY_NAME('dbo.fs016_rework_source_proofs','OBJECT','UPDATE')=0
              AND HAS_PERMS_BY_NAME('dbo.fs016_rework_source_proofs','OBJECT','DELETE')=0
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) != 1) return null;
        if (!(await ReworkAccess(actor,branch,id,"CLEANING_VIEW",ct)).IsGranted) return null;
        var proof = await db.Database.SqlQuery<AuthorityProofRow>($"""
            SELECT p.origin AS Origin,p.contract_version AS ContractVersion,p.issuer AS Issuer,p.issuer_version AS IssuerVersion,
              p.actor_user_id AS ActorUserId,p.operation_key AS OperationId,p.writer_transaction_id AS WriterTransactionId,
              p.rework_id AS ReworkId,p.inspection_id AS InspectionId,p.cleaning_task_id AS CleaningTaskId,p.room_id AS RoomId,
              p.branch_id AS BranchId,p.version_id AS VersionId,p.inspection_revision AS InspectionRevision,
              p.source_digest AS SourceDigest,p.evidence_digest AS EvidenceDigest,CURRENT_TRANSACTION_ID() AS ReaderTransactionId,
              CONVERT(bit,CASE WHEN x.is_active=1 AND x.source_commit LIKE REPLICATE('[0-9a-f]',40)
                AND m.execute_as_principal_id=-2 AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition))=x.module_digest
                THEN 1 ELSE 0 END) AS TrustedIssuer,
              CONVERT(bit,CASE WHEN o.completed=1 AND ISJSON(o.result_json)=1
                AND JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.contract')='FE05_REWORK_AUTHORITY_V1'
                AND JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.origin')=p.origin
                AND TRY_CONVERT(int,JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.version'))=p.contract_version
                AND JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.issuer') COLLATE Latin1_General_100_BIN2=p.issuer
                AND TRY_CONVERT(int,JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.issuerVersion'))=p.issuer_version
                AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.actorUserId'))=p.actor_user_id
                AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.operationId'))=p.operation_key
                AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.writerTransactionId'))=p.writer_transaction_id
                AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.reworkId'))=p.rework_id
                AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.inspectionId'))=p.inspection_id
                AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.cleaningTaskId'))=p.cleaning_task_id
                AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.roomId'))=p.room_id
                AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.branchId'))=p.branch_id
                AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.versionId'))=p.version_id
                AND JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.inspectionETag')=CHAR(34)+CONVERT(varchar(16),p.inspection_revision,2)+CHAR(34)
                AND JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.sourceDigest')=CONVERT(varchar(64),p.source_digest,2)
                AND JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.evidenceDigest')=CONVERT(varchar(64),p.evidence_digest,2)
                AND JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.conclusion')='FAILED' THEN 1 ELSE 0 END) AS ReceiptMatches,
              CONVERT(bit,CASE WHEN (SELECT COUNT(*) FROM dbo.audit_logs a WITH(HOLDLOCK)
                WHERE a.actor_user_id=p.actor_user_id AND a.branch_id=p.branch_id AND a.entity_type='cleaning_reworks'
                  AND TRY_CONVERT(bigint,a.entity_id)=p.rework_id
                  AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END,'$.operationId'))=p.operation_key
                  AND JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END,'$.authorityContract')='FE05_REWORK_AUTHORITY_V1'
                  AND JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END,'$.sourceDigest')=CONVERT(varchar(64),p.source_digest,2))=1
                THEN 1 ELSE 0 END) AS AuditMatches
            FROM dbo.fs016_rework_source_proofs p WITH(HOLDLOCK)
            LEFT JOIN dbo.fe04_rework_authority_issuers x WITH(HOLDLOCK) ON x.issuer=p.issuer AND x.version=p.issuer_version
            LEFT JOIN sys.sql_modules m ON m.object_id=x.writer_object_id
            LEFT JOIN dbo.notification_operation_reservations o WITH(HOLDLOCK)
              ON o.actor_user_id=p.actor_user_id AND o.operation_key=p.operation_key
            WHERE p.rework_id={id} AND p.origin='FE05_COMMITTED_V1'
            """).SingleOrDefaultAsync(ct);
        if (proof == null) return null;
        var tx = db.Database.CurrentTransaction!;
        var snapshot = await BuildReworkAuthoritySnapshotAsync(db.Database.GetDbConnection(),tx.GetDbTransaction(),id,root,ct);
        if (snapshot == null || snapshot.Binding.BranchId != branch) return null;
        var authority = new ReworkAuthorityProof(new(proof.ReworkId,proof.InspectionId,proof.CleaningTaskId,proof.RoomId,
            proof.BranchId,proof.VersionId,proof.InspectionRevision),proof.Origin,proof.ContractVersion,proof.Issuer,proof.IssuerVersion,
            proof.ActorUserId,proof.OperationId,proof.WriterTransactionId,proof.SourceDigest,proof.EvidenceDigest);
        var observed = new ReworkAuthorityObservation(snapshot.Binding,snapshot.CurrentCycleId,snapshot.CurrentFailedSource,true,
            proof.TrustedIssuer,proof.ReceiptMatches,proof.AuditMatches,proof.ReaderTransactionId,snapshot.SourceDigest,snapshot.EvidenceDigest);
        return ReworkAuthorityContract.Validate(authority,observed) == null ? authority : null;
    }
}
