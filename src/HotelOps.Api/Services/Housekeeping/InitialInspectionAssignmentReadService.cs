using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public Task<RoomTypeBatchResult> ReadInitialInspectionQueueAsync(SessionReference ticket, long sid, long branch, long? room, int page, int pageSize, CancellationToken ct)
    {
        if (branch <= 0 || room <= 0 || page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ValidationException("Invalid inspection queue scope or pagination.");
        return BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            if (!await HousekeepingSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var count = await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM dbo.cleaning_tasks t JOIN dbo.rooms r ON r.room_id=t.room_id
                JOIN dbo.floors f ON f.floor_id=r.floor_id LEFT JOIN dbo.areas a ON a.area_id=r.area_id
                JOIN dbo.room_cleaning_cycle_state c ON c.room_id=r.room_id AND c.cleaning_task_id=t.cleaning_task_id
                WHERE r.branch_id={branch} AND ({room} IS NULL OR r.room_id={room}) AND r.is_active=1 AND f.is_active=1
                  AND (r.area_id IS NULL OR a.is_active=1) AND t.workflow_status='PENDING_INSPECTION' AND t.status='IN_PROGRESS'
                """).SingleAsync(token);
            var rows = await db.Database.SqlQuery<InitialInspectionQueueRow>($"""
                SELECT t.cleaning_task_id AS TaskId,r.room_id AS RoomId,r.room_number AS RoomNumber,t.created_at AS CreatedAt
                FROM dbo.cleaning_tasks t JOIN dbo.rooms r ON r.room_id=t.room_id
                JOIN dbo.floors f ON f.floor_id=r.floor_id LEFT JOIN dbo.areas a ON a.area_id=r.area_id
                JOIN dbo.room_cleaning_cycle_state c ON c.room_id=r.room_id AND c.cleaning_task_id=t.cleaning_task_id
                WHERE r.branch_id={branch} AND ({room} IS NULL OR r.room_id={room}) AND r.is_active=1 AND f.is_active=1
                  AND (r.area_id IS NULL OR a.is_active=1) AND t.workflow_status='PENDING_INSPECTION' AND t.status='IN_PROGRESS'
                ORDER BY t.created_at DESC,t.cleaning_task_id DESC OFFSET {(page - 1) * pageSize} ROWS FETCH NEXT {pageSize} ROWS ONLY
                """).ToArrayAsync(token);
            return new(RoomTypeBatchStatus.Success, new InitialInspectionQueue(rows, count, page, pageSize));
        }, ct, requireRoomTypeSchema: false, permissionCode: "INSPECTION_VIEW", eligibility: token => EligibleInspector(ticket.UserId, branch, token));
    }

    public Task<RoomTypeBatchResult> ReadInitialInspectionOptionsAsync(SessionReference ticket, long sid, long branch, long task, CancellationToken ct)
    {
        if (branch <= 0 || task <= 0) throw new ValidationException("Branch and cleaning task are required.");
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await HousekeepingSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var row = await db.CleaningTasks.AsNoTracking().Where(t => t.CleaningTaskId == task && t.Room.BranchId == branch
                && t.Room.IsActive && t.Room.Floor.IsActive && (t.Room.AreaId == null || t.Room.Area!.IsActive))
                .Select(t => new { t.RoomId, t.Room.RoomNumber, t.Room.RoomTypeId, t.Status }).SingleOrDefaultAsync(token);
            if (row == null) return new(RoomTypeBatchStatus.NotFound);
            var state = await CleaningState(task, token);
            if (state.State != "PENDING_INSPECTION" || row.Status != "IN_PROGRESS" || !await ReworkCurrentCycle(row.RoomId, task, token)
                || await db.CleaningReworks.AsNoTracking().AnyAsync(w => w.CleaningTaskId == task, token)) return new(RoomTypeBatchStatus.Inactive);
            var cycle = await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_cleaning_cycle_state WHERE room_id={row.RoomId} AND cleaning_task_id={task}").SingleAsync(token);
            var open = await db.RoomInspections.AsNoTracking().Where(i => i.CleaningTaskId == task && i.Status != "COMPLETED" && i.Status != "CANCELLED")
                .OrderBy(i => i.InspectionId).Select(i => new { i.InspectionId, i.InspectorUserId, i.VersionId }).Take(2).ToArrayAsync(token);
            if (open.Length > 1) return new(RoomTypeBatchStatus.Unavailable);
            // Existing work keeps the version assigned to it, even after checklist retirement.
            var version = open.Length == 1
                ? await db.InspectionChecklistVersions.AsNoTracking().Where(v => v.VersionId == open[0].VersionId)
                    .Select(v => new InitialChecklist(v.VersionId, v.Checklist.ChecklistName, v.VersionNo)).SingleAsync(token)
                : await CurrentInitialInspectionVersion(branch, row.RoomTypeId, token);
            var canAssign = open.Length == 0 && version != null && await InitialInspectionWriterAvailable(true, token)
                && (await permissions.EvaluateBranchAsync("INSPECTION_PERFORM", branch, token)).IsGranted;
            var inspectors = canAssign ? await EligibleInspectors(branch, clock.GetUtcNow().UtcDateTime)
                .Where(u => !db.CleaningAssignments.Any(a => a.CleaningTaskId == task && a.AssigneeUserId == u.UserId && a.AssignmentStatus != "CANCELLED")
                    && !db.CleaningProgressLogs.Any(p => p.CleaningTaskId == task && p.LoggedBy == u.UserId))
                .OrderBy(u => u.FullName).ThenBy(u => u.UserId).Select(u => new ReinspectionAssigneeDto(u.UserId, u.FullName)).ToArrayAsync(token) : [];
            return new(RoomTypeBatchStatus.Success, new InitialInspectionOptions(task, branch, row.RoomId, row.RoomNumber, WorkflowTag(state.Revision), WorkflowTag(cycle),
                version?.Id, version?.Name, version?.Number, open.SingleOrDefault()?.InspectionId, open.SingleOrDefault()?.InspectorUserId, inspectors,
                canAssign && inspectors.Length > 0, open.Length > 0 ? "ALREADY_ASSIGNED" : version == null ? "CHECKLIST_UNAVAILABLE"
                    : !canAssign ? "ASSIGNMENT_UNAVAILABLE" : inspectors.Length == 0 ? "NO_ELIGIBLE_INSPECTOR" : null), WorkflowTag(state.Revision));
        }, ct, permissionCode: "INSPECTION_VIEW", eligibility: token => EligibleInspector(ticket.UserId, branch, token));
    }

    private sealed record InitialChecklist(long Id, string Name, int Number);
    private async Task<InitialChecklist?> CurrentInitialInspectionVersion(long branch, long type, CancellationToken ct)
    {
        var checklist = await db.BranchRoomTypes.AsNoTracking().Where(t => t.RoomTypeId == type && t.IsActive && (t.BranchId == branch || t.BranchId == null))
            .Select(t => t.DefaultChecklistId).SingleOrDefaultAsync(ct);
        if (checklist == null || !await ChecklistEligible(checklist.Value, branch, type, ct)) return null;
        var now = clock.GetUtcNow().UtcDateTime;
        return await db.InspectionChecklistVersions.AsNoTracking().Where(v => v.ChecklistId == checklist && v.Status == "PUBLISHED"
            && (v.EffectiveAt == null || v.EffectiveAt <= now) && v.InspectionChecklistItems.Any(i => i.IsActive))
            .OrderByDescending(v => v.VersionNo).Select(v => new InitialChecklist(v.VersionId, v.Checklist.ChecklistName, v.VersionNo)).FirstOrDefaultAsync(ct);
    }
}
