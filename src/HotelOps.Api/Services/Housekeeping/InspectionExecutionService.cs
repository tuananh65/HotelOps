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

    public async Task<RoomTypeBatchResult> ExecuteInspectionAsync(SessionReference ticket, long sid, long branch, long id, string? expected,
        string operation, SaveInspectionRequest? save, RecordInspectionDefectRequest? defect, string? reason, string privateRoot, CancellationToken ct)
    {
        if (id <= 0 || operation is not ("read" or "start" or "save" or "finish" or "defect")) throw new ValidationException("Invalid inspection operation.");
        if (operation != "read" && !WorkflowStrongTag(expected)) throw new ValidationException("A strong rowversion If-Match is required.");
        var files = new List<string>(); var committed = false;
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
                if (source == null) return new(RoomTypeBatchStatus.NotFound);
                if (source.InspectorUserId != actor) return new(RoomTypeBatchStatus.Forbidden);
                var state = await InspectionState(id, token);
                if (state.State == null) return new(RoomTypeBatchStatus.SchemaRequired);
                if (operation != "read" && expected != WorkflowTag(state.Revision)) return new(RoomTypeBatchStatus.Stale);
                var before = await ExecutionDto(source, state, branch, token);
                if (operation == "read") return new(RoomTypeBatchStatus.Success, before, WorkflowTag(state.Revision));
                var auditReason = operation == "save" ? WorkflowReason(save?.Reason) : operation == "defect" ? "Record inspection defect" : WorkflowReason(reason);
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
                        var note = ChecklistText(result.Note, 4000, false);
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
                    if (criteria.Length == 0 || criteria.Any(i => i.IsRequired && !results.Any(r => r.ChecklistItemId == i.ItemId && r.Outcome is "PASS" or "FAIL"))
                        || results.Any(r => r.Outcome == "FAIL" && !defects.Contains(r.ChecklistItemId))) return new(RoomTypeBatchStatus.InvalidReference);
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
                    var row = new InspectionDefect { InspectionId = id, ChecklistItemId = defect.ItemId, SeverityId = defect.SeverityId, Description = description, Status = "OPEN" };
                    db.Add(row); await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
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
            }, ct, permissionCode: "INSPECTION_PERFORM");
            committed = response.Status == RoomTypeBatchStatus.Success;
            return response;
        }
        finally
        {
            if (!committed) foreach (var file in files) File.Delete(file); // Only server-generated files owned by this invocation.
        }
    }
    private async Task<InspectionExecutionDto> ExecutionDto(RoomInspection source, HousekeepingState state, long branch, CancellationToken ct)
    {
        var history = await HistoryInspectionRows(db.RoomInspections.AsNoTracking().Where(i => i.InspectionId == source.InspectionId)).SingleAsync(ct);
        var detail = (await HistoryInspectionDetails([history], ct))[0];
        return new(source.InspectionId, branch, source.RoomId, source.CleaningTaskId!.Value, source.InspectorUserId, source.VersionId, state.State!, state.DueAt, detail.Criteria, detail.Defects, detail.Evidence);
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
