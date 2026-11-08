using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private async Task<bool> EligibleHousekeeper(long user, long branch, DateTime now, CancellationToken ct) => await db.Users.AsNoTracking().AnyAsync(u => u.UserId == user
        && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE" && u.DeletedAt == null
        && u.Department != null && u.Department.IsActive && u.Department.BranchId == branch && u.Department.DepartmentCode == "HK"
        && u.UserBranchAccessUsers.Any(a => a.BranchId == branch && a.RevokedAt == null && a.GrantedAt <= now)
        && db.UserRoles.Any(r => r.UserId == user && r.BranchId == branch && r.IsActive && r.Role.IsActive && r.Role.RoleCode == "HK_STAFF" && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now))
        && db.ShiftAssignments.Any(a => a.UserId == user && a.AssignmentStatus == "ASSIGNED" && a.Shift.Status == "PLANNED"
            && a.Shift.BranchId == branch && a.Shift.DepartmentId == u.DepartmentId && a.Shift.ShiftTemplate.IsActive && a.Shift.ShiftTemplate.BranchId == branch
            && a.Shift.ScheduledStartAt <= now && a.Shift.ScheduledEndAt > now), ct);
    private async Task<bool> EligibleInspector(long user, long branch, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return await db.Users.AsNoTracking().AnyAsync(u => u.UserId == user && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE" && u.DeletedAt == null
            && u.UserBranchAccessUsers.Any(a => a.BranchId == branch && a.RevokedAt == null && a.GrantedAt <= now)
            && db.UserRoles.Any(r => r.UserId == user && r.BranchId == branch && r.IsActive && r.Role.IsActive && r.Role.RoleCode == "HK_SUPERVISOR"
                && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)), ct);
    }
    private static void WorkflowDue(DateTime due, DateTime now)
    {
        if (due.Kind != DateTimeKind.Utc || due <= now) throw new ValidationException("Due time must be future UTC.");
    }
    public Task<RoomTypeBatchResult> AssignCleaningAsync(SessionReference ticket, long sid, long branch, long task, string tag, AssignCleaningRequest input, CancellationToken ct)
    {
        if (task <= 0 || input.AssigneeId <= 0 || !WorkflowStrongTag(tag)) throw new ValidationException("Invalid assignment or If-Match.");
        var reason = WorkflowReason(input.Reason); WorkflowDue(input.DueAt, clock.GetUtcNow().UtcDateTime);
        // FS008: DEV2-FS007-UC029-AUTH-01 requires verified accepted A01 invocation.
        // No alternate publisher or successful business commit until that executable package is available.
        return Task.FromResult(new RoomTypeBatchResult(RoomTypeBatchStatus.Unavailable));
    }

    private Task<RoomTypeBatchResult> AssignCleaningCoreAsync(SessionReference ticket, long sid, long branch, long task, string tag, AssignCleaningRequest input, CancellationToken ct)
    {
        if (task <= 0 || input.AssigneeId <= 0 || !WorkflowStrongTag(tag)) throw new ValidationException("Invalid assignment or If-Match.");
        var reason = WorkflowReason(input.Reason); WorkflowDue(input.DueAt, clock.GetUtcNow().UtcDateTime);
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await HousekeepingSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var rooms = await HistoryRooms(token); if (rooms == null) return new(RoomTypeBatchStatus.SchemaRequired);
            var ids = rooms.Where(r => r.BranchId == branch && r.IsActive).Select(r => r.RoomId);
            var row = await db.CleaningTasks.AsNoTracking().SingleOrDefaultAsync(t => t.CleaningTaskId == task && ids.Contains(t.RoomId), token);
            if (row == null) return new(RoomTypeBatchStatus.NotFound);
            var state = await CleaningState(task, token);
            if (tag != WorkflowTag(state.Revision)) return new(RoomTypeBatchStatus.Stale);
            if (state.State != "PENDING_ASSIGNMENT" && !(state.State == null && row.Status == "PENDING" && input.AdoptUnassignedLegacy)) return new(RoomTypeBatchStatus.Inactive);
            if (await db.CleaningAssignments.AnyAsync(a => a.CleaningTaskId == task && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null, token)) return new(RoomTypeBatchStatus.Duplicate);
            db.Add(new CleaningAssignment { CleaningTaskId = task, AssigneeUserId = input.AssigneeId, AssignedBy = actor, AssignedAt = clock.GetUtcNow().UtcDateTime, AssignmentStatus = "ACTIVE", Reason = reason });
            await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status={"ASSIGNED"},status={"PENDING"},due_at={input.DueAt} WHERE cleaning_task_id={task}", token);
            var after = new HousekeepingWriteDto(task, "ASSIGNED");
            await BatchAuditAsync(actor, branch, "cleaning_tasks", task, new { row.Status, state.State }, new { reason, assigneeId = input.AssigneeId, entity = after }, ["assignee", "workflowStatus", "dueAt"], token);
            return new(RoomTypeBatchStatus.Success, after, WorkflowTag((await CleaningState(task, token)).Revision));
        }, ct, additionalUsers: [input.AssigneeId], eligibility: token => DueHousekeeper(input.AssigneeId, branch, input.DueAt, token), permissionCode: "CLEANING_ASSIGN");
    }

    public Task<RoomTypeBatchResult> AssignInspectionAsync(SessionReference ticket, long sid, long branch, long task, AssignInspectionRequest input, CancellationToken ct)
    {
        if (task <= 0 || input.InspectorId <= 0) throw new ValidationException("Invalid task or inspector.");
        var reason = WorkflowReason(input.Reason); WorkflowDue(input.DueAt, clock.GetUtcNow().UtcDateTime);
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await HousekeepingSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var rooms = await HistoryRooms(token); if (rooms == null) return new(RoomTypeBatchStatus.SchemaRequired);
            var ids = rooms.Where(r => r.BranchId == branch && r.IsActive && r.Floor.IsActive && (r.AreaId == null || r.Area!.IsActive)).Select(r => r.RoomId);
            var row = await db.CleaningTasks.AsNoTracking().SingleOrDefaultAsync(t => t.CleaningTaskId == task && ids.Contains(t.RoomId), token);
            if (row == null) return new(RoomTypeBatchStatus.NotFound);
            var state = await CleaningState(task, token);
            if (state.State != "PENDING_INSPECTION" && !await db.Database.SqlQuery<int>($"SELECT CONVERT(int,rework_id) AS [Value] FROM dbo.cleaning_reworks WHERE cleaning_task_id={task} AND workflow_status={"PENDING_REINSPECTION"}").AnyAsync(token))
                return new(RoomTypeBatchStatus.Inactive);
            if (await db.CleaningAssignments.AnyAsync(a => a.CleaningTaskId == task && a.AssigneeUserId == input.InspectorId && a.AssignmentStatus != "CANCELLED", token)
                || await db.CleaningProgressLogs.AnyAsync(p => p.CleaningTaskId == task && p.LoggedBy == input.InspectorId, token)
                || await db.CleaningReworks.AnyAsync(r => r.CleaningTaskId == task && r.AssigneeUserId == input.InspectorId, token)) return new(RoomTypeBatchStatus.InvalidReference);
            if (await db.RoomInspections.AnyAsync(i => i.CleaningTaskId == task && i.Status != "COMPLETED" && i.Status != "CANCELLED", token)) return new(RoomTypeBatchStatus.Duplicate);
            var type = await db.Rooms.Where(r => r.RoomId == row.RoomId).Select(r => r.RoomTypeId).SingleAsync(token);
            var checklist = await db.BranchRoomTypes.Where(t => t.RoomTypeId == type && t.IsActive && (t.BranchId == branch || t.BranchId == null)).Select(t => t.DefaultChecklistId).SingleOrDefaultAsync(token);
            if (checklist == null || !await ChecklistEligible(checklist.Value, branch, type, token)) return new(RoomTypeBatchStatus.InvalidReference);
            var now = clock.GetUtcNow().UtcDateTime;
            var version = await db.InspectionChecklistVersions.Where(v => v.ChecklistId == checklist && v.Status == "PUBLISHED" && (v.EffectiveAt == null || v.EffectiveAt <= now)
                && v.InspectionChecklistItems.Any(c => c.IsActive)).OrderByDescending(v => v.VersionNo).Select(v => v.VersionId).FirstAsync(token);
            var inspection = new RoomInspection { InspectionNo = "INSP-" + Guid.NewGuid().ToString("N"), RoomId = row.RoomId, CleaningTaskId = task,
                InspectorUserId = input.InspectorId, VersionId = version, Status = "PENDING", CreatedAt = now };
            db.Add(inspection); await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status={"ASSIGNED"},due_at={input.DueAt} WHERE inspection_id={inspection.InspectionId}", token);
            var after = new HousekeepingWriteDto(inspection.InspectionId, "ASSIGNED");
            await BatchAuditAsync(actor, branch, "room_inspections", inspection.InspectionId, null, new { reason, inspectorId = input.InspectorId, version, entity = after }, ["assignedInspector", "version", "dueAt"], token);
            return new(RoomTypeBatchStatus.Success, after, WorkflowTag((await InspectionState(inspection.InspectionId, token)).Revision));
        }, ct, additionalUsers: [input.InspectorId], eligibility: async token => input.DueAt > clock.GetUtcNow().UtcDateTime && await EligibleInspector(input.InspectorId, branch, token), permissionCode: "INSPECTION_PERFORM");
    }

    public Task<RoomTypeBatchResult> RequestCleaningReworkAsync(SessionReference ticket,long sid,long branch,long inspection,string tag,RequestReworkRequest input,CancellationToken ct)
        => RequestAuthoritativeReworkAsync(ticket,sid,branch,inspection,tag,input,ct);
    private async Task<bool> DueHousekeeper(long id, long branch, DateTime due, CancellationToken ct) => due > clock.GetUtcNow().UtcDateTime
        && await EligibleHousekeeper(id, branch, clock.GetUtcNow().UtcDateTime, ct);
}
