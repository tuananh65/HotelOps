using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private async Task<PermissionEvaluation> CancellationAccess(long actor, long branch, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var supervisor = await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == actor && r.BranchId == branch
            && r.IsActive && r.Role.IsActive && r.Role.RoleCode == "HK_SUPERVISOR" && r.AssignedAt <= now
            && (r.ExpiresAt == null || r.ExpiresAt > now), ct);
        if (!supervisor || !await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == actor && a.BranchId == branch
            && a.RevokedAt == null && a.GrantedAt <= now, ct)) return new(PermissionDecision.Denied);
        return await CleaningExecutionAccess(actor, branch, 0, "CLEANING_ASSIGN", ct);
    }

    public Task<RoomTypeBatchResult> ReadCancellationAsync(SessionReference ticket, long sid, long branch, long task, CancellationToken ct)
    {
        if (branch <= 0 || task <= 0) throw new ValidationException("Positive branch/task required.");
        return BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            var row = await CancellationTask(branch, task, token);
            if (row == null) return new(RoomTypeBatchStatus.NotFound);
            var state = await CleaningState(task, token);
            var enabled = candidateNotification != null && await candidateNotification.CancellationAvailableAsync(token);
            var blocker = enabled ? await CancellationBlocker(row, state.State, token) : "CANCELLATION_CONTRACT_REQUIRED";
            return new(RoomTypeBatchStatus.Success, new CleaningCancellationOptions(task, branch, WorkflowTag(state.Revision),
                blocker == null ? "PROVISIONAL_V1" : "UNAVAILABLE", blocker), WorkflowTag(state.Revision));
        }, ct, requireRoomTypeSchema: false, featurePermission: token => CancellationAccess(ticket.UserId, branch, token));
    }

    private Task<CleaningTask?> CancellationTask(long branch, long task, CancellationToken ct)
        => db.CleaningTasks.AsNoTracking().SingleOrDefaultAsync(t => t.CleaningTaskId == task && t.Room.BranchId == branch
            && t.Room.IsActive && t.Room.Floor.IsActive && t.Room.Floor.BranchId == branch
            && (t.Room.AreaId == null || t.Room.Area!.IsActive && t.Room.Area.BranchId == branch), ct);

    // Fail closed for previous cycles until their terminal Inspection/readiness authority is accepted.
    // This V1 safely supports a first cycle (including earlier cancelled cycles), never infers READY.
    private async Task<string?> CancellationBlocker(CleaningTask row, string? state, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var assignments = await db.CleaningAssignments.AsNoTracking().Where(a => a.CleaningTaskId == row.CleaningTaskId
            && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null).Take(2).ToArrayAsync(ct);
        if (!CleaningCancellationRules.CanCancel(state, row.Status, row.ActualStartAt, row.ActualEndAt, assignments.Length)
            || assignments.Any(a => a.AssignedAt > now)) return "TASK_NOT_CANCELLABLE";
        var pointer = await db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS [Value] FROM dbo.room_cleaning_cycle_state WITH(UPDLOCK,HOLDLOCK) WHERE room_id={row.RoomId}").SingleOrDefaultAsync(ct);
        if (pointer != row.CleaningTaskId) return "CURRENT_CYCLE_REQUIRED";
        if (await db.WorkIntervals.AsNoTracking().AnyAsync(w => w.WorkItemKind == "CLEANING" && w.WorkItemId == row.CleaningTaskId, ct)
            || await db.CleaningProgressLogs.AsNoTracking().AnyAsync(p => p.CleaningTaskId == row.CleaningTaskId, ct)) return "TASK_HAS_EXECUTION_HISTORY";
        if (await db.RoomInspections.AsNoTracking().AnyAsync(i => i.RoomId == row.RoomId, ct)
            || await db.CleaningReworks.AsNoTracking().AnyAsync(r => r.CleaningTaskId == row.CleaningTaskId, ct)) return "INSPECTION_REWORK_SOURCE_REQUIRED";
        if (await db.RoomBlocks.AsNoTracking().AnyAsync(b => b.RoomId == row.RoomId && (b.ReleasedAt == null || b.ReleasedAt > now), ct)) return "ROOM_BLOCK_SOURCE_CONFLICT";
        if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.cleaning_tasks WITH(UPDLOCK,HOLDLOCK) WHERE room_id={row.RoomId} AND cleaning_task_id<>{row.CleaningTaskId} AND (workflow_status IS NULL OR workflow_status<>'CANCELLED' OR status<>'CANCELLED')").SingleAsync(ct) != 0)
            return "PREVIOUS_CYCLE_READINESS_CONTRACT_REQUIRED";
        if (await db.CleaningTasks.AsNoTracking().AnyAsync(t => t.RoomId == row.RoomId && t.CleaningTaskId != row.CleaningTaskId
            && (t.ActualStartAt != null || t.ActualEndAt != null || t.CleaningProgressLogs.Any() || t.CleaningReworks.Any()
                || db.WorkIntervals.Any(w => w.WorkItemKind == "CLEANING" && w.WorkItemId == t.CleaningTaskId)), ct))
            return "CANCELLED_CYCLE_SOURCE_CONFLICT";
        // DIRTY is a persisted foundation status. Other operational states require an accepted lifecycle contract.
        if (!await db.Rooms.AsNoTracking().AnyAsync(r => r.RoomId == row.RoomId && r.OperationalStatus == "DIRTY", ct)) return "ROOM_STATUS_SOURCE_CONFLICT";
        var readiness = await db.Database.SqlQuery<string>($"SELECT TOP(1) CASE WHEN occurred_at>{now} OR has_effective_block=1 THEN 'CONFLICT' ELSE readiness END AS [Value] FROM dbo.room_readiness_events WHERE room_id={row.RoomId} AND cleaning_task_id={row.CleaningTaskId} ORDER BY occurred_at DESC,event_id DESC").SingleOrDefaultAsync(ct);
        if (readiness != "PENDING_CLEANING") return "READINESS_SOURCE_REQUIRED";
        if (assignments.Length == 1 && !(await CleaningExecutionAccess(assignments[0].AssigneeUserId,
            (await db.Rooms.Where(r => r.RoomId == row.RoomId).Select(r => r.BranchId).SingleAsync(ct)), row.CleaningTaskId, "CLEANING_VIEW", ct)).IsGranted)
            return "ASSIGNEE_SOURCE_READ_REQUIRED";
        return null;
    }

    public async Task<RoomTypeBatchResult> CancelCleaningAsync(SessionReference ticket, long sid, long branch, long task,
        string tag, string key, CancelCleaningRequest input, CancellationToken ct)
    {
        var reason = CleaningCancellationRules.Validate(branch, task, tag, key, input);
        var operation = CleaningLifecycleRules.Operation(key);
        var fingerprint = CleaningCancellationRules.Fingerprint(ticket.UserId, branch, task, tag, reason);
        try { return await Core(false, ct); }
        catch (WorkTimeContractException error) { return LifecycleTimeFailure(error); }
        catch (Exception error) when (error is System.Data.Common.DbException or DbUpdateException or OperationCanceledException)
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { return await Core(true, budget.Token); }
            catch (Exception recovery) when (recovery is System.Data.Common.DbException or DbUpdateException or OperationCanceledException or WorkTimeContractException)
            { return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "CANCELLATION_OUTCOME_UNCONFIRMED"); }
        }

        async Task<RoomTypeBatchResult> Core(bool recoveryOnly, CancellationToken token)
        {
            // Preflight only supplies canonical user lock order; ownership is re-read under the transaction.
            var owners = await db.CleaningAssignments.AsNoTracking().Where(a => a.CleaningTaskId == task
                && a.CleaningTask.Room.BranchId == branch && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null)
                .Select(a => a.AssigneeUserId).Distinct().Order().ToArrayAsync(token);
            var users = owners.Append(ticket.UserId).Distinct().Order().ToArray();
            var time = new SqlWorkTimeTransactionParticipant(db);
            CleaningCancellationResult? written = null;
            return await BatchAsync(ticket, sid, branch, async (actor, inner) =>
            {
                var row = await CancellationTask(branch, task, inner);
                if (row == null) return new(RoomTypeBatchStatus.NotFound);
                if (candidateNotification == null || !await candidateNotification.CancellationAvailableAsync(inner))
                    return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "CANCELLATION_CONTRACT_REQUIRED");
                if (!await LifecycleInstalled(inner) || !await CleaningExecutionInstalled(inner)) return new(RoomTypeBatchStatus.SchemaRequired);
                var stored = await candidateNotification.FindAsync(actor, operation, inner);
                if (stored != null)
                {
                    if (!stored.Fingerprint.SequenceEqual(fingerprint)) return new(RoomTypeBatchStatus.Duplicate);
                    var receipt = JsonSerializer.Deserialize<CleaningCancellationResult>(stored.ResultJson!, Json);
                    if (receipt == null || !CleaningCancellationRules.ValidReceipt(receipt, branch, task, operation))
                        return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "DURABLE_RESULT_CONTRACT_INVALID");
                    await candidateNotification.VerifyCancellationAsync(actor, receipt, inner);
                    return new(RoomTypeBatchStatus.Success, receipt, receipt.SourceETag);
                }
                if (recoveryOnly) return new(RoomTypeBatchStatus.Unavailable);
                var state = await CleaningState(task, inner);
                if (WorkflowTag(state.Revision) != tag) return new(RoomTypeBatchStatus.Stale);
                if (await CancellationBlocker(row, state.State, inner) is { } conflict)
                    return LifecycleFailure(conflict.Contains("CONTRACT") || conflict.Contains("SOURCE") ? RoomTypeBatchStatus.Unavailable : RoomTypeBatchStatus.Inactive, conflict);
                var assignments = await db.CleaningAssignments.AsNoTracking().Where(a => a.CleaningTaskId == task
                    && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null).ToArrayAsync(inner);
                if (!assignments.Select(a => a.AssigneeUserId).Distinct().Order().SequenceEqual(owners))
                    return LifecycleFailure(RoomTypeBatchStatus.Inactive, "ASSIGNMENT_CHANGED_RETRY");
                var owner = assignments.SingleOrDefault();
                var now = DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(inner), DateTimeKind.Utc);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET status='CANCELLED',workflow_status='CANCELLED',updated_at={now} WHERE cleaning_task_id={task}", inner);
                if (owner != null)
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_assignments SET assignment_status='CANCELLED',ended_at={now} WHERE assignment_id={owner.AssignmentId}", inner);
                var eventId = (await db.Database.SqlQuery<long>($"INSERT dbo.room_readiness_events(room_id,cleaning_task_id,readiness,has_effective_block,actor_user_id,occurred_at) OUTPUT INSERTED.event_id AS [Value] VALUES({row.RoomId},{task},'PENDING_CLEANING',0,{actor},{now})").ToArrayAsync(inner)).Single();
                written = new(task, branch, row.RoomId, owner?.AssignmentId, owner?.AssigneeUserId, operation,
                    owner == null ? null : Guid.NewGuid(), eventId, now, WorkflowTag((await CleaningState(task, inner)).Revision));
                await BatchAuditAsync(actor, branch, "cleaning_tasks", task, new { state.State, row.Status, assignmentId = owner?.AssignmentId },
                    new { operationId = operation, action = "CANCEL_CLEANING", reason, confirmed = true, entity = written },
                    ["workflowStatus", "assignment", "readiness", "notification"], inner);
                await candidateNotification.StageCancellationAsync(actor, fingerprint, written, inner);
                return new(RoomTypeBatchStatus.Success, written, written.SourceETag);
            }, token, requireRoomTypeSchema: false, additionalUsers: users, preserveCommitErrors: true,
                featurePermission: inner => CancellationAccess(ticket.UserId, branch, inner),
                afterUserLocks: inner => time.LockUsersAsync(users, inner),
                beforeCommit: async inner =>
                {
                    if (!await candidateNotification!.CancellationAvailableAsync(inner)) throw new InvalidOperationException("CANCELLATION_DISABLED");
                    if (written != null) await candidateNotification.VerifyCancellationAsync(ticket.UserId, written, inner);
                });
        }
    }
}
