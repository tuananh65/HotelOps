using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    // These routes deliberately cannot commit a lifecycle mutation. Neither the persisted
    // blocker contract nor the mandatory supervisor event contract has been accepted.
    // The FS013 provisional assignment publisher must not enable these operations.
    public Task<RoomTypeBatchResult> ReportCleaningBlockerAsync(SessionReference ticket, long sid,
        long branch, long task, string tag, string key, ReportCleaningBlockerRequest input, CancellationToken ct)
    {
        ValidateCleaningClosure(branch, task, tag, key, input.Note);
        if (input.ReasonId <= 0) throw new ValidationException("A positive blocker reference is required.");
        return CleaningClosureBoundaryAsync(ticket, sid, branch, task, tag, input.ReasonId, ct);
    }

    public Task<RoomTypeBatchResult> CompleteCleaningAsync(SessionReference ticket, long sid,
        long branch, long task, string tag, string key, CompleteCleaningRequest input, CancellationToken ct)
    {
        ValidateCleaningClosure(branch, task, tag, key, input.Note);
        return CleaningClosureBoundaryAsync(ticket, sid, branch, task, tag, null, ct);
    }

    private static void ValidateCleaningClosure(long branch, long task, string tag, string key, string? note)
    {
        if (branch <= 0 || task <= 0 || !CleaningLifecycleRules.StrongTag(tag))
            throw new ValidationException("Positive branch/task and a strong task ETag are required.");
        CleaningLifecycleRules.Operation(key);
        // V1 transport limit follows existing lifecycle notes; not a new role/status grant.
        CleaningLifecycleRules.Reason(note, 1);
    }

    private Task<RoomTypeBatchResult> CleaningClosureBoundaryAsync(SessionReference ticket, long sid,
        long branch, long task, string tag, long? reason, CancellationToken ct)
        => BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await CleaningExecutionInstalled(token) || !await LifecycleInstalled(token))
                return new(RoomTypeBatchStatus.SchemaRequired);
            var row = await db.CleaningTasks.AsNoTracking().SingleOrDefaultAsync(x => x.CleaningTaskId == task
                && x.Room.BranchId == branch && x.Room.IsActive && x.Room.Floor.IsActive
                && (x.Room.AreaId == null || x.Room.Area!.IsActive), token);
            if (row == null) return new(RoomTypeBatchStatus.NotFound);
            var state = await CleaningState(task, token);
            if (tag != WorkflowTag(state.Revision)) return new(RoomTypeBatchStatus.Stale);
            var owners = await db.CleaningAssignments.AsNoTracking().Where(x => x.CleaningTaskId == task
                && x.AssignmentStatus == "ACTIVE" && x.EndedAt == null)
                .OrderBy(x => x.AssignmentId).Take(2).ToArrayAsync(token);
            if (owners.Length != 1) return LifecycleFailure(RoomTypeBatchStatus.Inactive, "CURRENT_ASSIGNMENT_REQUIRED");
            if (owners[0].AssigneeUserId != actor)
                return LifecycleFailure(RoomTypeBatchStatus.Forbidden, "CURRENT_ASSIGNEE_REQUIRED");
            if (owners[0].AssignedAt > clock.GetUtcNow().UtcDateTime)
                return LifecycleFailure(RoomTypeBatchStatus.Inactive, "CURRENT_ASSIGNMENT_REQUIRED");
            if (state.State != "IN_PROGRESS") return LifecycleFailure(RoomTypeBatchStatus.Inactive, "TASK_NOT_IN_PROGRESS");
            if (await db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS [Value] FROM dbo.room_cleaning_cycle_state WITH(UPDLOCK,HOLDLOCK) WHERE room_id={row.RoomId}").SingleOrDefaultAsync(token) != task)
                return LifecycleFailure(RoomTypeBatchStatus.Inactive, "CURRENT_CYCLE_REQUIRED");
            if (reason is { } reference && !await db.ReferenceValues.AsNoTracking().AnyAsync(x => x.ReferenceId == reference
                && x.GroupCode == "BLOCKER" && x.IsActive, token)) return new(RoomTypeBatchStatus.InvalidReference);

            // No append to logs, audit, intervals, task, readiness or notification tables.
            // Absence of a BLOCKED log is not proof that no active blocker exists.
            return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "CLEANING_CLOSURE_CONTRACT_REQUIRED");
        }, ct, requireRoomTypeSchema: false,
            featurePermission: token => CleaningExecutionAccess(ticket.UserId, branch, task, "CLEANING_EXECUTE", token));
}
