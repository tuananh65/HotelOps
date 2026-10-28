using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using HotelOps.Api.DTOs.Assets;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetService
{
    private static readonly Expression<Func<AssetHandover, AssetHandoverDto>> HandoverProjection = h => new()
    {
        HandoverId = h.HandoverId, HandoverNo = h.HandoverNo, AssetId = h.AssetId, AssetCode = h.Asset.AssetCode,
        FromDepartmentId = h.FromDepartmentId, FromDepartmentName = h.FromDepartment == null ? null : h.FromDepartment.DepartmentName,
        ToDepartmentId = h.ToDepartmentId, ToDepartmentName = h.ToDepartment == null ? null : h.ToDepartment.DepartmentName,
        FromUserId = h.FromUserId, FromUserName = h.FromUser == null ? null : h.FromUser.FullName,
        ToUserId = h.ToUserId, ToUserName = h.ToUser == null ? null : h.ToUser.FullName,
        ConditionNote = h.ConditionNote, HandoverStatus = h.HandoverStatus, HandedOverAt = h.HandedOverAt,
        AcceptedAt = h.AcceptedAt, CreatedBy = h.CreatedBy, CreatedAt = h.CreatedAt
    };

    public Task<AssetHandoverDto?> GetHandoverAsync(long handoverId, CancellationToken cancellationToken) =>
        _dbContext.AssetHandovers.AsNoTracking().Where(h => h.HandoverId == handoverId)
            .Select(HandoverProjection).SingleOrDefaultAsync(cancellationToken);

    public async Task<AssetPagedResponse<AssetHandoverDto>?> GetHandoversAsync(long assetId, AssetPageRequest request, CancellationToken cancellationToken)
    {
        var offset = Offset(request);
        if (!await _dbContext.Assets.AsNoTracking().AnyAsync(a => a.AssetId == assetId, cancellationToken)) return null;
        var query = _dbContext.AssetHandovers.AsNoTracking().Where(h => h.AssetId == assetId);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(h => h.CreatedAt).ThenByDescending(h => h.HandoverId)
            .Skip(offset).Take(request.PageSize).Select(HandoverProjection).ToListAsync(cancellationToken);
        return Page(items, count, request);
    }

    private async Task ValidateHandoverTargetAsync(Asset asset, long? departmentId, long? userId, CancellationToken cancellationToken)
    {
        if (departmentId is null || !await _dbContext.Departments.AsNoTracking().AnyAsync(
            d => d.DepartmentId == departmentId && d.BranchId == asset.BranchId && d.IsActive, cancellationToken))
            throw new ValidationException("ToDepartmentId must identify an active department in the Asset branch.");
        if (userId is null) throw new ValidationException("ToUserId is required.");
        // The future receiver was already validated under a retained User lock.
    }

    public async Task<AssetHandoverDto?> CreateHandoverAsync(long assetId, CreateAssetHandoverRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await new HotelOps.Api.Services.Users.UserEligibilityLock(_dbContext).RequireEligibleAsync(request.ToUserId, cancellationToken);
        var asset = await LockAssetAsync(assetId, cancellationToken);
        if (asset is null) return null;
        await _dbContext.Entry(asset).ReloadAsync(cancellationToken);
        RequireOperational(asset);
        await RequireActorAsync(request.ActorUserId, cancellationToken);
        await ValidateHandoverTargetAsync(asset, request.ToDepartmentId, request.ToUserId, cancellationToken);
        if (asset.CurrentDepartmentId == request.ToDepartmentId && asset.CustodianUserId == request.ToUserId)
            throw new ValidationException("Target custody must differ from current custody.");
        if (await _dbContext.AssetHandovers.AsNoTracking().AnyAsync(
            h => h.AssetId == assetId && h.HandoverStatus == "PENDING", cancellationToken))
            throw new ValidationException("The Asset already has a pending handover.");
        var now = DateTime.UtcNow;
        var handover = new AssetHandover
        {
            AssetId = assetId, HandoverNo = await AllocateHandoverNoAsync(now, cancellationToken),
            FromDepartmentId = asset.CurrentDepartmentId, FromUserId = asset.CustodianUserId,
            ToDepartmentId = request.ToDepartmentId, ToUserId = request.ToUserId,
            ConditionNote = OptionalNote(request.ConditionNote), HandoverStatus = "PENDING",
            HandedOverAt = now, CreatedAt = now, CreatedBy = request.ActorUserId
        };
        _dbContext.AssetHandovers.Add(handover);
        await _dbContext.SaveChangesAsync(cancellationToken);
        Audit("asset_handovers", handover.HandoverId, asset, request.ActorUserId, now, null, HandoverSnapshot(handover));
        await _dbContext.SaveChangesAsync(cancellationToken);
        var result = await GetHandoverAsync(handover.HandoverId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public Task<AssetHandoverDto?> AcceptHandoverAsync(long handoverId, AcceptAssetHandoverRequest request, CancellationToken cancellationToken) =>
        FinalizeHandoverAsync(handoverId, request.ActorUserId, null, cancellationToken);

    public Task<AssetHandoverDto?> RejectHandoverAsync(long handoverId, RejectAssetHandoverRequest request, CancellationToken cancellationToken) =>
        FinalizeHandoverAsync(handoverId, request.ActorUserId, Required(request.Reason, "Reason", minimum: 10), cancellationToken);

    private async Task<AssetHandoverDto?> FinalizeHandoverAsync(long handoverId, long actor, string? rejectionReason, CancellationToken cancellationToken)
    {
        var discovered = await _dbContext.AssetHandovers.AsNoTracking().Where(h => h.HandoverId == handoverId)
            .Select(h => new { h.AssetId, h.FromUserId, h.ToUserId }).SingleOrDefaultAsync(cancellationToken);
        if (discovered is null) return null;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (rejectionReason is null)
        {
            if (discovered.ToUserId is not long receiver) throw new ValidationException("ToUserId is required.");
            var ids = new[] { discovered.FromUserId, discovered.ToUserId }.Where(x => x.HasValue).Select(x => x!.Value);
            var locked = await new HotelOps.Api.Services.Users.UserEligibilityLock(_dbContext).LockAsync(ids, cancellationToken);
            HotelOps.Api.Services.Users.UserEligibilityLock.RequireEligible(receiver, locked);
        }
        // Accept: sorted custody Users -> Asset -> Handover. Reject: Asset -> Handover.
        var asset = await LockAssetAsync(discovered.AssetId, cancellationToken);
        var handover = await _dbContext.AssetHandovers
            .FromSqlInterpolated($"SELECT * FROM asset_handovers WITH (UPDLOCK, HOLDLOCK) WHERE handover_id = {handoverId}")
            .AsTracking().SingleOrDefaultAsync(cancellationToken);
        if (handover is null) return null;
        await _dbContext.Entry(handover).ReloadAsync(cancellationToken);
        if (asset is not null) await _dbContext.Entry(asset).ReloadAsync(cancellationToken);
        if (asset is null || handover.AssetId != asset.AssetId)
            throw new ValidationException("Handover relationship changed. Reload before retrying.");
        if (rejectionReason is null && (handover.FromUserId != discovered.FromUserId || handover.ToUserId != discovered.ToUserId))
            throw new ValidationException("Handover custody identifiers changed after discovery. Reload before retrying.");
        RequireOperational(asset);
        await RequireActorAsync(actor, cancellationToken);
        if (handover.HandoverStatus != "PENDING") throw new ValidationException("Only a PENDING handover can be finalized.");
        if (handover.ToUserId != actor) throw new ValidationException("Only the designated receiver can finalize this handover.");
        var now = DateTime.UtcNow;
        var beforeHandover = HandoverSnapshot(handover);
        var beforeAsset = Snapshot(asset);
        if (rejectionReason is null)
        {
            if (asset.CurrentDepartmentId != handover.FromDepartmentId || asset.CustodianUserId != handover.FromUserId)
                throw new ValidationException("Current custody no longer matches the source snapshot.");
            await ValidateHandoverTargetAsync(asset, handover.ToDepartmentId, handover.ToUserId, cancellationToken);
            handover.HandoverStatus = "ACCEPTED";
            handover.AcceptedAt = now;
            asset.CurrentDepartmentId = handover.ToDepartmentId;
            asset.CustodianUserId = handover.ToUserId;
            asset.UpdatedAt = now;
        }
        else handover.HandoverStatus = "REJECTED";
        await _dbContext.SaveChangesAsync(cancellationToken);
        Audit("asset_handovers", handoverId, asset, actor, now, beforeHandover, HandoverSnapshot(handover), rejectionReason);
        if (rejectionReason is null)
            Audit("assets", asset.AssetId, asset, actor, now, beforeAsset, Snapshot(asset));
        await _dbContext.SaveChangesAsync(cancellationToken);
        var result = await GetHandoverAsync(handoverId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static Dictionary<string, object?> HandoverSnapshot(AssetHandover h) => new()
    {
        ["handover_id"] = h.HandoverId, ["handover_no"] = h.HandoverNo, ["asset_id"] = h.AssetId,
        ["from_department_id"] = h.FromDepartmentId, ["from_user_id"] = h.FromUserId,
        ["to_department_id"] = h.ToDepartmentId, ["to_user_id"] = h.ToUserId, ["condition_note"] = h.ConditionNote,
        ["handover_status"] = h.HandoverStatus, ["handed_over_at"] = h.HandedOverAt, ["accepted_at"] = h.AcceptedAt,
        ["created_by"] = h.CreatedBy, ["created_at"] = h.CreatedAt
    };

    private async Task<string> AllocateHandoverNoAsync(DateTime now, CancellationToken cancellationToken)
    {
        var prefix = "HO-" + now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-";
        var resource = "HotelOps:AssetHandoverNo:" + prefix;
        await _dbContext.Database.ExecuteSqlInterpolatedAsync($@"
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = {resource},
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
            IF @result < 0 THROW 50003, 'Could not acquire handover number allocation lock.', 1;", cancellationToken);
        var maximum = await _dbContext.Database.SqlQuery<long>($@"
            SELECT COALESCE(MAX(TRY_CONVERT(bigint, SUBSTRING(handover_no, 13, 40))), CAST(0 AS bigint)) AS [Value]
            FROM asset_handovers WITH (UPDLOCK, HOLDLOCK) WHERE handover_no LIKE {prefix + "%"}")
            .SingleAsync(cancellationToken);
        return prefix + checked(maximum + 1).ToString("D3", CultureInfo.InvariantCulture);
    }
}
