using HotelOps.Api.Services.Dev2Authorization;
using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Assets;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetService
{
    public async Task<AssetDetailDto> CreateAssetAsync(CreateAssetRequest request, CancellationToken cancellationToken)
    {
        Dev2ServiceAuthority.RequireEntry(_authorityHttp, "Assets", ["CreateAsset"], request.ActorUserId, null, request);
        await ValidateMasterAsync(request, cancellationToken);
        await using var transaction = await Dev2ServiceAuthority.BeginTransactionAsync(_dbContext, _authorityHttp, cancellationToken);
        if (!await _dbContext.Branches.AsNoTracking()
            .AnyAsync(b => b.BranchId == request.BranchId && b.IsActive, cancellationToken))
            throw new ValidationException("BranchId must identify an active Branch.");
        if (request.CurrentDepartmentId is long department && !await _dbContext.Departments.AsNoTracking()
            .AnyAsync(d => d.DepartmentId == department && d.BranchId == request.BranchId && d.IsActive, cancellationToken))
            throw new ValidationException("CurrentDepartmentId must identify an active department in the Asset branch.");
        var now = DateTime.UtcNow;
        var asset = new Asset
        {
            BranchId = request.BranchId, CurrentDepartmentId = request.CurrentDepartmentId,
            ConditionStatus = "GOOD", LifecycleStatus = "IN_USE", CreatedAt = now
        };
        ApplyMaster(asset, request);
        _dbContext.Assets.Add(asset);
        try
        {
            await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateAssetCode(exception))
        {
            throw new ValidationException("AssetCode already exists.", exception);
        }
        Audit("assets", asset.AssetId, asset, TrustedActorId, now, null, Snapshot(asset));
        await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, cancellationToken);
        var result = await RequireDetailAsync(asset.AssetId, cancellationToken);
        await Dev2ServiceAuthority.CommitAsync(_dbContext, _authorityHttp, transaction, cancellationToken);
        return result;
    }
}
