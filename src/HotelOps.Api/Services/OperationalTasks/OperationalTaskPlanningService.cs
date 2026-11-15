using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private static string? TaskPlanningReason(string? value)
    {
        if (value?.Length > 4000 || value?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new ValidationException("Reason must contain at most 4000 characters without unsupported controls.");
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
    public Task<RoomTypeBatchResult> ReadTaskPlanAsync(SessionReference ticket, long sid, long branch, long id, int page, CancellationToken ct)
    {
        if (id <= 0 || page < 1 || page > int.MaxValue / 25) throw new ValidationException("Invalid task or history page.");
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await TaskPlanningStore.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var task = await TaskAssignmentEligibility.ScopedTasks(db, branch).SingleOrDefaultAsync(t => t.TaskId == id, token);
            if (task == null) return new(RoomTypeBatchStatus.NotFound);
            var version = await ProgressRevision(false, id, token);
            if (version == null) return new(RoomTypeBatchStatus.SchemaRequired);
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token);
            if (!await TaskWorkflowRead.Query(db, now).AnyAsync(v => v.TaskId == id && v.State == version.State, token)) return new(RoomTypeBatchStatus.Inactive);
            var definition = await TaskPlanningStore.Definitions(db).SingleOrDefaultAsync(d => d.TaskId == id, token);
            var manager = (await VerificationManager(actor, branch, token)).IsGranted;
            var manage = manager && version.State is not ("CLOSED" or "CANCELLED");
            long? series = null;
            if (manage && await TaskSeriesStore.InstalledAsync(db, token))
                series = await db.Database.SqlQuery<long?>($"SELECT o.series_id AS Value FROM dbo.task_series_occurrences o JOIN dbo.operational_task_series s ON s.series_id=o.series_id WHERE o.task_id={id} AND s.branch_id={branch} AND s.department_id={task.DepartmentId}").SingleOrDefaultAsync(token);
            var canCancel = manage && version.State is "PENDING_ASSIGNMENT" or "ASSIGNED" or "ACCEPTED" && task.ActualStartAt == null
                && !await db.WorkIntervals.AnyAsync(i => i.WorkItemKind == "TASK" && i.WorkItemId == id, token);
            var audit = TaskPlanNotificationStore.Query(db).Where(a => a.TaskId == id && a.BranchId == branch);
            var count = await audit.CountAsync(token);
            var history = await audit.OrderByDescending(a => a.AuditId).Skip((page - 1) * 25).Take(25).ToArrayAsync(token);
            return new(RoomTypeBatchStatus.Success, new TaskPlanDto(id, branch, version.State, definition?.RequestedResult, task.PriorityId,
                TaskPlanUtc(task.PlannedStartAt), TaskPlanUtc(task.DueAt), definition?.SlaMinutes, manage, canCancel,
                history.Select(a => new TaskPlanHistoryDto(a.AuditId, TaskPlanUtc(a.At)!.Value, a.ActorId, a.PreviousPriorityId,
                    TaskPlanUtc(a.PreviousPlannedStartAt), TaskPlanUtc(a.PreviousDueAt), a.PreviousSlaMinutes,
                    a.PriorityId, TaskPlanUtc(a.PlannedStartAt), TaskPlanUtc(a.DueAt), a.SlaMinutes, a.Reason)).ToArray(), count, page, 25, series), WorkflowTag(version.Revision));
        }, ct, requireRoomTypeSchema: false, featurePermission: async token => (await VerificationManager(ticket.UserId, branch, token)).IsGranted
            ? new(PermissionDecision.Granted) : new(await TaskVerificationStore.ExecutorReadAsync(db, ticket.UserId, branch, id, clock.GetUtcNow().UtcDateTime, token) ? PermissionDecision.Granted : PermissionDecision.Denied));
    }

    public Task<RoomTypeBatchResult> PlanOperationalTaskAsync(SessionReference ticket, long sid, long branch, long id, string expected, PlanOperationalTaskRequest input, CancellationToken ct)
    {
        ValidateTaskPlan(input.PriorityId, input.PlannedStartAt, input.DueAt, input.SlaMinutes);
        return EditTaskPlanAsync(ticket, sid, branch, id, expected, input, TaskPlanningReason(input.Reason), ct);
    }
    public Task<RoomTypeBatchResult> CancelOperationalTaskAsync(SessionReference ticket, long sid, long branch, long id, string expected, CancelOperationalTaskRequest input, CancellationToken ct)
    {
        var reason = TaskPlanningReason(input.Reason);
        if (reason == null || reason.Length < 10) throw new ValidationException("Cancellation reason must contain at least 10 characters.");
        return EditTaskPlanAsync(ticket, sid, branch, id, expected, null, reason, ct);
    }
    private async Task<RoomTypeBatchResult> EditTaskPlanAsync(SessionReference ticket, long sid, long branch, long id, string expected,
        PlanOperationalTaskRequest? input, string? reason, CancellationToken ct)
    {
        if (id <= 0 || !WorkflowStrongTag(expected)) throw new ValidationException("A task and strong ETag are required.");
        var hint = await db.OperationalTaskAssignments.AsNoTracking().Where(a => a.TaskId == id && a.Task.BranchId == branch && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null)
            .OrderBy(a => a.AssignmentId).Take(2).ToArrayAsync(ct);
        var users = hint.Select(a => a.AssigneeId).Append(ticket.UserId).Distinct().Order().ToArray();
        var time = new SqlWorkTimeTransactionParticipant(db);
        return await BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await TaskPlanningStore.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var task = await db.OperationalTasks.FromSqlInterpolated($"SELECT * FROM dbo.operational_tasks WITH(UPDLOCK,HOLDLOCK) WHERE task_id={id}").AsNoTracking().SingleOrDefaultAsync(token);
            if (task == null || !await TaskAssignmentEligibility.ScopedTasks(db, branch).AnyAsync(t => t.TaskId == id, token)) return new(RoomTypeBatchStatus.NotFound);
            var version = await ProgressRevision(false, id, token);
            if (version == null) return new(RoomTypeBatchStatus.SchemaRequired);
            if (WorkflowTag(version.Revision) != expected) return new(RoomTypeBatchStatus.Stale);
            if (version.State is "CLOSED" or "CANCELLED" || task.CompletedAt != null) return new(RoomTypeBatchStatus.Inactive);
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token);
            if (!await TaskWorkflowRead.Query(db, now).AnyAsync(v => v.TaskId == id && v.State == version.State, token)) return new(RoomTypeBatchStatus.Inactive);
            var owners = await db.OperationalTaskAssignments.AsNoTracking().Where(a => a.TaskId == id && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null).OrderBy(a => a.AssignmentId).ToArrayAsync(token);
            if (!owners.Select(a => a.AssignmentId).SequenceEqual(hint.Select(a => a.AssignmentId))
                || !owners.Select(a => a.AssigneeId).SequenceEqual(hint.Select(a => a.AssigneeId))) return new(RoomTypeBatchStatus.Stale);
            if (owners.Length != (version.State == "PENDING_ASSIGNMENT" ? 0 : 1) || owners.Any(a => a.AssignedAt > now)) return new(RoomTypeBatchStatus.Inactive);
            var owner = owners.SingleOrDefault();
            if (owner != null && reason == null) throw new ValidationException("Changing an assigned task plan requires a reason.");
            var definition = await TaskPlanningStore.Definitions(db).SingleOrDefaultAsync(d => d.TaskId == id, token);
            if (input == null)
            {
                if (version.State is not ("PENDING_ASSIGNMENT" or "ASSIGNED" or "ACCEPTED") || task.ActualStartAt != null
                    || await db.WorkIntervals.AnyAsync(i => i.WorkItemKind == "TASK" && i.WorkItemId == id, token)) return new(RoomTypeBatchStatus.Inactive);
                if (owner != null) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_task_assignments SET assignment_status='CANCELLED',ended_at={now} WHERE assignment_id={owner.AssignmentId}", token);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status='CANCELLED' WHERE task_id={id}; UPDATE dbo.operational_tasks SET status='CANCELLED',updated_at={now} WHERE task_id={id}", token);
            }
            else
            {
                if (!await ValidTaskPriority(input.PriorityId, token)) return new(RoomTypeBatchStatus.InvalidReference);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_tasks SET priority_id={input.PriorityId},planned_start_at={input.PlannedStartAt?.UtcDateTime},due_at={input.DueAt?.UtcDateTime},updated_at={now} WHERE task_id={id}", token);
                if (definition == null) await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.operational_task_definitions(task_id,sla_minutes) VALUES({id},{input.SlaMinutes})", token);
                else await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_task_definitions SET sla_minutes={input.SlaMinutes} WHERE task_id={id}", token);
            }
            var state = input == null ? "CANCELLED" : version.State;
            var progress = new OperationalTaskProgress { TaskId = id, ProgressPercent = task.ProgressPercent, OldStatus = task.Status,
                NewStatus = input == null ? "CANCELLED" : task.Status, Description = input == null ? "Task cancelled: " + reason : "Task plan updated." + (reason == null ? "" : " " + reason), ReportedBy = actor, ReportedAt = now };
            db.OperationalTaskProgresses.Add(progress); await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
            var recipients = owner == null ? Array.Empty<long>() : [owner.AssigneeId];
            db.AuditLogs.Add(new AuditLog { ActorUserId = actor, BranchId = branch, EntityType = "operational_tasks", EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "UPDATE", OccurredAt = now,
                OldValues = JsonSerializer.Serialize(new { workflowState = version.State, priorityId = task.PriorityId, plannedStartAt = task.PlannedStartAt, dueAt = task.DueAt, slaMinutes = definition?.SlaMinutes }),
                NewValues = JsonSerializer.Serialize(new { operation = input == null ? "CANCEL_TASK" : "PLAN", workflowState = state,
                    priorityId = input?.PriorityId ?? task.PriorityId, plannedStartAt = input == null ? task.PlannedStartAt : input.PlannedStartAt?.UtcDateTime,
                    dueAt = input == null ? task.DueAt : input.DueAt?.UtcDateTime, slaMinutes = input == null ? definition?.SlaMinutes : input.SlaMinutes,
                    reason, assignmentId = owner?.AssignmentId, recipientId = owner?.AssigneeId, recipients, progressId = progress.ProgressId,
                    notificationKind = input == null ? "TASK_CANCELLED" : "TASK_PLANNED" }),
                ChangedFields = input == null ? "[\"workflowState\",\"assignment\",\"reason\",\"notificationIntent\"]" : "[\"priority\",\"plannedStartAt\",\"dueAt\",\"slaMinutes\",\"reason\",\"notificationIntent\"]" });
            await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
            return new(RoomTypeBatchStatus.Success, new { taskId = id, branchId = branch, state, messageCode = "MSG08" }, WorkflowTag((await ProgressRevision(false, id, token))!.Revision));
        }, ct, additionalUsers: users, requireRoomTypeSchema: false, featurePermission: token => VerificationManager(ticket.UserId, branch, token), afterUserLocks: token => time.LockUsersAsync(users, token));
    }
}
