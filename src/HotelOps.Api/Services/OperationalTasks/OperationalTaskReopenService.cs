using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    internal static (string Reason, string Instructions) NormalizeTaskReopen(ReopenOperationalTaskRequest input)
    {
        var reason = TaskResultText(input.Reason);
        var instructions = TaskResultText(input.Instructions);
        if (input.AssigneeId <= 0 || reason == null || reason.Length < 10 || instructions == null)
            throw new ValidationException("An eligible assignee, reason of at least 10 characters and remediation instructions are required.");
        return (reason, instructions);
    }

    private async Task<StoredTaskVerification?> ClosedTaskVerification(OperationalTask task, CancellationToken ct)
    {
        if (task.Status != "COMPLETED" || task.CompletedAt == null || task.ActualStartAt == null
            || await db.OperationalTaskAssignments.AnyAsync(a => a.TaskId == task.TaskId && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null, ct)
            || await db.WorkIntervals.AnyAsync(i => i.WorkItemKind == "TASK" && i.WorkItemId == task.TaskId && i.ClosedAt == null, ct)) return null;
        var submission = await TaskSubmissionStore.Query(db).Where(s => s.TaskId == task.TaskId).OrderByDescending(s => s.ProgressId).FirstOrDefaultAsync(ct);
        if (submission == null) return null;
        var verified = await TaskVerificationStore.Query(db).SingleOrDefaultAsync(v => v.SubmissionId == submission.ProgressId
            && v.Decision == "ACCEPT" && v.DecidedAt == task.CompletedAt, ct);
        if (verified == null || !await db.OperationalTaskAssignments.AnyAsync(a => a.AssignmentId == verified.AssignmentId
            && a.AssignmentStatus == "COMPLETED" && a.EndedAt == verified.DecidedAt, ct)) return null;
        return verified;
    }

    public Task<RoomTypeBatchResult> ReadTaskReopenAsync(SessionReference ticket, long sid, long branch, long id,
        int page, string? search, CancellationToken ct)
    {
        search = search?.Trim();
        if (id <= 0 || page < 1 || page > int.MaxValue / 25 || search?.Length > 150 || search?.Any(char.IsControl) == true)
            throw new ValidationException("Invalid task or executor filters.");
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await TaskAssignmentEligibility.InstalledAsync(db, token) || !await TaskReopenStore.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var task = await TaskAssignmentEligibility.ScopedTasks(db, branch).SingleOrDefaultAsync(t => t.TaskId == id, token);
            if (task == null) return new(RoomTypeBatchStatus.NotFound);
            var version = await ProgressRevision(false, id, token);
            if (version == null) return new(RoomTypeBatchStatus.SchemaRequired);
            var verified = version.State == "CLOSED" ? await ClosedTaskVerification(task, token) : null;
            var can = verified != null && (await VerificationManager(actor, branch, token)).IsGranted;
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token);
            var rows = new List<TaskExecutorDto>(); var total = 0;
            if (can)
            {
                var candidates = TaskAssignmentEligibility.Candidates(db, branch, task.DepartmentId, now);
                if (!string.IsNullOrEmpty(search)) candidates = candidates.Where(u => u.FullName.Contains(search) || u.EmployeeCode.Contains(search));
                total = await candidates.CountAsync(token);
                var users = await candidates.OrderBy(u => u.FullName).ThenBy(u => u.UserId).Skip((page - 1) * 25).Take(25)
                    .Select(u => new { u.UserId, u.FullName }).ToArrayAsync(token);
                var start = TaskAssignmentEligibility.StartOfDayUtc(now);
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
            }
            var history = await TaskReopenStore.Query(db).Where(r => r.TaskId == id).OrderBy(r => r.PreviousSubmissionId).ToArrayAsync(token);
            return new(RoomTypeBatchStatus.Success, new TaskReopenReviewDto(id, branch, version.State, can,
                verified?.SubmittedBy, rows, total, page, 25, history.Select(TaskReopenStore.Dto).ToArray()), WorkflowTag(version.Revision));
        }, ct, requireRoomTypeSchema: false, featurePermission: async token =>
            (await VerificationManager(ticket.UserId, branch, token)).IsGranted ? new(PermissionDecision.Granted) :
            new(await TaskVerificationStore.ExecutorReadAsync(db, ticket.UserId, branch, id, clock.GetUtcNow().UtcDateTime, token) ? PermissionDecision.Granted : PermissionDecision.Denied));
    }

    public Task<RoomTypeBatchResult> ReopenOperationalTaskAsync(SessionReference ticket, long sid, long branch, long id,
        string expected, ReopenOperationalTaskRequest input, CancellationToken ct)
    {
        if (id <= 0 || !WorkflowStrongTag(expected)) throw new ValidationException("A task and strong ETag are required.");
        var (reason, instructions) = NormalizeTaskReopen(input);
        long department = 0;
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await TaskAssignmentEligibility.InstalledAsync(db, token) || !await TaskReopenStore.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var task = await db.OperationalTasks.FromSqlInterpolated($"SELECT * FROM dbo.operational_tasks WITH(UPDLOCK,HOLDLOCK) WHERE task_id={id}")
                .AsNoTracking().SingleOrDefaultAsync(token);
            if (task == null || !await TaskAssignmentEligibility.ScopedTasks(db, branch).AnyAsync(t => t.TaskId == id, token)) return new(RoomTypeBatchStatus.NotFound);
            var version = await ProgressRevision(false, id, token);
            if (version == null) return new(RoomTypeBatchStatus.SchemaRequired);
            if (WorkflowTag(version.Revision) != expected) return new(RoomTypeBatchStatus.Stale);
            var verified = version.State == "CLOSED" ? await ClosedTaskVerification(task, token) : null;
            if (verified == null) return new(RoomTypeBatchStatus.Inactive);
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token);
            if (task.ActualStartAt > now || verified.DecidedAt > now) return new(RoomTypeBatchStatus.Inactive);
            department = task.DepartmentId;
            if (!await TaskAssignmentEligibility.Candidates(db, branch, department, now).AnyAsync(u => u.UserId == input.AssigneeId, token))
                return new(RoomTypeBatchStatus.InvalidReference, new TaskEntryFailure("MSG03"));
            var cutoff = await db.WorkIntervals.Where(i => i.WorkItemKind == "TASK" && i.WorkItemId == id).MaxAsync(i => (long?)i.IntervalId, token);
            if (cutoff == null || !await db.WorkIntervals.AnyAsync(i => i.IntervalId == cutoff && i.BranchId == branch && i.ClosedAt <= verified.DecidedAt, token))
                return new(RoomTypeBatchStatus.Inactive);
            var assignment = new OperationalTaskAssignment { TaskId = id, AssigneeId = input.AssigneeId, AssignedBy = actor,
                AssignedAt = now, AssignmentStatus = "ACTIVE", Note = reason };
            db.OperationalTaskAssignments.Add(assignment);
            db.OperationalTaskProgresses.Add(new() { TaskId = id, ProgressPercent = 0, OldStatus = "COMPLETED", NewStatus = "TODO",
                Description = "Task reopened for remediation.", ReportedBy = actor, ReportedAt = now });
            await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
            var audit = new AuditLog { ActorUserId = actor, BranchId = branch, EntityType = "operational_tasks",
                EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "UPDATE", OccurredAt = now,
                OldValues = JsonSerializer.Serialize(new { workflowState = "CLOSED", task.CompletedAt, task.ActualStartAt, task.ProgressPercent }),
                NewValues = JsonSerializer.Serialize(new { operation = "REOPEN", workflowState = "ASSIGNED", previousSubmissionId = verified.SubmissionId,
                    assignmentId = assignment.AssignmentId, assigneeId = input.AssigneeId, intervalCutoff = cutoff.Value, reason, instructions,
                    notificationKind = TaskReopenStore.NotificationKind }),
                ChangedFields = "[\"workflowState\",\"processingCycle\",\"assignment\",\"progress\",\"notificationIntent\"]" };
            db.AuditLogs.Add(audit); await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.task_reopen_events(previous_submission_id,task_id,assignment_id,reopened_by,reopened_at,interval_cutoff,reason,instructions,audit_id)
                VALUES({verified.SubmissionId},{id},{assignment.AssignmentId},{actor},{now},{cutoff.Value},{reason},{instructions},{audit.AuditId});
                UPDATE dbo.task_progress_versions SET workflow_status='ASSIGNED' WHERE task_id={id};
                UPDATE dbo.operational_tasks SET status='TODO',completed_at=NULL,progress_percent=0,updated_at={now} WHERE task_id={id};
                UPDATE dbo.task_completion_criteria SET outcome=NULL,note=NULL WHERE task_id={id};
                """, token);
            return new(RoomTypeBatchStatus.Success, new TaskAssignmentResult(id, branch, input.AssigneeId, "ASSIGNED", "MSG08"),
                WorkflowTag((await ProgressRevision(false, id, token))!.Revision));
        }, ct, additionalUsers: [input.AssigneeId], requireRoomTypeSchema: false,
            featurePermission: token => VerificationManager(ticket.UserId, branch, token), beforeCommit: async token =>
            {
                if (!await TaskAssignmentEligibility.Candidates(db, branch, department, clock.GetUtcNow().UtcDateTime).AnyAsync(u => u.UserId == input.AssigneeId, token))
                    throw new ValidationException("Assignee eligibility changed. Reload the task.");
            });
    }
}
