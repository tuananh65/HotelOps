using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Assets;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetService
{
    public async Task<AssetDetailDto?> UpdateAssetAsync(long id, UpdateAssetRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var asset = await LockAssetAsync(id, cancellationToken);
        if (asset is null) return null;
        RequireOperational(asset);
        await ValidateMasterAsync(request, cancellationToken);
        var before = Snapshot(asset);
        ApplyMaster(asset, request);
        if (Snapshot(asset).All(pair => Equals(before[pair.Key], pair.Value)))
        {
            var unchanged = await RequireDetailAsync(id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return unchanged;
        }
        var now = DateTime.UtcNow;
        asset.UpdatedAt = now;
        Audit("assets", id, asset, request.ActorUserId, now, before, Snapshot(asset));
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateAssetCode(exception))
        {
            throw new ValidationException("AssetCode already exists.", exception);
        }
        var result = await RequireDetailAsync(id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
