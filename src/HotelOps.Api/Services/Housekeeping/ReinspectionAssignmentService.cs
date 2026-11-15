using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    // Unlike the generic cleaning-task assignment, this command names the exact
    // submitted rework. Its protected binding is the source for UC047.
    public Task<RoomTypeBatchResult> AssignReinspectionAsync(SessionReference ticket, long sid, long branch,
        long rework, string? tag, string? key, AssignInspectionRequest input, string? root, CancellationToken ct)
    {
        if (branch <= 0 || rework <= 0 || input == null || input.InspectorId <= 0 || !WorkflowStrongTag(tag))
            throw new ValidationException("A rework identity, inspector and strong ETag are required.");
        if (input.DueAt.Kind != DateTimeKind.Utc) throw new ValidationException("Due time must be UTC.");
        var reason = WorkflowReason(input.Reason);
        var operation = CleaningLifecycleRules.Operation(key!);
        var fingerprint = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            contract = ReinspectionAssignmentContract, actor = ticket.UserId, branch, rework,
            tag, input.InspectorId, input.DueAt, reason
        }, Json));
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await HousekeepingSchema.InstalledAsync(db, token) || !await ReinspectionWorkflowInstalled(token))
                return new(RoomTypeBatchStatus.SchemaRequired);
            var row = await db.CleaningReworks.AsNoTracking().Where(w => w.ReworkId == rework
                && w.CleaningTask.Room.BranchId == branch && w.CleaningTask.Room.IsActive
                && w.CleaningTask.Room.Floor.IsActive
                && (w.CleaningTask.Room.AreaId == null || w.CleaningTask.Room.Area!.IsActive)
                && w.Inspection.CleaningTaskId == w.CleaningTaskId && w.Inspection.RoomId == w.CleaningTask.RoomId)
                .Select(w => new { w.AssigneeUserId, w.CleaningTaskId, w.InspectionId,
                    w.Inspection.VersionId, w.CleaningTask.RoomId, w.CompletedAt }).SingleOrDefaultAsync(token);
            if (row == null) return new(RoomTypeBatchStatus.NotFound);
            var prior = await ReadReinspectionOperation<ReinspectionAssignmentResult>(actor, branch, rework,
                operation, fingerprint, ReinspectionAssignmentContract, token);
            if (prior.Exists) return prior.Value is { } acknowledged
                ? new(RoomTypeBatchStatus.Success, acknowledged, acknowledged.SourceETag)
                : LifecycleFailure(RoomTypeBatchStatus.Duplicate, "REINSPECTION_OPERATION_CONFLICT");
            if (await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations WITH(HOLDLOCK)
                WHERE actor_user_id={actor} AND operation_key={operation}
                """).SingleAsync(token) != 0)
                return LifecycleFailure(RoomTypeBatchStatus.Duplicate, "REINSPECTION_OPERATION_CONFLICT");
            var state = await ReworkState(rework, token);
            if (tag != WorkflowTag(state.Revision)) return new(RoomTypeBatchStatus.Stale);
            if (state.State != "PENDING_REINSPECTION" || row.CompletedAt != null
                || (await CleaningState(row.CleaningTaskId, token)).State != "REWORK_REQUIRED"
                || !await ReworkCurrentCycle(row.RoomId, row.CleaningTaskId, token))
                return new(RoomTypeBatchStatus.Inactive);
            WorkflowDue(input.DueAt, clock.GetUtcNow().UtcDateTime);
            if (row.AssigneeUserId is not { } executor) return new(RoomTypeBatchStatus.InvalidReference);
            var submission = await ReadCurrentReworkSubmission(executor, branch, rework, root, token);
            if (submission == null || submission.Entity.SourceETag != tag)
                return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "REWORK_SUBMISSION_UNPROVEN");
            if (await InspectorWorkedOnCycle(input.InspectorId, row.CleaningTaskId, token))
                return LifecycleFailure(RoomTypeBatchStatus.InvalidReference,"INSPECTOR_CLEANER_CONFLICT");
            if (await db.RoomInspections.AsNoTracking().AnyAsync(i => i.CleaningTaskId == row.CleaningTaskId
                && i.Status != "COMPLETED" && i.Status != "CANCELLED", token))
                return LifecycleFailure(RoomTypeBatchStatus.Duplicate,"INSPECTION_ALREADY_ASSIGNED");
            var now = clock.GetUtcNow().UtcDateTime;
            // Never remap original defects to a different version by matching labels.
            if (!await db.InspectionChecklistVersions.AsNoTracking().AnyAsync(v => v.VersionId == row.VersionId
                && v.Status == "PUBLISHED" && (v.EffectiveAt == null || v.EffectiveAt <= now)
                && v.InspectionChecklistItems.Any(c => c.IsActive), token))
                return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "ORIGINAL_CHECKLIST_VERSION_UNAVAILABLE");
            if (!await CheckReinspectionWriter("FE05.UC041", "dbo.fe05_bind_reinspection", true, token))
                return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "REINSPECTION_WRITER_UNAVAILABLE");
            var inspection = new RoomInspection
            {
                InspectionNo = "INSP-" + Guid.NewGuid().ToString("N"), RoomId = row.RoomId,
                CleaningTaskId = row.CleaningTaskId, VersionId = row.VersionId,
                InspectorUserId = input.InspectorId, Status = "PENDING", CreatedAt = now, SummaryNote = reason
            };
            db.Add(inspection);
            await db.SaveChangesAsync(token);
            db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.room_inspections SET workflow_status='ASSIGNED',due_at={input.DueAt}
                WHERE inspection_id={inspection.InspectionId};
                EXEC dbo.fe05_bind_reinspection @actor={actor},@branch={branch},@rework={rework},
                  @inspection={inspection.InspectionId},@operation={operation},@fingerprint={fingerprint},@submission={submission.OperationId};
                """, token);
            var result = await ReadWrittenReinspectionResult<ReinspectionAssignmentResult>(actor, operation, token);
            if (!ValidReinspectionAssignmentReceipt(result, branch, inspection.InspectionId, rework, operation))
                throw new InvalidOperationException("REINSPECTION_ASSIGNMENT_RECEIPT_INVALID");
            return new(RoomTypeBatchStatus.Success, result, result.SourceETag);
        }, ct, requireRoomTypeSchema: false, permissionCode: "INSPECTION_PERFORM",
            additionalUsers: [input.InspectorId],
            eligibility: async token => await EligibleInspector(ticket.UserId, branch, token)
                && await EligibleInspector(input.InspectorId, branch, token));
    }

    private async Task<bool> InspectorWorkedOnCycle(long inspector, long task, CancellationToken ct)
        => await db.CleaningAssignments.AsNoTracking().AnyAsync(a => a.CleaningTaskId == task
                && a.AssigneeUserId == inspector && a.AssignmentStatus != "CANCELLED", ct)
            || await db.CleaningProgressLogs.AsNoTracking().AnyAsync(p => p.CleaningTaskId == task && p.LoggedBy == inspector, ct)
            || await db.CleaningReworks.AsNoTracking().AnyAsync(w => w.CleaningTaskId == task && w.AssigneeUserId == inspector, ct);
}
