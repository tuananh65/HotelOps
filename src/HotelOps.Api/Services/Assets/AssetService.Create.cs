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
        AssetRegistrationPhotos.Validate(request.Photos);
        using var files = new HotelOps.Api.Services.Housekeeping.EvidenceFileLifetime();
        await using var transaction = await Dev2ServiceAuthority.BeginTransactionAsync(_dbContext, _authorityHttp, cancellationToken);
        await ValidateMasterAsync(request, cancellationToken);
        if (!await _dbContext.Branches.AsNoTracking()
            .AnyAsync(b => b.BranchId == request.BranchId && b.IsActive, cancellationToken))
            throw new ValidationException("BranchId must identify an active Branch.");
        if (request.CurrentDepartmentId is not long department || !await _dbContext.Departments.AsNoTracking()
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
            throw new ValidationException("MSG35: AssetCode already exists.", exception);
        }
        if (request.Photos is { Count: > 0 })
        {
            var services = _authorityHttp?.HttpContext?.RequestServices ?? throw new InvalidOperationException("Asset evidence context unavailable.");
            var environment = services.GetRequiredService<IWebHostEnvironment>();
            var root = services.GetRequiredService<IConfiguration>()["Evidence:PrivateRoot"] ?? Path.Combine(environment.ContentRootPath, "App_Data", "evidence");
            await AssetRegistrationPhotos.StageAsync(_dbContext, asset.AssetId, TrustedActorId, now, request.Photos, root, files, cancellationToken);
        }
        Audit("assets", asset.AssetId, asset, TrustedActorId, now, null, Snapshot(asset));
        await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, cancellationToken);
        var result = await RequireDetailAsync(asset.AssetId, cancellationToken);
        await files.BeforeCommitAsync(cancellationToken);
        await Dev2ServiceAuthority.CommitAsync(_dbContext, _authorityHttp, transaction, cancellationToken);
        return result;
    }
}
