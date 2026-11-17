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
            var recurring = await TaskSeriesStore.InstalledAsync(db, token);
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token);
            var executors = recurring && await TaskAssignmentEligibility.InstalledAsync(db, token) && department is { } selected
                ? await TaskAssignmentEligibility.Candidates(db, branch, selected, now).OrderBy(u => u.FullName).ThenBy(u => u.UserId)
                    .Select(u => new TaskPlanningLookup(u.UserId, u.FullName)).ToArrayAsync(token)
                : [];
            return new(RoomTypeBatchStatus.Success, new TaskPlanningOptions(departments, categories, priorities, executors, recurring));
        }, ct, requireRoomTypeSchema: false, featurePermission: token => VerificationManager(ticket.UserId, branch, token));
    }

    internal static CreateOperationalTaskRequest NormalizeTaskCreation(CreateOperationalTaskRequest input)
    {
        if (input.RequestKey == Guid.Empty || input.DepartmentId <= 0 || input.CategoryId <= 0 || input.RoomId is <= 0 || input.AreaId is <= 0
            || input.RoomId != null && input.AreaId != null || input.InternalRequestId is <= 0)
            throw new ValidationException("Valid creation key, department, category and at most one location required.");
        if ((input.InternalRequestId == null) != (input.RequestLink == null))
            throw new ValidationException("Linked creation requires a source request, its loaded version and explicit completion requirement.");
        if (input.RequestLink != null) RequestCreationVersion(input.RequestLink);
        var title = ChecklistText(input.Title, 200);
        var outcome = TaskResultText(input.RequestedResult) ?? throw new ValidationException("Expected outcome is required.");
        var description = TaskResultText(input.Description);
        var criteria = Definitions(new InitializeProgressRequest(input.Criteria, "Create task"));
        ValidateTaskPlan(input.PriorityId, input.PlannedStartAt, input.DueAt, input.SlaMinutes);
        return input with { Title = title, RequestedResult = outcome, Description = description, Criteria = criteria,
            PlannedStartAt = input.PlannedStartAt?.ToUniversalTime(), DueAt = input.DueAt?.ToUniversalTime() };
    }

    public Task<RoomTypeBatchResult> CreateOperationalTaskAsync(SessionReference ticket, long sid, long branch, CreateOperationalTaskRequest input, CancellationToken ct)
    {
        var normalized = NormalizeTaskCreation(input);
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(normalized));
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await TaskPlanningStore.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var prior = await TaskPlanningStore.Definitions(db).SingleOrDefaultAsync(d => d.CreationKey == input.RequestKey, token);
            if (prior != null)
            {
                var existing = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.TaskId == prior.TaskId, token);
                if (existing.CreatedBy != actor || existing.BranchId != branch || prior.CreationHash == null || !CryptographicOperations.FixedTimeEquals(hash, prior.CreationHash))
                    return new(RoomTypeBatchStatus.Duplicate);
                if (normalized.InternalRequestId is { } source && (existing.SourceType != "INTERNAL_REQUEST" || existing.SourceId != source
                    || !await CreatedTaskRequestLinkMatches(source, existing.TaskId, normalized.RequestLink!.Mandatory, token)))
                    return new(RoomTypeBatchStatus.SchemaRequired);
                var revision = await ProgressRevision(false, existing.TaskId, token);
                if (revision == null) return new(RoomTypeBatchStatus.SchemaRequired);
                return new(RoomTypeBatchStatus.Success, new TaskCreatedDto(existing.TaskId, branch, existing.TaskCode, revision.State, "MSG08"), WorkflowTag(revision.Revision));
            }
            if (normalized.InternalRequestId is { } request)
            {
                var row = await db.InternalRequests.AsNoTracking().SingleAsync(r => r.InternalRequestId == request && r.BranchId == branch, token);
                if (!(await HotelOps.Api.Services.InternalRequests.InternalRequestDecisionStore.RevisionAsync(db, request, token))
                    .SequenceEqual(RequestCreationVersion(normalized.RequestLink!))) return new(RoomTypeBatchStatus.Stale);
                if (row.Status is not ("ACCEPTED" or "IN_PROGRESS")) return new(RoomTypeBatchStatus.Inactive);
            }
            if (!await TaskCreationReferencesAsync(db, branch, normalized, token))
                return new(RoomTypeBatchStatus.InvalidReference, new TaskEntryFailure("MSG03"));
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token);
            var task = await WriteTaskCreationAsync(db, actor, branch, normalized, now, token);
            if (normalized.InternalRequestId is { } linkedRequest)
                await WriteCreatedTaskRequestLink(db,actor, branch, linkedRequest, task.TaskId, normalized.RequestLink!, now, token);
            return new(RoomTypeBatchStatus.Success, new TaskCreatedDto(task.TaskId, branch, task.TaskCode, "PENDING_ASSIGNMENT", "MSG08"), WorkflowTag((await ProgressRevision(false, task.TaskId, token))!.Revision));
        }, ct, requireRoomTypeSchema: false, featurePermission: token => LinkedTaskCreationAuthority(ticket.UserId, branch, normalized.InternalRequestId, token));
    }
}
