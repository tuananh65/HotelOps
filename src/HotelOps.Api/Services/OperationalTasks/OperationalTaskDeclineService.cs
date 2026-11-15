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
    public Task<RoomTypeBatchResult> DeclineOperationalTaskAsync(SessionReference ticket, long sid, long branch, long id,
        string expected, DeclineOperationalTaskRequest input, CancellationToken ct)
    {
        if (id <= 0 || !WorkflowStrongTag(expected) || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 4000
            || input.Reason.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ValidationException("A task, strong ETag and nonblank reason of at most 4000 characters are required.");
        var reason = input.Reason.Trim();
        long writtenAudit = 0;
        var time = new SqlWorkTimeTransactionParticipant(db);
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await TaskDeclineStore.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var task = await db.OperationalTasks.FromSqlInterpolated($"SELECT * FROM dbo.operational_tasks WITH(UPDLOCK,HOLDLOCK) WHERE task_id={id}")
                .AsNoTracking().SingleOrDefaultAsync(token);
            if (task == null || task.BranchId != branch) return new(RoomTypeBatchStatus.NotFound);
            if (!await ProgressOwned(false, id, branch, actor, token)) return new(RoomTypeBatchStatus.Forbidden);
            if (!await db.Departments.AsNoTracking().AnyAsync(d => d.DepartmentId == task.DepartmentId && d.BranchId == branch && d.IsActive, token))
                return new(RoomTypeBatchStatus.Inactive);
            var version = await ProgressRevision(false, id, token);
            if (version == null) return new(RoomTypeBatchStatus.SchemaRequired);
            if (WorkflowTag(version.Revision) != expected) return new(RoomTypeBatchStatus.Stale);
            if (version.State != "ASSIGNED" || task.Status != "TODO" || task.CompletedAt != null
                || await db.WorkIntervals.AsNoTracking().AnyAsync(i => i.WorkItemKind == "TASK" && i.WorkItemId == id && i.ClosedAt == null, token))
                return new(RoomTypeBatchStatus.Inactive);
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token);
            var assignment = await db.OperationalTaskAssignments.AsNoTracking().SingleAsync(a => a.TaskId == id && a.AssigneeId == actor
                && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null && a.AssignedAt <= now, token);
            var progress = new OperationalTaskProgress { TaskId = id, ProgressPercent = task.ProgressPercent, OldStatus = "TODO", NewStatus = "TODO",
                Description = "Task declined: " + reason, ReportedBy = actor, ReportedAt = now };
            db.OperationalTaskProgresses.Add(progress); await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
            var recipients = (await TaskSubmissionAccess.ManagersAsync(db, branch, now, token)).Where(u => u != actor).ToArray();
            var audit = new AuditLog { ActorUserId = actor, BranchId = branch, EntityType = "operational_tasks", EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Action = "UPDATE", OccurredAt = now, OldValues = JsonSerializer.Serialize(new { workflowState = "ASSIGNED", assigneeId = actor }),
                NewValues = JsonSerializer.Serialize(new { operation = "DECLINE", workflowState = "PENDING_ASSIGNMENT", assignmentId = assignment.AssignmentId,
                    progressId = progress.ProgressId, reason, recipients, notificationKind = TaskDeclineStore.NotificationKind }),
                ChangedFields = "[\"workflowState\",\"assignment\",\"reason\",\"notificationIntent\"]" };
            db.AuditLogs.Add(audit); await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.operational_task_assignments SET assignment_status='CANCELLED',ended_at={now} WHERE assignment_id={assignment.AssignmentId};
                UPDATE dbo.task_progress_versions SET workflow_status='PENDING_ASSIGNMENT' WHERE task_id={id};
                UPDATE dbo.operational_tasks SET updated_at={now} WHERE task_id={id};
                """, token);
            writtenAudit = audit.AuditId;
            return new(RoomTypeBatchStatus.Success, new TaskDeclineResult(id, branch, "PENDING_ASSIGNMENT", "MSG19"),
                WorkflowTag((await ProgressRevision(false, id, token))!.Revision));
        }, ct, requireRoomTypeSchema: false, afterUserLocks: token => time.LockUsersAsync([ticket.UserId], token), featurePermission: async token =>
        {
            if (writtenAudit == 0) return await ProgressAccess(false, id, branch, ticket.UserId, token);
            // Only the final gate of THIS transaction may prove ownership from its
            // newly persisted receipt. This does not grant historical task access.
            if (!await TaskDeclineStore.Query(db).AnyAsync(e => e.AuditId == writtenAudit && e.TaskId == id && e.BranchId == branch && e.DeclinedBy == ticket.UserId, token))
                return new(PermissionDecision.Denied);
            var data = new EfPermissionDataSource(db); var now = clock.GetUtcNow().UtcDateTime;
            if (!await data.IsActiveUserAsync(ticket.UserId, token) || !await data.IsActiveBranchAsync(branch, token)) return new(PermissionDecision.Denied);
            return new((await data.ReadPathsAsync(ticket.UserId, "TASK_EXECUTE", now, token)).Any(p => p.PermissionCode == "TASK_EXECUTE"
                && PermissionGrantRules.IsEligible(p, now) && (PermissionGrantRules.EffectiveScope(p) == "GLOBAL"
                    || p.BranchId == branch && PermissionGrantRules.EffectiveScope(p) is "BRANCH" or "OWN")) ? PermissionDecision.Granted : PermissionDecision.Denied);
        });
    }
}
