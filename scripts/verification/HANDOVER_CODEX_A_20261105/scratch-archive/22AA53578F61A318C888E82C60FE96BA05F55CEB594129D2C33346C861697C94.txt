using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class ActivationRaceSuite
{
    internal static async Task Run(string connection, Action<bool, string> check, CancellationToken ct)
    {
        HotelOpsDbContext Open(params IInterceptor[] hooks) => new(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(connection).AddInterceptors(hooks).Options);
        await using var observer = Open();
        var ownedUsers = new List<long>(); var ownedRoles = new List<long>();
        var now = DateTime.UtcNow;
        var prefix = "T36R_" + Guid.NewGuid().ToString("N")[..8];
        var branch = await observer.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId).FirstAsync(ct);
        var permission = await observer.Permissions.AsNoTracking().SingleAsync(p => p.PermissionCode == "USER_MANAGE", ct);
        try
        {
            foreach (var kind in new[] { "assignment", "role", "grant", "actor-membership", "target-membership", "session", "actor-status" })
            {
                User User(string suffix) => new() { EmployeeCode = prefix + kind + suffix, Username = prefix + kind + suffix,
                    Email = prefix + kind + suffix + "@example.invalid", FullName = "Activation race fixture",
                    AccountStatus = suffix == "a" ? "ACTIVE" : "INACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = now };
                var actor = User("a"); var target = User("t");
                observer.AddRange(actor, target); await observer.SaveChangesAsync(ct); observer.ChangeTracker.Clear();
                ownedUsers.AddRange([actor.UserId, target.UserId]);
                var role = new Role { RoleCode = prefix + kind, RoleName = "Owned activation race role", IsActive = true, IsSystemRole = false };
                observer.Add(role); await observer.SaveChangesAsync(ct); observer.ChangeTracker.Clear(); ownedRoles.Add(role.RoleId);
                observer.RolePermissions.Add(new RolePermission { RoleId = role.RoleId, PermissionId = permission.PermissionId, DataScope = "BRANCH", GrantedAt = now });
                observer.UserRoles.Add(new UserRole { UserId = actor.UserId, RoleId = role.RoleId, BranchId = branch.BranchId, IsActive = true, AssignedAt = now });
                foreach (var user in new[] { actor, target }) observer.UserBranchAccesses.Add(new UserBranchAccess
                    { UserId = user.UserId, BranchId = branch.BranchId, GrantedAt = now, GrantedBy = actor.UserId, IsPrimary = true, AccessLevel = "STANDARD" });
                var raw = SessionCredential.Generate();
                var session = new UserSession { UserId = actor.UserId, SessionKeyHash = SessionCredential.Hash(raw),
                    CreatedAt = now, LastUsedAt = now, ExpiresAt = now.AddMinutes(30) };
                observer.Add(session); await observer.SaveChangesAsync(ct); observer.ChangeTracker.Clear();
                var ticket = SessionReference.FromProtectedTicket(actor.UserId, raw, false, now);
                UserAccountActivationService Workflow(HotelOpsDbContext db)
                {
                    var current = new Identity(actor.UserId, session.SessionId); var data = new EfPermissionDataSource(db);
                    var permissions = new PermissionService(current, data, new UnresolvedPermissionResourceResolver(), TimeProvider.System);
                    return new(db, new(new UserCredentialResetAccess(db, current, permissions, data, TimeProvider.System)), current,
                        new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System), TimeProvider.System);
                }
                var pause = new PauseActivationCommit();
                await using var activationDb = Open(pause); await activationDb.Database.OpenConnectionAsync(ct);
                await using var revokeDb = Open(); await revokeDb.Database.OpenConnectionAsync(ct);
                var activationSpid = await activationDb.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                var revokeSpid = await revokeDb.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                var activation = Workflow(activationDb).ActivateAsync(ticket, session.SessionId, target.UserId, target.RowVersion,
                    new("ACTIVE", "Verified SQL activation race"), ct);
                await pause.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
                Task<int> revoke = kind switch
                {
                    "assignment" => revokeDb.UserRoles.Where(r => r.UserId == actor.UserId && r.RoleId == role.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct),
                    "role" => revokeDb.Roles.Where(r => r.RoleId == role.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct),
                    "grant" => revokeDb.RolePermissions.Where(r => r.RoleId == role.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.DataScope, "OWN"), ct),
                    "actor-membership" => revokeDb.UserBranchAccesses.Where(a => a.UserId == actor.UserId && a.BranchId == branch.BranchId).ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, now), ct),
                    "target-membership" => revokeDb.UserBranchAccesses.Where(a => a.UserId == target.UserId && a.BranchId == branch.BranchId).ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, now), ct),
                    "session" => revokeDb.UserSessions.Where(s => s.SessionId == session.SessionId).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, now), ct),
                    _ => revokeDb.Users.Where(u => u.UserId == actor.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "INACTIVE"), ct)
                };
                var blocked = false;
                try
                {
                    for (var attempt = 0; attempt < 60 && !revoke.IsCompleted; attempt++)
                    {
                        blocked = await observer.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.dm_exec_requests WHERE session_id={revokeSpid} AND blocking_session_id={activationSpid} AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct) > 0;
                        if (blocked) break;
                        await Task.Delay(50, ct);
                    }
                }
                finally { pause.Release.TrySetResult(); }
                var first = await activation; await revoke;
                check(blocked, kind + " SQL writer has LCK_M wait on activation SPID after final gate");
                check(first.Status == AccountActivationStatus.Success, kind + " revocation serializes after authorized activation commit");
                await using var nextDb = Open();
                var next = await Workflow(nextDb).ActivateAsync(ticket, session.SessionId, target.UserId, first.Version!,
                    new("ACTIVE", "Denied after authority revocation"), ct);
                var denied = kind is "session" or "actor-status" ? AccountActivationStatus.SessionInvalid : AccountActivationStatus.Forbidden;
                check(next.Status == denied && next.Version is null, kind + " committed revocation denies next activation before target disclosure");
            }
        }
        finally
        {
            await observer.UserSessions.Where(s => ownedUsers.Contains(s.UserId) && s.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, DateTime.UtcNow), CancellationToken.None);
            await observer.UserRoles.Where(r => ownedRoles.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
            await observer.RolePermissions.Where(r => ownedRoles.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
            await observer.Roles.Where(r => ownedRoles.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
        }
    }
    private sealed class Identity(long userId, long sessionId) : ICurrentUserAccessor
    {
        public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(userId, sessionId, false); return true; }
    }
    private sealed class PauseActivationCommit : DbTransactionInterceptor
    {
        public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction tx, TransactionEventData data,
            InterceptionResult result, CancellationToken ct = default)
        {
            Arrived.TrySetResult(); await Release.Task.WaitAsync(ct); return result;
        }
    }
}
