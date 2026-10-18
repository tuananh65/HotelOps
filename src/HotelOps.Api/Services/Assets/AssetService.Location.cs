using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Assets;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetService
{
    public Task<AssetDetailDto?> AssignLocationAsync(long id, AssignAssetLocationRequest request, CancellationToken cancellationToken) =>
        MoveAsync(id, request.ActorUserId, request.RoomId, request.AreaId, request.Reason, null, null, null, cancellationToken);

    public Task<AssetDetailDto?> TransferLocationAsync(long id, TransferAssetLocationRequest request, CancellationToken cancellationToken) =>
        MoveAsync(id, request.ActorUserId, request.RoomId, request.AreaId, request.Reason,
            Required(request.ExpectedLocationType, "ExpectedLocationType").ToUpperInvariant(),
            request.ExpectedLocationId, request.ConfirmationNote, cancellationToken);

    private async Task<AssetDetailDto?> MoveAsync(long id, long actor, long? roomId, long? areaId,
        string? reason, string? expectedType, long? expectedId, string? confirmation, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var asset = await LockAssetAsync(id, cancellationToken);
        if (asset is null) return null;
        RequireOperational(asset);
        await RequireActorAsync(actor, cancellationToken);
        if (roomId.HasValue == areaId.HasValue)
            throw new ValidationException("Supply exactly one of RoomId and AreaId.");
        var currentType = asset.CurrentRoomId.HasValue ? "ROOM" : asset.CurrentAreaId.HasValue ? "AREA" : null;
        var currentId = asset.CurrentRoomId ?? asset.CurrentAreaId;
        if (asset.CurrentRoomId.HasValue && asset.CurrentAreaId.HasValue)
            throw new ValidationException("The legacy physical location is ambiguous; no automatic repair is performed.");
        if (expectedType is null)
        {
            if (currentType is not null)
                throw new ValidationException("Asset already has a physical location.");
        }
        else
        {
            if (expectedType is not ("ROOM" or "AREA") || currentType is null ||
                expectedType != currentType || expectedId != currentId)
                throw new ValidationException("Expected location does not match the current Asset location.");
            reason = Required(reason, "Reason", minimum: 10);
        }
        var targetType = roomId.HasValue ? "ROOM" : "AREA";
        var targetId = (roomId ?? areaId)!.Value;
        if (targetType == currentType && targetId == currentId)
            throw new ValidationException("Target must differ from the current location.");
        var validTarget = roomId.HasValue
            ? await _dbContext.Rooms.AsNoTracking().AnyAsync(r => r.RoomId == targetId &&
                r.BranchId == asset.BranchId && r.IsActive, cancellationToken)
            : await _dbContext.Areas.AsNoTracking().AnyAsync(a => a.AreaId == targetId &&
                a.BranchId == asset.BranchId && a.IsActive, cancellationToken);
        if (!validTarget)
            throw new ValidationException("Target must be an active room/area in the Asset branch.");
        confirmation = OptionalNote(confirmation);
        if (expectedType is not null && await _dbContext.MaintenanceWorks.AsNoTracking()
            .AnyAsync(w => w.Incident != null && w.Incident.AssetId == id &&
                (w.Status == "PENDING" || w.Status == "IN_PROGRESS" ||
                 w.Status == "ON_HOLD" || w.Status == "COMPLETED"), cancellationToken) && confirmation is null)
            throw new ValidationException("ConfirmationNote is required while active corrective maintenance work exists.");
        var before = Snapshot(asset);
        var now = DateTime.UtcNow;
        asset.CurrentRoomId = roomId;
        asset.CurrentAreaId = areaId;
        asset.UpdatedAt = now;
        var history = new AssetLocationHistory
        {
            AssetId = id, FromLocationType = currentType, FromLocationId = currentId,
            ToLocationType = targetType, ToLocationId = targetId, EffectiveAt = now,
            MovedBy = actor, Reason = OptionalNote(reason)
        };
        _dbContext.AssetLocationHistories.Add(history);
        await _dbContext.SaveChangesAsync(cancellationToken);
        Audit("assets", id, asset, actor, now, before, Snapshot(asset), history.Reason, confirmation);
        Audit("asset_location_history", history.LocationHistoryId, asset, actor, now, null, new()
        {
            ["asset_id"] = id, ["from_location_type"] = currentType, ["from_location_id"] = currentId,
            ["to_location_type"] = targetType, ["to_location_id"] = targetId,
            ["effective_at"] = now, ["moved_by"] = actor, ["reason"] = history.Reason
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
        var result = await RequireDetailAsync(id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
