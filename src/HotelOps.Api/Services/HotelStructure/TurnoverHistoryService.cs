using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class TurnoverHistoryService
{
    private static OperationalHistoryQuery HistoryInput(OperationalHistoryQuery query)
    {
        var search = query.Search?.Trim(); var result = query.Result?.Trim().ToUpperInvariant();
        if (query.BranchId <= 0 || query.RoomId <= 0 || query.InspectorId <= 0 || query.HousekeeperId <= 0
            || query.Page < 1 || query.PageSize is < 1 or > 100 || query.Page > int.MaxValue / query.PageSize
            || query.From?.Kind is { } fk && fk != DateTimeKind.Utc || query.To?.Kind is { } tk && tk != DateTimeKind.Utc
            || query.From > query.To || search?.Length > 150 || search?.Any(char.IsControl) == true
            || result is not (null or "" or "PASS" or "FAIL" or "CONDITIONAL")) throw new ValidationException("Invalid history filters; dates must be UTC.");
        return query with { Search = string.IsNullOrEmpty(search) ? null : search, Result = string.IsNullOrEmpty(result) ? null : result };
    }
    private async Task<long[]> HistoryBranches(string permission, long? branch, CancellationToken ct)
    {
        if (branch is { } id) return [id];
        var ids = await db.Branches.AsNoTracking().Where(x => x.IsActive).Select(x => x.BranchId).ToArrayAsync(ct);
        var global = await permissions.EvaluateGlobalAsync(permission, ct);
        if (global.Decision == PermissionDecision.Unavailable) throw new SessionStoreUnavailableException(new InvalidOperationException("History scope unavailable."));
        if (global.IsGranted) return ids;
        var visible = new List<long>();
        foreach (var candidate in ids)
        {
            var access = await permissions.EvaluateBranchAsync(permission, candidate, ct);
            if (access.Decision == PermissionDecision.Unavailable) throw new SessionStoreUnavailableException(new InvalidOperationException("History scope unavailable."));
            if (access.IsGranted) visible.Add(candidate);
        }
        return visible.ToArray();
    }
    private async Task<bool> HistoryFinal(string permission, IEnumerable<long> branches, CancellationToken ct)
    {
        foreach (var branch in branches)
        {
            var access = await permissions.EvaluateBranchAsync(permission, branch, ct);
            if (access.Decision == PermissionDecision.Unavailable) throw new SessionStoreUnavailableException(new InvalidOperationException("History scope unavailable."));
            if (!access.IsGranted) return false;
        }
        return true;
    }
    private async Task<IQueryable<Room>?> HistoryRooms(CancellationToken ct)
    {
        var query = await TurnoverHistorySchema.RoomsAsync(db, ct);
        return query?.AsNoTracking().Where(r => r.Floor.BranchId == r.BranchId && (r.AreaId == null
            || r.Area != null && r.Area.BranchId == r.BranchId && (r.Area.FloorId == null || r.Area.FloorId == r.FloorId)));
    }
    private static IQueryable<HistoryInspectionDto> HistoryInspectionRows(IQueryable<RoomInspection> source) => source.Select(i =>
        new HistoryInspectionDto(i.InspectionId, i.InspectionNo, i.RoomId, i.Room.RoomNumber, i.Room.BranchId, i.CleaningTaskId,
            i.VersionId, i.Version.VersionNo, i.InspectorUserId, i.InspectorUser.FullName, i.Status, i.Result,
            i.InspectedAt ?? i.ScheduledAt ?? i.CreatedAt, i.SummaryNote,
            i.CleaningReworks.Count(r => r.CleaningTask.RoomId == i.RoomId && r.CleaningTaskId == i.CleaningTaskId)));

    public Task<TurnoverHistoryResult> TurnoverHistoryAsync(SessionReference ticket, long sid, long roomId, OperationalHistoryQuery input, long? cycleId, CancellationToken ct, long? attachmentId = null, string? privateRoot = null)
    {
        if (input.RoomId is { } filterRoom && filterRoom != roomId || input.InspectorId != null || input.HousekeeperId != null || !string.IsNullOrWhiteSpace(input.Search))
            throw new ValidationException("Turnover history supports room, branch, date range, result and pagination filters.");
        var q = HistoryInput(input with { RoomId = roomId });
        if (roomId <= 0 || cycleId <= 0) throw new ValidationException("Room and cycle IDs must be positive.");
        return ReadAsync(ticket, sid, q.BranchId, async token =>
        {
            var branches = await HistoryBranches("CLEANING_VIEW", q.BranchId, token); var rooms = await HistoryRooms(token);
            if (rooms == null) return new(TurnoverHistoryStatus.SchemaRequired);
            var room = await rooms.Where(x => x.RoomId == roomId && branches.Contains(x.BranchId)).Select(x => new { x.RoomId, x.BranchId }).SingleOrDefaultAsync(token);
            if (room == null) return new(TurnoverHistoryStatus.NotFound);
            if (!await HistoryFinal("INSPECTION_VIEW", [room.BranchId], token)) return new(TurnoverHistoryStatus.Forbidden);
            var tasks = db.CleaningTasks.AsNoTracking().Where(t => t.RoomId == roomId);
            if (cycleId != null) tasks = tasks.Where(t => t.CleaningTaskId == cycleId);
            if (q.From is { } from) tasks = tasks.Where(t => (t.ActualStartAt ?? t.ScheduledStartAt ?? t.CreatedAt) >= from);
            if (q.To is { } to) tasks = tasks.Where(t => (t.ActualStartAt ?? t.ScheduledStartAt ?? t.CreatedAt) <= to);
            if (q.Result != null) tasks = tasks.Where(t => t.RoomInspections.Where(i => i.RoomId == t.RoomId).OrderByDescending(i => i.InspectedAt ?? i.ScheduledAt ?? i.CreatedAt)
                .ThenByDescending(i => i.InspectionId).Select(i => i.Result).FirstOrDefault() == q.Result);
            var total = await tasks.CountAsync(token);
            if (cycleId != null && total == 0) return new(TurnoverHistoryStatus.NotFound);
            var page = await tasks.OrderByDescending(t => t.ActualStartAt ?? t.ScheduledStartAt ?? t.CreatedAt).ThenByDescending(t => t.CleaningTaskId)
                .Skip(cycleId != null ? 0 : (q.Page - 1) * q.PageSize).Take(cycleId != null ? 1 : q.PageSize).TagWith("UC038:Page").ToArrayAsync(token);
            var cycles = await HistoryCycles(page, token);
            object value = new OperationalHistoryPage<HistoryCycleDto>(cycles, total, q);
            if (cycleId != null)
            {
                var ids = page.Select(x => x.CleaningTaskId).ToArray();
                var inspectionRows = await HistoryInspectionRows(db.RoomInspections.AsNoTracking().Where(i => ids.Contains(i.CleaningTaskId ?? 0) && i.RoomId == roomId)
                    .OrderBy(i => i.InspectedAt ?? i.ScheduledAt ?? i.CreatedAt).ThenBy(i => i.InspectionId)).ToArrayAsync(token);
                var inspections = (await HistoryInspectionDetails(inspectionRows, token)).Select(i => i with { Evidence = i.Evidence.Select(e => e with { DownloadPath = $"/api/rooms/{roomId}/turnover-history/{cycleId}/evidence/{e.Id}?branchId={room.BranchId}" }).ToArray() }).ToArray();
                var reworks = await Reworks(ids, token);
                var progress = await db.CleaningProgressLogs.AsNoTracking().Where(p => ids.Contains(p.CleaningTaskId)).OrderBy(p => p.LoggedAt).ThenBy(p => p.ProgressId)
                    .Select(p => new HistoryProgressDto(p.ProgressId, p.ProgressPercent, p.Status, p.BlockerTypeId, p.Note, p.LoggedBy, p.LoggedAt)).ToArrayAsync(token);
                var evidence = await HistoryEvidence(ids, inspectionRows.Select(i => i.Id).ToArray(), inspections.SelectMany(i => i.Defects).Select(d => d.Id).ToArray(), reworks.Select(r => r.Id).ToArray(),
                    $"/api/rooms/{roomId}/turnover-history/{cycleId}/evidence", room.BranchId, token);
                value = new HistoryCycleDetailDto(cycles[0], progress, inspections, reworks, evidence);
                if (attachmentId is { } aid)
                {
                    var file = await HistoryFile(evidence, aid, privateRoot, token);
                    if (file == null) return new(TurnoverHistoryStatus.NotFound);
                    value = file;
                }
            }
            if (!await HistoryFinal("CLEANING_VIEW", [room.BranchId], token) || !await HistoryFinal("INSPECTION_VIEW", [room.BranchId], token)) return new(TurnoverHistoryStatus.Forbidden);
            return new(TurnoverHistoryStatus.Success, value);
        }, ct);
    }

    private async Task<HistoryCycleDto[]> HistoryCycles(CleaningTask[] tasks, CancellationToken ct)
    {
        var ids = tasks.Select(t => t.CleaningTaskId).ToArray();
        var assignments = await Assignments(ids, ct);
        var typeIds = tasks.Select(t => t.CleaningTypeId).Distinct().ToArray();
        var roomIds = tasks.Select(t => t.RoomId).Distinct().ToArray();
        var types = await db.ReferenceValues.AsNoTracking().Where(x => typeIds.Contains(x.ReferenceId)).ToDictionaryAsync(x => x.ReferenceId, x => x.DisplayName, ct);
        var rooms = await db.Rooms.AsNoTracking().Where(x => roomIds.Contains(x.RoomId)).ToDictionaryAsync(x => x.RoomId, ct);
        var inspections = await HistoryInspectionRows(db.RoomInspections.AsNoTracking().Where(x => ids.Contains(x.CleaningTaskId ?? 0) && x.RoomId == x.CleaningTask!.RoomId)).ToArrayAsync(ct);
        var reworks = await Reworks(ids, ct);
        var keys = ids.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray();
        var cancellations = await db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "cleaning_tasks" && keys.Contains(x.EntityId) && x.Action == "CANCEL")
            .Select(x => new { x.EntityId, x.AuditId, x.NewValues }).ToArrayAsync(ct);
        return tasks.Select(t => new HistoryCycleDto(t.CleaningTaskId, t.TaskNo, t.RoomId, rooms[t.RoomId].RoomNumber, rooms[t.RoomId].BranchId,
            types[t.CleaningTypeId], t.Status, t.ActualStartAt ?? t.ScheduledStartAt ?? t.CreatedAt, t.ActualStartAt, t.ActualEndAt,
            inspections.Where(i => i.CleaningTaskId == t.CleaningTaskId).OrderByDescending(i => i.At).ThenByDescending(i => i.Id).Select(i => i.Result).FirstOrDefault(),
            reworks.Count(r => r.CleaningTaskId == t.CleaningTaskId), Reason(cancellations.Where(c => c.EntityId == t.CleaningTaskId.ToString(CultureInfo.InvariantCulture)).OrderByDescending(c => c.AuditId).Select(c => c.NewValues).FirstOrDefault()),
            assignments.Where(a => a.Task == t.CleaningTaskId).Select(a => a.Value).ToArray())).ToArray();
        static string? Reason(string? json)
        {
            if (json == null) return null;
            using var value = JsonDocument.Parse(json);
            return value.RootElement.ValueKind == JsonValueKind.Object && value.RootElement.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String ? reason.GetString() : null;
        }
    }
    private sealed record AssignmentRow(long Task, HistoryAssignmentDto Value);
    private Task<AssignmentRow[]> Assignments(long[] ids, CancellationToken ct) => db.CleaningAssignments.AsNoTracking().Where(a => ids.Contains(a.CleaningTaskId))
        .OrderBy(a => a.AssignedAt).ThenBy(a => a.AssignmentId).Select(a => new AssignmentRow(a.CleaningTaskId,
            new HistoryAssignmentDto(a.AssignmentId, a.AssigneeUserId, a.AssigneeUser.FullName, a.AssignmentStatus, a.AssignedAt, a.EndedAt, a.Reason))).ToArrayAsync(ct);
    private Task<HistoryReworkDto[]> Reworks(long[] cycles, CancellationToken ct) => db.CleaningReworks.AsNoTracking().Where(r => cycles.Contains(r.CleaningTaskId)
        && r.Inspection.RoomId == r.CleaningTask.RoomId && r.Inspection.CleaningTaskId == r.CleaningTaskId).OrderBy(r => r.RequestedAt).ThenBy(r => r.ReworkId)
        .Select(r => new HistoryReworkDto(r.ReworkId, r.CleaningTaskId, r.InspectionId, r.AssigneeUserId, r.Reason, r.Status, r.RequestedAt, r.CompletedAt, r.Note)).ToArrayAsync(ct);
    private async Task<HistoryInspectionDetailDto[]> HistoryInspectionDetails(HistoryInspectionDto[] rows, CancellationToken ct)
    {
        var ids = rows.Select(x => x.Id).ToArray(); var versions = rows.Select(x => x.VersionId).Distinct().ToArray();
        var cycles = rows.Where(x => x.CleaningTaskId != null).Select(x => x.CleaningTaskId!.Value).Distinct().ToArray();
        var items = await db.InspectionChecklistItems.AsNoTracking().Where(x => versions.Contains(x.VersionId)).OrderBy(x => x.SortOrder).ThenBy(x => x.ItemId).ToArrayAsync(ct);
        var itemIds = items.Select(i => i.ItemId).ToArray();
        var groups = await TurnoverHistorySchema.ChecklistStateAsync(db, ct) == 1
            ? await db.Database.SqlQueryRaw<CriterionGroupRow>("SELECT item_id AS ItemId,criterion_group AS [Group] FROM dbo.inspection_checklist_items").Where(i => itemIds.Contains(i.ItemId)).ToDictionaryAsync(i => i.ItemId, i => i.Group, ct)
            : new Dictionary<long, string?>();
        var results = await db.InspectionResults.AsNoTracking().Where(r => ids.Contains(r.InspectionId) && r.ChecklistItem.VersionId == r.Inspection.VersionId).ToArrayAsync(ct);
        var defects = await db.InspectionDefects.AsNoTracking().Where(d => ids.Contains(d.InspectionId) && (d.ChecklistItemId == null || d.ChecklistItem!.VersionId == d.Inspection.VersionId))
            .OrderBy(d => d.DefectId).ToArrayAsync(ct);
        var reworks = await Reworks(cycles, ct); var assignments = await Assignments(cycles, ct);
        var related = await HistoryInspectionRows(db.RoomInspections.AsNoTracking().Where(i => cycles.Contains(i.CleaningTaskId ?? 0) && i.RoomId == i.CleaningTask!.RoomId)
            .OrderBy(i => i.InspectedAt ?? i.ScheduledAt ?? i.CreatedAt).ThenBy(i => i.InspectionId)).ToArrayAsync(ct);
        var evidence = await HistoryEvidence(cycles, ids, defects.Select(d => d.DefectId).ToArray(), reworks.Select(r => r.Id).ToArray(), "", 0, ct);
        var locations = Array.Empty<DefectLocationRow>(); var defectFiles = Array.Empty<InspectionPhotoRow>(); var resultFiles = Array.Empty<InspectionPhotoRow>();
        if (await TurnoverHistorySchema.HousekeepingInstalledAsync(db, ct))
        {
            locations = await db.Database.SqlQueryRaw<DefectLocationRow>("SELECT inspection_id AS InspectionId,defect_id AS TargetId,room_location AS Location FROM dbo.inspection_defects")
                .Where(d => ids.Contains(d.InspectionId)).ToArrayAsync(ct);
            defectFiles = await db.Database.SqlQueryRaw<InspectionPhotoRow>("SELECT d.inspection_id AS InspectionId,l.defect_id AS TargetId,l.attachment_id AS AttachmentId FROM dbo.inspection_defect_attachments l JOIN dbo.inspection_defects d ON d.defect_id=l.defect_id JOIN dbo.attachments a ON a.attachment_id=l.attachment_id WHERE a.deleted_at IS NULL")
                .Where(d => ids.Contains(d.InspectionId)).ToArrayAsync(ct);
            resultFiles = await db.Database.SqlQueryRaw<InspectionPhotoRow>("SELECT r.inspection_id AS InspectionId,l.result_id AS TargetId,l.attachment_id AS AttachmentId FROM dbo.inspection_result_attachments l JOIN dbo.inspection_results r ON r.result_id=l.result_id JOIN dbo.attachments a ON a.attachment_id=l.attachment_id WHERE a.deleted_at IS NULL")
                .Where(d => ids.Contains(d.InspectionId)).ToArrayAsync(ct);
        }
        return rows.Select(row => new HistoryInspectionDetailDto(row, assignments.Where(a => a.Task == row.CleaningTaskId).Select(a => a.Value).ToArray(),
            items.Where(i => i.VersionId == row.VersionId).SelectMany(item =>
            {
                var found = results.Where(r => r.InspectionId == row.Id && r.ChecklistItemId == item.ItemId).ToArray();
                return found.Length == 0 ? [new HistoryCriterionDto(null, item.ItemId, item.Criterion, item.Description, item.IsRequired, item.PassCondition, null, null, null, groups.GetValueOrDefault(item.ItemId))]
                    : found.Select(r => new HistoryCriterionDto(r.ResultId, item.ItemId, item.Criterion, item.Description, item.IsRequired, item.PassCondition, r.Outcome, r.Note, r.InspectedAt, groups.GetValueOrDefault(item.ItemId),
                        resultFiles.Where(f => f.TargetId == r.ResultId && f.InspectionId == row.Id).Select(f => f.AttachmentId).ToArray())).ToArray();
            }).ToArray(), defects.Where(d => d.InspectionId == row.Id).Select(d => new HistoryDefectDto(d.DefectId, d.ChecklistItemId, d.Description, d.Status, d.SeverityId, d.CorrectiveAction, d.ResolvedAt, d.ResolutionNote,
                locations.FirstOrDefault(l => l.TargetId == d.DefectId)?.Location, defectFiles.Where(f => f.TargetId == d.DefectId && f.InspectionId == row.Id).Select(f => f.AttachmentId).ToArray())).ToArray(),
            reworks.Where(r => r.InspectionId == row.Id).ToArray(), related.Where(i => i.CleaningTaskId == row.CleaningTaskId).ToArray(),
            evidence.Where(e => e.EntityType == "INSPECTION" && e.EntityId == row.Id || e.EntityType == "CLEANING_TASK" && e.EntityId == row.CleaningTaskId
                )
                .Select(e => e with { DownloadPath = $"/api/inspection-history/{row.Id}/evidence/{e.Id}?branchId={row.BranchId}" }).ToArray())).ToArray();
    }
    private sealed class CriterionGroupRow { public long ItemId { get; set; } public string? Group { get; set; } }
    public sealed class DefectLocationRow { public long InspectionId { get; set; } public long TargetId { get; set; } public string? Location { get; set; } }
    public sealed class InspectionPhotoRow { public long InspectionId { get; set; } public long TargetId { get; set; } public long AttachmentId { get; set; } }
    private async Task<HistoryEvidenceDto[]> HistoryEvidence(long[] cycles, long[] inspections, long[] defects, long[] reworks,
        string path, long branch, CancellationToken ct) => await db.Attachments.AsNoTracking().Where(a => a.DeletedAt == null && (
            a.EntityType == "CLEANING_TASK" && cycles.Contains(a.EntityId) || a.EntityType == "INSPECTION" && inspections.Contains(a.EntityId)
            ))
        .OrderBy(a => a.UploadedAt).ThenBy(a => a.AttachmentId).Select(a => new HistoryEvidenceDto(a.AttachmentId, a.EntityType, a.EntityId,
            a.OriginalFileName ?? a.FileName, a.MimeType, a.FileSizeBytes, a.UploadedAt, path + "/" + a.AttachmentId + "?branchId=" + branch)).ToArrayAsync(ct);
}
