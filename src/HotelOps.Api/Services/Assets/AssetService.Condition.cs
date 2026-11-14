using HotelOps.Api.Services.Dev2Authorization;
using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Assets;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetService
{
    public async Task<AssetDetailDto?> ChangeConditionAsync(long id, ChangeAssetConditionRequest request, CancellationToken cancellationToken)
    {
        Dev2ServiceAuthority.RequireEntry(_authorityHttp, "Assets", ["ChangeCondition"], request.ActorUserId, id, request);
        var expected = Condition(request.ExpectedCondition, "ExpectedCondition");
        var target = Condition(request.NewCondition, "NewCondition");
        var note = Required(request.Note, "Note");
        await using var transaction = await Dev2ServiceAuthority.BeginTransactionAsync(_dbContext, _authorityHttp, cancellationToken);
        var asset = await LockAssetAsync(id, cancellationToken);
        if (asset is null) return null;
        RequireOperational(asset);
        await RequireActorAsync(TrustedActorId, cancellationToken);
        if (asset.ConditionStatus != expected)
            throw new ValidationException("ExpectedCondition does not match the current Asset condition.");
        if (request.RelatedIncidentId is long incidentId && !await _dbContext.Incidents.AsNoTracking()
            .AnyAsync(i => i.IncidentId == incidentId && i.AssetId == id, cancellationToken))
            throw new ValidationException("RelatedIncidentId must identify an Incident belonging to this Asset.");
        if (expected == target)
        {
            var unchanged = await RequireDetailAsync(id, cancellationToken);
            await Dev2ServiceAuthority.CommitAsync(_dbContext, _authorityHttp, transaction, cancellationToken);
            return unchanged;
        }
        var before = Snapshot(asset);
        var now = DateTime.UtcNow;
        asset.ConditionStatus = target;
        asset.UpdatedAt = now;
        var history = new AssetConditionHistory
        {
            AssetId = id, OldCondition = expected, NewCondition = target, Note = note,
            RelatedIncidentId = request.RelatedIncidentId, ChangedBy = TrustedActorId, ChangedAt = now
        };
        _dbContext.AssetConditionHistories.Add(history);
        await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, cancellationToken);
        Audit("assets", id, asset, TrustedActorId, now, before, Snapshot(asset));
        Audit("asset_condition_history", history.ConditionHistoryId, asset, TrustedActorId, now, null, new()
        {
            ["asset_id"] = id, ["old_condition"] = expected, ["new_condition"] = target,
            ["note"] = note, ["related_incident_id"] = request.RelatedIncidentId,
            ["changed_by"] = TrustedActorId, ["changed_at"] = now
        });
        await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, cancellationToken);
        var result = await RequireDetailAsync(id, cancellationToken);
        await Dev2ServiceAuthority.CommitAsync(_dbContext, _authorityHttp, transaction, cancellationToken);
        return result;
    }
}
