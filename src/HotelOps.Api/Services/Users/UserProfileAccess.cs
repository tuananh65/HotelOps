using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

/// <summary>Profile-only scope composition; the foundation remains the grant evaluator.</summary>
public sealed class UserProfileAccess(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, IPermissionDataSource data, TimeProvider clock)
{
    // Profile supports OWN in addition to reset's scopes, but retains the same authority
    // rows/ranges. Reuse only the lock acquisition, never the reset permission decision.
    internal Task LockAuthorityAsync(long actorId, long targetId, CancellationToken ct)
        => new UserCredentialResetAccess(db, current, permissions, data, clock).LockAuthorityAsync(actorId, targetId, ct);

    public async Task<bool> CanManageAsync(long targetId, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.IsRestricted) return false;
        var now = clock.GetUtcNow().UtcDateTime;
        var administratorBranches = await db.UserRoles.AsNoTracking().Where(r => r.UserId == actor.UserId
            && r.Role.RoleCode == "SYS_ADMIN" && r.IsActive && r.Role.IsActive
            && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && (r.BranchId == null || r.Branch != null && r.Branch.IsActive
                && db.UserBranchAccesses.Any(b => b.UserId == actor.UserId && b.BranchId == r.BranchId
                    && b.RevokedAt == null && b.GrantedAt <= now)))
            .Select(r => r.BranchId).Distinct().ToArrayAsync(ct);
        if (administratorBranches.Length == 0) return false;
        var globalAdministrator = administratorBranches.Contains(null);
        var globalPermission = (await permissions.EvaluateGlobalAsync("USER_MANAGE", ct)).IsGranted;
        if (globalAdministrator && globalPermission) return true;
        // Membership is authoritative for both users; PrimaryBranchId is never a substitute.
        var branches = await db.UserBranchAccesses.AsNoTracking()
            .Where(a => a.UserId == actor.UserId && a.RevokedAt == null && a.GrantedAt <= now && a.Branch.IsActive
                && db.UserBranchAccesses.Any(t => t.UserId == targetId && t.RevokedAt == null && t.GrantedAt <= now
                    && t.BranchId == a.BranchId && t.User.DeletedAt == null))
            .Where(a => globalAdministrator || administratorBranches.Contains(a.BranchId))
            .Select(a => a.BranchId).Distinct().OrderBy(id => id).ToArrayAsync(ct);
        if (globalPermission && branches.Length > 0) return true;
        foreach (var branch in branches)
        foreach (var scope in actor.UserId == targetId ? new[] { "BRANCH", "OWN" } : new[] { "BRANCH" })
        {
            // Filter scopes, not identities: no principal substitution, grant fabrication or copied evaluator.
            var evaluator = new PermissionService(current, new ScopeData(data, scope),
                new ProfileResolver(actor.UserId, targetId, branch), clock);
            if ((await evaluator.EvaluateResourceAsync("USER_MANAGE", new("user_profile", targetId), ct)).IsGranted)
                return true;
        }
        return false;
    }

    private sealed class ScopeData(IPermissionDataSource source, string scope) : IPermissionDataSource
    {
        public Task<bool> IsActiveUserAsync(long id, CancellationToken ct) => source.IsActiveUserAsync(id, ct);
        public Task<bool> IsActiveBranchAsync(long id, CancellationToken ct) => source.IsActiveBranchAsync(id, ct);
        public async Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long id, string code, DateTime now, CancellationToken ct)
            => (await source.ReadPathsAsync(id, code, now, ct)).Where(p => p.DataScope == scope).ToArray();
    }

    private sealed class ProfileResolver(long actorId, long targetId, long branchId) : IPermissionResourceResolver
    {
        public Task<VerifiedPermissionResource?> ResolveAsync(string code, PermissionResource resource, long callerId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(code == "USER_MANAGE" && callerId == actorId && resource.ResourceType == "user_profile"
                && resource.ResourceId == targetId ? new VerifiedPermissionResource(branchId, actorId == targetId) : null);
        }
    }
}
