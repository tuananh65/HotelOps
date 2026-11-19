using System.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Rooms;

public sealed record RoomWorkspaceBranch(long Id, string Name, bool IsPrimary, bool CanViewRooms,
    bool CanMonitor, bool CanCreate, bool CanReadOwn);
public sealed record RoomWorkspaceResult(RoomDirectoryStatus Status, IReadOnlyList<RoomWorkspaceBranch>? Items = null);

public sealed partial class RoomDirectoryService
{
    public async Task<RoomWorkspaceResult> WorkspaceAsync(SessionReference ticket, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.IsRestricted || ticket.IsRestricted || actor.UserId != ticket.UserId)
            return new(RoomDirectoryStatus.SessionInvalid);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await HasSessionAsync(ticket, actor, ct)) return new(RoomDirectoryStatus.SessionInvalid);
        var primary = await db.Users.AsNoTracking().Where(u => u.UserId == actor.UserId).Select(u => u.PrimaryBranchId).SingleAsync(ct);
        var now = DateTime.UtcNow;
        var branches = await db.UserBranchAccesses.AsNoTracking().Where(a => a.UserId == actor.UserId
            && a.RevokedAt == null && a.GrantedAt <= now && a.Branch.IsActive)
            .Select(a => new { a.BranchId, a.Branch.BranchName }).Distinct().OrderBy(a => a.BranchName).ToArrayAsync(ct);
        async Task<List<RoomWorkspaceBranch>?> Read()
        {
            var at = DateTime.UtcNow;
            var paths = await HousekeepingFeatureAccess.PathsAsync(db, actor.UserId, "CLEANING_VIEW", at, ct);
            var items = new List<RoomWorkspaceBranch>();
            foreach (var b in branches)
            {
                var room = await permissions.EvaluateBranchAsync("ROOM_VIEW", b.BranchId, ct);
                var cleaning = await permissions.EvaluateBranchAsync("CLEANING_VIEW", b.BranchId, ct);
                var create = await permissions.EvaluateBranchAsync("CLEANING_ASSIGN", b.BranchId, ct);
                if (new[] { room, cleaning, create }.Any(p => p.Decision == PermissionDecision.Unavailable)) return null;
                // Read-only monitor for supervisors and operations managers; creation stays supervisor-only.
                var supervisorPaths = await (from r in db.UserRoles.AsNoTracking()
                    join p in db.RolePermissions.AsNoTracking() on r.RoleId equals p.RoleId
                    where r.UserId == actor.UserId && (r.Role.RoleCode == "HK_SUPERVISOR" || r.Role.RoleCode == "OPERATIONS_MANAGER")
                        && p.Permission.PermissionCode == "CLEANING_VIEW" && (r.BranchId == null || r.BranchId == b.BranchId)
                    select new PermissionGrantPath(p.Permission.PermissionCode, p.DataScope, r.BranchId,
                        r.IsActive, r.Role.IsActive, r.AssignedAt, r.ExpiresAt, r.Branch == null || r.Branch.IsActive,
                        db.UserBranchAccesses.Any(a => a.UserId == actor.UserId && a.BranchId == b.BranchId && a.RevokedAt == null && a.GrantedAt <= at)))
                    .ToArrayAsync(ct);
                var supervisor = await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == actor.UserId && r.IsActive && r.Role.IsActive
                    && r.Role.RoleCode == "HK_SUPERVISOR" && (r.BranchId == null || r.BranchId == b.BranchId)
                    && r.AssignedAt <= at && (r.ExpiresAt == null || r.ExpiresAt > at), ct);
                var monitor = cleaning.IsGranted && CleaningMonitorService.SupervisorCanMonitor(supervisorPaths, b.BranchId, at);
                var own = paths.Any(p => PermissionGrantRules.IsEligible(p, at) && (PermissionGrantRules.EffectiveScope(p) == "GLOBAL"
                    || p.BranchId == b.BranchId && PermissionGrantRules.EffectiveScope(p) is "BRANCH" or "OWN"));
                if (room.IsGranted || monitor || own)
                    items.Add(new(b.BranchId, b.BranchName, b.BranchId == primary, room.IsGranted, monitor, create.IsGranted && supervisor, own));
            }
            return items;
        }
        var result = await Read();
        if (result is null) return new(RoomDirectoryStatus.AuthorizationUnavailable);
        if (!await HasSessionAsync(ticket, actor, ct)) return new(RoomDirectoryStatus.SessionInvalid);
        var final = await Read();
        if (final is null) return new(RoomDirectoryStatus.AuthorizationUnavailable);
        await tx.CommitAsync(ct);
        return new(RoomDirectoryStatus.Success, final);
    }
}
