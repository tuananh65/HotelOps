using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private static readonly string[] AssignableTaskStates = ["PENDING_ASSIGNMENT", "ASSIGNED", "ACCEPTED", "IN_PROGRESS", "PAUSED"];

    public Task<RoomTypeBatchResult> ReadTaskAssignmentAsync(SessionReference ticket, long sid, long branch, long id,
        int page, string? search, CancellationToken ct)
    {
        search = search?.Trim();
        if (id <= 0 || page < 1 || page > int.MaxValue / 25 || search?.Length > 150 || search?.Any(char.IsControl) == true)
            throw new ValidationException("Invalid task or executor filters.");
        return BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            if (!await TaskAssignmentEligibility.InstalledAsync(db, token) || !await TaskEntryInstalled(token) || !await TaskDeclineStore.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var task = await TaskAssignmentEligibility.ScopedTasks(db, branch).SingleOrDefaultAsync(t => t.TaskId == id, token);
            if (task == null) return new(RoomTypeBatchStatus.NotFound);
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token);
            var version = await ProgressRevision(false, id, token);
            var state = await TaskWorkflowRead.Query(db, now).Where(t => t.TaskId == id).Select(t => t.State).SingleOrDefaultAsync(token);
            if (version == null || state == null) return new(RoomTypeBatchStatus.SchemaRequired);
            if (!AssignableTaskStates.Contains(state)) return new(RoomTypeBatchStatus.Inactive);
            var owners = await db.OperationalTaskAssignments.AsNoTracking().Where(a => a.TaskId == id && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null).ToArrayAsync(token);
            if (owners.Length != (state == "PENDING_ASSIGNMENT" ? 0 : 1) || owners.Any(a => a.AssignedAt > now)) return new(RoomTypeBatchStatus.Inactive);
            var candidates = TaskAssignmentEligibility.Candidates(db, branch, task.DepartmentId, now);
            if (!string.IsNullOrEmpty(search)) candidates = candidates.Where(u => u.FullName.Contains(search) || u.EmployeeCode.Contains(search));
            var total = await candidates.CountAsync(token);
            var users = await candidates.OrderBy(u => u.FullName).ThenBy(u => u.UserId).Skip((page - 1) * 25).Take(25)
                .Select(u => new { u.UserId, u.FullName }).ToArrayAsync(token);
            var start = TaskAssignmentEligibility.StartOfDayUtc(now);
            var rows = new List<TaskExecutorDto>();
            foreach (var user in users)
            {
                var count = await TaskAssignmentEligibility.ScopedTasks(db, branch).CountAsync(t => t.Status != "COMPLETED" && t.Status != "CANCELLED"
                    && t.OperationalTaskAssignments.Any(a => a.AssigneeId == user.UserId && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null && a.AssignedAt <= now), token);
                var seconds = await db.Database.SqlQuery<long>($"""
                    SELECT COALESCE(SUM(DATEDIFF_BIG(second,CASE WHEN effective_started_at<{start} THEN {start} ELSE effective_started_at END,
                      CASE WHEN effective_ended_at>{now} THEN {now} ELSE effective_ended_at END)),0) AS Value
                    FROM dbo.work_intervals WHERE user_id={user.UserId} AND branch_id={branch} AND closed_at IS NOT NULL
                      AND effective_ended_at>{start} AND effective_started_at<{now} AND effective_ended_at>=effective_started_at
                    """).SingleAsync(token);
                rows.Add(new(user.UserId, user.FullName, count, seconds));
            }
            return new(RoomTypeBatchStatus.Success, new TaskAssignmentReviewDto(id, branch, state, owners.SingleOrDefault()?.AssigneeId,
                new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)), "Asia/Ho_Chi_Minh", rows, total, page, 25), WorkflowTag(version.Revision));
        }, ct, requireRoomTypeSchema: false, featurePermission: token => VerificationManager(ticket.UserId, branch, token));
    }

    public async Task<RoomTypeBatchResult> AssignOperationalTaskAsync(SessionReference ticket, long sid, long branch, long id,
        string expected, AssignOperationalTaskRequest input, CancellationToken ct)
    {
        if (id <= 0 || input.AssigneeId <= 0 || !WorkflowStrongTag(expected) || input.Reason?.Length > 4000
            || input.Reason?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new ValidationException("Valid task, assignee, strong ETag and reason of at most 4000 characters required.");
        var reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim();
        // This hint grants no access. Recheck it under ordered user/time locks and
        // the task lock before touching either owner or interval.
        var hint = await db.OperationalTaskAssignments.AsNoTracking().Where(a => a.TaskId == id && a.Task.BranchId == branch
            && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null).OrderBy(a => a.AssignmentId).Take(2).ToArrayAsync(ct);
        var users = hint.Select(a => a.AssigneeId).Append(input.AssigneeId).Append(ticket.UserId).Distinct().Order().ToArray();
        var time = new SqlWorkTimeTransactionParticipant(db);
        long department = 0;
        try
        {
            return await BatchAsync(ticket, sid, branch, async (actor, token) =>
            {
                if (!await TaskAssignmentEligibility.InstalledAsync(db, token) || !await TaskEntryInstalled(token) || !await TaskDeclineStore.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
                var task = await db.OperationalTasks.FromSqlInterpolated($"SELECT * FROM dbo.operational_tasks WITH(UPDLOCK,HOLDLOCK) WHERE task_id={id}")
                    .AsNoTracking().SingleOrDefaultAsync(token);
                if (task == null || !await TaskAssignmentEligibility.ScopedTasks(db, branch).AnyAsync(t => t.TaskId == id, token)) return new(RoomTypeBatchStatus.NotFound);
                var version = await ProgressRevision(false, id, token);
                if (version == null) return new(RoomTypeBatchStatus.SchemaRequired);
                if (WorkflowTag(version.Revision) != expected) return new(RoomTypeBatchStatus.Stale);
                var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token);
                if (!AssignableTaskStates.Contains(version.State) || task.CompletedAt != null || task.ActualStartAt > now
                    || !await TaskWorkflowRead.Query(db, now).AnyAsync(t => t.TaskId == id && t.State == version.State, token)) return new(RoomTypeBatchStatus.Inactive);
                var owners = await db.OperationalTaskAssignments.AsNoTracking().Where(a => a.TaskId == id && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null).OrderBy(a => a.AssignmentId).ToArrayAsync(token);
                if (!owners.Select(a => a.AssignmentId).SequenceEqual(hint.Select(a => a.AssignmentId))
                    || !owners.Select(a => a.AssigneeId).SequenceEqual(hint.Select(a => a.AssigneeId))) return new(RoomTypeBatchStatus.Stale);
                if (owners.Length != (version.State == "PENDING_ASSIGNMENT" ? 0 : 1) || owners.Any(a => a.AssignedAt > now)) return new(RoomTypeBatchStatus.Inactive);
                var previous = owners.SingleOrDefault();
                if (previous?.AssigneeId == input.AssigneeId) return new(RoomTypeBatchStatus.Duplicate);
                if (previous != null && reason == null) throw new ValidationException("Reassignment reason is required.");
                department = task.DepartmentId;
                if (!await TaskAssignmentEligibility.Candidates(db, branch, department, now).AnyAsync(u => u.UserId == input.AssigneeId, token)) return new(RoomTypeBatchStatus.InvalidReference, new TaskEntryFailure("MSG03"));
                var intervals = await db.WorkIntervals.AsNoTracking().Where(i => i.WorkItemKind == "TASK" && i.WorkItemId == id && i.ClosedAt == null).ToArrayAsync(token);
                if (intervals.Length != (version.State == "IN_PROGRESS" ? 1 : 0)) return new(RoomTypeBatchStatus.Inactive);
                long? closedInterval = null;
                if (intervals.SingleOrDefault() is { } interval)
                {
                    if (previous == null || interval.UserId != previous.AssigneeId || interval.BranchId != branch) return new(RoomTypeBatchStatus.Inactive);
                    var closed = await time.CloseIntervalAsync(new(actor, previous.AssigneeId, branch, "TASK", id), interval.IntervalId, interval.RowVersion, token);
                    now = closed.ClosedAtUtc!.Value;
                    closedInterval = closed.IntervalId;
                    db.ChangeTracker.Clear();
                }
                if (previous != null) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_task_assignments SET assignment_status='REASSIGNED',ended_at={now} WHERE assignment_id={previous.AssignmentId}", token);
                var assignment = new OperationalTaskAssignment { TaskId = id, AssigneeId = input.AssigneeId, AssignedBy = actor, AssignedAt = now, AssignmentStatus = "ACTIVE", Note = reason };
                var progress = new OperationalTaskProgress { TaskId = id, ProgressPercent = task.ProgressPercent, OldStatus = task.Status, NewStatus = "TODO",
                    Description = previous == null ? "Task assigned." : "Task reassigned: " + reason, ReportedBy = actor, ReportedAt = now };
                db.OperationalTaskAssignments.Add(assignment); db.OperationalTaskProgresses.Add(progress);
                await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
                var recipients = previous == null ? new[] { input.AssigneeId } : new[] { previous.AssigneeId, input.AssigneeId };
                db.AuditLogs.Add(new AuditLog { ActorUserId = actor, BranchId = branch, EntityType = "operational_tasks", EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Action = "UPDATE", OccurredAt = now, OldValues = JsonSerializer.Serialize(new { workflowState = version.State, assigneeId = previous?.AssigneeId }),
                    NewValues = JsonSerializer.Serialize(new { operation = "ASSIGN", workflowState = "ASSIGNED", assignmentId = assignment.AssignmentId,
                        previousAssignmentId = previous?.AssignmentId, previousAssigneeId = previous?.AssigneeId, assigneeId = input.AssigneeId,
                        progressId = progress.ProgressId, reason, closedIntervalId = closedInterval, recipients, notificationKind = "TASK_ASSIGNED" }),
                    ChangedFields = "[\"workflowState\",\"assignment\",\"workInterval\",\"reason\",\"notificationIntent\"]" });
                await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status='ASSIGNED' WHERE task_id={id}; UPDATE dbo.operational_tasks SET status='TODO',updated_at={now} WHERE task_id={id}", token);
                return new(RoomTypeBatchStatus.Success, new TaskAssignmentResult(id, branch, input.AssigneeId, "ASSIGNED", "MSG08"), WorkflowTag((await ProgressRevision(false, id, token))!.Revision));
            }, ct, additionalUsers: users, requireRoomTypeSchema: false, afterUserLocks: token => time.LockUsersAsync(users, token),
                featurePermission: token => VerificationManager(ticket.UserId, branch, token), beforeCommit: async token =>
                {
                    if (!await TaskAssignmentEligibility.Candidates(db, branch, department, clock.GetUtcNow().UtcDateTime).AnyAsync(u => u.UserId == input.AssigneeId, token))
                        throw new ValidationException("Assignee eligibility changed. Reload the task.");
                });
        }
        catch (WorkTimeContractException error)
        {
            return new(error.Code switch
            {
                "TIME_SCOPE_DENIED" or "TIME_ACTOR_INVALID" => RoomTypeBatchStatus.Forbidden,
                "TIME_STALE" => RoomTypeBatchStatus.Stale,
                "TIME_WORKDAY_REQUIRED" or "TIME_STATE_CONFLICT" => RoomTypeBatchStatus.Inactive,
                "TIME_SOURCE_UNRESOLVED" => RoomTypeBatchStatus.SchemaRequired,
                _ => RoomTypeBatchStatus.Unavailable
            });
        }
    }
}
