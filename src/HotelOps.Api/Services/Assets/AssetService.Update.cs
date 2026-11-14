using HotelOps.Api.Services.Dev2Authorization;
using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Assets;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetService
{
    public async Task<AssetDetailDto?> UpdateAssetAsync(long id, UpdateAssetRequest request, CancellationToken cancellationToken)
    {
        Dev2ServiceAuthority.RequireEntry(_authorityHttp, "Assets", ["UpdateAsset"], request.ActorUserId, id, request);
        await using var transaction = await Dev2ServiceAuthority.BeginTransactionAsync(_dbContext, _authorityHttp, cancellationToken);
        var asset = await LockAssetAsync(id, cancellationToken);
        if (asset is null) return null;
        RequireOperational(asset);
        await ValidateMasterAsync(request, cancellationToken);
        var before = Snapshot(asset);
        ApplyMaster(asset, request);
        if (Snapshot(asset).All(pair => Equals(before[pair.Key], pair.Value)))
        {
            var unchanged = await RequireDetailAsync(id, cancellationToken);
            await Dev2ServiceAuthority.CommitAsync(_dbContext, _authorityHttp, transaction, cancellationToken);
            return unchanged;
        }
        var now = DateTime.UtcNow;
        asset.UpdatedAt = now;
        Audit("assets", id, asset, TrustedActorId, now, before, Snapshot(asset));
        try
        {
            await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateAssetCode(exception))
        {
            throw new ValidationException("AssetCode already exists.", exception);
        }
        var result = await RequireDetailAsync(id, cancellationToken);
        await Dev2ServiceAuthority.CommitAsync(_dbContext, _authorityHttp, transaction, cancellationToken);
        return result;
    }
}
