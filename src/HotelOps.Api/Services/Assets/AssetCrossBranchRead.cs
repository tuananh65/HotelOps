using System.Data;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetCrossBranchTransfer
{
    private async Task<bool> MayInitiateAsync(long branch, CancellationToken ct)
    {
        try { await placement.RequireAsync(branch, ct, "ASSET_TRANSFER"); return true; }
        catch (InsightException e) when (e.Status == 403) { return false; }
    }

    public async Task<object> ReadAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await RequireStorageAsync(ct);
        var asset = await AssetAsync(id, ct);
        var canSubmit = await MayInitiateAsync(asset.BranchId, ct);
        var destinations = new List<object>();
        if (canSubmit)
        {
            var branches = await db.Branches.AsNoTracking().Where(b => b.IsActive && b.BranchId != asset.BranchId)
                .OrderBy(b => b.BranchId).Select(b => new { b.BranchId, b.BranchName }).ToArrayAsync(ct);
            foreach (var branch in branches)
            {
                if (!await MayInitiateAsync(branch.BranchId, ct)) continue;
                var rooms = await db.Rooms.AsNoTracking().Where(r => r.BranchId == branch.BranchId && r.IsActive
                    && r.RoomType.IsActive && r.Floor.IsActive && r.Floor.BranchId == branch.BranchId
                    && (r.Area == null || r.Area.IsActive && r.Area.BranchId == branch.BranchId && (r.Area.FloorId == null || r.Area.FloorId == r.FloorId)))
                    .OrderBy(r => r.RoomNumber).Select(r => new { Id = r.RoomId, Name = r.RoomNumber }).ToArrayAsync(ct);
                var areas = await db.Areas.AsNoTracking().Where(a => a.BranchId == branch.BranchId && a.IsActive
                    && (a.Floor == null || a.Floor.IsActive && a.Floor.BranchId == branch.BranchId))
                    .OrderBy(a => a.AreaName).Select(a => new { Id = a.AreaId, Name = a.AreaName }).ToArrayAsync(ct);
                var departments = await db.Departments.AsNoTracking().Where(d => d.BranchId == branch.BranchId && d.IsActive)
                    .Select(d => new { d.DepartmentId, d.DepartmentName }).ToArrayAsync(ct);
                var users = await db.UserRoles.AsNoTracking().Where(r => r.Role.RoleCode == "DEPT_MANAGER" && r.Role.IsActive && r.IsActive
                    && (r.BranchId == null || r.BranchId == branch.BranchId)).Select(r => r.UserId).Distinct().ToArrayAsync(ct);
                var receivers = new List<object>();
                foreach (var department in departments)
                    foreach (var user in users)
                        if (await AssetHandoverAuthority.AllowsAsync(db, user, branch.BranchId, department.DepartmentId, true, ct)
                            && await ReceiverCanReadSourceAsync(user, asset.BranchId, ct))
                            receivers.Add(new { department.DepartmentId, department.DepartmentName, UserId = user,
                                Name = await db.Users.Where(u => u.UserId == user).Select(u => u.FullName).SingleAsync(ct) });
                destinations.Add(new { branch.BranchId, branch.BranchName, Rooms = rooms, Areas = areas, Receivers = receivers });
            }
        }
        var anyPending = await db.AssetHandovers.AnyAsync(h => h.AssetId == id && h.HandoverStatus == "PENDING", ct);
        var candidates = await db.AssetHandovers.AsNoTracking().Where(h => h.AssetId == id && h.HandoverStatus == "PENDING"
            && (h.CreatedBy == access.UserId || h.ToUserId == access.UserId)).ToArrayAsync(ct);
        var pending = new List<object>();
        foreach (var handover in candidates)
        {
            var request = await RequestAsync(handover.HandoverId, ct);
            if (request == null || !Matches(request, handover) || request.SourceBranchId != asset.BranchId
                || !await profile.AllowedAsync(request.DestinationBranchId, ct)) continue;
            var decide = handover.ToUserId == access.UserId && handover.ToDepartmentId is {} department
                && await AssetHandoverAuthority.AllowsAsync(db, access.UserId, request.DestinationBranchId, department, true, ct)
                && await ReceiverCanReadSourceAsync(access.UserId, request.SourceBranchId, ct);
            var cancel = handover.CreatedBy == access.UserId && canSubmit && await MayInitiateAsync(request.DestinationBranchId, ct);
            pending.Add(new { handover.HandoverId, handover.HandoverNo, handover.ConditionNote, handover.CreatedAt,
                request.SourceBranchId, request.DestinationBranchId, request.TargetRoomId, request.TargetAreaId,
                request.Reason, request.ConfirmationNote, handover.ToDepartmentId, handover.ToUserId, CanDecide = decide, CanCancel = cancel });
        }
        var result = new { asset.AssetId, asset.AssetCode, asset.AssetName, asset.BranchId, asset.CurrentRoomId, asset.CurrentAreaId,
            Version = await AssetVersion.ReadAsync(db, id, ct), HasOpenWork = await HasOpenWorkAsync(id, ct),
            CanSubmit = canSubmit && !anyPending, HasPendingHandover = anyPending, Destinations = destinations, Pending = pending };
        await profile.RequireAsync(asset.BranchId, ct);
        if (canSubmit && !await MayInitiateAsync(asset.BranchId, ct)) throw new InsightException(403, "ACCESS_DENIED", "Transfer authority changed.");
        await tx.CommitAsync(ct); return result;
    }
}
