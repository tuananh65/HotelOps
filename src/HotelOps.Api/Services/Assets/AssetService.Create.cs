using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Assets;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetService
{
    public async Task<AssetDetailDto> CreateAssetAsync(CreateAssetRequest request, CancellationToken cancellationToken)
    {
        await ValidateMasterAsync(request, cancellationToken);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
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
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateAssetCode(exception))
        {
            throw new ValidationException("AssetCode already exists.", exception);
        }
        Audit("assets", asset.AssetId, asset, request.ActorUserId, now, null, Snapshot(asset));
        await _dbContext.SaveChangesAsync(cancellationToken);
        var result = await RequireDetailAsync(asset.AssetId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
