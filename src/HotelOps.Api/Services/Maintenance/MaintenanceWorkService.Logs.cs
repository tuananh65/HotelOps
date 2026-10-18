using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Entities;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService
{
    public Task<MaintenanceWorkDetailDto?> HoldWorkAsync(long id,
        HoldMaintenanceWorkRequest request, CancellationToken cancellationToken) =>
        ExecuteCorrectiveAsync(id, request, null, (work, incident, now, token) =>
        {
            RequireState(work, "IN_PROGRESS");
            RequireAssignee(work, request.ActorUserId);
            var reason = RequiredNote(request.Reason, "Reason", 10);
            work.Status = "ON_HOLD";
            AddWorkLog(work, request.ActorUserId, reason, 0, now);
            return Task.CompletedTask;
        }, cancellationToken);

    public Task<MaintenanceWorkDetailDto?> ResumeWorkAsync(long id,
        ResumeMaintenanceWorkRequest request, CancellationToken cancellationToken) =>
        ExecuteCorrectiveAsync(id, request, null, (work, incident, now, token) =>
        {
            RequireState(work, "ON_HOLD");
            RequireAssignee(work, request.ActorUserId);
            work.Status = "IN_PROGRESS";
            var note = NormalizeNote(request.Note);
            if (note is not null)
                AddWorkLog(work, request.ActorUserId, note, 0, now);
            return Task.CompletedTask;
        }, cancellationToken);

    public async Task<MaintenanceWorkLogDto?> CreateLogAsync(long id,
        CreateMaintenanceWorkLogRequest request, CancellationToken cancellationToken)
    {
        MaintenanceWorkLog? log = null;
        var result = await ExecuteCorrectiveAsync(id, request, null, async (work, incident, now, token) =>
        {
            RequireState(work, "IN_PROGRESS", "ON_HOLD");
            RequireAssignee(work, request.ActorUserId);
            var note = RequiredNote(request.WorkNote, "WorkNote");
            log = AddWorkLog(work, request.ActorUserId, note, request.SpentMinutes, now);
            await _dbContext.SaveChangesAsync(token);
            // The technical log is a business record, not a substitute for its CREATE audit.
            AddAudit("maintenance_work_logs", log.LogId, incident.BranchId, request.ActorUserId,
                now, null, new Dictionary<string, object?>
                {
                    ["maintenance_work_id"] = log.MaintenanceWorkId,
                    ["work_note"] = log.WorkNote,
                    ["spent_minutes"] = log.SpentMinutes,
                    ["material_issue_id"] = log.MaterialIssueId,
                    ["status"] = log.Status,
                    ["logged_by"] = log.LoggedBy,
                    ["logged_at"] = log.LoggedAt
                });
        }, cancellationToken);
        if (result is null)
            return null;
        return new MaintenanceWorkLogDto
        {
            LogId = log!.LogId,
            MaintenanceWorkId = log.MaintenanceWorkId,
            WorkNote = log.WorkNote,
            SpentMinutes = log.SpentMinutes,
            MaterialIssueId = log.MaterialIssueId,
            Status = log.Status,
            LoggedBy = log.LoggedBy,
            LoggedAt = log.LoggedAt
        };
    }

    private MaintenanceWorkLog AddWorkLog(MaintenanceWork work, long actorId,
        string note, int spentMinutes, DateTime now)
    {
        var log = new MaintenanceWorkLog
        {
            MaintenanceWorkId = work.MaintenanceWorkId,
            WorkNote = note,
            SpentMinutes = spentMinutes,
            MaterialIssueId = null,
            Status = work.Status,
            LoggedBy = actorId,
            LoggedAt = now
        };
        _dbContext.MaintenanceWorkLogs.Add(log);
        return log;
    }

    private static string? NormalizeNote(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    private static string RequiredNote(string? note, string field, int minimumLength = 1)
    {
        var normalized = NormalizeNote(note);
        if (normalized is null || normalized.Length < minimumLength)
            throw new ValidationException($"{field} must contain at least {minimumLength} trimmed characters.");
        return normalized;
    }
}
