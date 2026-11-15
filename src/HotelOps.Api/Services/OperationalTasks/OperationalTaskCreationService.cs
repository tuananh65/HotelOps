using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private static readonly string[] TaskPriorityCodes = ["LOW", "NORMAL", "HIGH", "CRITICAL"];
    private static void ValidateTaskPlan(long priority, DateTimeOffset? start, DateTimeOffset? due, int? minutes)
    {
        if (priority <= 0 || minutes is <= 0 || start != null && due != null && due < start)
            throw new ValidationException("Valid priority and positive duration required; deadline cannot precede planned start.");
    }
    private Task<bool> ValidTaskPriority(long priority, CancellationToken ct) => db.ReferenceValues.AsNoTracking()
        .AnyAsync(r => r.ReferenceId == priority && r.GroupCode == "PRIORITY" && TaskPriorityCodes.Contains(r.ValueCode) && r.IsActive, ct);
    private static DateTimeOffset? TaskPlanUtc(DateTime? value) => value is { } time ? new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Utc)) : null;

    public Task<RoomTypeBatchResult> TaskCreationOptionsAsync(SessionReference ticket, long sid, long branch, long? department, CancellationToken ct)
    {
        if (department is <= 0) throw new ValidationException("Invalid department.");
        return BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            if (!await TaskPlanningStore.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var departments = await db.Departments.AsNoTracking().Where(d => d.BranchId == branch && d.IsActive)
                .OrderBy(d => d.DepartmentName).ThenBy(d => d.DepartmentId).Select(d => new TaskPlanningLookup(d.DepartmentId, d.DepartmentName)).ToArrayAsync(token);
            if (department != null && !departments.Any(d => d.Id == department)) return new(RoomTypeBatchStatus.InvalidReference);
            var supported = TaskPlanningStore.OperationalCategories(db);
            var categories = await db.WorkCategories.AsNoTracking().Where(c => c.IsActive && supported.Contains(c.CategoryId)
                && (c.DepartmentId == null || c.DepartmentId == department && c.Department!.BranchId == branch && c.Department.IsActive))
                .OrderBy(c => c.CategoryName).ThenBy(c => c.CategoryId).Select(c => new TaskPlanningLookup(c.CategoryId, c.CategoryName)).ToArrayAsync(token);
            var priorities = await db.ReferenceValues.AsNoTracking().Where(r => r.GroupCode == "PRIORITY" && r.IsActive && TaskPriorityCodes.Contains(r.ValueCode))
                .OrderBy(r => r.SortOrder).ThenBy(r => r.ReferenceId).Select(r => new TaskPlanningLookup(r.ReferenceId, r.DisplayName)).ToArrayAsync(token);
            return new(RoomTypeBatchStatus.Success, new TaskPlanningOptions(departments, categories, priorities));
        }, ct, requireRoomTypeSchema: false, featurePermission: token => VerificationManager(ticket.UserId, branch, token));
    }

    public Task<RoomTypeBatchResult> CreateOperationalTaskAsync(SessionReference ticket, long sid, long branch, CreateOperationalTaskRequest input, CancellationToken ct)
    {
        if (input.RequestKey == Guid.Empty || input.DepartmentId <= 0 || input.CategoryId <= 0 || input.RoomId is <= 0 || input.AreaId is <= 0
            || input.RoomId != null && input.AreaId != null || input.InternalRequestId is <= 0)
            throw new ValidationException("Valid creation key, department, category and at most one location required.");
        var title = ChecklistText(input.Title, 200); var outcome = ChecklistText(input.RequestedResult, 4000);
        var description = string.IsNullOrWhiteSpace(input.Description) ? null : ChecklistText(input.Description, 4000);
        var criteria = Definitions(new InitializeProgressRequest(input.Criteria, "Create task"));
        ValidateTaskPlan(input.PriorityId, input.PlannedStartAt, input.DueAt, input.SlaMinutes);
        var normalized = input with { Title = title, RequestedResult = outcome, Description = description, Criteria = criteria,
            PlannedStartAt = input.PlannedStartAt?.ToUniversalTime(), DueAt = input.DueAt?.ToUniversalTime() };
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(normalized));
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await TaskPlanningStore.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            // UC118/FE13 still lacks its public resource authorization contract.
            // Do not infer permission to bind somebody else's Internal Request.
            if (input.InternalRequestId != null) return new(RoomTypeBatchStatus.SchemaRequired);
            var prior = await TaskPlanningStore.Definitions(db).SingleOrDefaultAsync(d => d.CreationKey == input.RequestKey, token);
            if (prior != null)
            {
                var existing = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.TaskId == prior.TaskId, token);
                if (existing.CreatedBy != actor || existing.BranchId != branch || prior.CreationHash == null || !CryptographicOperations.FixedTimeEquals(hash, prior.CreationHash))
                    return new(RoomTypeBatchStatus.Duplicate);
                var revision = await ProgressRevision(false, existing.TaskId, token);
                if (revision == null) return new(RoomTypeBatchStatus.SchemaRequired);
                return new(RoomTypeBatchStatus.Success, new TaskCreatedDto(existing.TaskId, branch, existing.TaskCode, revision.State, "MSG08"), WorkflowTag(revision.Revision));
            }
            if (!await db.Departments.AnyAsync(d => d.DepartmentId == input.DepartmentId && d.BranchId == branch && d.IsActive, token)
                || !await db.WorkCategories.AnyAsync(c => c.CategoryId == input.CategoryId && c.IsActive
                    && (c.DepartmentId == null || c.DepartmentId == input.DepartmentId), token)
                || !await TaskPlanningStore.OperationalCategories(db).ContainsAsync(input.CategoryId, token)
                || !await ValidTaskPriority(input.PriorityId, token)
                || input.RoomId != null && !await db.Rooms.AnyAsync(r => r.RoomId == input.RoomId && r.BranchId == branch && r.IsActive, token)
                || input.AreaId != null && !await db.Areas.AnyAsync(a => a.AreaId == input.AreaId && a.BranchId == branch && a.IsActive, token))
                return new(RoomTypeBatchStatus.InvalidReference, new TaskEntryFailure("MSG03"));
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token);
            var task = new OperationalTask { TaskCode = "TASK-" + input.RequestKey.ToString("N").ToUpperInvariant(), BranchId = branch,
                DepartmentId = input.DepartmentId, CategoryId = input.CategoryId, Title = title, Description = description, PriorityId = input.PriorityId,
                RoomId = input.RoomId, AreaId = input.AreaId, SourceType = "MANUAL", Status = "TODO", ProgressPercent = 0,
                PlannedStartAt = normalized.PlannedStartAt?.UtcDateTime, DueAt = normalized.DueAt?.UtcDateTime, CreatedBy = actor, CreatedAt = now };
            db.OperationalTasks.Add(task); await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.task_progress_versions(task_id,workflow_status) VALUES({task.TaskId},'PENDING_ASSIGNMENT');
                INSERT dbo.operational_task_definitions(task_id,requested_result,sla_minutes,creation_key,creation_hash)
                VALUES({task.TaskId},{outcome},{input.SlaMinutes},{input.RequestKey},{hash});
                """, token);
            for (var n = 0; n < criteria.Count; n++)
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_completion_criteria(task_id,label,is_required,sort_order) VALUES({task.TaskId},{criteria[n].Label},{criteria[n].Required},{n})", token);
            db.AuditLogs.Add(new AuditLog { ActorUserId = actor, BranchId = branch, EntityType = "operational_tasks", EntityId = task.TaskId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Action = "CREATE", OccurredAt = now, NewValues = JsonSerializer.Serialize(new { operation = "CREATE", workflowState = "PENDING_ASSIGNMENT", request = normalized }),
                ChangedFields = "[\"definition\",\"criteria\",\"plan\",\"workflowState\"]" });
            await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
            return new(RoomTypeBatchStatus.Success, new TaskCreatedDto(task.TaskId, branch, task.TaskCode, "PENDING_ASSIGNMENT", "MSG08"), WorkflowTag((await ProgressRevision(false, task.TaskId, token))!.Revision));
        }, ct, requireRoomTypeSchema: false, featurePermission: token => VerificationManager(ticket.UserId, branch, token));
    }
}
