using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

internal static class ActivationSqlSuite
{
    internal static async Task RunAsync(string[] args)
    {
        if (args.Length != 2 || !Regex.IsMatch(args[0], @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$")
            || File.Exists(args[1])) throw new ArgumentException("Owned patched trial and NEW local receipt required");
        var connection = new SqlConnectionStringBuilder { DataSource = @"DESKTOP-JI8U50T\SQLEXPRESS", InitialCatalog = args[0],
            IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10 };
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var ct = budget.Token;
        HotelOpsDbContext Open(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(connection.ConnectionString).AddInterceptors(interceptors).Options);
        await using var db = Open();
        await db.Database.OpenConnectionAsync(ct);
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT DB_NAME(),CONVERT(nvarchar(128),@@SERVERNAME),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')";
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct) || reader.GetString(0) != args[0] || reader.GetString(1) != connection.DataSource
                || reader.IsDBNull(2) || reader.GetString(2) != "Task17") throw new InvalidOperationException("Trial guard failed");
        }
        var checks = new List<string>();
        void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException("FAIL: " + label);
            checks.Add(label); Console.WriteLine("PASS: " + label);
        }
        Check(await new StartupSchemaValidator(db).ValidateAsync(ct) == SchemaCompatibility.Compatible, "SQL startup schema compatible");
        var prefix = "T35_" + Guid.NewGuid().ToString("N")[..10];
        var owned = new List<long>();
        var roles = new List<long>();
        var now = DateTime.UtcNow;
        var hasher = new PasswordCredentialService(Options.Create(new PasswordHasherOptions
            { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
        var reason = new UserAccountStatusChange("ACTIVE", "Approved trial activation");
        async Task<User> User(string suffix, string status = "INACTIVE")
        {
            var code = prefix + suffix;
            var row = new User { EmployeeCode = code, Username = code, Email = code + "@example.invalid", FullName = "Status fixture",
                AccountStatus = status, EmploymentStatus = "ACTIVE", CreatedAt = now, LockedUntil = now.AddHours(1) };
            row.PasswordHash = hasher.HashPassword(row, SessionCredential.Generate());
            db.Users.Add(row); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); owned.Add(row.UserId); return row;
        }
        async Task<(SessionReference Reference, long Id)> Session(User user)
        {
            var raw = SessionCredential.Generate();
            var row = new UserSession { UserId = user.UserId, SessionKeyHash = SessionCredential.Hash(raw), CreatedAt = now,
                LastUsedAt = now, ExpiresAt = now.AddMinutes(30) };
            db.Add(row); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            return (SessionReference.FromProtectedTicket(user.UserId, raw, false, now), row.SessionId);
        }
        UserAccountStatusAccess Access(HotelOpsDbContext context, Identity identity)
        {
            var data = new EfPermissionDataSource(context);
            var permission = new PermissionService(identity, data, new UnresolvedPermissionResourceResolver(), TimeProvider.System);
            return new(new UserCredentialResetAccess(context, identity, permission, data, TimeProvider.System));
        }
        UserAccountActivationService Service(HotelOpsDbContext context, Identity identity) => new(context, Access(context, identity), identity,
            new SqlSessionService(context, new SessionRevocationService(context), TimeProvider.System), TimeProvider.System);
        Task<User> Read(long id) => db.Users.AsNoTracking().SingleAsync(u => u.UserId == id, ct);
        async Task<string> Preserved(long id)
        {
            var user = await db.Users.AsNoTracking().Where(u => u.UserId == id).Select(u => new { u.PasswordHash,
                u.TemporaryPasswordExpiresAt, u.FailedLoginCount, u.LockedUntil, u.EmployeeCode, u.Username, u.Email,
                u.FullName, u.Phone, u.AvatarUrl, u.PrimaryBranchId, u.DepartmentId, u.ManagerUserId, u.EmploymentStatus,
                u.LastLoginAt, u.CreatedAt, u.DeletedAt }).SingleAsync(ct);
            var grants = await db.UserRoles.AsNoTracking().Where(r => r.UserId == id).OrderBy(r => r.UserRoleId).ToArrayAsync(ct);
            var memberships = await db.UserBranchAccesses.AsNoTracking().Where(r => r.UserId == id).OrderBy(r => r.AccessId).ToArrayAsync(ct);
            var sessions = await db.UserSessions.AsNoTracking().Where(s => s.UserId == id).OrderBy(s => s.SessionId).ToArrayAsync(ct);
            return JsonSerializer.Serialize(new { user, grants, memberships, sessions }); // Memory only.
        }
        try
        {
            var actor = await User("admin", "ACTIVE"); var actor2 = await User("admin2", "ACTIVE");
            var session = await Session(actor); var session2 = await Session(actor2);
            var identity = new Identity(actor.UserId, session.Id); var identity2 = new Identity(actor2.UserId, session2.Id);
            var adminRole = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "SYS_ADMIN", ct);
            foreach (var id in new[] { actor.UserId, actor2.UserId })
                db.UserRoles.Add(new UserRole { UserId = id, RoleId = adminRole.RoleId, AssignedBy = actor.UserId, AssignedAt = now, IsActive = true });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            var target = await User("activate");
            var oldSession = await Session(target);
            await db.UserSessions.Where(s => s.SessionId == oldSession.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
            var preserved = await Preserved(target.UserId);
            var success = await Service(db, identity).ActivateAsync(session.Reference, session.Id, target.UserId, target.RowVersion, reason, ct);
            Check(success.Status == AccountActivationStatus.Success && success.Version?.Length == 8, "authorized activation returns committed version");
            var saved = await Read(target.UserId);
            Check(saved.AccountStatus == "ACTIVE" && !saved.RowVersion.SequenceEqual(target.RowVersion)
                && saved.RowVersion.SequenceEqual(success.Version!), "activation updates actual SQL status and rowversion");
            Check(await Preserved(target.UserId) == preserved, "activation preserves credential lock roles branch department profile and revoked sessions");
            var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityId == target.UserId.ToString() && a.EntityType == "users", ct);
            Check(audit.ActorUserId == actor.UserId && audit.Action == "UPDATE"
                && audit.OldValues!.Contains("INACTIVE") && audit.NewValues!.Contains(reason.Reason)
                && !audit.NewValues.Contains("password", StringComparison.OrdinalIgnoreCase), "atomic audit records actor target before-after and reason only");
            Check((await Service(db, identity).ActivateAsync(session.Reference, session.Id, target.UserId, target.RowVersion, reason, ct)).Status
                == AccountActivationStatus.Stale, "retry after committed activation rejects old token");
            Check((await Service(db, identity).ActivateAsync(session.Reference, session.Id, target.UserId, saved.RowVersion, reason, ct)).Status
                == AccountActivationStatus.Conflict, "activation no-op conflicts");
            Check((await Service(db, identity).ActivateAsync(session.Reference, session.Id, actor.UserId, actor.RowVersion, reason, ct)).Status
                == AccountActivationStatus.Forbidden, "self-activation denied even with ALL");
            Check((await Service(db, identity).ActivateAsync(session.Reference, session.Id, target.UserId, saved.RowVersion,
                new("INACTIVE", "No unsafe deactivation"), ct)).Status == AccountActivationStatus.DeactivationBlocked, "deactivation cannot execute via primitive");
            var deniedActor = await User("denied", "ACTIVE"); var deniedSession = await Session(deniedActor);
            Check((await Service(db, new(deniedActor.UserId, deniedSession.Id)).ActivateAsync(deniedSession.Reference, deniedSession.Id,
                target.UserId, saved.RowVersion, reason, ct)).Status == AccountActivationStatus.Forbidden, "SQL actor without grant denied");
            Check((await Service(db, identity).ActivateAsync(session2.Reference, session2.Id, target.UserId, saved.RowVersion, reason, ct)).Status
                == AccountActivationStatus.SessionInvalid, "actor cannot borrow another SQL session");
            var deleted = await User("deleted");
            await db.Users.Where(u => u.UserId == deleted.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.DeletedAt, now), ct);
            Check((await Service(db, identity).ActivateAsync(session.Reference, session.Id, deleted.UserId, deleted.RowVersion, reason, ct)).Status
                == AccountActivationStatus.NotFound, "soft-deleted target hidden after authorization");
            Check((await Service(db, identity).ActivateAsync(session.Reference, session.Id, long.MaxValue, saved.RowVersion, reason, ct)).Status
                == AccountActivationStatus.NotFound, "missing target hidden after authorization");
            await db.Users.Where(u => u.UserId == actor.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "INACTIVE"), ct);
            Check((await Service(db, identity).ActivateAsync(session.Reference, session.Id, target.UserId, saved.RowVersion, reason, ct)).Status
                == AccountActivationStatus.SessionInvalid, "inactive actor rejected by real SQL validation");
            await db.Users.Where(u => u.UserId == actor.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "ACTIVE"), ct);
            await db.UserRoles.Where(r => r.UserId == actor.UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
            Check((await Service(db, identity).ActivateAsync(session.Reference, session.Id, target.UserId, saved.RowVersion, reason, ct)).Status
                == AccountActivationStatus.Forbidden, "revoked assignment denied before activation");
            await db.UserRoles.Where(r => r.UserId == actor.UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct);
            foreach (var stage in new[] { "audit", "commit", "after-commit" })
            {
                var faultTarget = await User(stage.Replace("-", ""));
                await using var faultDb = Open(new Fault(stage), new AuditFault(stage));
                var threw = false;
                try { await Service(faultDb, identity).ActivateAsync(session.Reference, session.Id, faultTarget.UserId, faultTarget.RowVersion, reason, ct); }
                catch (Exception error) when (error is InjectedFailure || error.InnerException is InjectedFailure) { threw = true; }
                Check(threw, stage + " injected fault yields no success result");
                var row = await Read(faultTarget.UserId);
                var audits = await db.AuditLogs.CountAsync(a => a.EntityId == faultTarget.UserId.ToString() && a.EntityType == "users", ct);
                Check(stage == "after-commit" ? row.AccountStatus == "ACTIVE" && audits == 1
                    : row.AccountStatus == "INACTIVE" && row.RowVersion.SequenceEqual(faultTarget.RowVersion) && audits == 0,
                    stage + " preserves atomic SQL outcome");
                if (stage == "after-commit")
                    Check((await Service(db, identity).ActivateAsync(session.Reference, session.Id, faultTarget.UserId, faultTarget.RowVersion, reason, ct)).Status
                        == AccountActivationStatus.Stale, "post-commit failure retry cannot repeat transition");
            }
            var race = await User("race");
            await using (var db1 = Open())
            await using (var db2 = Open())
            {
                var results = await Task.WhenAll(Service(db1, identity).ActivateAsync(session.Reference, session.Id, race.UserId, race.RowVersion, reason, ct),
                    Service(db2, identity2).ActivateAsync(session2.Reference, session2.Id, race.UserId, race.RowVersion, reason, ct));
                Check(results.Count(r => r.Status == AccountActivationStatus.Success) == 1 && results.Count(r => r.Status == AccountActivationStatus.Stale) == 1,
                    "concurrent admins same rowversion yield one success and one stale");
            }
            // Temporary role grants exist only inside this owned trial; official catalog is never modified.
            var branches = await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId).Take(2).ToArrayAsync(ct);
            var permissionId = await db.Permissions.Where(p => p.PermissionCode == "USER_MANAGE").Select(p => p.PermissionId).SingleAsync(ct);
            var branchRole = new Role { RoleCode = prefix, RoleName = "Owned UC006 trial", IsActive = true, CreatedAt = now };
            db.Roles.Add(branchRole); await db.SaveChangesAsync(ct); roles.Add(branchRole.RoleId); db.ChangeTracker.Clear();
            db.RolePermissions.Add(new RolePermission { RoleId = branchRole.RoleId, PermissionId = permissionId, DataScope = "BRANCH", GrantedAt = now });
            db.UserRoles.Add(new UserRole { UserId = deniedActor.UserId, RoleId = branchRole.RoleId, BranchId = branches[0].BranchId, IsActive = true, AssignedAt = now });
            foreach (var id in new[] { deniedActor.UserId, target.UserId })
                db.UserBranchAccesses.Add(new UserBranchAccess { UserId = id, BranchId = branches[0].BranchId, GrantedBy = actor.UserId,
                    GrantedAt = now, AccessLevel = "STANDARD", IsPrimary = true });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            var branchIdentity = new Identity(deniedActor.UserId, deniedSession.Id);
            Check(await Access(db, branchIdentity).CanManageAsync(target.UserId, ct), "BRANCH requires actual grant and effective shared membership");
            await db.UserRoles.Where(r => r.UserId == deniedActor.UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.BranchId, branches[1].BranchId), ct);
            Check(!await Access(db, branchIdentity).CanManageAsync(target.UserId, ct), "cross-branch assignment cannot borrow other shared membership");
            await db.UserRoles.Where(r => r.UserId == deniedActor.UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.BranchId, branches[0].BranchId), ct);
            await db.RolePermissions.Where(r => r.RoleId == branchRole.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.DataScope, "OWN"), ct);
            Check(!await Access(db, branchIdentity).CanManageAsync(target.UserId, ct), "OWN rejected");
            await db.UserSessions.Where(s => s.SessionId == session.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.RevokedAt, DateTime.UtcNow), ct);
            var inactive = await User("sessiondenied");
            Check((await Service(db, identity).ActivateAsync(session.Reference, session.Id, inactive.UserId, inactive.RowVersion, reason, ct)).Status
                == AccountActivationStatus.SessionInvalid, "revoked SQL session denies activation");
            await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new { success = true, checks = checks.Count, cases = checks,
                httpEndpointVerified = false, deactivationVerified = false, actualNetworkLossVerified = false, secretsPersisted = false },
                new JsonSerializerOptions { WriteIndented = true }), ct);
            Console.WriteLine($"PASS: {checks.Count} SQL activation/helper assertions; deactivation remains BLOCKED; HTTP certification is separate.");
        }
        finally
        {
            await db.UserSessions.Where(s => owned.Contains(s.UserId) && s.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.RevokedAt, DateTime.UtcNow), CancellationToken.None);
            await db.UserRoles.Where(r => owned.Contains(r.UserId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.RolePermissions.Where(r => roles.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Roles.Where(r => roles.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
        }
    }
    private sealed class Identity(long userId, long sessionId) : ICurrentUserAccessor
    {
        public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(userId, sessionId, false); return true; }
    }
    private sealed class InjectedFailure : IOException;
    private sealed class AuditFault(string stage) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (stage == "audit" && command.CommandText.Contains("INSERT INTO [audit_logs]")) throw new InjectedFailure();
            return ValueTask.FromResult(result);
        }
    }
    private sealed class Fault(string stage) : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData data,
            InterceptionResult result, CancellationToken ct = default)
        {
            if (stage == "commit") throw new InjectedFailure();
            return ValueTask.FromResult(result);
        }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData data, CancellationToken ct = default)
        {
            if (stage == "after-commit") throw new InjectedFailure();
            return Task.CompletedTask;
        }
    }
}
