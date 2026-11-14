using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

internal sealed record ReinspectionStoredCandidate(string InspectionETag,string BindingETag,string ReworkETag,IReadOnlyList<ReinspectionStoredDecision> Decisions)
{internal bool AuthoritativeOrCommitted=>false;}
internal sealed record ReinspectionStoredDecision(long DefectId,bool? Fixed,long? RecordedBy,DateTime? RecordedAt);
public sealed partial class RoomTypeCreationService
{
    private async Task RequireReinspectionComponentSchemaAsync(CancellationToken ct)
    {
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is not { } transaction
            || transaction.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.Serializable
            || System.Transactions.Transaction.Current != null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("REINSPECTION_CALLER_SERIALIZABLE_TRANSACTION_REQUIRED");
        // This integration is admitted only on the independently verified sprint disposable target.
        // The original candidate install script remains unchanged; no public writer is opened.
        if(await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN DB_NAME()='HotelOps_DEV3_142A_20261114_cc78c13f'
            AND CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita'
            AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
                AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
            AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run'
                AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
            AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE05.ReinspectionCandidate'
                AND CONVERT(nvarchar(128),value)='FAST014_COMPONENT_NOT_ACCEPTED')
            AND OBJECT_ID('dbo.fe05_reinspection_bindings_v1','U') IS NOT NULL
            AND OBJECT_ID('dbo.fe05_reinspection_defect_results_v1','U') IS NOT NULL
            AND (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_fe05_reinspection_binding_guard_v1',
                'TR_fe05_reinspection_result_revision_v1','TR_fe05_trial_remediation_revision') AND is_disabled=0)=3
            THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)!=1)
            throw new InvalidOperationException("REINSPECTION_ISOLATED_COMPONENT_SCHEMA_REQUIRED");
    }
    // COMPONENT candidate only. Does not implement an accepted UC041 responsibility setter.
    // Explicit exact identities, no latest lookup/backfill; original and assigned versions remain separate.
    internal async Task<ReinspectionStoredCandidate> BindReinspectionComponentInCallerTransactionAsync(SessionReference ticket,long sid,long branch,
        long inspection,long rework,string inspectionTag,string reworkTag,CancellationToken ct)
    {
        await RequireReinspectionComponentSchemaAsync(ct);
        var source=await ReadReinspectionCandidateInCallerTransactionAsync(ticket,sid,branch,inspection,rework,inspectionTag,reworkTag,ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.fe05_reinspection_bindings_v1(inspection_id,rework_id,original_inspection_id,cleaning_task_id,room_id,branch_id,
              cycle_revision,original_version_id,assigned_version_id,rework_revision,inspection_revision,reinspector_user_id,bound_by,bound_at,touched_at)
            SELECT i.inspection_id,{rework},{source.OriginalInspectionId},i.cleaning_task_id,i.room_id,{branch},c.row_version,
              {source.OriginalChecklistVersionId},{source.AssignedChecklistVersionId},{Convert.FromHexString(reworkTag.Trim('"'))},i.row_version,
              {ticket.UserId},{ticket.UserId},SYSUTCDATETIME(),SYSUTCDATETIME()
            FROM dbo.room_inspections i JOIN dbo.room_cleaning_cycle_state c ON c.room_id=i.room_id AND c.cleaning_task_id=i.cleaning_task_id
            WHERE i.inspection_id={inspection} AND i.row_version={Convert.FromHexString(inspectionTag.Trim('"'))};
            IF @@ROWCOUNT<>1 THROW 51997,'Candidate binding source changed',1;
            INSERT dbo.fe05_reinspection_defect_results_v1(inspection_id,rework_id,defect_id)
            SELECT {inspection},{rework},defect_id FROM dbo.cleaning_rework_defects WHERE rework_id={rework};
            """,ct);
        await CandidateAudit("CREATE",ticket.UserId,branch,inspection,JsonSerializer.Serialize(new{reworkId=rework,componentCandidate=true}),ct);
        return await ReadStoredReinspectionAsync(inspection,rework,ct);
    }
    internal async Task<ReinspectionStoredCandidate> SaveReinspectionComponentInCallerTransactionAsync(SessionReference ticket,long sid,long branch,
        long inspection,long rework,string inspectionTag,string reworkTag,string bindingTag,IReadOnlyList<ReinspectionDefectDecision> decisions,CancellationToken ct)
    {
        await RequireReinspectionComponentSchemaAsync(ct);
        if(!WorkflowStrongTag(bindingTag))throw new InvalidOperationException("REINSPECTION_BINDING_REVISION_INVALID");
        var source=await ReadReinspectionCandidateInCallerTransactionAsync(ticket,sid,branch,inspection,rework,inspectionTag,reworkTag,ct);
        var validation=ReinspectionDecisionValidator.Validate(source.Defects.Select(d=>d.DefectId).ToArray(),decisions,
            Convert.FromHexString(source.ReworkRevision.Trim('"')),reworkTag,Convert.FromHexString(source.InspectionRevision.Trim('"')),inspectionTag);
        if(!validation.IsComplete)throw new InvalidOperationException("REINSPECTION_EXACT_COMPLETE_DECISIONS_REQUIRED");
        // Lock parent before child rows; revision is aggregate invalidated by every result mutation.
        var binding=await db.Database.SqlQuery<long>($"""
            SELECT b.inspection_id AS Value FROM dbo.fe05_reinspection_bindings_v1 b WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.room_cleaning_cycle_state c WITH(HOLDLOCK) ON c.room_id=b.room_id AND c.cleaning_task_id=b.cleaning_task_id
            WHERE b.inspection_id={inspection} AND b.rework_id={rework} AND b.branch_id={branch} AND b.reinspector_user_id={ticket.UserId}
              AND b.original_inspection_id={source.OriginalInspectionId} AND b.assigned_version_id={source.AssignedChecklistVersionId}
              AND b.original_version_id={source.OriginalChecklistVersionId} AND b.cycle_revision=c.row_version
              AND b.rework_revision={Convert.FromHexString(reworkTag.Trim('"'))} AND b.row_version={Convert.FromHexString(bindingTag.Trim('"'))}
            """).SingleOrDefaultAsync(ct);
        if(binding!=inspection)throw new InvalidOperationException("REINSPECTION_BINDING_STALE_OR_WRONG_SOURCE");
        var encoded=JsonSerializer.Serialize(decisions.Select(d=>new{defectId=d.DefectId,fixedResult=d.Fixed}));
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE r SET fixed=j.fixed_result,recorded_by={ticket.UserId},recorded_at=SYSUTCDATETIME()
            FROM dbo.fe05_reinspection_defect_results_v1 r
            JOIN OPENJSON({encoded}) WITH(defect_id bigint '$.defectId',fixed_result bit '$.fixedResult') j ON j.defect_id=r.defect_id
            WHERE r.inspection_id={inspection} AND r.rework_id={rework};
            IF @@ROWCOUNT<>{decisions.Count} THROW 51997,'Candidate selected-result storage mismatch',1;
            """,ct);
        await CandidateAudit("UPDATE",ticket.UserId,branch,inspection,encoded,ct);
        return await ReadStoredReinspectionAsync(inspection,rework,ct);
    }
    private Task CandidateAudit(string action,long actor,long branch,long inspection,string payload,CancellationToken ct)
        => db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,new_values,occurred_at) VALUES({branch},{actor},'FE05_REINSPECTION_CANDIDATE',CONVERT(nvarchar(50),{inspection}),{action},{payload},SYSUTCDATETIME())",ct);
    private async Task<ReinspectionStoredCandidate> ReadStoredReinspectionAsync(long inspection,long rework,CancellationToken ct)
    {
        var revisions=await db.Database.SqlQuery<StoredReinspectionRevision>($"SELECT i.row_version AS InspectionRevision,b.row_version AS BindingRevision,w.row_version AS ReworkRevision FROM dbo.fe05_reinspection_bindings_v1 b JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id JOIN dbo.cleaning_reworks w ON w.rework_id=b.rework_id WHERE b.inspection_id={inspection} AND b.rework_id={rework}").SingleAsync(ct);
        var results=await db.Database.SqlQuery<StoredReinspectionResult>($"SELECT defect_id AS DefectId,fixed AS Fixed,recorded_by AS RecordedBy,recorded_at AS RecordedAt FROM dbo.fe05_reinspection_defect_results_v1 WHERE inspection_id={inspection} AND rework_id={rework} ORDER BY defect_id").ToArrayAsync(ct);
        return new(WorkflowTag(revisions.InspectionRevision),WorkflowTag(revisions.BindingRevision),WorkflowTag(revisions.ReworkRevision),results.Select(r=>new ReinspectionStoredDecision(r.DefectId,r.Fixed,r.RecordedBy,r.RecordedAt)).ToArray());
    }
    private sealed class StoredReinspectionRevision {public byte[] InspectionRevision{get;set;}=[];public byte[] BindingRevision{get;set;}=[];public byte[] ReworkRevision{get;set;}=[];}
    private sealed class StoredReinspectionResult {public long DefectId{get;set;}public bool? Fixed{get;set;}public long? RecordedBy{get;set;}public DateTime? RecordedAt{get;set;}}
}
