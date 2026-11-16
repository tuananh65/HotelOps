using HotelOps.Api.Services.Dev2Authorization;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Entities;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService
{
    public Task<MaintenanceWorkDetailDto?> HoldWorkAsync(long id,
        HoldMaintenanceWorkRequest request, CancellationToken cancellationToken) {
        Dev2ServiceAuthority.RequireEntry(_authorityHttp, "MaintenanceWorks", ["HoldWork"], request.ActorUserId, id, request);
        return ExecuteCorrectiveAsync(id, request, null, (work, incident, now, token) =>
        {
            RequireState(work, "IN_PROGRESS");
            RequireAssignee(work, TrustedActorId);
            var reason = RequiredNote(request.Reason, "Reason", 10);
            work.Status = "ON_HOLD";
            AddWorkLog(work, TrustedActorId, reason, 0, now);
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public Task<MaintenanceWorkDetailDto?> ResumeWorkAsync(long id,
        ResumeMaintenanceWorkRequest request, CancellationToken cancellationToken) {
        Dev2ServiceAuthority.RequireEntry(_authorityHttp, "MaintenanceWorks", ["ResumeWork"], request.ActorUserId, id, request);
        return ExecuteCorrectiveAsync(id, request, null, (work, incident, now, token) =>
        {
            // IN_PROGRESS is accepted only with the current, unused Return proof checked
            // by ExecuteCorrectiveCoreAsync under user/time/source locks and native ETag.
            RequireState(work, "ON_HOLD", "IN_PROGRESS");
            RequireAssignee(work, TrustedActorId);
            work.Status = "IN_PROGRESS";
            var note = NormalizeNote(request.Note);
            if (note is not null)
                AddWorkLog(work, TrustedActorId, note, 0, now);
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public async Task<MaintenanceWorkLogDto?> CreateLogAsync(long id,
        CreateMaintenanceWorkLogRequest request, CancellationToken cancellationToken)
    {
        Dev2ServiceAuthority.RequireEntry(_authorityHttp, "MaintenanceWorks", ["CreateLog"], request.ActorUserId, id, request);
        MaintenanceWorkLog? log = null;
        IReadOnlyList<MaintenanceMaterialUsageDto> recordedUsages = [];
        var result = await ExecuteCorrectiveAsync(id, request, null, async (work, incident, now, token) =>
        {
            RequireState(work, "IN_PROGRESS", "ON_HOLD");
            RequireAssignee(work, TrustedActorId);
            var note = RequiredNote(request.WorkNote, "WorkNote");
            var materials = request.MaterialUsages is { Count: > 0 } ? await PrepareUsages(work, incident, request.MaterialUsages, token) : [];
            log = AddWorkLog(work, TrustedActorId, note, request.SpentMinutes, now);
            await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, token);
            if (request.ProgressPercent is int percent)
                await MaintenanceProgressStore.WriteAsync(_dbContext, log.LogId, percent, token);
            if (materials.Count > 0) await SaveUsages(log, materials, incident, TrustedActorId, now, token);
            // Advance the owning native version for child writes without changing workflow state.
            await _dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.maintenance_works SET status=status WHERE maintenance_work_id={id}", token);
            if (materials.Count > 0)
                recordedUsages = (await UsageDetails(id, token)).Where(x => x.MaintenanceWorkLogId == log.LogId).ToArray();
            // The technical log is a business record, not a substitute for its CREATE audit.
            var values = new Dictionary<string, object?>
                {
                    ["maintenance_work_id"] = log.MaintenanceWorkId,
                    ["work_note"] = log.WorkNote,
                    ["progress_percent"] = request.ProgressPercent,
                    ["spent_minutes"] = log.SpentMinutes,
                    ["material_issue_id"] = log.MaterialIssueId,
                    ["status"] = log.Status,
                    ["logged_by"] = log.LoggedBy,
                    ["logged_at"] = log.LoggedAt
                };
            if (materials.Count > 0) values["material_usages"] = materials.Select(x => new { x.Row.UsageId, x.Row.StockDocumentLineId, x.Row.UsedBaseQty }).ToArray();
            AddAudit("maintenance_work_logs", log.LogId, incident.BranchId, TrustedActorId, now, null, values);
        }, cancellationToken, request.MaterialUsages is { Count: > 0 } ? token => PrepareMaterialLocks(request.MaterialUsages, token) : null);
        if (result is null)
            return null;
        return new MaintenanceWorkLogDto
        {
            ExecutionETag = result.ExecutionETag,
            LogId = log!.LogId,
            ProgressPercent = request.ProgressPercent,
            MaintenanceWorkId = log.MaintenanceWorkId,
            WorkNote = log.WorkNote,
            SpentMinutes = log.SpentMinutes,
            MaterialIssueId = log.MaterialIssueId,
            Status = log.Status,
            LoggedBy = log.LoggedBy,
            LoggedAt = log.LoggedAt,
            MaterialUsages = recordedUsages
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
