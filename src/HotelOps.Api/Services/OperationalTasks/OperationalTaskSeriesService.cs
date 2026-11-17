using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public Task<RoomTypeBatchResult> CreateTaskSeriesAsync(SessionReference ticket, long sid, long branch, CreateTaskSeriesRequest request, CancellationToken ct)
    {
        if (request.Template == null || request.Recurrence == null) throw new ValidationException("Task template and recurrence are required.");
        var template = NormalizeTaskCreation(request.Template);
        if (template.DueAt == null) throw new ValidationException("Recurring work requires its first deadline.");
        var rule = TaskRecurrenceSchedule.Normalize(request.Recurrence, template.DueAt.Value.UtcDateTime);
        var hash = TaskSeriesStore.Hash(template, rule);
        var newlyCreated = false;
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await TaskAssignmentEligibility.InstalledAsync(db, token) || !await TaskSeriesStore.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var prior = await TaskSeriesStore.Query(db).SingleOrDefaultAsync(s => s.CreationKey == template.RequestKey, token);
            if (prior != null)
            {
                if (prior.CreatedBy != actor || prior.BranchId != branch || !CryptographicOperations.FixedTimeEquals(prior.CreationHash, hash))
                    return new(RoomTypeBatchStatus.Duplicate);
                var replay = await TaskSeriesStore.DtoAsync(db, prior, token);
                return new(RoomTypeBatchStatus.Success, new { series = replay, messageCode = "MSG08" }, replay.ETag);
            }
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token);
            if(template.InternalRequestId is {} source)
            {
                if(!await TaskSeriesRequestLink.AllowedAsync(db,actor,branch,source,now,token))return new(RoomTypeBatchStatus.Forbidden);
                if(!(await HotelOps.Api.Services.InternalRequests.InternalRequestDecisionStore.RevisionAsync(db,source,token)).SequenceEqual(RequestCreationVersion(template.RequestLink!)))
                    return new(RoomTypeBatchStatus.Stale);
            }
            if (!await TaskCreationReferencesAsync(db, branch, template, token)
                || !await TaskAssignmentEligibility.Candidates(db, branch, template.DepartmentId, now).AnyAsync(u => u.UserId == rule.DefaultAssigneeId, token))
                return new(RoomTypeBatchStatus.InvalidReference);
            var mask = rule.Weekdays!.Aggregate(0, (value, day) => value | 1 << day);
            var json = JsonSerializer.Serialize(template);
            var ids = await db.Database.SqlQuery<long>($"""
                DECLARE @created TABLE(Value bigint NOT NULL);
                INSERT dbo.operational_task_series(branch_id,department_id,created_by,default_assignee_id,rule_code,weekday_mask,month_day,
                    first_due_at,next_due_at,end_date,is_active,template_json,creation_key,creation_hash,created_at)
                OUTPUT inserted.series_id INTO @created
                VALUES({branch},{template.DepartmentId},{actor},{rule.DefaultAssigneeId},{rule.Rule},{mask},{rule.MonthDay},
                    {template.DueAt.Value.UtcDateTime},{template.DueAt.Value.UtcDateTime},{rule.EndDate},1,{json},{template.RequestKey},{hash},{now});
                SELECT Value FROM @created;
                """).ToArrayAsync(token);
            var id = ids.Single();
            newlyCreated = true;
            db.AuditLogs.Add(new AuditLog { ActorUserId = actor, BranchId = branch, EntityType = "operational_task_series", EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Action = "CREATE", OccurredAt = now, NewValues = JsonSerializer.Serialize(new { operation = "CREATE_SERIES", template, recurrence = rule, isActive = true }),
                ChangedFields = "[\"template\",\"recurrence\",\"defaultAssignee\",\"isActive\"]" });
            await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
            var row = await TaskSeriesStore.Query(db).SingleAsync(s => s.SeriesId == id, token);
            // A series created after today's scheduled run must still materialize
            // its first eligible occurrence inside the same authorized transaction.
            await TaskSeriesGeneration.MaterializeAsync(db, row, now, token);
            row = await TaskSeriesStore.Query(db).SingleAsync(s => s.SeriesId == id, token);
            var dto = await TaskSeriesStore.DtoAsync(db, row, token);
            return new(RoomTypeBatchStatus.Success, new { series = dto, messageCode = "MSG08" }, dto.ETag);
        }, ct, additionalUsers: [rule.DefaultAssigneeId], requireRoomTypeSchema: false,
            featurePermission: token => LinkedTaskCreationAuthority(ticket.UserId, branch, template.InternalRequestId, token), beforeCommit: async token =>
            {
                if (newlyCreated && !await TaskAssignmentEligibility.Candidates(db, branch, template.DepartmentId, clock.GetUtcNow().UtcDateTime).AnyAsync(u => u.UserId == rule.DefaultAssigneeId, token))
                    throw new ValidationException("Default executor eligibility changed.");
            });
    }

    public Task<RoomTypeBatchResult> ReadTaskSeriesAsync(SessionReference ticket, long sid, long branch, int page, long? series, CancellationToken ct)
    {
        if (page < 1 || page > int.MaxValue / 25 || series is <= 0) throw new ValidationException("Invalid recurring task page or series.");
        return BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            if (!await TaskSeriesStore.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var rows = TaskSeriesStore.Query(db).Where(s => s.BranchId == branch
                && (series == null || s.SeriesId == series)
                && db.Departments.Any(d => d.DepartmentId == s.DepartmentId && d.BranchId == branch));
            var total = await rows.CountAsync(token);
            var selected = await rows.OrderByDescending(s => s.SeriesId).Skip((page - 1) * 25).Take(25).ToArrayAsync(token);
            var items = new List<TaskSeriesDto>();
            foreach (var row in selected) items.Add(await TaskSeriesStore.DtoAsync(db, row, token));
            return new(RoomTypeBatchStatus.Success, new TaskSeriesPage(items, total, page, 25));
        }, ct, requireRoomTypeSchema: false, featurePermission: token => VerificationManager(ticket.UserId, branch, token));
    }

    public async Task<RoomTypeBatchResult> StopTaskSeriesAsync(SessionReference ticket, long sid, long branch, long id, string expected, StopTaskSeriesRequest request, CancellationToken ct)
    {
        if (id <= 0 || request.TaskId is <= 0 || !WorkflowStrongTag(expected)) throw new ValidationException("Series identity and strong ETag required.");
        var reason = TaskPlanningReason(request.Reason);
        // Task-context Stop follows UC103's assignee notification step. A list
        // action without a selected task must not guess a historical recipient.
        var hint = request.TaskId is { } taskId
            ? await db.OperationalTaskAssignments.AsNoTracking().Where(a => a.TaskId == taskId && a.Task.BranchId == branch && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null)
                .OrderBy(a => a.AssignmentId).Take(2).ToArrayAsync(ct) : [];
        return await BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await TaskSeriesStore.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var locked = await db.Database.SqlQuery<long>($"SELECT series_id AS Value FROM dbo.operational_task_series WITH(UPDLOCK,HOLDLOCK) WHERE series_id={id} AND branch_id={branch}").ToArrayAsync(token);
            if (locked.Length == 0) return new(RoomTypeBatchStatus.NotFound);
            var row = await TaskSeriesStore.Query(db).SingleAsync(s => s.SeriesId == id, token);
            if ('"' + Convert.ToHexString(row.Revision) + '"' != expected) return new(RoomTypeBatchStatus.Stale);
            if (!row.IsActive) return new(RoomTypeBatchStatus.Inactive);
            if (!await db.Departments.AnyAsync(d => d.DepartmentId == row.DepartmentId && d.BranchId == branch, token)) return new(RoomTypeBatchStatus.NotFound);
            _ = TaskSeriesStore.Template(row);
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token);
            if (request.TaskId is { } sourceTask)
            {
                if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_series_occurrences o JOIN dbo.operational_tasks t ON t.task_id=o.task_id WHERE o.series_id={id} AND o.task_id={sourceTask} AND t.branch_id={branch} AND t.department_id={row.DepartmentId}").SingleAsync(token) != 1)
                    return new(RoomTypeBatchStatus.NotFound);
                if (!await TaskWorkflowRead.Query(db, now).AnyAsync(t => t.TaskId == sourceTask && t.State != "CLOSED" && t.State != "CANCELLED", token))
                    return new(RoomTypeBatchStatus.Inactive);
                var current = await db.OperationalTaskAssignments.AsNoTracking().Where(a => a.TaskId == sourceTask && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null).OrderBy(a => a.AssignmentId).ToArrayAsync(token);
                if (current.Length > 1 || !current.Select(a => (a.AssignmentId, a.AssigneeId)).SequenceEqual(hint.Select(a => (a.AssignmentId, a.AssigneeId))) || current.Any(a => a.AssignedAt > now))
                    return new(RoomTypeBatchStatus.Stale);
            }
            var assignment = hint.SingleOrDefault();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_task_series SET is_active=0,stopped_by={actor},stopped_at={now} WHERE series_id={id}", token);
            db.AuditLogs.Add(new AuditLog { ActorUserId = actor, BranchId = branch, EntityType = "operational_task_series", EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Action = "UPDATE", OccurredAt = now, OldValues = "{\"isActive\":true}", NewValues = JsonSerializer.Serialize(new { operation = "STOP_SERIES", isActive = false, reason,
                    taskId = request.TaskId, assignmentId = assignment?.AssignmentId, recipientId = assignment?.AssigneeId, notificationKind = "TASK_SERIES_STOPPED" }),
                ChangedFields = "[\"isActive\",\"stoppedAt\",\"stoppedBy\",\"notificationIntent\"]" });
            await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
            var dto = await TaskSeriesStore.DtoAsync(db, await TaskSeriesStore.Query(db).SingleAsync(s => s.SeriesId == id, token), token);
            return new(RoomTypeBatchStatus.Success, new { series = dto, messageCode = "MSG08" }, dto.ETag);
        }, ct, additionalUsers: hint.Select(a => a.AssigneeId).ToArray(), requireRoomTypeSchema: false, featurePermission: token => VerificationManager(ticket.UserId, branch, token));
    }
}
