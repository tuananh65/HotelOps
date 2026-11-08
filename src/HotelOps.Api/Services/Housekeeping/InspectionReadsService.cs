using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private static InspectionReadQuery InspectionReadInput(InspectionReadQuery q)
    {
        var search = q.Search?.Trim();
        if (q.Page < 1 || q.PageSize is < 1 or > 100 || q.Page > int.MaxValue / q.PageSize
            || search?.Length > 150 || search?.Any(char.IsControl) == true)
            throw new ValidationException("Invalid inspection collection filters.");
        return q with { Search = string.IsNullOrEmpty(search) ? null : search };
    }
    public Task<RoomTypeBatchResult> InspectionSeveritiesAsync(SessionReference ticket, long sid, long branch, CancellationToken ct)
        => BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            // Exact catalog restriction used by the existing defect writer. No catalog mutation.
            var values = await db.ReferenceValues.AsNoTracking()
                .Where(v => v.IsActive && v.GroupCode == "SEVERITY" && (v.ValueCode == "MINOR" || v.ValueCode == "MAJOR"))
                .OrderBy(v => v.SortOrder).ThenBy(v => v.ReferenceId)
                .Select(v => new InspectionSeverityDto(v.ReferenceId, v.ValueCode, v.DisplayName)).ToArrayAsync(token);
            return new(RoomTypeBatchStatus.Success, values);
        }, ct, requireRoomTypeSchema: false, permissionCode: "INSPECTION_PERFORM");

    public Task<RoomTypeBatchResult> AssignedInspectionsAsync(SessionReference ticket, long sid, long branch,
        InspectionReadQuery input, CancellationToken ct)
    {
        var q = InspectionReadInput(input);
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await HousekeepingSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var rooms = await HistoryRooms(token); if (rooms == null) return new(RoomTypeBatchStatus.SchemaRequired);
            var visible = rooms.Where(r => r.BranchId == branch && r.IsActive && r.Floor.IsActive && (r.AreaId == null || r.Area!.IsActive));
            var states = db.Database.SqlQuery<HousekeepingState>($"SELECT inspection_id AS Id,workflow_status AS State,row_version AS Revision,due_at AS DueAt FROM dbo.room_inspections");
            var query = from i in db.RoomInspections.AsNoTracking()
                        join r in visible on i.RoomId equals r.RoomId
                        join s in states on i.InspectionId equals s.Id
                        where i.InspectorUserId == actor && i.CleaningTaskId != null && i.CleaningTask!.RoomId == i.RoomId
                            && (s.State == "ASSIGNED" || s.State == "INSPECTING" || s.State == "PENDING_CONCLUSION")
                        select new { Id=i.InspectionId, Number=i.InspectionNo, i.RoomId, r.RoomNumber,
                            CleaningTaskId=i.CleaningTaskId!.Value, InspectorId=i.InspectorUserId, InspectorName=i.InspectorUser.FullName, i.VersionId, s.State, s.DueAt };
            if (q.Search != null) query = query.Where(i => i.Number.Contains(q.Search) || i.RoomNumber.Contains(q.Search));
            var total = await query.CountAsync(token);
            var items = await query.OrderBy(i => i.DueAt).ThenBy(i => i.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
                .Select(i => new AssignedInspectionDto(i.Id,i.Number,branch,i.RoomId,i.RoomNumber,i.CleaningTaskId,i.InspectorId,i.InspectorName,i.VersionId,i.State!,i.DueAt)).ToArrayAsync(token);
            return new(RoomTypeBatchStatus.Success, new InspectionReadPage<AssignedInspectionDto>(items, total, q.Page, q.PageSize));
        }, ct, requireRoomTypeSchema: false, permissionCode: "INSPECTION_PERFORM");
    }

    public Task<RoomTypeBatchResult> InspectionAssignmentTasksAsync(SessionReference ticket, long sid, long branch,
        InspectionReadQuery input, CancellationToken ct)
    {
        var q = InspectionReadInput(input);
        return BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            if (!await HousekeepingSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var rooms = await HistoryRooms(token); if (rooms == null) return new(RoomTypeBatchStatus.SchemaRequired);
            var visible = rooms.Where(r => r.BranchId == branch && r.IsActive && r.Floor.IsActive && (r.AreaId == null || r.Area!.IsActive));
            var pending = db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS Value FROM dbo.cleaning_reworks WHERE workflow_status={"PENDING_REINSPECTION"}");
            var states = db.Database.SqlQuery<HousekeepingState>($"SELECT cleaning_task_id AS Id,workflow_status AS State,row_version AS Revision,due_at AS DueAt FROM dbo.cleaning_tasks");
            // Explicit persisted cycle identity, same phase/open-inspection checks as AssignInspectionAsync.
            var query = from t in db.CleaningTasks.AsNoTracking()
                        join r in visible on t.RoomId equals r.RoomId
                        join s in states on t.CleaningTaskId equals s.Id
                        where (s.State == "PENDING_INSPECTION" || pending.Contains(t.CleaningTaskId))
                            && !db.RoomInspections.Any(i => i.CleaningTaskId == t.CleaningTaskId && i.Status != "COMPLETED" && i.Status != "CANCELLED")
                        select new { Id=t.CleaningTaskId, Number=t.TaskNo, r.RoomId, r.RoomNumber, s.State, PendingReinspection=pending.Contains(t.CleaningTaskId) };
            if (q.Search != null) query = query.Where(t => t.Number.Contains(q.Search) || t.RoomNumber.Contains(q.Search));
            var total = await query.CountAsync(token);
            var items = await query.OrderBy(t => t.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
                .Select(t => new InspectionAssignmentTaskDto(t.Id,t.Number,branch,t.RoomId,t.RoomNumber,t.State,t.PendingReinspection)).ToArrayAsync(token);
            return new(RoomTypeBatchStatus.Success, new InspectionReadPage<InspectionAssignmentTaskDto>(items, total, q.Page, q.PageSize));
        }, ct, requireRoomTypeSchema: false, permissionCode: "INSPECTION_VIEW");
    }
}
