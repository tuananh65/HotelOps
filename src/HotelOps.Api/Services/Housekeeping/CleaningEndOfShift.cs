using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private async Task<bool> CleaningEndOfShiftInstalled(CancellationToken ct)
        => await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.CleaningEndOfShift.V1')
              AND OBJECT_ID('dbo.cleaning_end_of_shift_events','U') IS NOT NULL
              AND COL_LENGTH('dbo.cleaning_end_of_shift_events','hold_revision')=8
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.cleaning_end_of_shift_events') AND is_disabled=0 AND is_not_trusted=0)=2
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_cleaning_end_of_shift_immutable') AND is_disabled=0)
            THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    // A receipt of THIS hold revision, not absence of blockers, proves the reason for ON_HOLD.
    private async Task<bool> IsCleaningEndOfShiftHold(long task, CancellationToken ct)
    {
        if (!await CleaningEndOfShiftInstalled(ct)) return false;
        return await db.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM dbo.cleaning_end_of_shift_events e WITH(HOLDLOCK)
            JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=e.cleaning_task_id AND t.row_version=e.hold_revision
            JOIN dbo.work_intervals i ON i.interval_id=e.interval_id AND i.work_item_kind='CLEANING'
              AND i.work_item_id=e.cleaning_task_id AND i.closed_at=e.paused_at AND i.effective_ended_at=e.paused_at
            WHERE t.cleaning_task_id={task} AND t.workflow_status='ON_HOLD' AND e.reason_code='END_OF_SHIFT'
            """).AnyAsync(ct);
    }

    internal async Task<RoomTypeBatchResult> HoldCleaningForWorkdayEndAsync(long actor, long branch, long task,
        SqlWorkTimeTransactionParticipant time, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new WorkTimeContractException("TIME_TRANSACTION_REQUIRED");
        if (!await CleaningEndOfShiftInstalled(ct) || !await ResumeTrialInstalled(ct)) return new(RoomTypeBatchStatus.SchemaRequired);
        if (!(await CleaningExecutionAccess(actor, branch, task, "CLEANING_EXECUTE", ct)).IsGranted)
            return new(RoomTypeBatchStatus.Forbidden);
        var room = await db.Database.SqlQuery<long>($"SELECT room_id AS Value FROM dbo.cleaning_tasks WITH(READUNCOMMITTED) WHERE cleaning_task_id={task}").SingleOrDefaultAsync(ct);
        if (room <= 0) return new(RoomTypeBatchStatus.NotFound);
        if (!await db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.rooms WITH(UPDLOCK,HOLDLOCK) WHERE room_id={room}")
            .AsNoTracking().AnyAsync(r => r.BranchId == branch && r.IsActive && r.Floor.IsActive && (r.AreaId == null || r.Area!.IsActive), ct)) return new(RoomTypeBatchStatus.NotFound);
        var row = await db.CleaningTasks.FromSqlInterpolated($"SELECT * FROM dbo.cleaning_tasks WITH(UPDLOCK,HOLDLOCK) WHERE cleaning_task_id={task}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (row is null || row.RoomId != room || row.Status != "IN_PROGRESS" || row.ActualStartAt == null)
            return new(RoomTypeBatchStatus.Inactive);
        if (!await ProgressOwned(true, task, branch, actor, ct)) return new(RoomTypeBatchStatus.Forbidden);
        var state = await CleaningState(task, ct);
        if (state.State != "IN_PROGRESS") return new(RoomTypeBatchStatus.Inactive);
        if (await db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS Value FROM dbo.room_cleaning_cycle_state WITH(UPDLOCK,HOLDLOCK) WHERE room_id={room}").SingleOrDefaultAsync(ct) != task)
            return new(RoomTypeBatchStatus.Inactive);
        if (await db.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM dbo.cleaning_progress_logs p LEFT JOIN dbo.fs014_cleaning_blocker_resolutions b ON b.progress_id=p.progress_id
            WHERE p.cleaning_task_id={task} AND p.status='BLOCKED'
              AND (b.progress_id IS NULL OR b.resolved_at IS NULL OR b.incident_id IS NOT NULL OR b.material_request_id IS NOT NULL
                OR p.blocker_type_id IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.reference_values r WHERE r.reference_id=p.blocker_type_id AND r.group_code='BLOCKER'))
            """).AnyAsync(ct)) return new(RoomTypeBatchStatus.Inactive);
        var interval = await db.WorkIntervals.AsNoTracking().SingleOrDefaultAsync(i => i.UserId == actor && i.ClosedAt == null, ct);
        if (interval is null || interval.BranchId != branch || interval.WorkItemKind != "CLEANING" || interval.WorkItemId != task)
            return new(RoomTypeBatchStatus.Inactive);
        var roomHistory = await HotelOps.Api.Services.Rooms.RoomReadinessHistory.CaptureAsync(db, room, ct);
        var closed = await time.CloseIntervalAsync(new(actor, actor, branch, "CLEANING", task), interval.IntervalId, interval.RowVersion, ct);
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.cleaning_tasks SET workflow_status='ON_HOLD'
            WHERE cleaning_task_id={task} AND workflow_status='IN_PROGRESS' AND row_version={state.Revision}
            """, ct);
        if (changed != 1) return new(RoomTypeBatchStatus.Stale);
        var held = await CleaningState(task, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.cleaning_end_of_shift_events(interval_id,cleaning_task_id,hold_revision,reason_code,paused_at)
            VALUES({interval.IntervalId},{task},{held.Revision},'END_OF_SHIFT',{closed.ClosedAtUtc})
            """, ct);
        await HotelOps.Api.Services.Rooms.RoomReadinessHistory.FinishAsync(db, roomHistory, "CLEANING_TASK", task, actor, closed.ClosedAtUtc!.Value, "END_OF_SHIFT", ct);
        await BatchAuditAsync(actor, branch, "cleaning_tasks", task, new { workflowState = state.State },
            new { workflowState = "ON_HOLD", operation = "WORKDAY_PAUSE_ALL", reasonCode = "END_OF_SHIFT", interval.IntervalId, closed.ClosedAtUtc },
            ["workflowState", "workInterval", "pauseReason"], ct);
        return (await CleaningExecutionAccess(actor, branch, task, "CLEANING_EXECUTE", ct)).IsGranted
            ? new(RoomTypeBatchStatus.Success) : new(RoomTypeBatchStatus.Forbidden);
    }
}
