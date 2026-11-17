using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed record AssetPlacementInput(long? RoomId, long? AreaId, DateTimeOffset? EffectiveAt, string? Reason = null, string? ConfirmationNote = null);

public sealed class AssetPlacementWriter(HotelOpsDbContext db, InsightAccess access, AssetProfileAccess profile, AssetPlacementCatalog catalog)
{
    public async Task<object> SaveAsync(long id, string? version, AssetPlacementInput input, CancellationToken ct, bool transfer = false)
    {
        if (input.RoomId.HasValue == input.AreaId.HasValue || (input.RoomId ?? input.AreaId) is not > 0)
            throw new InsightException(400, "MSG03", "Select exactly one room or area.");
        if (transfer && input.EffectiveAt != null) throw new InsightException(400, "MSG03", "Transfer uses the server confirmation timestamp.");
        var reason = input.Reason?.Trim(); var confirmation = input.ConfirmationNote?.Trim();
        if (transfer && (reason?.Length ?? 0) < 10 || reason?.Length > 4000 || confirmation?.Length > 4000)
            throw new InsightException(400, "MSG03", "Transfer reason requires 10-4000 characters; confirmation maximum 4000.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var asset = await db.Assets.FromSqlInterpolated($"SELECT * FROM dbo.assets WITH(UPDLOCK,HOLDLOCK) WHERE asset_id={id}")
            .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new InsightException(404, "ASSET_UNAVAILABLE", "Asset unavailable.");
        await catalog.RequireAsync(asset.BranchId, ct, transfer ? "ASSET_TRANSFER" : "ASSET_MANAGE");
        if (!await profile.Query(asset.BranchId).AnyAsync(a => a.AssetId == id, ct))
            throw new InsightException(409, "ASSET_SCOPE_INCONSISTENT", "Asset scope requires correction.");
        var token = await AssetVersion.RequireAsync(db, id, version, ct);
        if (!transfer && (asset.CurrentRoomId != null || asset.CurrentAreaId != null || await db.AssetLocationHistories.AnyAsync(h => h.AssetId == id, ct)))
            throw new InsightException(409, "ASSET_ALREADY_PLACED", "First placement is unavailable; use the transfer workflow.");
        if (transfer)
        {
            await access.BranchesAsync("ASSET_TRANSFER", asset.BranchId, ct);
            if (asset.CurrentRoomId == null && asset.CurrentAreaId == null)
                throw new InsightException(409, "ASSET_NOT_PLACED", "Assign the first location before transferring an asset.");
            if (asset.CurrentRoomId == input.RoomId && asset.CurrentAreaId == input.AreaId)
                throw new InsightException(400, "MSG03", "The target must differ from the current location.");
            var openIncident = await db.Incidents.AnyAsync(i => i.AssetId == id && i.Status != "CLOSED" && i.Status != "CANCELLED", ct);
            var openOccurrence = await db.PreventiveMaintenanceRuns.AnyAsync(r => r.Schedule.AssetId == id
                && (r.MaintenanceWorkId == null && r.PerformedAt == null || r.MaintenanceWork != null && r.MaintenanceWork.Status != "CLOSED" && r.MaintenanceWork.Status != "CANCELLED"), ct);
            if ((openIncident || openOccurrence) && string.IsNullOrWhiteSpace(confirmation))
                throw new InsightException(409, "TRANSFER_CONFIRMATION_REQUIRED", "Linked incident or maintenance occurrence remains open. Enter a confirmation note before transferring.");
            if (await db.AssetHandovers.AnyAsync(h => h.AssetId == id && h.HandoverStatus == "PENDING", ct))
                throw new InsightException(409, "HANDOVER_PENDING", "Resolve the pending custody handover before moving the asset.");
        }
        if (asset.LifecycleStatus is not ("IN_USE" or "UNDER_REPAIR") || asset.CurrentDepartmentId == null
            || !await db.Departments.AnyAsync(d => d.DepartmentId == asset.CurrentDepartmentId && d.BranchId == asset.BranchId && d.IsActive, ct))
            throw new InsightException(409, "ASSET_PLACEMENT_INELIGIBLE", "Active asset and custodian department required.");
        var valid = input.RoomId != null
            ? await db.Rooms.AsNoTracking().AnyAsync(r => r.RoomId == input.RoomId && r.BranchId == asset.BranchId && r.IsActive
                && r.Floor.IsActive && r.Floor.BranchId == asset.BranchId && r.RoomType.IsActive
                && (r.Area == null || r.Area.IsActive && r.Area.BranchId == asset.BranchId && (r.Area.FloorId == null || r.Area.FloorId == r.FloorId)), ct)
            : await db.Areas.AsNoTracking().AnyAsync(a => a.AreaId == input.AreaId && a.BranchId == asset.BranchId && a.IsActive
                && (a.Floor == null || a.Floor.IsActive && a.Floor.BranchId == asset.BranchId), ct);
        if (!valid) throw new InsightException(400, "MSG03", "Location and its hierarchy must be active in the asset branch.");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var effective = input.EffectiveAt?.UtcDateTime ?? now;
        if (effective > now || asset.CreatedAt != null && effective < asset.CreatedAt)
            throw new InsightException(400, "MSG03", "Effective date must not be future or precede asset registration.");
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.assets SET current_room_id={input.RoomId},current_area_id={input.AreaId},updated_at={now}
            WHERE asset_id={id} AND row_version={token}
            """, ct);
        if (changed != 1) throw new InsightException(412, "MSG04", "Asset changed. Reload before saving.");
        var movement = new AssetLocationHistory { AssetId = id, FromLocationType = asset.CurrentRoomId != null ? "ROOM" : asset.CurrentAreaId != null ? "AREA" : null,
            FromLocationId = asset.CurrentRoomId ?? asset.CurrentAreaId, ToLocationType = input.RoomId != null ? "ROOM" : "AREA",
            ToLocationId = (input.RoomId ?? input.AreaId)!.Value, EffectiveAt = effective, MovedBy = access.UserId, Reason = reason };
        db.AssetLocationHistories.Add(movement);
        await db.SaveChangesAsync(ct);
        var after = await db.Assets.AsNoTracking().SingleAsync(a => a.AssetId == id, ct);
        db.AuditLogs.Add(new AuditLog { EntityType = "assets", EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            BranchId = asset.BranchId, ActorUserId = access.UserId, Action = "UPDATE", OccurredAt = now,
            OldValues = JsonSerializer.Serialize(AssetEditService.Snapshot(asset)), NewValues = JsonSerializer.Serialize(AssetEditService.Snapshot(after)),
            ChangedFields = JsonSerializer.Serialize(new[] { "current_room_id", "current_area_id" }) });
        db.AuditLogs.Add(new AuditLog { EntityType = "asset_location_history", EntityId = movement.LocationHistoryId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            BranchId = asset.BranchId, ActorUserId = access.UserId, Action = "CREATE", OccurredAt = now,
            NewValues = JsonSerializer.Serialize(new { asset_id = id, from_location_type = movement.FromLocationType, from_location_id = movement.FromLocationId,
                from_branch_id = asset.BranchId, to_branch_id = asset.BranchId, to_location_type = movement.ToLocationType,
                to_location_id = movement.ToLocationId, effective_at = effective, recorded_at = now, moved_by = access.UserId, reason, confirmation_note = confirmation }) });
        await db.SaveChangesAsync(ct);
        await catalog.RequireAsync(asset.BranchId, ct, transfer ? "ASSET_TRANSFER" : "ASSET_MANAGE");
        if (transfer) await access.BranchesAsync("ASSET_TRANSFER", asset.BranchId, ct);
        var result = new { AssetId = id, Version = await AssetVersion.ReadAsync(db, id, ct), Message = "MSG08" };
        await tx.CommitAsync(ct);
        return result;
    }
}
