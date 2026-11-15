using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    // Caller holds the current actor/executor, branch, catalog and transaction guards.
    // A job records its actual system execution separately from creator attribution.
    internal static async Task<OperationalTask> WriteTaskCreationAsync(HotelOpsDbContext context, long actor, long branch,
        CreateOperationalTaskRequest input, DateTime now, CancellationToken ct, long? seriesId = null)
    {
        if (context.Database.CurrentTransaction == null) throw new InvalidOperationException("Task creation requires its authorized transaction.");
        input = NormalizeTaskCreation(input);
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input));
        var task = new OperationalTask { TaskCode = "TASK-" + input.RequestKey.ToString("N").ToUpperInvariant(), BranchId = branch,
            DepartmentId = input.DepartmentId, CategoryId = input.CategoryId, Title = input.Title, Description = input.Description, PriorityId = input.PriorityId,
            RoomId = input.RoomId, AreaId = input.AreaId, SourceType = input.InternalRequestId == null ? "MANUAL" : "INTERNAL_REQUEST", SourceId = input.InternalRequestId, Status = "TODO", ProgressPercent = 0,
            PlannedStartAt = input.PlannedStartAt?.UtcDateTime, DueAt = input.DueAt?.UtcDateTime, CreatedBy = actor, CreatedAt = now };
        context.OperationalTasks.Add(task); await context.SaveChangesAsync(ct); context.ChangeTracker.Clear();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.task_progress_versions(task_id,workflow_status) VALUES({task.TaskId},'PENDING_ASSIGNMENT');
            INSERT dbo.operational_task_definitions(task_id,requested_result,sla_minutes,creation_key,creation_hash)
            VALUES({task.TaskId},{input.RequestedResult},{input.SlaMinutes},{input.RequestKey},{hash});
            """, ct);
        for (var n = 0; n < input.Criteria!.Count; n++)
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_completion_criteria(task_id,label,is_required,sort_order) VALUES({task.TaskId},{input.Criteria[n].Label},{input.Criteria[n].Required},{n})", ct);
        context.AuditLogs.Add(new AuditLog { ActorUserId = actor, BranchId = branch, EntityType = "operational_tasks", EntityId = task.TaskId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Action = "CREATE", OccurredAt = now, NewValues = JsonSerializer.Serialize(new { operation = "CREATE", workflowState = "PENDING_ASSIGNMENT", request = input, automation = seriesId == null ? null : new { job = "JOB05", seriesId } }),
            ChangedFields = "[\"definition\",\"criteria\",\"plan\",\"workflowState\"]" });
        await context.SaveChangesAsync(ct); context.ChangeTracker.Clear();
        return task;
    }

    internal static async Task<bool> TaskCreationReferencesAsync(HotelOpsDbContext context, long branch, CreateOperationalTaskRequest input, CancellationToken ct) =>
        await context.Departments.AsNoTracking().AnyAsync(d => d.DepartmentId == input.DepartmentId && d.BranchId == branch && d.IsActive, ct)
        && await context.WorkCategories.AsNoTracking().AnyAsync(c => c.CategoryId == input.CategoryId && c.IsActive && (c.DepartmentId == null || c.DepartmentId == input.DepartmentId), ct)
        && await TaskPlanningStore.OperationalCategories(context).ContainsAsync(input.CategoryId, ct)
        && await context.ReferenceValues.AsNoTracking().AnyAsync(r => r.ReferenceId == input.PriorityId && r.GroupCode == "PRIORITY" && TaskPriorityCodes.Contains(r.ValueCode) && r.IsActive, ct)
        && (input.RoomId == null || await context.Rooms.AsNoTracking().AnyAsync(r => r.RoomId == input.RoomId && r.BranchId == branch && r.IsActive, ct))
        && (input.AreaId == null || await context.Areas.AsNoTracking().AnyAsync(a => a.AreaId == input.AreaId && a.BranchId == branch && a.IsActive, ct));
}
