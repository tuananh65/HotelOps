using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private async Task<bool> ReworkEndOfShiftInstalled(CancellationToken ct)
        => await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.ReworkEndOfShift.V1')
              AND COL_LENGTH('dbo.rework_end_of_shift_events','hold_revision')=8
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.rework_end_of_shift_events') AND is_disabled=0 AND is_not_trusted=0)=2
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_rework_end_of_shift_immutable') AND is_disabled=0)
              AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.cleaning_reworks')
                AND name='CK_rework_workflow_v1' AND is_disabled=0 AND is_not_trusted=0 AND definition LIKE '%ON_HOLD%')
            THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    private async Task<bool> IsReworkEndOfShiftHold(long id, CancellationToken ct)
    {
        if (!await ReworkEndOfShiftInstalled(ct)) return false;
        return await db.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM dbo.rework_end_of_shift_events e WITH(HOLDLOCK)
            JOIN dbo.cleaning_reworks w ON w.rework_id=e.rework_id AND w.row_version=e.hold_revision
            JOIN dbo.work_intervals i ON i.interval_id=e.interval_id AND i.work_item_kind='REWORK'
              AND i.work_item_id=e.rework_id AND i.user_id=w.assignee_user_id
              AND i.closed_at=e.paused_at AND i.effective_ended_at=e.paused_at
            WHERE w.rework_id={id} AND w.workflow_status='ON_HOLD' AND w.status='IN_PROGRESS'
              AND w.completed_at IS NULL AND e.reason_code='END_OF_SHIFT'
            """).AnyAsync(ct);
    }

    // Caller owns the serializable workday transaction and user/time locks. No separate commit.
    internal async Task<RoomTypeBatchResult> HoldReworkForWorkdayEndAsync(long actor, long branch, long id,
        SqlWorkTimeTransactionParticipant time, string? root, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new WorkTimeContractException("TIME_TRANSACTION_REQUIRED");
        if (!await ReworkEndOfShiftInstalled(ct) || !await AssignedReworkExecutionInstalled(ct))
            return new(RoomTypeBatchStatus.SchemaRequired);
        if (!(await ReworkAccess(actor, branch, id, "CLEANING_EXECUTE", ct)).IsGranted)
            return new(RoomTypeBatchStatus.Forbidden);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await EligibleReworkAssignees(branch, now).AnyAsync(u => u.UserId == actor, ct))
            return new(RoomTypeBatchStatus.Forbidden);
        // This reader locks the source and verifies committed producer, source/evidence digests,
        // failed inspection and current cleaning cycle. An assignment alone is not authority.
        var authority = await ReadTrustedReworkAuthorityAsync(actor, branch, id, root, ct);
        if (authority is null) return new(RoomTypeBatchStatus.Unavailable);
        var row = await db.CleaningReworks.AsNoTracking().Where(w => w.ReworkId == id && w.AssigneeUserId == actor
            && w.Status == "IN_PROGRESS" && w.CompletedAt == null && w.Inspection.InspectorUserId != actor
            && w.CleaningTask.Room.BranchId == branch && w.CleaningTask.Room.IsActive && w.CleaningTask.Room.Floor.IsActive
            && (w.CleaningTask.Room.AreaId == null || w.CleaningTask.Room.Area!.IsActive))
            .Select(w => new { w.InspectionId }).SingleOrDefaultAsync(ct);
        if (row is null) return new(RoomTypeBatchStatus.Inactive);
        if (await db.CleaningReworks.CountAsync(w => w.InspectionId == row.InspectionId && w.Status != "COMPLETED" && w.Status != "CANCELLED", ct) != 1)
            return new(RoomTypeBatchStatus.Inactive);
        var state = await ReworkState(id, ct);
        if (state.State != "IN_PROGRESS") return new(RoomTypeBatchStatus.Inactive);
        var interval = await db.WorkIntervals.AsNoTracking().SingleOrDefaultAsync(i => i.UserId == actor && i.ClosedAt == null, ct);
        if (interval is null || interval.BranchId != branch || interval.WorkItemKind != "REWORK" || interval.WorkItemId != id)
            return new(RoomTypeBatchStatus.Inactive);
        var closed = await time.CloseIntervalAsync(new(actor, actor, branch, "REWORK", id), interval.IntervalId, interval.RowVersion, ct);
        if (await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.cleaning_reworks SET workflow_status='ON_HOLD'
            WHERE rework_id={id} AND workflow_status='IN_PROGRESS' AND status='IN_PROGRESS' AND row_version={state.Revision}
            """, ct) != 1) return new(RoomTypeBatchStatus.Stale);
        var held = await ReworkState(id, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.rework_end_of_shift_events(interval_id,rework_id,hold_revision,reason_code,paused_at)
            VALUES({interval.IntervalId},{id},{held.Revision},'END_OF_SHIFT',{closed.ClosedAtUtc})
            """, ct);
        await BatchAuditAsync(actor, branch, "cleaning_reworks", id, new { workflowState = state.State },
            new { workflowState = "ON_HOLD", operation = "WORKDAY_PAUSE_ALL", reasonCode = "END_OF_SHIFT",
                interval.IntervalId, closed.ClosedAtUtc, authorityOperationId = authority.OperationId },
            ["workflowState", "workInterval", "pauseReason"], ct);
        now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        return await EligibleReworkAssignees(branch, now).AnyAsync(u => u.UserId == actor, ct)
            && (await ReworkAccess(actor, branch, id, "CLEANING_EXECUTE", ct)).IsGranted
            && SameTrustedReworkAuthority(authority, await ReadTrustedReworkAuthorityAsync(actor, branch, id, root, ct))
                ? new(RoomTypeBatchStatus.Success) : new(RoomTypeBatchStatus.Forbidden);
    }
}
