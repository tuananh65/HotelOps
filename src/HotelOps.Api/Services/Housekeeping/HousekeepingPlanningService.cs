using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

// Reuses the existing feature transaction/SQL session boundary without new shared wiring.
public sealed partial class RoomTypeCreationService
{
    public Task<RoomTypeBatchResult> HousekeepingPlanningAsync(SessionReference ticket, long sid, long branch,
        long taskId, bool inspection, HousekeepingPlanningQuery input, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var query = input with { Search = input.Search?.Trim(), From = input.From ?? now.Date, To = input.To ?? now.Date.AddDays(1) };
        if (taskId <= 0 || query.Page < 1 || query.PageSize is < 1 or > 100 || query.Page > int.MaxValue / query.PageSize
            || query.Search?.Length > 150 || query.Search?.Any(char.IsControl) == true
            || query.From!.Value.Kind != DateTimeKind.Utc || query.To!.Value.Kind != DateTimeKind.Utc
            || query.To <= query.From || query.To - query.From > TimeSpan.FromDays(2))
            throw new ValidationException("Invalid planning filters; use an explicit UTC window of at most two days.");
        var permission = inspection ? "INSPECTION_VIEW" : "CLEANING_ASSIGN";
        return BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            var roomQuery = await HistoryRooms(token);
            if (roomQuery == null) return new(RoomTypeBatchStatus.SchemaRequired);
            var roomIds = roomQuery.Where(r => r.BranchId == branch).Select(r => r.RoomId);
            var task = await db.CleaningTasks.AsNoTracking().Where(t => t.CleaningTaskId == taskId && roomIds.Contains(t.RoomId))
                .Select(t => new PlanningTaskDto(t.CleaningTaskId, t.TaskNo, t.RoomId, t.Room.RoomNumber, t.Room.BranchId, t.Status, t.DueAt))
                .SingleOrDefaultAsync(token);
            if (task == null) return new(RoomTypeBatchStatus.NotFound);
            var active = await roomQuery.AnyAsync(r => r.RoomId == task.RoomId && r.IsActive && r.Floor.IsActive && (r.AreaId == null || r.Area!.IsActive), token);
            if (!active) return new(RoomTypeBatchStatus.Inactive);
            var role = inspection ? "HK_SUPERVISOR" : "HK_STAFF";
            var candidates = db.Users.AsNoTracking().Where(u => u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE" && u.DeletedAt == null
                && u.UserBranchAccessUsers.Any(a => a.BranchId == branch && a.RevokedAt == null && a.GrantedAt <= now)
                && db.UserRoles.Any(r => r.UserId == u.UserId && r.BranchId == branch && r.IsActive && r.Role.IsActive && r.Role.RoleCode == role
                    && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)));
            if (!inspection) candidates = candidates.Where(u => u.Department != null && u.Department.IsActive && u.Department.BranchId == branch && u.Department.DepartmentCode == "HK");
            else candidates = candidates.Where(u =>
                !db.CleaningAssignments.Any(a => a.CleaningTaskId == taskId && a.AssigneeUserId == u.UserId && a.AssignmentStatus != "CANCELLED")
                && !db.CleaningProgressLogs.Any(p => p.CleaningTaskId == taskId && p.LoggedBy == u.UserId)
                && !db.CleaningReworks.Any(r => r.CleaningTaskId == taskId && r.AssigneeUserId == u.UserId));
            if (!string.IsNullOrEmpty(query.Search)) candidates = candidates.Where(u => u.FullName.Contains(query.Search));
            var total = await candidates.CountAsync(token);
            var page = await candidates.OrderBy(u => u.FullName).ThenBy(u => u.UserId).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
                .Select(u => new PlanningStaffDto(u.UserId, u.FullName,
                    u.Department != null && u.Department.BranchId == branch ? u.DepartmentId : null,
                    db.CleaningAssignments.Where(a => a.AssigneeUserId == u.UserId && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null
                        && roomIds.Contains(a.CleaningTask.RoomId) && a.CleaningTask.Status != "COMPLETED" && a.CleaningTask.Status != "CANCELLED").Select(a => a.CleaningTaskId).Distinct().Count(),
                    db.CleaningAssignments.Where(a => a.AssigneeUserId == u.UserId && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null
                        && roomIds.Contains(a.CleaningTask.RoomId) && a.CleaningTask.Status != "COMPLETED" && a.CleaningTask.Status != "CANCELLED"
                        && a.CleaningTask.CreatedAt >= query.From && a.CleaningTask.CreatedAt < query.To).Select(a => a.CleaningTaskId).Distinct().Count()))
                .TagWith("Task73:PlanningPage").ToArrayAsync(token);
            var ids = page.Select(u => u.Id).ToArray();
            // Raw shift states are intentionally not reinterpreted as an on-shift business decision.
            var shifts = await db.ShiftAssignments.AsNoTracking().Where(a => ids.Contains(a.UserId) && a.Shift.BranchId == branch
                && a.Shift.Department.BranchId == branch && a.Shift.Department.IsActive
                && a.Shift.ShiftTemplate.BranchId == branch && a.Shift.ShiftTemplate.IsActive
                && a.Shift.ScheduledStartAt < query.To && a.Shift.ScheduledEndAt > query.From && a.Shift.ScheduledEndAt > a.Shift.ScheduledStartAt)
                .OrderBy(a => a.Shift.ScheduledStartAt).ThenBy(a => a.ShiftAssignmentId)
                .Select(a => new PlanningShiftDto(a.ShiftId, a.UserId, a.Shift.ScheduledStartAt, a.Shift.ScheduledEndAt, a.Shift.Status, a.AssignmentStatus)).ToArrayAsync(token);
            PlanningChecklistDto? checklist = null;
            if (inspection && await RoomTypeSchema.ReadAsync(db, token) == RoomTypeContract.BranchV1)
            {
                var typeId = await db.Rooms.Where(r => r.RoomId == task.RoomId).Select(r => r.RoomTypeId).SingleAsync(token);
                var checklistId = await db.BranchRoomTypes.Where(t => t.RoomTypeId == typeId && t.IsActive && (t.BranchId == null || t.BranchId == branch)).Select(t => t.DefaultChecklistId).SingleOrDefaultAsync(token);
                if (checklistId is { } selected && await ChecklistEligible(selected, branch, typeId, token))
                    checklist = await db.InspectionChecklistVersions.AsNoTracking().Where(v => v.ChecklistId == selected && v.Status == "PUBLISHED"
                        && (v.EffectiveAt == null || v.EffectiveAt <= now) && v.InspectionChecklistItems.Any(i => i.IsActive))
                        .OrderByDescending(v => v.VersionNo).ThenByDescending(v => v.VersionId)
                        .Select(v => new PlanningChecklistDto(v.ChecklistId, v.Checklist.ChecklistName, v.VersionId, v.VersionNo, v.InspectionChecklistItems.Count(i => i.IsActive)))
                        .FirstOrDefaultAsync(token);
            }
            if (!await HistoryFinal(permission, [branch], token)) return new(RoomTypeBatchStatus.Forbidden);
            HousekeepingState? workflow = await HousekeepingSchema.InstalledAsync(db, token) ? await CleaningState(taskId, token) : null;
            if (workflow != null) task = task with { WorkflowState = workflow.State };
            return new(RoomTypeBatchStatus.Success, new HousekeepingPlanningDto(task, page, shifts, total, query, checklist, now, workflow?.State != null), workflow == null ? null : WorkflowTag(workflow.Revision));
        }, ct, requireRoomTypeSchema: false, permissionCode: permission);
    }
}
