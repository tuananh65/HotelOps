using HotelOps.Api.Services.Dev2Authorization;
using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Assets;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetService
{
    public async Task<AssetDetailDto?> RetireAssetAsync(long assetId, RetireAssetRequest request, CancellationToken cancellationToken)
    {
        Dev2ServiceAuthority.RequireEntry(_authorityHttp, "Assets", ["RetireAsset"], request.ActorUserId, assetId, request);
        var reason = Required(request.Reason, "Reason", minimum: 10);
        await using var transaction = await Dev2ServiceAuthority.BeginTransactionAsync(_dbContext, _authorityHttp, cancellationToken);
        var asset = await LockAssetAsync(assetId, cancellationToken);
        if (asset is null) return null;
        await RequireActorAsync(TrustedActorId, cancellationToken);
        if (asset.LifecycleStatus == "RETIRED")
        {
            var unchanged = await RequireDetailAsync(assetId, cancellationToken);
            await Dev2ServiceAuthority.CommitAsync(_dbContext, _authorityHttp, transaction, cancellationToken);
            return unchanged;
        }
        RequireOperational(asset);
        // Incident create takes this same Asset lock before writing. Handover
        // submit also locks Asset first, so neither can cross this decision.
        if (await _dbContext.Incidents.AsNoTracking().AnyAsync(i => i.AssetId == assetId &&
            (i.Status == "OPEN" || i.Status == "IN_PROGRESS" || i.Status == "RESOLVED"), cancellationToken) ||
            await _dbContext.MaintenanceWorks.AsNoTracking().AnyAsync(w => w.Incident != null && w.Incident.AssetId == assetId &&
            (w.Status == "PENDING" || w.Status == "IN_PROGRESS" || w.Status == "ON_HOLD" || w.Status == "COMPLETED"), cancellationToken) ||
            await _dbContext.PreventiveMaintenanceSchedules.AsNoTracking().AnyAsync(s => s.AssetId == assetId && s.IsActive, cancellationToken) ||
            await _dbContext.PreventiveMaintenanceRuns.AsNoTracking().AnyAsync(r => r.Schedule.AssetId == assetId && r.PerformedAt == null, cancellationToken) ||
            await _dbContext.AssetHandovers.AsNoTracking().AnyAsync(h => h.AssetId == assetId && h.HandoverStatus == "PENDING", cancellationToken))
            throw new ValidationException("Asset has a blocking Incident, maintenance work, preventive schedule/run or pending handover.");
        var before = Snapshot(asset);
        var now = DateTime.UtcNow;
        asset.LifecycleStatus = "RETIRED";
        asset.UpdatedAt = now;
        await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, cancellationToken);
        Audit("assets", assetId, asset, TrustedActorId, now, before, Snapshot(asset), reason);
        await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, cancellationToken);
        var result = await RequireDetailAsync(assetId, cancellationToken);
        await Dev2ServiceAuthority.CommitAsync(_dbContext, _authorityHttp, transaction, cancellationToken);
        return result;
    }
}
