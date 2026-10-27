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
        if ((await permissions.EvaluateGlobalAsync("USER_MANAGE", ct)).IsGranted) return true;
        var now = clock.GetUtcNow().UtcDateTime;
        var branches = await db.UserBranchAccesses.AsNoTracking()
            .Where(a => a.UserId == actor.UserId && a.RevokedAt == null && a.GrantedAt <= now && a.Branch.IsActive
                && db.UserBranchAccesses.Any(t => t.UserId == targetId && t.RevokedAt == null && t.GrantedAt <= now
                    && t.BranchId == a.BranchId && t.User.DeletedAt == null))
            .Select(a => a.BranchId).Distinct().OrderBy(id => id).ToArrayAsync(ct);
        var evaluator = new PermissionService(current, new BranchOnlyData(data),
            new UnresolvedPermissionResourceResolver(), clock);
        foreach (var branch in branches)
            if ((await evaluator.EvaluateBranchAsync("USER_MANAGE", branch, ct)).IsGranted) return true;
        return false;
    }

    private sealed class BranchOnlyData(IPermissionDataSource source) : IPermissionDataSource
    {
        public Task<bool> IsActiveUserAsync(long id, CancellationToken ct) => source.IsActiveUserAsync(id, ct);
        public Task<bool> IsActiveBranchAsync(long id, CancellationToken ct) => source.IsActiveBranchAsync(id, ct);
        public async Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long id, string code, DateTime now, CancellationToken ct)
            => (await source.ReadPathsAsync(id, code, now, ct)).Where(p => p.DataScope == "BRANCH").ToArray();
    }
}
