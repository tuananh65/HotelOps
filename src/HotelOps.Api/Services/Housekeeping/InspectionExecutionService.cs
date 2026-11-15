using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public sealed class HousekeepingState
    {
        public long Id { get; set; }
        public string? State { get; set; }
        public byte[] Revision { get; set; } = [];
        public DateTime? DueAt { get; set; }
    }
    private Task<HousekeepingState> InspectionState(long id, CancellationToken ct) => db.Database.SqlQuery<HousekeepingState>(
        $"SELECT inspection_id AS Id,workflow_status AS State,row_version AS Revision,due_at AS DueAt FROM dbo.room_inspections WHERE inspection_id={id}").SingleAsync(ct);
    private Task<HousekeepingState> CleaningState(long id, CancellationToken ct) => db.Database.SqlQuery<HousekeepingState>(
        $"SELECT cleaning_task_id AS Id,workflow_status AS State,row_version AS Revision,due_at AS DueAt FROM dbo.cleaning_tasks WHERE cleaning_task_id={id}").SingleAsync(ct);
    private static string WorkflowTag(byte[] bytes) => '"' + Convert.ToHexString(bytes) + '"';
    private static bool WorkflowStrongTag(string? tag) => tag is { Length: 18 } && tag[0] == '"' && tag[^1] == '"'
        && tag.Skip(1).Take(16).All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static string WorkflowReason(string? text) => ChecklistText(text, 500);
    private static string InspectionCriterionNote(string? text)
    {
        var note = (text ?? "").Trim();
        if (note.Length > 4000 || note.Any(c => char.IsControl(c) && c is not ('\r' or '\n')))
            throw new ValidationException("Criterion note must contain at most 4000 characters without unsupported control characters.");
        return note;
    }

    public async Task<RoomTypeBatchResult> ExecuteInspectionAsync(SessionReference ticket, long sid, long branch, long id, string? expected,
        string operation, SaveInspectionRequest? save, RecordInspectionDefectRequest? defect, string? reason, string privateRoot, CancellationToken ct, long? defectId = null)
    {
        if (id <= 0 || operation is not ("read" or "start" or "save" or "finish" or "defect" or "edit-defect")
            || (operation == "edit-defect" ? defectId is not > 0 : defectId != null)) throw new ValidationException("Invalid inspection operation.");
        if (operation != "read" && !WorkflowStrongTag(expected)) throw new ValidationException("A strong rowversion If-Match is required.");
        var evidence = new HotelOps.Api.Services.Housekeeping.EvidenceFileLifetime();
        var files = evidence.Files;
        try
        {
            var response = await BatchAsync(ticket, sid, branch, async (actor, token) =>
            {
                if (!await HousekeepingSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
                var rooms = await HistoryRooms(token);
                if (rooms == null) return new(RoomTypeBatchStatus.SchemaRequired);
                var scopedRooms = rooms.Where(r => r.BranchId == branch && r.IsActive && r.Floor.IsActive && (r.AreaId == null || r.Area!.IsActive)).Select(r => r.RoomId);
                var source = await db.RoomInspections.AsNoTracking().SingleOrDefaultAsync(i => i.InspectionId == id && scopedRooms.Contains(i.RoomId)
                    && i.CleaningTaskId != null && i.CleaningTask!.RoomId == i.RoomId, token);
                if (source == null || source.CleaningTaskId is not { } cleaningTask) return new(RoomTypeBatchStatus.NotFound);
                if (source.InspectorUserId != actor) return new(RoomTypeBatchStatus.Forbidden);
                // UC047 owns exact submitted-defect decisions. The generic checklist workflow
                // must not start/save/finish a reinspection or rewrite its historical source.
                if (operation != "read" && await db.CleaningReworks.AsNoTracking()
                    .AnyAsync(w => w.CleaningTaskId == source.CleaningTaskId, token))
                    return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "REINSPECTION_CONTRACT_REQUIRED");
                if (operation != "read" && (!await ReworkCurrentCycle(source.RoomId, cleaningTask, token)
                    || (await CleaningState(cleaningTask, token)).State != "PENDING_INSPECTION"))
                    return new(RoomTypeBatchStatus.Inactive);
                var state = await InspectionState(id, token);
                if (state.State == null) return new(RoomTypeBatchStatus.SchemaRequired);
                if (operation != "read" && expected != WorkflowTag(state.Revision)) return new(RoomTypeBatchStatus.Stale);
                var before = await ExecutionDto(source, state, branch, token);
                if (operation == "read") return new(RoomTypeBatchStatus.Success, before, WorkflowTag(state.Revision));
                var auditReason = operation == "save" ? WorkflowReason(save?.Reason) : operation == "defect" ? "Record inspection defect"
                    : operation == "edit-defect" ? "Update inspection defect" : WorkflowReason(reason);
                if (operation == "start")
                {
                    if (state.State != "ASSIGNED") return new(RoomTypeBatchStatus.Inactive);
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status={"INSPECTING"},status={"IN_PROGRESS"} WHERE inspection_id={id}", token);
                }
                else if (operation == "save")
                {
                    if (state.State != "INSPECTING") return new(RoomTypeBatchStatus.Inactive);
                    if (save?.Results == null || save.Results.Count > 200 || save.Results.Any(r => r == null) || save.Results.Select(r => r.ItemId).Distinct().Count() != save.Results.Count)
                        throw new ValidationException("Invalid criterion results.");
                    if (save.Results.Sum(r => r.Photos?.Count ?? 0) > 5) throw new ValidationException("At most five files per save.");
                    var validItems = await db.InspectionChecklistItems.AsNoTracking().Where(i => i.VersionId == source.VersionId && i.IsActive).Select(i => i.ItemId).ToArrayAsync(token);
                    foreach (var result in save.Results)
                    {
                        if (!validItems.Contains(result.ItemId) || result.Outcome is not ("PASS" or "FAIL")) throw new ValidationException("Result must be PASS/FAIL for an active assigned criterion.");
                        var note = InspectionCriterionNote(result.Note);
                        if (result.Outcome == "PASS" && await db.InspectionDefects.AnyAsync(d => d.InspectionId == id && d.ChecklistItemId == result.ItemId, token))
                            return new(RoomTypeBatchStatus.InvalidReference);
                        var row = await db.InspectionResults.SingleOrDefaultAsync(r => r.InspectionId == id && r.ChecklistItemId == result.ItemId, token);
                        if (row == null) { row = new InspectionResult { InspectionId = id, ChecklistItemId = result.ItemId }; db.Add(row); }
                        row.Outcome = result.Outcome; row.Note = note; row.InspectedAt = clock.GetUtcNow().UtcDateTime;
                        await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
                        foreach (var photo in result.Photos ?? [])
                        {
                            var attachment = await StoreInspectionEvidence(id, actor, photo, privateRoot, files, token);
                            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.inspection_result_attachments(result_id,attachment_id) VALUES({row.ResultId},{attachment})", token);
                        }
                    }
                }
                else if (operation == "finish")
                {
                    if (state.State != "INSPECTING") return new(RoomTypeBatchStatus.Inactive);
                    var criteria = await db.InspectionChecklistItems.AsNoTracking().Where(i => i.VersionId == source.VersionId && i.IsActive).ToArrayAsync(token);
                    var results = await db.InspectionResults.AsNoTracking().Where(r => r.InspectionId == id && r.ChecklistItem.VersionId == source.VersionId).ToArrayAsync(token);
                    var defects = await db.InspectionDefects.AsNoTracking().Where(d => d.InspectionId == id && d.Description.Trim() != "").Select(d => d.ChecklistItemId).ToArrayAsync(token);
                    if (criteria.Length == 0) return new(RoomTypeBatchStatus.InvalidReference);
                    var missing = criteria.OrderBy(i => i.SortOrder).ThenBy(i => i.ItemId)
                        .Where(i => i.IsRequired && !results.Any(r => r.ChecklistItemId == i.ItemId && r.Outcome is "PASS" or "FAIL"))
                        .Select(i => new InspectionMissingItemDto(i.ItemId, "RESULT_REQUIRED"))
                        .Concat(results.Where(r => r.Outcome == "FAIL" && !defects.Contains(r.ChecklistItemId))
                            .OrderBy(r => r.ChecklistItemId).Select(r => new InspectionMissingItemDto(r.ChecklistItemId, "DEFECT_REQUIRED")))
                        .ToArray();
                    if (missing.Length > 0) return new(RoomTypeBatchStatus.InvalidReference,
                        new InspectionIncompleteDto(id, branch, source.VersionId, WorkflowTag(state.Revision), missing));
                    var now = clock.GetUtcNow().UtcDateTime;
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status={"PENDING_CONCLUSION"},status={"IN_PROGRESS"},inspected_at={now} WHERE inspection_id={id}", token);
                }
                else
                {
                    if (state.State is not ("INSPECTING" or "PENDING_CONCLUSION")) return new(RoomTypeBatchStatus.Inactive);
                    if (defect == null || defect.ItemId <= 0 || defect.SeverityId <= 0 || (defect.Photos?.Count ?? 0) > 5) throw new ValidationException("Invalid defect.");
                    var description = ChecklistText(defect.Description, 4000); var location = ChecklistText(defect.Location, 250);
                    if (!await db.ReferenceValues.AnyAsync(r => r.ReferenceId == defect.SeverityId && r.IsActive && r.GroupCode == "SEVERITY" && (r.ValueCode == "MINOR" || r.ValueCode == "MAJOR"), token)
                        || !await db.InspectionResults.AnyAsync(r => r.InspectionId == id && r.ChecklistItemId == defect.ItemId && r.Outcome == "FAIL"
                            && r.ChecklistItem.VersionId == source.VersionId && r.ChecklistItem.IsActive, token)) return new(RoomTypeBatchStatus.InvalidReference);
                    InspectionDefect row;
                    if (operation == "edit-defect")
                    {
                        var existing = await db.InspectionDefects.SingleOrDefaultAsync(d => d.DefectId == defectId && d.InspectionId == id, token);
                        if (existing == null) return new(RoomTypeBatchStatus.NotFound);
                        // Editing details must not rebind historical evidence to another criterion.
                        if (existing.ChecklistItemId != defect.ItemId) return new(RoomTypeBatchStatus.InvalidReference);
                        row = existing; row.SeverityId = defect.SeverityId; row.Description = description;
                    }
                    else
                    {
                        row = new InspectionDefect { InspectionId = id, ChecklistItemId = defect.ItemId, SeverityId = defect.SeverityId, Description = description, Status = "OPEN" };
                        db.Add(row);
                    }
                    await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.inspection_defects SET room_location={location} WHERE defect_id={row.DefectId}", token);
                    foreach (var photo in defect.Photos ?? [])
                    {
                        var attachment = await StoreInspectionEvidence(id, actor, photo, privateRoot, files, token);
                        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.inspection_defect_attachments(defect_id,attachment_id) VALUES({row.DefectId},{attachment})", token);
                    }
                }
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET summary_note=summary_note WHERE inspection_id={id}", token);
                var afterState = await InspectionState(id, token);
                var after = await ExecutionDto(source, afterState, branch, token);
                await BatchAuditAsync(actor, branch, "room_inspections", id, before, new { reason = auditReason, operation, entity = after }, [operation, "workflowStatus", "criteria", "defects", "evidence"], token);
                return new(RoomTypeBatchStatus.Success, after, WorkflowTag(afterState.Revision));
            }, ct, permissionCode: "INSPECTION_PERFORM", preserveCommitErrors: true,
                eligibility: token => EligibleInspector(ticket.UserId, branch, token),
                beforeCommit: evidence.BeforeCommitAsync);
            return response;
        }
        finally
        {
            evidence.Dispose();
        }
    }
    private async Task<InspectionExecutionDto> ExecutionDto(RoomInspection source, HousekeepingState state, long branch, CancellationToken ct)
    {
        var history = await HistoryInspectionRows(db.RoomInspections.AsNoTracking().Where(i => i.InspectionId == source.InspectionId)).SingleAsync(ct);
        var detail = (await HistoryInspectionDetails([history], ct))[0];
        var severities = await db.ReferenceValues.AsNoTracking()
            .Where(r => r.IsActive && r.GroupCode == "SEVERITY" && (r.ValueCode == "MINOR" || r.ValueCode == "MAJOR"))
            .OrderBy(r => r.SortOrder).ThenBy(r => r.ReferenceId)
            .Select(r => new InspectionSeverityDto(r.ReferenceId, r.ValueCode, r.DisplayName)).ToArrayAsync(ct);
        var readiness = (await new HotelOps.Api.Services.Rooms.EffectiveRoomReadiness(db)
            .ReadAsync([source.RoomId], clock.GetUtcNow().UtcDateTime, ct))[source.RoomId];
        return new(source.InspectionId, branch, source.RoomId, source.CleaningTaskId!.Value, source.InspectorUserId, source.VersionId, state.State!, state.DueAt, detail.Criteria, detail.Defects, detail.Evidence, severities)
            { EffectiveReadiness = readiness.Readiness };
    }
    private async Task<long> StoreInspectionEvidence(long inspection, long actor, EvidenceUpload upload, string root, List<string> owned, CancellationToken ct)
        => await StoreFeatureEvidence("INSPECTION", inspection, actor, upload, root, owned, ct);

    private async Task<long> StoreFeatureEvidence(string entityType, long source, long actor, EvidenceUpload upload, string root, List<string> owned, CancellationToken ct)
    {
        if (upload == null || string.IsNullOrWhiteSpace(upload.Name) || upload.Name.Length > 200 || upload.Name.Any(char.IsControl)
            || upload.Name.IndexOfAny(['/', '\\', ':']) >= 0 || string.IsNullOrEmpty(upload.Base64) || upload.Base64.Length > 14 * 1024 * 1024)
            throw new ValidationException("Invalid evidence metadata.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(upload.Base64); } catch (FormatException) { throw new ValidationException("Invalid evidence encoding."); }
        if (bytes.Length is < 1 or > 10 * 1024 * 1024) throw new ValidationException("Evidence must be at most 10 MiB.");
        var mime = bytes.AsSpan().StartsWith(new byte[] {137,80,78,71,13,10,26,10}) ? "image/png"
            : bytes.AsSpan().StartsWith(new byte[] {255,216,255}) ? "image/jpeg" : bytes.AsSpan().StartsWith("%PDF-"u8) ? "application/pdf" : null;
        if (mime == null || mime != upload.MimeType) throw new ValidationException("Evidence must be JPG, PNG or PDF with matching MIME.");
        var extension = Path.GetExtension(upload.Name).ToLowerInvariant();
        if (mime == "image/png" && extension != ".png" || mime == "image/jpeg" && extension is not (".jpg" or ".jpeg") || mime == "application/pdf" && extension != ".pdf")
            throw new ValidationException("Evidence extension must match its format.");
        var directory = Path.GetFullPath(root); Directory.CreateDirectory(directory);
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new ValidationException("Private storage unavailable.");
        var key = Guid.NewGuid().ToString("N") + (mime == "image/png" ? ".png" : mime == "image/jpeg" ? ".jpg" : ".pdf");
        var path = Path.Combine(directory, key);
        await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
        { owned.Add(path); await stream.WriteAsync(bytes, ct); await stream.FlushAsync(ct); }
        var row = new Attachment { EntityType = entityType, EntityId = source, FileName = key, OriginalFileName = upload.Name,
            MimeType = mime, FileSizeBytes = bytes.Length, StorageKey = key, UploadedBy = actor, UploadedAt = clock.GetUtcNow().UtcDateTime };
        db.Add(row); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); return row.AttachmentId;
    }
}
