using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

// Internal uncommitted domain step. Not an issuer receipt, replay result or public writer.
internal sealed record InspectionApproveDomainResult(long InspectionId, string Revision)
{
    internal bool AuthoritativeCommitAccepted => false;
}

public sealed partial class RoomTypeCreationService
{
    internal async Task<InspectionApproveDomainResult> ApproveDomainInCallerTransactionAsync(
        SessionReference ticket, long sid, long branch, long inspection, string expectedETag, CancellationToken ct)
    {
        var loaded = await PrepareApproveInCallerTransactionAsync(ticket, sid, branch, inspection, expectedETag, ct);
        if (loaded.Status != RoomTypeBatchStatus.Success)
            throw new InvalidOperationException("FE05_APPROVE_SOURCE_" + loaded.Status);
        var source = loaded.Source!;
        var i = source.Domain.Inspection;
        var oldJson = "{\"workflow_status\":\"PENDING_CONCLUSION\"}";
        var newJson = "{\"workflow_status\":\"PASSED\",\"result\":\"PASS\"}";
        // Preflight owns authorization/SOD and retained source locks. SQL repeats the critical
        // persisted predicates; client snapshots and ETag comparison are not concurrency authority.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SET XACT_ABORT ON;
            IF NOT EXISTS(SELECT 1 FROM dbo.room_cleaning_cycle_state WITH(UPDLOCK,HOLDLOCK)
                WHERE room_id={i.RoomId} AND cleaning_task_id={i.CleaningTaskId})
                THROW 51990,'INSPECTION_CURRENT_CYCLE_REQUIRED',1;
            IF EXISTS(SELECT 1 FROM dbo.cleaning_reworks WITH(UPDLOCK,HOLDLOCK) WHERE cleaning_task_id={i.CleaningTaskId})
                THROW 51990,'REINSPECTION_CONTRACT_REQUIRED',1;
            IF EXISTS(SELECT 1 FROM dbo.inspection_checklist_items c WITH(HOLDLOCK)
                WHERE c.version_id={i.VersionId} AND c.is_active=1 AND c.is_required=1
                AND NOT EXISTS(SELECT 1 FROM dbo.inspection_results r WITH(HOLDLOCK)
                    WHERE r.inspection_id={inspection} AND r.checklist_item_id=c.item_id AND r.outcome='PASS'))
                THROW 51990,'MANDATORY_PASS_REQUIRED',1;
            UPDATE dbo.room_inspections SET workflow_status='PASSED',status='COMPLETED',result='PASS',inspected_at=SYSUTCDATETIME()
            WHERE inspection_id={inspection} AND inspector_user_id={ticket.UserId} AND room_id={i.RoomId}
                AND cleaning_task_id={i.CleaningTaskId} AND version_id={i.VersionId} AND row_version={source.Domain.Revision}
                AND workflow_status='PENDING_CONCLUSION' AND status='IN_PROGRESS' AND result IS NULL;
            IF @@ROWCOUNT<>1 THROW 51990,'INSPECTION_WRITE_CONFLICT',1;
            UPDATE dbo.cleaning_tasks SET workflow_status='COMPLETED',status='COMPLETED'
            WHERE cleaning_task_id={i.CleaningTaskId} AND room_id={i.RoomId}
                AND workflow_status='PENDING_INSPECTION' AND status='IN_PROGRESS';
            IF @@ROWCOUNT<>1 THROW 51990,'CLEANING_PHASE_CONFLICT',1;
            INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,old_values,new_values,occurred_at)
            VALUES({branch},{ticket.UserId},'INSPECTION',CONVERT(nvarchar(50),{inspection}),'UPDATE',
                {oldJson},{newJson},SYSUTCDATETIME());
            """, ct);
        var revision = await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={inspection}").SingleAsync(ct);
        return new(inspection, WorkflowTag(revision));
    }
}
