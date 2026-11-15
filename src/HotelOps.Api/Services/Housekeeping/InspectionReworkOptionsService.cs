using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private async Task<bool> ReworkCurrentCycle(long room, long task, CancellationToken ct) =>
        await db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS Value FROM dbo.room_cleaning_cycle_state WITH(UPDLOCK,HOLDLOCK) WHERE room_id={room}")
            .SingleOrDefaultAsync(ct) == task;

    public Task<RoomTypeBatchResult> ReadInspectionReworkOptionsAsync(SessionReference ticket, long sid, long branch, long inspection, CancellationToken ct)
    {
        if (inspection <= 0) throw new ValidationException("Inspection ID must be positive.");
        return BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            if (!await HousekeepingSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var rooms = await HistoryRooms(token);
            if (rooms == null) return new(RoomTypeBatchStatus.SchemaRequired);
            var ids = rooms.Where(r => r.BranchId == branch && r.IsActive && r.Floor.IsActive && (r.AreaId == null || r.Area!.IsActive)).Select(r => r.RoomId);
            var row = await db.RoomInspections.AsNoTracking().SingleOrDefaultAsync(i => i.InspectionId == inspection && ids.Contains(i.RoomId)
                && i.CleaningTaskId != null && i.CleaningTask!.RoomId == i.RoomId, token);
            if (row == null) return new(RoomTypeBatchStatus.NotFound);
            var state = await InspectionState(inspection, token);
            if (state.State != "FAILED" || !await ReworkCurrentCycle(row.RoomId, row.CleaningTaskId!.Value, token)) return new(RoomTypeBatchStatus.Inactive);
            var open = await db.CleaningReworks.AsNoTracking().Where(r => r.InspectionId == inspection && r.Status != "COMPLETED" && r.Status != "CANCELLED")
                .OrderBy(r => r.ReworkId).Select(r => (long?)r.ReworkId).FirstOrDefaultAsync(token);
            var defects = await db.InspectionDefects.AsNoTracking().Where(d => d.InspectionId == inspection && d.ChecklistItem != null
                && d.ChecklistItem.VersionId == row.VersionId && db.InspectionResults.Any(r => r.InspectionId == inspection
                    && r.ChecklistItemId == d.ChecklistItemId && r.Outcome == "FAIL"))
                .OrderBy(d => d.DefectId).Select(d => new ReworkDefectOption(d.DefectId, d.Description)).ToArrayAsync(token);
            if (open != null) return new(RoomTypeBatchStatus.Success, new InspectionReworkOptionsDto(inspection, branch, open, defects, []), WorkflowTag(state.Revision));
            if ((await CleaningState(row.CleaningTaskId.Value, token)).State != "REWORK_REQUIRED") return new(RoomTypeBatchStatus.Inactive);
            var cleaner = await ReadInspectionCleanerAttributionAsync(db.Database.GetDbConnection(), db.Database.CurrentTransaction!.GetDbTransaction(), inspection, branch, token);
            if (cleaner == null) return new(RoomTypeBatchStatus.Unavailable);
            var assignees = await EligibleHousekeepers(branch, clock.GetUtcNow().UtcDateTime)
                .OrderByDescending(u => u.UserId == cleaner.CleanerUserId).ThenBy(u => u.FullName).ThenBy(u => u.UserId)
                .Select(u => new ReworkAssigneeOption(u.UserId, u.FullName, u.UserId == cleaner.CleanerUserId)).ToArrayAsync(token);
            return new(RoomTypeBatchStatus.Success, new InspectionReworkOptionsDto(inspection, branch, null, defects, assignees), WorkflowTag(state.Revision));
        }, ct, permissionCode: "INSPECTION_PERFORM");
    }
}
