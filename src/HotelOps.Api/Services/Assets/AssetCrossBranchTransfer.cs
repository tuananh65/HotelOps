using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed record AssetCrossBranchInput(long BranchId, long? RoomId, long? AreaId, long DepartmentId,
    long ReceiverId, string Reason, string ConditionNote, string? ConfirmationNote);

public sealed partial class AssetCrossBranchTransfer(HotelOpsDbContext db, InsightAccess access, AssetProfileAccess profile, AssetPlacementCatalog placement)
{
    private async Task RequireStorageAsync(CancellationToken ct)
    {
        if (!await AssetTransferStorage.InstalledAsync(db, ct)) throw new InsightException(503, "ASSET_TRANSFER_STORAGE_REQUIRED", "Install the guarded asset transfer schema before cross-branch handover.");
    }
    private async Task<Asset> AssetAsync(long id, CancellationToken ct)
    {
        var asset = await db.Assets.FromSqlInterpolated($"SELECT * FROM dbo.assets WITH(UPDLOCK,HOLDLOCK) WHERE asset_id={id}").AsNoTracking().SingleOrDefaultAsync(ct)
            ?? throw new InsightException(404, "ASSET_UNAVAILABLE", "Asset unavailable.");
        await profile.RequireAsync(asset.BranchId, ct);
        if (!await profile.Query(asset.BranchId).AnyAsync(a => a.AssetId == id, ct) || asset.LifecycleStatus is not ("IN_USE" or "UNDER_REPAIR")
            || asset.CurrentDepartmentId == null || asset.CurrentRoomId == null && asset.CurrentAreaId == null)
            throw new InsightException(409, "ASSET_TRANSFER_INELIGIBLE", "An active asset with consistent placement and custody is required.");
        return asset;
    }
    private async Task TargetAsync(long branch, long? room, long? area, CancellationToken ct)
    {
        if (room.HasValue == area.HasValue) throw new InsightException(400, "MSG03", "Select one target room or area.");
        var valid = room != null ? await db.Rooms.AnyAsync(r => r.RoomId == room && r.BranchId == branch && r.IsActive && r.RoomType.IsActive
            && r.Floor.IsActive && r.Floor.BranchId == branch && (r.Area == null || r.Area.IsActive && r.Area.BranchId == branch && (r.Area.FloorId == null || r.Area.FloorId == r.FloorId)), ct)
            : await db.Areas.AnyAsync(a => a.AreaId == area && a.BranchId == branch && a.IsActive && (a.Floor == null || a.Floor.IsActive && a.Floor.BranchId == branch), ct);
        if (!valid) throw new InsightException(400, "MSG03", "Target location hierarchy must be active in the destination branch.");
    }
    private async Task<bool> HasOpenWorkAsync(long id, CancellationToken ct) =>
        await db.Incidents.AnyAsync(i => i.AssetId == id && i.Status != "CLOSED" && i.Status != "CANCELLED", ct)
        || await db.PreventiveMaintenanceRuns.AnyAsync(r => r.Schedule.AssetId == id && (r.MaintenanceWorkId == null && r.PerformedAt == null
            || r.MaintenanceWork != null && r.MaintenanceWork.Status != "CLOSED" && r.MaintenanceWork.Status != "CANCELLED"), ct);

    public async Task<object> SubmitAsync(long id, string? expected, AssetCrossBranchInput input, CancellationToken ct)
    {
        var reason = input.Reason?.Trim(); var condition = input.ConditionNote?.Trim(); var confirmation = input.ConfirmationNote?.Trim();
        if (reason?.Length is not >= 10 || reason.Length > 4000 || string.IsNullOrWhiteSpace(condition) || condition.Length > 4000
            || confirmation?.Length > 4000 || input.ReceiverId <= 0 || input.DepartmentId <= 0 || input.BranchId <= 0)
            throw new InsightException(400, "MSG03", "Destination, receiving Head, condition and reason (10-4000 characters) required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId, input.ReceiverId], ct);
        await RequireStorageAsync(ct); var asset = await AssetAsync(id, ct);
        await placement.RequireAsync(asset.BranchId, ct, "ASSET_TRANSFER");
        await placement.RequireAsync(input.BranchId, ct, "ASSET_TRANSFER");
        if (input.BranchId == asset.BranchId) throw new InsightException(400, "MSG03", "Use the current-branch transfer action.");
        var token = await AssetVersion.RequireAsync(db, id, expected, ct);
        await TargetAsync(input.BranchId, input.RoomId, input.AreaId, ct);
        if (!await AssetHandoverAuthority.AllowsAsync(db, input.ReceiverId, input.BranchId, input.DepartmentId, true, ct)
            || !await ReceiverCanReadSourceAsync(input.ReceiverId, asset.BranchId, ct))
            throw new InsightException(403, "ACCESS_DENIED", "Receiving Head requires current mapped custody authority and source asset read access.");
        var open = await HasOpenWorkAsync(id, ct);
        if (open && string.IsNullOrWhiteSpace(confirmation)) throw new InsightException(400, "SOURCE_WORK_CONFIRMATION_REQUIRED", "Confirm that existing open work remains the responsibility of its source branch.");
        if (await db.AssetHandovers.AnyAsync(h => h.AssetId == id && h.HandoverStatus == "PENDING", ct)) throw new InsightException(409, "HANDOVER_PENDING", "Resolve the pending handover first.");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var handover = new AssetHandover { AssetId = id, HandoverNo = "HO-" + Guid.NewGuid().ToString("N"), FromDepartmentId = asset.CurrentDepartmentId,
            FromUserId = asset.CustodianUserId, ToDepartmentId = input.DepartmentId, ToUserId = input.ReceiverId,
            HandoverStatus = "PENDING", ConditionNote = condition, CreatedBy = access.UserId, CreatedAt = now, HandedOverAt = now };
        db.AssetHandovers.Add(handover); await db.SaveChangesAsync(ct);
        var snapshot = JsonSerializer.Serialize(new { handover_id = handover.HandoverId, handover_no = handover.HandoverNo, asset_id = id,
            branch_id = asset.BranchId, from_branch_id = asset.BranchId, to_branch_id = input.BranchId, from_department_id = asset.CurrentDepartmentId,
            to_department_id = input.DepartmentId, from_user_id = asset.CustodianUserId, to_user_id = input.ReceiverId, created_by = access.UserId,
            created_at = now, handed_over_at = now, accepted_at = (DateTime?)null, handover_status = "PENDING", condition_note = condition,
            source_room_id = asset.CurrentRoomId, source_area_id = asset.CurrentAreaId,
            target_room_id = input.RoomId, target_area_id = input.AreaId,
            reason, confirmation_note = confirmation, source_work_remains_at_origin = true, notification_kind = "ASSET_HANDOVER_SUBMITTED", notification_version = 1 });
        var audit = new AuditLog { EntityType = "asset_handovers", EntityId = handover.HandoverId.ToString(), BranchId = asset.BranchId,
            ActorUserId = access.UserId, Action = "CREATE", OccurredAt = now, NewValues = snapshot };
        db.AuditLogs.Add(audit); await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.asset_handover_versions(handover_id,previous_handover_id,submission_audit_id,snapshot_json)
            VALUES({handover.HandoverId},NULL,{audit.AuditId},{snapshot});
            INSERT dbo.asset_transfer_requests(handover_id,source_branch_id,destination_branch_id,source_room_id,source_area_id,target_room_id,target_area_id,reason,confirmation_note)
            VALUES({handover.HandoverId},{asset.BranchId},{input.BranchId},{asset.CurrentRoomId},{asset.CurrentAreaId},{input.RoomId},{input.AreaId},{reason},{confirmation});
            """, ct);
        if (await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.assets SET updated_at={now} WHERE asset_id={id} AND row_version={token}", ct) != 1)
            throw new InsightException(412, "MSG04", "Asset changed. Reload before submitting.");
        await placement.RequireAsync(asset.BranchId, ct, "ASSET_TRANSFER"); await placement.RequireAsync(input.BranchId, ct, "ASSET_TRANSFER");
        if (!await AssetHandoverAuthority.AllowsAsync(db, input.ReceiverId, input.BranchId, input.DepartmentId, true, ct)
            || !await ReceiverCanReadSourceAsync(input.ReceiverId, asset.BranchId, ct))
            throw new InsightException(403, "ACCESS_DENIED", "Receiving authority changed before commit.");
        await tx.CommitAsync(ct); return new { handover.HandoverId, State = "PENDING_ACKNOWLEDGMENT" };
    }

    private async Task<bool> ReceiverCanReadSourceAsync(long user, long branch, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var source = new HotelOps.Api.Security.Authorization.EfPermissionDataSource(db);
        if (!await source.IsActiveUserAsync(user, ct) || !await db.UserBranchAccesses.AnyAsync(a => a.UserId == user && a.BranchId == branch
            && a.Branch.IsActive && a.RevokedAt == null && a.GrantedAt <= now, ct)) return false;
        return (await source.ReadPathsAsync(user, "ASSET_VIEW", now, ct)).Any(p => HotelOps.Api.Security.Authorization.PermissionGrantRules.IsEligible(p, now)
            && (HotelOps.Api.Security.Authorization.PermissionGrantRules.EffectiveScope(p) == "GLOBAL"
                || p.BranchId == branch && HotelOps.Api.Security.Authorization.PermissionGrantRules.EffectiveScope(p) == "BRANCH"));
    }
}
