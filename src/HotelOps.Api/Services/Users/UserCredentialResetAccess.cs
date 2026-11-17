using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

/// <summary>
/// Task 32 permission/scope check only. This is NOT reauthentication proof or
/// employee identity verification. The reset workflow combines all these gates.
/// </summary>
public sealed class UserCredentialResetAccess(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, IPermissionDataSource data, TimeProvider clock)
{
    /// <summary>Retain relevant authorization rows and key ranges until the reset transaction commits.</summary>
    internal async Task LockAuthorityAsync(long actorId, long targetId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null || !db.Database.IsSqlServer())
            throw new InvalidOperationException("Reset authority locks require the caller SQL transaction.");
        // Lock all paths, including inactive/absent ranges, before evaluating them. Ordinary
        // SQL UPDATE/DELETE/INSERT writers conflict with HOLDLOCK without a new writer protocol.
        _ = await db.UserRoles.FromSqlInterpolated($"SELECT * FROM dbo.user_roles WITH (HOLDLOCK) WHERE user_id={actorId}")
            .AsNoTracking().ToListAsync(ct);
        _ = await db.Roles.FromSqlInterpolated($"SELECT * FROM dbo.roles r WITH (HOLDLOCK) WHERE EXISTS (SELECT 1 FROM dbo.user_roles ur WHERE ur.user_id={actorId} AND ur.role_id=r.role_id)")
            .AsNoTracking().ToListAsync(ct);
        _ = await db.RolePermissions.FromSqlInterpolated($"SELECT * FROM dbo.role_permissions rp WITH (HOLDLOCK) WHERE EXISTS (SELECT 1 FROM dbo.user_roles ur WHERE ur.user_id={actorId} AND ur.role_id=rp.role_id)")
            .AsNoTracking().ToListAsync(ct);
        _ = await db.Permissions.FromSqlInterpolated($"SELECT * FROM dbo.permissions WITH (HOLDLOCK) WHERE permission_code={"USER_MANAGE"}")
            .AsNoTracking().ToListAsync(ct);
        foreach (var id in new[] { actorId, targetId }.Order())
            _ = await db.UserBranchAccesses.FromSqlInterpolated($"SELECT * FROM dbo.user_branch_access WITH (HOLDLOCK) WHERE user_id={id}")
                .AsNoTracking().ToListAsync(ct);
        _ = await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches b WITH (HOLDLOCK) WHERE EXISTS (SELECT 1 FROM dbo.user_branch_access a WHERE a.user_id IN ({actorId},{targetId}) AND a.branch_id=b.branch_id) OR EXISTS (SELECT 1 FROM dbo.user_roles ur WHERE ur.user_id={actorId} AND ur.branch_id=b.branch_id)")
            .AsNoTracking().ToListAsync(ct);
    }

    public async Task<bool> CanResetAsync(long targetId, CancellationToken ct)
    {
        if (targetId <= 0 || !current.TryGetCurrentUser(out var actor) || actor.IsRestricted
            || actor.UserId == targetId) return false;
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
        var branches = await db.UserBranchAccesses.AsNoTracking()
            .Where(a => a.UserId == actor.UserId && a.RevokedAt == null && a.GrantedAt <= now && a.Branch.IsActive
                && db.UserBranchAccesses.Any(t => t.UserId == targetId && t.RevokedAt == null && t.GrantedAt <= now
                    && t.BranchId == a.BranchId && t.User.DeletedAt == null))
            .Where(a => globalAdministrator || administratorBranches.Contains(a.BranchId))
            .Select(a => a.BranchId).Distinct().OrderBy(id => id).ToArrayAsync(ct);
        if (globalPermission && branches.Length > 0) return true;
        var evaluator = new PermissionService(current, new BranchOnlyData(data),
            new UnresolvedPermissionResourceResolver(), clock);
        foreach (var branch in branches)
            if ((await evaluator.EvaluateBranchAsync("USER_MANAGE", branch, ct)).IsGranted) return true;
        return false;
    }

    internal async Task<bool> CanInspectDependenciesInBranchAsync(long branchId, CancellationToken ct)
    {
        if (branchId <= 0 || !current.TryGetCurrentUser(out var actor) || actor.IsRestricted) return false;
        var now = clock.GetUtcNow().UtcDateTime;
        var roles = await db.UserRoles.AsNoTracking().Where(r => r.UserId == actor.UserId
            && r.IsActive && r.Role.IsActive && r.Role.RoleCode == "SYS_ADMIN"
            && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now))
            .Select(r => r.BranchId).ToArrayAsync(ct);
        var global = (await permissions.EvaluateGlobalAsync("USER_MANAGE", ct)).IsGranted;
        if (roles.Contains(null) && global) return true;
        if (!roles.Contains(null) && !roles.Contains(branchId)) return false;
        if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == actor.UserId
            && a.BranchId == branchId && a.RevokedAt == null && a.GrantedAt <= now && a.Branch.IsActive, ct)) return false;
        if (global) return true;
        var evaluator = new PermissionService(current, new BranchOnlyData(data),
            new UnresolvedPermissionResourceResolver(), clock);
        return (await evaluator.EvaluateBranchAsync("USER_MANAGE", branchId, ct)).IsGranted;
    }

    private sealed class BranchOnlyData(IPermissionDataSource source) : IPermissionDataSource
    {
        public Task<bool> IsActiveUserAsync(long id, CancellationToken ct) => source.IsActiveUserAsync(id, ct);
        public Task<bool> IsActiveBranchAsync(long id, CancellationToken ct) => source.IsActiveBranchAsync(id, ct);
        public async Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long id, string code, DateTime now, CancellationToken ct)
            => (await source.ReadPathsAsync(id, code, now, ct)).Where(p => p.DataScope == "BRANCH").ToArray();
    }
}
