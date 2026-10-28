using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

public sealed class UserBranchAssignmentAccess(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, IPermissionDataSource data, UserCredentialResetAccess authorityLocks, TimeProvider clock)
{
    internal Task LockAuthorityAsync(long actorId, long targetId, CancellationToken ct)
        => authorityLocks.LockAuthorityAsync(actorId, targetId, ct);

    public async Task<bool> CanAddAsync(long targetId, long destinationId, long? insertedAccessId, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.IsRestricted || actor.UserId == targetId) return false;
        if ((await permissions.EvaluateGlobalAsync("USER_MANAGE", ct)).IsGranted) return true;
        var branchOnly = new PermissionService(current, new BranchPaths(data), new UnresolvedPermissionResourceResolver(), clock);
        // Destination authority and pre-existing target visibility are independently required.
        if (!(await branchOnly.EvaluateBranchAsync("USER_MANAGE", destinationId, ct)).IsGranted) return false;
        var now = clock.GetUtcNow().UtcDateTime;
        var common = await db.UserBranchAccesses.AsNoTracking()
            .Where(a => a.UserId == actor.UserId && a.RevokedAt == null && a.GrantedAt <= now && a.Branch.IsActive
                && db.UserBranchAccesses.Any(t => t.UserId == targetId && t.BranchId == a.BranchId
                    && t.RevokedAt == null && t.GrantedAt <= now && t.User.DeletedAt == null
                    && (!insertedAccessId.HasValue || t.AccessId != insertedAccessId.Value)))
            .Select(a => a.BranchId).Distinct().OrderBy(id => id).ToArrayAsync(ct);
        foreach (var branchId in common)
            if ((await branchOnly.EvaluateBranchAsync("USER_MANAGE", branchId, ct)).IsGranted) return true;
        return false;
    }

    private sealed class BranchPaths(IPermissionDataSource source) : IPermissionDataSource
    {
        public Task<bool> IsActiveUserAsync(long id, CancellationToken ct) => source.IsActiveUserAsync(id, ct);
        public Task<bool> IsActiveBranchAsync(long id, CancellationToken ct) => source.IsActiveBranchAsync(id, ct);
        public async Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long id, string code, DateTime now, CancellationToken ct)
            => (await source.ReadPathsAsync(id, code, now, ct)).Where(p => p.DataScope == "BRANCH").ToArray();
    }
}
