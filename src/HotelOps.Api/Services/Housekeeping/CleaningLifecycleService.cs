using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private async Task<bool> CleaningExecutionInstalled(CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS013.Lifecycle' AND CONVERT(nvarchar(100),value)='V1') AND OBJECT_ID('dbo.workdays') IS NOT NULL AND OBJECT_ID('dbo.work_intervals') IS NOT NULL AND OBJECT_ID('dbo.fs013_reassign_with_evidence','P') IS NOT NULL THEN 1 ELSE 0 END AS [Value]").SingleAsync(ct) == 1;
    private static RoomTypeBatchResult LifecycleFailure(RoomTypeBatchStatus status, string code) => new(status, new CleaningLifecycleFailure(code));
    private async Task<PermissionEvaluation> CleaningExecutionAccess(long actor, long branch, long task, string permission, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var data = new EfPermissionDataSource(db);
        if (!await data.IsActiveUserAsync(actor, ct) || !await data.IsActiveBranchAsync(branch, ct)) return new(PermissionDecision.Denied);
        var own = await db.CleaningAssignments.AsNoTracking().CountAsync(x => x.CleaningTaskId == task && x.AssigneeUserId == actor
            && x.AssignmentStatus == "ACTIVE" && x.EndedAt == null && x.AssignedAt <= now && x.CleaningTask.Room.BranchId == branch, ct) == 1;
        foreach (var grant in await data.ReadPathsAsync(actor, permission, now, ct))
            if (grant.PermissionCode == permission && PermissionGrantRules.IsEligible(grant, now)
                && (PermissionGrantRules.EffectiveScope(grant) == "GLOBAL" || grant.BranchId == branch &&
                    (PermissionGrantRules.EffectiveScope(grant) == "BRANCH" || PermissionGrantRules.EffectiveScope(grant) == "OWN" && own))) return new(PermissionDecision.Granted);
        return new(PermissionDecision.Denied);
    }
    public Task<RoomTypeBatchResult> ReadCleaningExecutionAsync(SessionReference ticket, long sid, long branch, long task, CancellationToken ct)
    {
        if (task <= 0) throw new ValidationException("Task must be positive.");
        return BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            var row = await db.CleaningTasks.AsNoTracking().SingleOrDefaultAsync(x => x.CleaningTaskId == task && x.Room.BranchId == branch && x.Room.IsActive, token);
            if (row == null) return new(RoomTypeBatchStatus.NotFound);
            if (!await HousekeepingInstalled(token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var state = await CleaningState(task, token);
            var assignments = await db.CleaningAssignments.AsNoTracking().Where(x => x.CleaningTaskId == task && x.AssignmentStatus == "ACTIVE" && x.EndedAt == null).Take(2).ToArrayAsync(token);
            var active = assignments.Length == 1 ? assignments[0] : null;
            var installed = await CleaningExecutionInstalled(token);
            var start = installed && active?.AssigneeUserId == ticket.UserId && (await CleaningExecutionAccess(ticket.UserId, branch, task, "CLEANING_EXECUTE", token)).IsGranted
                ? state.State == "ASSIGNED" ? "AVAILABLE" : state.State == "ON_HOLD" ? "BLOCKER_RESOLUTION_CONTRACT_REQUIRED" : "STATE_CONFLICT" : "UNAVAILABLE";
            var reassign = installed && candidateNotification != null && await candidateNotification.AvailableAsync(token)
                && (await permissions.EvaluateBranchAsync("CLEANING_ASSIGN", branch, token)).IsGranted ? "PROVISIONAL_NOT_ACCEPTED" : "UNAVAILABLE";
            return new(RoomTypeBatchStatus.Success, new CleaningExecutionDto(task, branch, row.TaskNo, state.State, active?.AssignmentId,
                active?.AssigneeUserId, row.ActualStartAt is { } time ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : null,
                WorkflowTag(state.Revision), start, reassign), WorkflowTag(state.Revision));
        }, ct, requireRoomTypeSchema: false, featurePermission: token => CleaningExecutionAccess(ticket.UserId, branch, task, "CLEANING_VIEW", token));
    }
    public async Task<RoomTypeBatchResult> StartCleaningAsync(SessionReference ticket, long sid, long branch, long task, string tag, StartCleaningRequest input, CancellationToken ct)
    {
        if (task <= 0 || !CleaningLifecycleRules.StrongTag(tag)) throw new ValidationException("Task and a strong task ETag are required.");
        if (input.Note != null) CleaningLifecycleRules.Reason(input.Note, 1);
        var time = new SqlWorkTimeTransactionParticipant(db);
        try
        {
            return await BatchAsync(ticket, sid, branch, async (actor, token) =>
            {
                if (!await CleaningExecutionInstalled(token) || !await LifecycleInstalled(token)) return new(RoomTypeBatchStatus.SchemaRequired);
                var row = await db.CleaningTasks.AsNoTracking().SingleOrDefaultAsync(x => x.CleaningTaskId == task && x.Room.BranchId == branch
                    && x.Room.IsActive && x.Room.Floor.IsActive && (x.Room.AreaId == null || x.Room.Area!.IsActive), token);
                if (row == null) return new(RoomTypeBatchStatus.NotFound);
                var state = await CleaningState(task, token);
                if (tag != WorkflowTag(state.Revision)) return new(RoomTypeBatchStatus.Stale);
                var assignments = await db.CleaningAssignments.AsNoTracking().Where(x => x.CleaningTaskId == task && x.AssignmentStatus == "ACTIVE" && x.EndedAt == null).Take(2).ToArrayAsync(token);
                if (assignments.Length != 1) return LifecycleFailure(RoomTypeBatchStatus.Inactive, "CURRENT_ASSIGNMENT_REQUIRED");
                var owner = assignments[0];
                var conflict = CleaningLifecycleRules.StartConflict(state.State, actor, owner.AssigneeUserId, input.ResolveBlocker);
                if (conflict != null) return LifecycleFailure(conflict == "CURRENT_ASSIGNEE_REQUIRED" ? RoomTypeBatchStatus.Forbidden
                    : conflict == "BLOCKER_RESOLUTION_CONTRACT_REQUIRED" ? RoomTypeBatchStatus.Unavailable : RoomTypeBatchStatus.Inactive, conflict);
                if (!await DueHousekeeper(actor, branch, DateTime.MaxValue, token)) return new(RoomTypeBatchStatus.InvalidReference);
                if (await db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS [Value] FROM dbo.room_cleaning_cycle_state WITH(UPDLOCK,HOLDLOCK) WHERE room_id={row.RoomId}").SingleOrDefaultAsync(token) != task)
                    return LifecycleFailure(RoomTypeBatchStatus.Inactive, "CURRENT_CYCLE_REQUIRED");
                // A BLOCKED history record cannot be silently interpreted as a resolved blocker.
                if (await db.CleaningProgressLogs.AnyAsync(x => x.CleaningTaskId == task && x.Status == "BLOCKED", token))
                    return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "BLOCKER_RESOLUTION_CONTRACT_REQUIRED");
                var now = DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(token), DateTimeKind.Utc);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status='IN_PROGRESS',status='IN_PROGRESS',actual_start_at=COALESCE(actual_start_at,{now}) WHERE cleaning_task_id={task}", token);
                if (!await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.cleaning_progress_versions WHERE cleaning_task_id={task}").AnyAsync(x => x > 0, token))
                {
                    var type = await db.Rooms.Where(x => x.RoomId == row.RoomId).Select(x => x.RoomTypeId).SingleAsync(token);
                    var count = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.cleaning_item_definitions WHERE room_type_id={type}").SingleAsync(token);
                    if (count is < 1 or > 100) return LifecycleFailure(RoomTypeBatchStatus.SchemaRequired, "CLEANING_ITEMS_REQUIRED");
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_progress_versions(cleaning_task_id) VALUES({task}); INSERT dbo.cleaning_task_items(cleaning_task_id,source_item_id,label,is_required,sort_order) SELECT {task},item_id,label,is_required,sort_order FROM dbo.cleaning_item_definitions WHERE room_type_id={type}", token);
                }
                var interval = await time.OpenIntervalAsync(new(actor, actor, branch, "CLEANING", task), token);
                var blocked = await db.RoomBlocks.AsNoTracking().AnyAsync(x => x.RoomId == row.RoomId && x.ReleasedAt == null, token);
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_readiness_events(room_id,cleaning_task_id,readiness,has_effective_block,actor_user_id,occurred_at) VALUES({row.RoomId},{task},'CLEANING_IN_PROGRESS',{blocked},{actor},{now})", token);
                var startedAt = row.ActualStartAt is { } first ? DateTime.SpecifyKind(first, DateTimeKind.Utc) : now;
                var result = new StartedCleaningDto(task, branch, owner.AssignmentId, "IN_PROGRESS", interval.IntervalId, startedAt, "CLEANING_IN_PROGRESS", blocked);
                await BatchAuditAsync(actor, branch, "cleaning_tasks", task, new { state.State, row.ActualStartAt }, new { action = "START_CLEANING", input.Note, entity = result }, ["workflowStatus", "actualStartAt", "workInterval", "readiness"], token);
                return new(RoomTypeBatchStatus.Success, result, WorkflowTag((await CleaningState(task, token)).Revision));
            }, ct, requireRoomTypeSchema: false, afterUserLocks: token => time.LockUsersAsync([ticket.UserId], token),
                featurePermission: token => CleaningExecutionAccess(ticket.UserId, branch, task, "CLEANING_EXECUTE", token));
        }
        catch (WorkTimeContractException error) { return LifecycleTimeFailure(error); }
    }
    private static RoomTypeBatchResult LifecycleTimeFailure(WorkTimeContractException error) => LifecycleFailure(error.Code switch
    {
        "TIME_SCOPE_DENIED" or "TIME_ACTOR_INVALID" => RoomTypeBatchStatus.Forbidden,
        "TIME_STALE" => RoomTypeBatchStatus.Stale,
        "TIME_SOURCE_UNRESOLVED" or "TIME_BUSY" => RoomTypeBatchStatus.Unavailable,
        _ => RoomTypeBatchStatus.Inactive
    }, error.Code);
}
