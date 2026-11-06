using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private async Task<bool> LifecycleInstalled(CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.CleaningLifecycle.V1' AND CONVERT(nvarchar(100),value)='FS008-V1') AND OBJECT_ID('dbo.room_cleaning_cycle_state') IS NOT NULL AND OBJECT_ID('dbo.room_readiness_events') IS NOT NULL AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.cleaning_tasks') AND name='UX_cleaning_tasks_open_room_v1' AND is_unique=1 AND is_disabled=0 AND has_filter=1) AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN('FK_room_cycle_task_v1','FK_readiness_task_room_v1') AND is_disabled=0 AND is_not_trusted=0)=2 THEN 1 ELSE 0 END AS [Value]").SingleAsync(ct) == 1;
    public Task<RoomTypeBatchResult> CleaningCreationOptionsAsync(SessionReference ticket, long sid, long branch, long room, CancellationToken ct)
    {
        if (room <= 0) throw new ValidationException("RoomId must be positive.");
        return BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            if (!await HotelOps.Api.Data.ProgressSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var rooms = await HistoryRooms(token);
            if (rooms == null) return new(RoomTypeBatchStatus.SchemaRequired);
            var r = await rooms.AsNoTracking().SingleOrDefaultAsync(x => x.RoomId == room && x.BranchId == branch, token);
            if (r == null) return new(RoomTypeBatchStatus.NotFound);
            if (!r.IsActive || !await db.Floors.AnyAsync(x => x.FloorId == r.FloorId && x.BranchId == branch && x.IsActive, token) || (r.AreaId != null && !await db.Areas.AnyAsync(x => x.AreaId == r.AreaId && x.BranchId == branch && x.IsActive, token))) return new(RoomTypeBatchStatus.Inactive);
            var refs = await db.ReferenceValues.AsNoTracking().Where(x => x.IsActive && (x.GroupCode == "CLEANING_TYPE" || x.GroupCode == "PRIORITY"))
                .Select(x => new { x.ReferenceId, x.ValueCode, x.DisplayName, x.GroupCode }).ToArrayAsync(token);
            var count = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.cleaning_item_definitions WHERE room_type_id={r.RoomTypeId}").SingleAsync(token);
            return new(RoomTypeBatchStatus.Success, new CleaningCreateOptions(room, r.RoomNumber, branch,
                refs.Where(x => x.GroupCode == "CLEANING_TYPE").Select(x => new CleaningReferenceDto(x.ReferenceId, x.ValueCode, x.DisplayName)).ToArray(),
                refs.Where(x => x.GroupCode == "PRIORITY").Select(x => new CleaningReferenceDto(x.ReferenceId, x.ValueCode, x.DisplayName)).ToArray(), count));
        }, ct, permissionCode: "CLEANING_ASSIGN");
    }

    public Task<RoomTypeBatchResult> CreateCleaningTaskAsync(SessionReference ticket, long sid, long branch, CreateCleaningTaskRequest input, CancellationToken ct)
    {
        if (input.RoomId <= 0 || input.CleaningTypeId <= 0 || input.PriorityId <= 0) throw new ValidationException("Positive room/type/priority identifiers required.");
        WorkflowDue(input.DueAt, clock.GetUtcNow().UtcDateTime);
        var instruction = ChecklistText(input.Instruction, 4000, false);
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await LifecycleInstalled(token) || !await HousekeepingInstalled(token) || !await HotelOps.Api.Data.ProgressSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var room = await db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.rooms WITH(UPDLOCK,HOLDLOCK) WHERE room_id={input.RoomId} AND branch_id={branch}").AsNoTracking().SingleOrDefaultAsync(token);
            if (room == null) return new(RoomTypeBatchStatus.NotFound);
            if (!room.IsActive || !await db.Floors.AnyAsync(x => x.FloorId == room.FloorId && x.BranchId == branch && x.IsActive, token)
                || room.AreaId != null && !await db.Areas.AnyAsync(x => x.AreaId == room.AreaId && x.BranchId == branch && x.IsActive, token)
                || !await db.BranchRoomTypes.AnyAsync(x => x.RoomTypeId == room.RoomTypeId && x.IsActive && (x.BranchId == branch || x.BranchId == null), token)) return new(RoomTypeBatchStatus.Inactive);
            if (!await db.ReferenceValues.AnyAsync(x => x.ReferenceId == input.CleaningTypeId && x.IsActive && x.GroupCode == "CLEANING_TYPE", token)
                || !await db.ReferenceValues.AnyAsync(x => x.ReferenceId == input.PriorityId && x.IsActive && x.GroupCode == "PRIORITY", token)) return new(RoomTypeBatchStatus.InvalidReference);
            var itemCount = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.cleaning_item_definitions WHERE room_type_id={room.RoomTypeId}").SingleAsync(token);
            if (itemCount is < 1 or > 100) return new(RoomTypeBatchStatus.InvalidReference);
            var conflicts = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.cleaning_tasks WITH(UPDLOCK,HOLDLOCK) WHERE room_id={room.RoomId} AND (workflow_status IS NULL OR workflow_status NOT IN('COMPLETED','CANCELLED'))").SingleAsync(token);
            if (conflicts != 0) return new(RoomTypeBatchStatus.Duplicate);
            // No implicit legacy adoption: any historical tasks without an explicit current pointer require reconciliation.
            var history = await db.CleaningTasks.AnyAsync(x => x.RoomId == room.RoomId, token);
            var pointers = await db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS [Value] FROM dbo.room_cleaning_cycle_state WITH(UPDLOCK,HOLDLOCK) WHERE room_id={room.RoomId}").ToArrayAsync(token);
            if (history && pointers.Length != 1) return new(RoomTypeBatchStatus.Inactive);
            var now = clock.GetUtcNow().UtcDateTime;
            if (input.DueAt <= now) return new(RoomTypeBatchStatus.InvalidReference);
            var task = new CleaningTask
            {
                RoomId = room.RoomId,
                CleaningTypeId = input.CleaningTypeId,
                PriorityId = input.PriorityId,
                Instruction = instruction,
                DueAt = input.DueAt,
                TaskNo = "CL-" + Guid.NewGuid().ToString("N"),
                Status = "PENDING",
                CreatedBy = actor,
                CreatedAt = now
            };
            db.Add(task); await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status='PENDING_ASSIGNMENT' WHERE cleaning_task_id={task.CleaningTaskId}", token);
            if (pointers.Length == 0) await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES({room.RoomId},{task.CleaningTaskId})", token);
            else await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_cleaning_cycle_state SET cleaning_task_id={task.CleaningTaskId} WHERE room_id={room.RoomId}", token);
            var blocked = await db.RoomBlocks.AsNoTracking().AnyAsync(x => x.RoomId == room.RoomId && x.ReleasedAt == null, token);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_readiness_events(room_id,cleaning_task_id,readiness,has_effective_block,actor_user_id,occurred_at) VALUES({room.RoomId},{task.CleaningTaskId},'PENDING_CLEANING',{blocked},{actor},{now})", token);
            var dto = new CreatedCleaningTask(task.CleaningTaskId, branch, room.RoomId, task.TaskNo, "PENDING_ASSIGNMENT", "PENDING_CLEANING", blocked);
            await BatchAuditAsync(actor, branch, "cleaning_tasks", task.CleaningTaskId, null, dto, ["create", "currentCycle", "readinessEvent"], token);
            return new(RoomTypeBatchStatus.Success, dto, WorkflowTag((await CleaningState(task.CleaningTaskId, token)).Revision));
        }, ct, permissionCode: "CLEANING_ASSIGN");
    }
    private Task<bool> HousekeepingInstalled(CancellationToken ct) => HotelOps.Api.Data.HousekeepingSchema.InstalledAsync(db, ct);
}
