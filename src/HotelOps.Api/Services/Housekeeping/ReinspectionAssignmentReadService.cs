using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public Task<RoomTypeBatchResult> ReadReinspectionAssignmentAsync(SessionReference ticket, long sid, long branch,
        long rework, string? root, CancellationToken ct)
    {
        if (branch <= 0 || rework <= 0) throw new ValidationException("Branch and rework are required.");
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await HousekeepingSchema.InstalledAsync(db, token) || !await ReinspectionWorkflowInstalled(token))
                return new(RoomTypeBatchStatus.SchemaRequired);
            var row = await db.CleaningReworks.AsNoTracking().Where(w => w.ReworkId == rework
                && w.CleaningTask.Room.BranchId == branch && w.CleaningTask.Room.IsActive && w.CleaningTask.Room.Floor.IsActive
                && (w.CleaningTask.Room.AreaId == null || w.CleaningTask.Room.Area!.IsActive)
                && w.Inspection.CleaningTaskId == w.CleaningTaskId && w.Inspection.RoomId == w.CleaningTask.RoomId)
                .Select(w => new { w.AssigneeUserId, w.CleaningTaskId, w.InspectionId, w.Inspection.VersionId, w.CleaningTask.RoomId,
                    w.CleaningTask.Room.RoomNumber, w.Inspection.Version.Checklist.ChecklistName, w.Inspection.Version.VersionNo }).SingleOrDefaultAsync(token);
            if (row?.AssigneeUserId is not { } executor) return new(RoomTypeBatchStatus.NotFound);
            var state = await ReworkState(rework, token);
            if (state.State != "PENDING_REINSPECTION" || !await ReworkCurrentCycle(row.RoomId, row.CleaningTaskId, token))
                return new(RoomTypeBatchStatus.Inactive);
            var submission = await ReadCurrentReworkSubmission(executor, branch, rework, root, token);
            if (submission == null || submission.Entity.SourceETag != WorkflowTag(state.Revision))
                return new(RoomTypeBatchStatus.Unavailable);
            var open = await db.RoomInspections.AsNoTracking().Where(i => i.CleaningTaskId == row.CleaningTaskId
                && i.Status != "COMPLETED" && i.Status != "CANCELLED").OrderBy(i => i.InspectionId)
                .Select(i => i.InspectionId).Take(2).ToArrayAsync(token);
            if (open.Length > 1) return new(RoomTypeBatchStatus.Unavailable);
            long? existing = open.Length == 1 ? open[0] : null;
            if (existing != null && (!await CheckReinspectionWriter("FE05.UC041", "dbo.fe05_bind_reinspection", false, token)
                || await db.Database.SqlQuery<int>($"""
                    SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_bindings_v1 b WITH(HOLDLOCK)
                    JOIN dbo.room_cleaning_cycle_state c ON c.room_id=b.room_id AND c.cleaning_task_id=b.cleaning_task_id AND c.row_version=b.cycle_revision
                    JOIN dbo.fe05_reinspection_responsibilities_v1 authority ON authority.inspection_id=b.inspection_id
                    JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id AND i.inspector_user_id=authority.inspector_user_id
                      AND i.cleaning_task_id=b.cleaning_task_id AND i.room_id=b.room_id AND i.version_id=b.version_id
                    WHERE b.inspection_id={existing} AND b.rework_id={rework} AND b.branch_id={branch}
                      AND b.submission_actor={executor} AND b.submission_operation={submission.OperationId}
                      AND b.rework_revision={state.Revision}
                    """).SingleAsync(token) != 1)) return new(RoomTypeBatchStatus.Unavailable);
            var now = clock.GetUtcNow().UtcDateTime;
            var published = await db.InspectionChecklistVersions.AsNoTracking().AnyAsync(v => v.VersionId == row.VersionId
                && v.Status == "PUBLISHED" && (v.EffectiveAt == null || v.EffectiveAt <= now)
                && v.InspectionChecklistItems.Any(c => c.IsActive), token);
            var canAssign = existing == null && published && await EligibleInspector(actor, branch, token)
                && (await permissions.EvaluateBranchAsync("INSPECTION_PERFORM", branch, token)).IsGranted
                && await CheckReinspectionWriter("FE05.UC041", "dbo.fe05_bind_reinspection", true, token);
            var inspectors = canAssign ? await EligibleInspectors(branch, now)
                .Where(u => !db.CleaningAssignments.Any(a => a.CleaningTaskId == row.CleaningTaskId && a.AssigneeUserId == u.UserId && a.AssignmentStatus != "CANCELLED")
                    && !db.CleaningProgressLogs.Any(p => p.CleaningTaskId == row.CleaningTaskId && p.LoggedBy == u.UserId)
                    && !db.CleaningReworks.Any(w => w.CleaningTaskId == row.CleaningTaskId && w.AssigneeUserId == u.UserId))
                .OrderBy(u => u.FullName).ThenBy(u => u.UserId).Select(u => new ReinspectionAssigneeDto(u.UserId, u.FullName)).ToArrayAsync(token) : [];
            return new(RoomTypeBatchStatus.Success, new ReinspectionAssignmentOptionsDto(rework, branch, row.RoomId, row.CleaningTaskId,
                row.InspectionId, row.VersionId, WorkflowTag(state.Revision), existing, inspectors, row.RoomNumber,
                row.ChecklistName, row.VersionNo, canAssign && inspectors.Length > 0,
                existing != null ? "ALREADY_ASSIGNED" : !published ? "CHECKLIST_UNAVAILABLE" : !canAssign ? "ASSIGNMENT_UNAVAILABLE"
                    : inspectors.Length == 0 ? "NO_ELIGIBLE_INSPECTOR" : null), WorkflowTag(state.Revision));
        }, ct, requireRoomTypeSchema: false, permissionCode: "INSPECTION_VIEW",
            eligibility: async token => (await InspectionFailureRecipients.SupervisorsAsync(db, branch, clock.GetUtcNow().UtcDateTime, token)).Contains(ticket.UserId));
    }
}
