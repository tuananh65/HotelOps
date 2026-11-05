using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public sealed class ProgressVersion
    {
        public long Id { get; set; }
        public byte[] Revision { get; set; } = [];
        public string State { get; set; } = "IN_PROGRESS";
    }
    public sealed class StoredProgressItem
    {
        public long Id { get; set; }
        public string Label { get; set; } = "";
        public bool Required { get; set; }
        public int Sort { get; set; }
        public bool? Done { get; set; }
        public string? Outcome { get; set; }
        public string? Note { get; set; }
    }
    private Task<ProgressVersion?> ProgressRevision(bool cleaning, long id, CancellationToken ct) => cleaning
        ? db.Database.SqlQuery<ProgressVersion>($"SELECT cleaning_task_id AS Id,row_version AS Revision,CAST('IN_PROGRESS' AS varchar(20)) AS State FROM dbo.cleaning_progress_versions WITH(UPDLOCK,HOLDLOCK) WHERE cleaning_task_id={id}").SingleOrDefaultAsync(ct)
        : db.Database.SqlQuery<ProgressVersion>($"SELECT task_id AS Id,row_version AS Revision,workflow_status AS State FROM dbo.task_progress_versions WITH(UPDLOCK,HOLDLOCK) WHERE task_id={id}").SingleOrDefaultAsync(ct);

    // Trusted SQL resource ownership, including for global grants. No role-name bypass.
    private async Task<bool> ProgressOwned(bool cleaning, long id, long branch, long actor, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (cleaning)
        {
            if (!await db.CleaningTasks.AsNoTracking().AnyAsync(t => t.CleaningTaskId == id && t.Room.BranchId == branch, ct)) return false;
            var owners = await db.CleaningAssignments.AsNoTracking().Where(a => a.CleaningTaskId == id && a.AssignmentStatus == "ACTIVE"
                && a.EndedAt == null && a.AssignedAt <= now).Select(a => a.AssigneeUserId).Take(2).ToArrayAsync(ct);
            return owners.Length == 1 && owners[0] == actor;
        }
        if (!await db.OperationalTasks.AsNoTracking().AnyAsync(t => t.TaskId == id && t.BranchId == branch, ct)) return false;
        var assigned = await db.OperationalTaskAssignments.AsNoTracking().Where(a => a.TaskId == id && a.AssignmentStatus == "ACTIVE"
            && a.EndedAt == null && a.AssignedAt <= now).Select(a => a.AssigneeId).Take(2).ToArrayAsync(ct);
        return assigned.Length == 1 && assigned[0] == actor;
    }
    private async Task<PermissionEvaluation> ProgressAccess(bool cleaning, long id, long branch, long actor, CancellationToken ct)
    {
        var data = new EfPermissionDataSource(db); var now = clock.GetUtcNow().UtcDateTime;
        if (!await data.IsActiveUserAsync(actor, ct) || !await data.IsActiveBranchAsync(branch, ct)) return new(PermissionDecision.Denied);
        var code = cleaning ? "CLEANING_EXECUTE" : "TASK_EXECUTE";
        var owned = await ProgressOwned(cleaning, id, branch, actor, ct);
        foreach (var grant in await data.ReadPathsAsync(actor, code, now, ct))
        {
            if (grant.PermissionCode != code || !PermissionGrantRules.IsEligible(grant, now)) continue;
            var scope = PermissionGrantRules.EffectiveScope(grant);
            if (scope == "GLOBAL" || grant.BranchId == branch && (scope == "BRANCH" || scope == "OWN" && owned))
                return new(PermissionDecision.Granted);
        }
        return new(PermissionDecision.Denied);
    }
    private static IReadOnlyList<ProgressDefinition> Definitions(InitializeProgressRequest input)
    {
        if (input.Items == null || input.Items.Count is < 1 or > 100 || input.Items.Any(x => x == null))
            throw new ValidationException("Provide one to one hundred definitions.");
        var items = input.Items.Select(x => x with { Label = ChecklistText(x.Label, 250) }).ToArray();
        if (items.Select(x => x.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Length)
            throw new ValidationException("Duplicate item labels.");
        return items;
    }
    public Task<RoomTypeBatchResult> InitializeWorkProgressAsync(SessionReference ticket, long sid, long branch, long id,
        string kind, InitializeProgressRequest input, CancellationToken ct)
    {
        if (id <= 0 || kind is not ("template" or "cleaning" or "task")) throw new ValidationException("Invalid progress initialization.");
        var reason = ChecklistText(input.Reason, 500);
        if (reason.Length < 10) throw new ValidationException("Initialization requires an explanatory reason.");
        var definitions = kind == "cleaning" ? null : Definitions(input);
        if (kind == "cleaning" && input.Items != null) throw new ValidationException("Cleaning items are copied from the room type; do not supply inspection or arbitrary items.");
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await ProgressSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            if (kind == "template")
            {
                if (!await db.BranchRoomTypes.AnyAsync(t => t.RoomTypeId == id && t.BranchId == branch && t.IsActive, token)) return new(RoomTypeBatchStatus.NotFound);
                if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.cleaning_item_definitions WHERE room_type_id={id}").SingleAsync(token) != 0) return new(RoomTypeBatchStatus.Duplicate);
                for (var n = 0; n < definitions!.Count; n++)
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_item_definitions(room_type_id,label,is_required,sort_order) VALUES({id},{definitions[n].Label},{definitions[n].Required},{n})", token);
            }
            else if (kind == "cleaning")
            {
                if (!await HousekeepingSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
                var task = await db.CleaningTasks.AsNoTracking().SingleOrDefaultAsync(t => t.CleaningTaskId == id && t.Room.BranchId == branch, token);
                if (task == null) return new(RoomTypeBatchStatus.NotFound);
                if (task.Status != "IN_PROGRESS" || task.ActualStartAt == null || (await CleaningState(id, token)).State != "IN_PROGRESS") return new(RoomTypeBatchStatus.Inactive);
                if (await ProgressRevision(true, id, token) != null) return new(RoomTypeBatchStatus.Duplicate);
                var type = await db.Rooms.Where(r => r.RoomId == task.RoomId).Select(r => r.RoomTypeId).SingleAsync(token);
                if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.cleaning_item_definitions WHERE room_type_id={type}").SingleAsync(token) == 0) return new(RoomTypeBatchStatus.InvalidReference);
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_progress_versions(cleaning_task_id) VALUES({id}); INSERT dbo.cleaning_task_items(cleaning_task_id,source_item_id,label,is_required,sort_order) SELECT {id},item_id,label,is_required,sort_order FROM dbo.cleaning_item_definitions WHERE room_type_id={type}", token);
            }
            else
            {
                var task = await db.OperationalTasks.AsNoTracking().SingleOrDefaultAsync(t => t.TaskId == id && t.BranchId == branch, token);
                if (task == null) return new(RoomTypeBatchStatus.NotFound);
                if (task.Status != "IN_PROGRESS" || task.ActualStartAt == null) return new(RoomTypeBatchStatus.Inactive);
                if (await ProgressRevision(false, id, token) != null) return new(RoomTypeBatchStatus.Duplicate);
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_progress_versions(task_id,workflow_status) VALUES({id},{"IN_PROGRESS"})", token);
                for (var n = 0; n < definitions!.Count; n++)
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_completion_criteria(task_id,label,is_required,sort_order) VALUES({id},{definitions[n].Label},{definitions[n].Required},{n})", token);
            }
            await BatchAuditAsync(actor, branch, kind == "template" ? "room_types" : kind == "cleaning" ? "cleaning_tasks" : "operational_tasks", id,
                new { progressModel = "NOT_INITIALIZED" }, new { operation = "INITIALIZE_PROGRESS", reason, definitions }, ["progressDefinitions"], token);
            return new(RoomTypeBatchStatus.Success, new { id, kind, initialized = true });
        }, ct, requireRoomTypeSchema: kind == "template",
            permissionCode: kind == "template" ? "MASTER_DATA_MANAGE" : kind == "cleaning" ? "CLEANING_ASSIGN" : "TASK_MANAGE");
    }

    public async Task<RoomTypeBatchResult> WorkProgressAsync(SessionReference ticket, long sid, long branch, long id, bool cleaning,
        string? expected, SaveProgressRequest? input, string privateRoot, CancellationToken ct, long? attachmentId = null)
    {
        if (id <= 0 || attachmentId <= 0) throw new ValidationException("Invalid progress resource.");
        if (input != null && (!WorkflowStrongTag(expected) || input.Percent is < 0 or > 100 || input.Items == null
            || input.Items.Count > 100 || input.Items.Any(x => x == null) || input.Items.Select(x => x.Id).Distinct().Count() != input.Items.Count
            || (input.Photos?.Count ?? 0) > 5)) throw new ValidationException("Invalid progress or rowversion.");
        var note = input == null ? null : ChecklistText(input.Note, 4000, false);
        var ownedFiles = new List<string>(); var committed = false;
        try
        {
            var result = await BatchAsync(ticket, sid, branch, async (actor, token) =>
            {
                if (!await ProgressSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
                var exists = cleaning
                    ? await db.CleaningTasks.AnyAsync(t => t.CleaningTaskId == id && t.Room.BranchId == branch, token)
                    : await db.OperationalTasks.AnyAsync(t => t.TaskId == id && t.BranchId == branch, token);
                if (!exists) return new(RoomTypeBatchStatus.NotFound);
                if (!await ProgressOwned(cleaning, id, branch, actor, token)) return new(RoomTypeBatchStatus.Forbidden);
                var version = await ProgressRevision(cleaning, id, token);
                if (version == null) return new(RoomTypeBatchStatus.SchemaRequired);
                if (cleaning)
                {
                    if (!await HousekeepingSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
                    if ((await CleaningState(id, token)).State != "IN_PROGRESS"
                        || !await db.CleaningTasks.AnyAsync(t => t.CleaningTaskId == id && t.Status == "IN_PROGRESS" && t.Room.IsActive, token)) return new(RoomTypeBatchStatus.Inactive);
                }
                else if (version.State is not ("IN_PROGRESS" or "PAUSED") || !await db.OperationalTasks.AnyAsync(t => t.TaskId == id && t.Status == "IN_PROGRESS", token))
                    return new(RoomTypeBatchStatus.Inactive);
                if (input != null && expected != WorkflowTag(version.Revision)) return new(RoomTypeBatchStatus.Stale);
                var before = await ProgressDto(cleaning, id, branch, version.State, token);
                if (attachmentId != null)
                {
                    var file = await HistoryFile(before.Evidence, attachmentId.Value, privateRoot, token);
                    return file == null ? new(RoomTypeBatchStatus.NotFound) : new(RoomTypeBatchStatus.Success, file);
                }
                if (input == null) return new(RoomTypeBatchStatus.Success, before, WorkflowTag(version.Revision));
                foreach (var item in input.Items)
                {
                    if (!before.Items.Any(x => x.Id == item.Id) || cleaning && (item.Done == null || item.Outcome != null)
                        || !cleaning && (item.Done != null || item.Outcome is not ("PASS" or "FAIL"))) throw new ValidationException("Item does not belong to this task or result is invalid.");
                    var itemNote = ChecklistText(item.Note, 1000, false);
                    if (cleaning) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_task_items SET is_done={item.Done},note={itemNote} WHERE item_id={item.Id} AND cleaning_task_id={id}", token);
                    else await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_completion_criteria SET outcome={item.Outcome},note={itemNote} WHERE criterion_id={item.Id} AND task_id={id}", token);
                }
                var now = clock.GetUtcNow().UtcDateTime; long progressId;
                if (cleaning)
                {
                    var log = new CleaningProgressLog { CleaningTaskId = id, ProgressPercent = input.Percent, Status = "IN_PROGRESS", Note = note, LoggedBy = actor, LoggedAt = now };
                    db.Add(log); await db.SaveChangesAsync(token); progressId = log.ProgressId;
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_progress_versions SET cleaning_task_id=cleaning_task_id WHERE cleaning_task_id={id}", token);
                }
                else
                {
                    var log = new OperationalTaskProgress { TaskId = id, ProgressPercent = input.Percent, OldStatus = "IN_PROGRESS", NewStatus = "IN_PROGRESS", Description = note, ReportedBy = actor, ReportedAt = now };
                    db.Add(log); await db.SaveChangesAsync(token); progressId = log.ProgressId;
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_tasks SET progress_percent={input.Percent},updated_at={now} WHERE task_id={id}; UPDATE dbo.task_progress_versions SET task_id=task_id WHERE task_id={id}", token);
                }
                db.ChangeTracker.Clear();
                foreach (var upload in input.Photos ?? [])
                {
                    var attachment = await StoreFeatureEvidence(cleaning ? "CLEANING_TASK" : "TASK", id, actor, upload, privateRoot, ownedFiles, token);
                    if (cleaning) await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.progress_evidence_links(attachment_id,cleaning_progress_id) VALUES({attachment},{progressId})", token);
                    else await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.progress_evidence_links(attachment_id,task_progress_id) VALUES({attachment},{progressId})", token);
                }
                var after = await ProgressDto(cleaning, id, branch, version.State, token);
                await BatchAuditAsync(actor, branch, cleaning ? "cleaning_tasks" : "operational_tasks", id, before, after, ["progress", "criteria", "notes", "evidence"], token);
                return new(RoomTypeBatchStatus.Success, after, WorkflowTag((await ProgressRevision(cleaning, id, token))!.Revision));
            }, ct, requireRoomTypeSchema: false, featurePermission: token => ProgressAccess(cleaning, id, branch, ticket.UserId, token));
            committed = result.Status == RoomTypeBatchStatus.Success;
            return result;
        }
        finally { if (!committed) foreach (var file in ownedFiles) File.Delete(file); }
    }

    private async Task<WorkProgressDto> ProgressDto(bool cleaning, long id, long branch, string state, CancellationToken ct)
    {
        var items = cleaning
            ? await db.Database.SqlQuery<StoredProgressItem>($"SELECT item_id AS Id,label AS Label,is_required AS Required,sort_order AS Sort,is_done AS Done,CAST(NULL AS varchar(4)) AS Outcome,note AS Note FROM dbo.cleaning_task_items WHERE cleaning_task_id={id} ORDER BY sort_order,item_id").ToArrayAsync(ct)
            : await db.Database.SqlQuery<StoredProgressItem>($"SELECT criterion_id AS Id,label AS Label,is_required AS Required,sort_order AS Sort,CAST(NULL AS bit) AS Done,outcome AS Outcome,note AS Note FROM dbo.task_completion_criteria WHERE task_id={id} ORDER BY sort_order,criterion_id").ToArrayAsync(ct);
        var timeline = cleaning
            ? await db.CleaningProgressLogs.AsNoTracking().Where(p => p.CleaningTaskId == id).OrderByDescending(p => p.LoggedAt).ThenByDescending(p => p.ProgressId).Take(100)
                .Select(p => new HistoryProgressDto(p.ProgressId, p.ProgressPercent, p.Status, p.BlockerTypeId, p.Note, p.LoggedBy, p.LoggedAt)).ToArrayAsync(ct)
            : await db.OperationalTaskProgresses.AsNoTracking().Where(p => p.TaskId == id).OrderByDescending(p => p.ReportedAt).ThenByDescending(p => p.ProgressId).Take(100)
                .Select(p => new HistoryProgressDto(p.ProgressId, p.ProgressPercent, p.NewStatus, null, p.Description, p.ReportedBy, p.ReportedAt)).ToArrayAsync(ct);
        var attachmentIds = cleaning
            ? await db.Database.SqlQuery<long>($"SELECT e.attachment_id AS [Value] FROM dbo.progress_evidence_links e JOIN dbo.cleaning_progress_logs p ON p.progress_id=e.cleaning_progress_id WHERE p.cleaning_task_id={id}").ToArrayAsync(ct)
            : await db.Database.SqlQuery<long>($"SELECT e.attachment_id AS [Value] FROM dbo.progress_evidence_links e JOIN dbo.operational_task_progress p ON p.progress_id=e.task_progress_id WHERE p.task_id={id}").ToArrayAsync(ct);
        var kind = cleaning ? "cleaning-tasks" : "operational-tasks";
        var sourceType = cleaning ? "CLEANING_TASK" : "TASK";
        var rows = await db.Attachments.AsNoTracking().Where(a => attachmentIds.Contains(a.AttachmentId) && a.DeletedAt == null
            && a.EntityType == sourceType && a.EntityId == id).OrderByDescending(a => a.UploadedAt).Take(100).ToArrayAsync(ct);
        var evidence = rows.Select(a => new HistoryEvidenceDto(a.AttachmentId, a.EntityType, a.EntityId, a.OriginalFileName ?? "evidence", a.MimeType, a.FileSizeBytes,
            a.UploadedAt, $"/api/branches/{branch}/{kind}/{id}/progress/evidence/{a.AttachmentId}")).ToArray();
        var percent = cleaning ? timeline.FirstOrDefault()?.Percent ?? 0 : await db.OperationalTasks.Where(t => t.TaskId == id).Select(t => t.ProgressPercent).SingleAsync(ct);
        return new(id, branch, cleaning ? "cleaning" : "task", state, percent,
            items.Select(i => new ProgressItemDto(i.Id, i.Label, i.Required, i.Sort, i.Done, i.Outcome, i.Note)).ToArray(), timeline, evidence);
    }
}
