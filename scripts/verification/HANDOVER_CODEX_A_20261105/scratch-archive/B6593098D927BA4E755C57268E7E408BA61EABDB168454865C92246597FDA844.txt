using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

static class AuthorizationRaceSuite
{
    public static async Task Run(string connection, TimeProvider clock, Action<bool, string> check, CancellationToken ct)
    {
        HotelOpsDbContext Open(params IInterceptor[] hooks) => new(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(connection).AddInterceptors(hooks).Options);
        await using var observer = Open();
        var ownedUsers = new List<long>(); var ownedRoles = new List<long>();
        var now = clock.GetUtcNow().UtcDateTime;
        var prefix = "T34_" + Guid.NewGuid().ToString("N")[..8];
        var hasher = new PasswordCredentialService(Options.Create(new PasswordHasherOptions
            { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
        var branch = await observer.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId).FirstAsync(ct);
        var permission = await observer.Permissions.AsNoTracking().SingleAsync(p => p.PermissionCode == "USER_MANAGE", ct);
        try
        {
            foreach (var kind in new[] { "assignment", "role", "grant", "membership", "session", "credential" })
            {
                var password = SessionCredential.Generate();
                User User(string suffix) => new() { EmployeeCode = prefix + kind + suffix, Username = prefix + kind + suffix,
                    Email = prefix + kind + suffix + "@example.invalid", FullName = "Authorization race fixture",
                    AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = now, PrimaryBranchId = branch.BranchId };
                var actor = User("a"); var target = User("t");
                actor.PasswordHash = hasher.HashPassword(actor, password); target.PasswordHash = hasher.HashPassword(target, password);
                observer.AddRange(actor, target); await observer.SaveChangesAsync(ct); observer.ChangeTracker.Clear();
                ownedUsers.AddRange([actor.UserId, target.UserId]);
                var role = new Role { RoleCode = prefix + kind, RoleName = "Owned race role", IsActive = true, IsSystemRole = false };
                observer.Add(role); await observer.SaveChangesAsync(ct); observer.ChangeTracker.Clear(); ownedRoles.Add(role.RoleId);
                observer.RolePermissions.Add(new RolePermission { RoleId = role.RoleId, PermissionId = permission.PermissionId, DataScope = "BRANCH", GrantedAt = now });
                observer.UserRoles.Add(new UserRole { UserId = actor.UserId, RoleId = role.RoleId, BranchId = branch.BranchId, IsActive = true, AssignedAt = now });
                foreach (var person in new[] { actor, target }) observer.UserBranchAccesses.Add(new UserBranchAccess
                    { UserId = person.UserId, BranchId = branch.BranchId, GrantedAt = now, GrantedBy = actor.UserId, IsPrimary = true, AccessLevel = "STANDARD" });
                var raw = SessionCredential.Generate();
                var session = new UserSession { UserId = actor.UserId, SessionKeyHash = SessionCredential.Hash(raw),
                    CreatedAt = now, LastUsedAt = now, ExpiresAt = now.AddMinutes(30) };
                observer.Add(session); await observer.SaveChangesAsync(ct); observer.ChangeTracker.Clear();
                var ticket = SessionReference.FromProtectedTicket(actor.UserId, raw, false, now);
                var proofs = new ReauthenticationProofStore(clock);
                AdminReauthenticationService Reauth(HotelOpsDbContext db) => new(db,
                    new SqlSessionService(db, new SessionRevocationService(db), clock), new CredentialLockoutService(db, hasher, clock), proofs, clock);
                AdminCredentialResetWorkflow Workflow(HotelOpsDbContext db)
                {
                    var current = new FixedIdentity(actor.UserId, session.SessionId); var data = new EfPermissionDataSource(db);
                    var permissions = new PermissionService(current, data, new UnresolvedPermissionResourceResolver(), clock);
                    return new(new TemporaryCredentialService(db, hasher, new SessionRevocationService(db), clock),
                        new UserCredentialResetAccess(db, current, permissions, data, clock), current, Reauth(db));
                }
                await using (var db = Open())
                    if ((await Reauth(db).ReauthenticateAsync(ticket, session.SessionId, password, ct)).Status != ReauthenticationStatus.Succeeded)
                        throw new InvalidOperationException("Race reauthentication setup failed");
                var pause = new PauseResetCommit();
                await using var resetDb = Open(pause); await resetDb.Database.OpenConnectionAsync(ct);
                await using var revokeDb = Open(); await revokeDb.Database.OpenConnectionAsync(ct);
                var resetSpid = await resetDb.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                var revokeSpid = await revokeDb.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                var reset = Workflow(resetDb).ResetAsync(ticket, session.SessionId, target.UserId, target.RowVersion, ct);
                await pause.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
                Task<int> revoke = kind switch
                {
                    "assignment" => revokeDb.UserRoles.Where(r => r.UserId == actor.UserId && r.RoleId == role.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct),
                    "role" => revokeDb.Roles.Where(r => r.RoleId == role.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct),
                    "grant" => revokeDb.RolePermissions.Where(r => r.RoleId == role.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.DataScope, "OWN"), ct),
                    "membership" => revokeDb.UserBranchAccesses.Where(a => a.UserId == target.UserId && a.BranchId == branch.BranchId).ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, now), ct),
                    "session" => revokeDb.UserSessions.Where(s => s.SessionId == session.SessionId).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, now), ct),
                    _ => revokeDb.Users.Where(u => u.UserId == actor.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, hasher.HashPassword(actor, SessionCredential.Generate())), ct)
                };
                var blocked = false;
                try
                {
                    for (var attempt = 0; attempt < 60 && !revoke.IsCompleted; attempt++)
                    {
                        blocked = await observer.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.dm_exec_requests WHERE session_id={revokeSpid} AND blocking_session_id={resetSpid} AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct) > 0;
                        if (blocked) break;
                        await Task.Delay(50, ct);
                    }
                }
                finally { pause.Release.TrySetResult(); }
                using var first = await reset; await revoke;
                check(blocked, kind + " writer demonstrably waits on reset SPID at final commit boundary");
                check(first.Status == ConditionalCredentialResetStatus.Success, kind + " revocation is serialized after already-authorized reset commit");
                await using var nextDb = Open();
                using var next = await Workflow(nextDb).ResetAsync(ticket, session.SessionId, target.UserId, first.Version!, ct);
                var denied = kind == "session" ? ConditionalCredentialResetStatus.SessionInvalid
                    : kind == "credential" ? ConditionalCredentialResetStatus.ReauthenticationRequired : ConditionalCredentialResetStatus.Forbidden;
                check(next.Status == denied && next.Credential == null, kind + " revoked state denies subsequent reset without stale evidence");
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

    private sealed class PauseResetCommit : DbTransactionInterceptor
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
