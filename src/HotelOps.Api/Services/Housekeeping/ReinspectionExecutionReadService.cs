using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Rooms;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private sealed class ReinspectionReadBinding
    {
        public long ReworkId { get; set; }
        public long OriginalInspectionId { get; set; }
        public long CleaningTaskId { get; set; }
        public long RoomId { get; set; }
        public long VersionId { get; set; }
        public long Executor { get; set; }
        public long BoundBy { get; set; }
        public Guid SubmissionOperation { get; set; }
        public byte[] Revision { get; set; } = [];
        public byte[] ReworkRevision { get; set; } = [];
        public byte[] CurrentReworkRevision { get; set; } = [];
        public string State { get; set; } = "";
    }
    private sealed class ReinspectionReadDefect
    {
        public long Id { get; set; }
        public string Description { get; set; } = "";
        public string Remediation { get; set; } = "";
        public bool? Fixed { get; set; }
        public string? Note { get; set; }
    }

    // Assigned inspector's current/historical read. Current role and scope still apply;
    // terminal receipts never authorize a new mutation or current room use.
    public Task<RoomTypeBatchResult> ReadReinspectionExecutionAsync(SessionReference ticket, long sid, long branch,
        long inspection, string? root, CancellationToken ct, long? attachmentId = null)
    {
        if (branch <= 0 || inspection <= 0 || attachmentId <= 0)
            throw new ValidationException("Invalid reinspection identifier.");
        return BatchAsync(ticket, sid, branch,
            (actor, token) => ReadReinspectionInCallerTransactionAsync(actor, branch, inspection, root, attachmentId, token, allowTerminal: true),
            ct, requireRoomTypeSchema: false, permissionCode: "INSPECTION_VIEW",
            eligibility: token => EligibleInspector(ticket.UserId, branch, token));
    }

    // The caller owns current authorization and the serializable transaction through commit.
    private async Task<RoomTypeBatchResult> ReadReinspectionInCallerTransactionAsync(long actor, long branch,
        long inspection, string? root, long? attachmentId, CancellationToken token, bool allowTerminal = false)
    {
            if (!await HousekeepingSchema.InstalledAsync(db, token) || !await ReinspectionWorkflowInstalled(token))
                return new(RoomTypeBatchStatus.SchemaRequired);
            var binding = await db.Database.SqlQuery<ReinspectionReadBinding>($"""
                SELECT b.rework_id AS ReworkId,b.original_inspection_id AS OriginalInspectionId,
                  b.cleaning_task_id AS CleaningTaskId,b.room_id AS RoomId,b.version_id AS VersionId,
                  b.submission_actor AS Executor,b.submission_operation AS SubmissionOperation,b.bound_by AS BoundBy,
                  b.row_version AS Revision,b.rework_revision AS ReworkRevision,w.row_version AS CurrentReworkRevision,
                  i.workflow_status AS State
                FROM dbo.fe05_reinspection_bindings_v1 b WITH(HOLDLOCK)
                JOIN dbo.fe05_reinspection_responsibilities_v1 authority ON authority.inspection_id=b.inspection_id
                JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id AND i.inspector_user_id=authority.inspector_user_id
                  AND i.cleaning_task_id=b.cleaning_task_id AND i.room_id=b.room_id AND i.version_id=b.version_id
                JOIN dbo.cleaning_reworks w ON w.rework_id=b.rework_id AND w.inspection_id=b.original_inspection_id
                  AND w.cleaning_task_id=b.cleaning_task_id AND w.assignee_user_id=b.submission_actor
                JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=b.cleaning_task_id AND t.room_id=b.room_id
                JOIN dbo.room_cleaning_cycle_state c ON c.room_id=b.room_id
                JOIN dbo.rooms r ON r.room_id=b.room_id AND r.branch_id=b.branch_id AND r.is_active=1
                JOIN dbo.floors f ON f.floor_id=r.floor_id AND f.is_active=1
                LEFT JOIN dbo.areas a ON a.area_id=r.area_id
                WHERE b.inspection_id={inspection} AND b.branch_id={branch} AND authority.inspector_user_id={actor}
                  AND b.submission_actor<>{actor} AND (r.area_id IS NULL OR a.is_active=1)
                  AND ((i.workflow_status='ASSIGNED' AND i.status='PENDING' AND i.result IS NULL
                    AND w.row_version=b.rework_revision AND w.workflow_status='PENDING_REINSPECTION'
                    AND w.status='IN_PROGRESS' AND w.completed_at IS NULL
                    AND t.workflow_status='REWORK_REQUIRED' AND t.status='IN_PROGRESS'
                    AND c.cleaning_task_id=b.cleaning_task_id AND c.row_version=b.cycle_revision)
                  OR ({allowTerminal}=1 AND i.status='COMPLETED' AND i.inspected_at IS NOT NULL
                    AND w.status='COMPLETED' AND w.completed_at=i.inspected_at
                    AND ((i.workflow_status='PASSED' AND i.result='PASS' AND w.workflow_status='COMPLETED')
                      OR(i.workflow_status='FAILED' AND i.result='FAIL' AND w.workflow_status='FAILED'))))
                """).SingleOrDefaultAsync(token);
            if (binding == null) return new(RoomTypeBatchStatus.NotFound);
            var terminal = binding.State is "PASSED" or "FAILED";
            if (await InspectorWorkedOnCycle(actor, binding.CleaningTaskId, token)
                || (!terminal && await db.RoomInspections.CountAsync(i => i.CleaningTaskId == binding.CleaningTaskId
                    && i.Status != "COMPLETED" && i.Status != "CANCELLED", token) != 1))
                return new(RoomTypeBatchStatus.Unavailable);
            var submission = await ReadCurrentReworkSubmission(binding.Executor, branch, binding.ReworkId, root, token);
            if (submission == null || submission.OperationId != binding.SubmissionOperation
                || submission.Entity.SourceETag != WorkflowTag(binding.ReworkRevision))
                return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "REWORK_SUBMISSION_UNPROVEN");
            var assignments = await db.Database.SqlQuery<SubmissionLedgerRow>($"""
                SELECT TOP(2) result_json AS Json,fingerprint AS Fingerprint,operation_key AS OperationId
                FROM dbo.notification_operation_reservations WITH(HOLDLOCK)
                WHERE actor_user_id={binding.BoundBy} AND completed=1 AND fingerprint_version=1
                  AND JSON_VALUE(CASE WHEN ISJSON(result_json)=1 THEN result_json ELSE N'[]' END,'$.contract')={ReinspectionAssignmentContract}
                  AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(result_json)=1 THEN result_json ELSE N'[]' END,'$.inspectionId'))={inspection}
                ORDER BY operation_key
                """).ToArrayAsync(token);
            if (assignments.Length != 1) return new(RoomTypeBatchStatus.Unavailable);
            var assignment = await ReadReinspectionOperation<ReinspectionAssignmentResult>(binding.BoundBy, branch,
                binding.ReworkId, assignments[0].OperationId, assignments[0].Fingerprint, ReinspectionAssignmentContract, token);
            if (assignment.Value?.Id != inspection) return new(RoomTypeBatchStatus.Unavailable);
            var state = await InspectionState(inspection, token);
            ReinspectionConclusionResult? conclusion = null;
            if (terminal)
            {
                var receipts = await db.Database.SqlQuery<SubmissionLedgerRow>($"""
                    SELECT TOP(2) result_json AS Json,fingerprint AS Fingerprint,operation_key AS OperationId
                    FROM dbo.notification_operation_reservations WITH(HOLDLOCK)
                    WHERE actor_user_id={actor} AND completed=1 AND fingerprint_version=1
                      AND JSON_VALUE(CASE WHEN ISJSON(result_json)=1 THEN result_json ELSE N'[]' END,'$.contract')={ReinspectionConclusionContract}
                      AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(result_json)=1 THEN result_json ELSE N'[]' END,'$.inspectionId'))={inspection}
                    ORDER BY operation_key
                    """).ToArrayAsync(token);
                if (receipts.Length != 1) return new(RoomTypeBatchStatus.Unavailable);
                conclusion = (await ReadReinspectionOperation<ReinspectionConclusionResult>(actor, branch, inspection,
                    receipts[0].OperationId, receipts[0].Fingerprint, ReinspectionConclusionContract, token)).Value;
                if (conclusion == null || conclusion.State != binding.State || conclusion.RoomId != binding.RoomId
                    || conclusion.CleaningTaskId != binding.CleaningTaskId || conclusion.ReworkId != binding.ReworkId
                    || conclusion.SourceETag != WorkflowTag(state.Revision)
                    || conclusion.ReworkETag != WorkflowTag(binding.CurrentReworkRevision)) return new(RoomTypeBatchStatus.Unavailable);
                if (await db.Database.SqlQuery<int>($"""
                    SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_defect_results_v1
                    WHERE inspection_id={inspection} AND (fixed IS NULL OR recorded_by IS NULL OR recorded_by<>{actor} OR recorded_at IS NULL)
                    """).SingleAsync(token) != 0) return new(RoomTypeBatchStatus.Unavailable);
            }
            var defects = await db.Database.SqlQuery<ReinspectionReadDefect>($"""
                SELECT d.defect_id AS Id,d.description AS Description,w.remediation_note AS Remediation,x.fixed AS Fixed,x.note AS Note
                FROM dbo.cleaning_rework_defects w
                JOIN dbo.inspection_defects d ON d.defect_id=w.defect_id AND d.inspection_id={binding.OriginalInspectionId}
                JOIN dbo.inspection_checklist_items item ON item.item_id=d.checklist_item_id AND item.version_id={binding.VersionId}
                JOIN dbo.fe05_reinspection_defect_results_v1 x ON x.rework_id=w.rework_id AND x.defect_id=w.defect_id AND x.inspection_id={inspection}
                WHERE w.rework_id={binding.ReworkId} AND w.remediation_note IS NOT NULL
                  AND EXISTS(SELECT 1 FROM dbo.inspection_results r WHERE r.inspection_id=d.inspection_id
                    AND r.checklist_item_id=d.checklist_item_id AND r.outcome='FAIL')
                """).OrderBy(d => d.Id).ToArrayAsync(token);
            var selected = await db.Database.SqlQuery<long>($"SELECT defect_id AS Value FROM dbo.cleaning_rework_defects WHERE rework_id={binding.ReworkId}").ToArrayAsync(token);
            var resultIds = await db.Database.SqlQuery<long>($"SELECT defect_id AS Value FROM dbo.fe05_reinspection_defect_results_v1 WHERE inspection_id={inspection}").ToArrayAsync(token);
            if (selected.Length is < 1 or > 200 || !selected.Order().SequenceEqual(defects.Select(d => d.Id))
                || !selected.Order().SequenceEqual(resultIds.Order())) return new(RoomTypeBatchStatus.Unavailable);
            if (terminal && (binding.State == "PASSED" ? defects.Any(d => d.Fixed != true)
                : !defects.Any(d => d.Fixed == false))) return new(RoomTypeBatchStatus.Unavailable);
            var output = new List<ReinspectionDefectDto>();
            foreach (var defect in defects)
            {
                var originalPhotos = await db.Database.SqlQuery<long>($"""
                    SELECT l.attachment_id AS Value FROM dbo.inspection_defect_attachments l JOIN dbo.attachments a ON a.attachment_id=l.attachment_id
                    WHERE l.defect_id={defect.Id} AND a.deleted_at IS NULL AND a.entity_type='INSPECTION' AND a.entity_id={binding.OriginalInspectionId}
                    """).ToArrayAsync(token);
                var remediationPhotos = await db.Database.SqlQuery<long>($"""
                    SELECT attachment_id AS Value FROM dbo.fs016_rework_evidence WHERE rework_id={binding.ReworkId} AND defect_id={defect.Id}
                    """).ToArrayAsync(token);
                var photos = await db.Database.SqlQuery<long>($"""
                    SELECT l.attachment_id AS Value FROM dbo.fe05_reinspection_defect_attachments_v1 l JOIN dbo.attachments a ON a.attachment_id=l.attachment_id
                    WHERE l.inspection_id={inspection} AND l.defect_id={defect.Id} AND a.deleted_at IS NULL
                      AND a.entity_type='INSPECTION' AND a.entity_id={inspection} AND a.uploaded_by={actor}
                    """).ToArrayAsync(token);
                output.Add(new(defect.Id, defect.Description, defect.Remediation, defect.Fixed, defect.Note,
                    originalPhotos, remediationPhotos, photos));
            }
            if (attachmentId is { } file)
            {
                var allowed = output.SelectMany(d => d.OriginalPhotos.Concat(d.RemediationPhotos).Concat(d.ReinspectionPhotos)).ToArray();
                var metadata = await db.Attachments.AsNoTracking().Where(a => a.AttachmentId == file && allowed.Contains(a.AttachmentId) && a.DeletedAt == null)
                    .Select(a => new HotelOps.Api.DTOs.HotelStructure.HistoryEvidenceDto(a.AttachmentId, a.EntityType, a.EntityId,
                        a.OriginalFileName ?? a.FileName, a.MimeType, a.FileSizeBytes, a.UploadedAt, "")).ToArrayAsync(token);
                var download = await HistoryFile(metadata, file, root, token);
                return download == null ? new(RoomTypeBatchStatus.NotFound) : new(RoomTypeBatchStatus.Success, download);
            }
            if (state.State != binding.State) return new(RoomTypeBatchStatus.Unavailable);
            var readiness = (await new EffectiveRoomReadiness(db).ReadAsync([binding.RoomId], clock.GetUtcNow().UtcDateTime, token))[binding.RoomId];
            return new(RoomTypeBatchStatus.Success, new ReinspectionExecutionDto(inspection, binding.ReworkId, branch,
                binding.RoomId, binding.CleaningTaskId, binding.OriginalInspectionId, actor, binding.VersionId, state.State,
                WorkflowTag(state.Revision), WorkflowTag(binding.CurrentReworkRevision), WorkflowTag(binding.Revision), output,
                readiness.Readiness, readiness.Readiness == "BLOCKED" ? "MSG17" : null, conclusion), WorkflowTag(state.Revision));
    }
}
