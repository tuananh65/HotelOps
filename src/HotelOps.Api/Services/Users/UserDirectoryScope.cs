using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

internal sealed record AuthorizedDirectoryScope(long ActorId, bool IsGlobal, long[] BranchIds)
{
    internal bool Covers(AuthorizedDirectoryScope original) => ActorId == original.ActorId
        && (IsGlobal || !original.IsGlobal && original.BranchIds.All(BranchIds.Contains));
}

// Request-local capability established inside the authenticated serializable read boundary.
public sealed class UserDirectoryScopeState
{
    internal AuthorizedDirectoryScope? Current { get; set; }
}

public sealed class UserDirectoryScopeResolver(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionDataSource data, TimeProvider clock)
{
    internal async Task<AuthorizedDirectoryScope?> ResolveAsync(CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.IsRestricted) return null;
        var now = clock.GetUtcNow().UtcDateTime;
        var administratorBranches = await db.UserRoles.AsNoTracking().Where(r => r.UserId == actor.UserId
            && r.Role.RoleCode == "SYS_ADMIN" && r.IsActive && r.Role.IsActive && r.AssignedAt <= now
            && (r.ExpiresAt == null || r.ExpiresAt > now)
            && (r.BranchId == null || r.Branch != null && r.Branch.IsActive
                && db.UserBranchAccesses.Any(b => b.UserId == actor.UserId && b.BranchId == r.BranchId
                    && b.RevokedAt == null && b.GrantedAt <= now)))
            .Select(r => r.BranchId).Distinct().ToArrayAsync(ct);
        if (administratorBranches.Length == 0) return null;
        var paths = (await data.ReadPathsAsync(actor.UserId, "USER_VIEW", now, ct))
            .Where(p => p.PermissionCode == "USER_VIEW" && PermissionGrantRules.IsEligible(p, now)).ToArray();
        var globalGrant = paths.Any(p => PermissionGrantRules.EffectiveScope(p) == "GLOBAL");
        var globalAdministrator = administratorBranches.Contains(null);
        if (globalGrant && globalAdministrator) return new(actor.UserId, true, []);
        var branches = globalGrant ? administratorBranches.Where(b => b.HasValue).Select(b => b!.Value).ToArray()
            : paths.Where(p => PermissionGrantRules.EffectiveScope(p) == "BRANCH")
                .Select(p => p.BranchId!.Value).Distinct().ToArray();
        if (!globalAdministrator) branches = branches.Where(b => administratorBranches.Contains(b)).ToArray();
        return branches.Length == 0 ? null : new(actor.UserId, false, branches);
    }
}
