using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Assets;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetService
{
    public async Task<AssetPagedResponse<AssetMaintenanceHistoryDto>?> GetMaintenanceHistoryAsync(
        long assetId, AssetMaintenanceHistoryRequest request, CancellationToken cancellationToken)
    {
        var offset = Offset(request);
        if (request.From > request.To) throw new ValidationException("From must not be after To.");
        var type = OptionalNote(request.Type)?.ToUpperInvariant();
        if (type is not (null or "CORRECTIVE_WORK" or "PREVENTIVE_RUN" or "CONDITION_CHANGE"))
            throw new ValidationException("Type must be CORRECTIVE_WORK, PREVENTIVE_RUN or CONDITION_CHANGE.");
        if (!await _dbContext.Assets.AsNoTracking().AnyAsync(a => a.AssetId == assetId, cancellationToken)) return null;
        // A work represented by a preventive run is not duplicated as corrective work.
        var corrective = _dbContext.MaintenanceWorks.AsNoTracking()
            .Where(w => w.Incident != null && w.Incident.AssetId == assetId && !w.PreventiveMaintenanceRuns.Any())
            .Select(w => new AssetMaintenanceHistoryDto
            {
                Type = "CORRECTIVE_WORK", SourceId = w.MaintenanceWorkId, OccurredAt = w.CreatedAt,
                IncidentId = w.IncidentId, MaintenanceWorkId = w.MaintenanceWorkId, ScheduleId = null,
                WorkNo = w.WorkNo, Status = w.Status, ActorUserId = w.AssigneeId,
                Note = w.ResolutionNote, Result = null, OldCondition = null, NewCondition = null
            });
        var preventive = _dbContext.PreventiveMaintenanceRuns.AsNoTracking().Where(r => r.Schedule.AssetId == assetId)
            .Select(r => new AssetMaintenanceHistoryDto
            {
                Type = "PREVENTIVE_RUN", SourceId = r.RunId, OccurredAt = r.PlannedAt,
                IncidentId = null, MaintenanceWorkId = r.MaintenanceWorkId, ScheduleId = r.ScheduleId,
                WorkNo = r.MaintenanceWork == null ? null : r.MaintenanceWork.WorkNo, Status = null,
                ActorUserId = r.PerformedBy, Note = r.Note, Result = r.Result, OldCondition = null, NewCondition = null
            });
        var conditions = _dbContext.AssetConditionHistories.AsNoTracking().Where(h => h.AssetId == assetId)
            .Select(h => new AssetMaintenanceHistoryDto
            {
                Type = "CONDITION_CHANGE", SourceId = h.ConditionHistoryId, OccurredAt = h.ChangedAt,
                IncidentId = h.RelatedIncidentId, MaintenanceWorkId = null, ScheduleId = null,
                WorkNo = null, Status = null, ActorUserId = h.ChangedBy, Note = h.Note,
                Result = null, OldCondition = h.OldCondition, NewCondition = h.NewCondition
            });
        var query = corrective.Concat(preventive).Concat(conditions);
        if (type is not null) query = query.Where(h => h.Type == type);
        // Inclusive boundaries apply to the documented source event timestamp.
        if (request.From is DateTime from) query = query.Where(h => h.OccurredAt >= from);
        if (request.To is DateTime to) query = query.Where(h => h.OccurredAt <= to);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(h => h.OccurredAt).ThenBy(h => h.Type)
            .ThenByDescending(h => h.SourceId).Skip(offset).Take(request.PageSize).ToListAsync(cancellationToken);
        return Page(items, count, request);
    }
}
