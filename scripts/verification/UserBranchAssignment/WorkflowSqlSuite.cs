using System.Data.Common;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class WorkflowSqlSuite
{
    internal static async Task Run(string connection, Action<bool, string> check, CancellationToken ct)
    {
        HotelOpsDbContext Open(params IInterceptor[] hooks) => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).AddInterceptors(hooks).Options);
        await using var db = Open();
        var prefix = "T38W_" + Guid.NewGuid().ToString("N")[..8]; var now = DateTime.UtcNow;
        var owned = new List<long>(); var roles = new List<long>();
        var branches = await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId).Take(2).ToArrayAsync(ct);
        var admin = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "SYS_ADMIN", ct);
        var hk = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "HK_STAFF", ct);
        var om = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "OPERATIONS_MANAGER", ct);
        var manage = await db.Permissions.AsNoTracking().SingleAsync(p => p.PermissionCode == "USER_MANAGE", ct);
        async Task<User> User(string suffix)
        {
            var code = prefix + suffix;
            var user = new User { EmployeeCode = code, Username = code, Email = code + "@example.invalid", FullName = "Assignment workflow fixture",
                AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", PrimaryBranchId = branches[0].BranchId, CreatedAt = now };
            db.Add(user); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); owned.Add(user.UserId);
            db.Add(new UserBranchAccess { UserId = user.UserId, BranchId = branches[0].BranchId, AccessLevel = "STANDARD", IsPrimary = true, GrantedAt = now });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); return user;
        }
        async Task<(SessionReference Reference, long Id)> Session(User user)
        {
            var raw = SessionCredential.Generate();
            var row = new UserSession { UserId = user.UserId, SessionKeyHash = SessionCredential.Hash(raw), CreatedAt = now, LastUsedAt = now, ExpiresAt = now.AddMinutes(30) };
            db.Add(row); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            return (SessionReference.FromProtectedTicket(user.UserId, raw, false, now), row.SessionId);
        }
        UserBranchAssignmentService Service(HotelOpsDbContext context, long actorId, long sessionId, TimeProvider? clock = null)
        {
            clock ??= TimeProvider.System;
            var identity = new Identity(actorId, sessionId); var data = new EfPermissionDataSource(context);
            var permission = new PermissionService(identity, data, new UnresolvedPermissionResourceResolver(), clock);
            var locks = new UserCredentialResetAccess(context, identity, permission, data, clock);
            return new(context, new(context, identity, permission, data, locks, clock), identity,
                new SqlSessionService(context, new SessionRevocationService(context), clock), clock);
        }
        Task<byte[]> Version(long id) => db.Users.Where(u => u.UserId == id).Select(u => u.RowVersion).SingleAsync(ct);
        async Task<string> Preserved(long id)
        {
            var user = await db.Users.AsNoTracking().Where(u => u.UserId == id).Select(u => new { u.PrimaryBranchId, u.DepartmentId,
                u.AccountStatus, u.EmploymentStatus, u.PasswordHash, u.TemporaryPasswordExpiresAt, u.LockedUntil, u.FailedLoginCount,
                u.FullName, u.Email, u.Phone, u.Username, u.EmployeeCode, u.AvatarUrl, u.ManagerUserId, u.CreatedAt, u.DeletedAt, u.LastLoginAt }).SingleAsync(ct);
            var roleRows = await db.UserRoles.AsNoTracking().Where(r => r.UserId == id).OrderBy(r => r.UserRoleId).ToArrayAsync(ct);
            var sessions = await db.UserSessions.AsNoTracking().Where(s => s.UserId == id).OrderBy(s => s.SessionId).ToArrayAsync(ct);
            return JsonSerializer.Serialize(new { user, roleRows, sessions }); // Memory only.
        }
        try
        {
            var actor = await User("admin"); var ticket = await Session(actor);
            db.Add(new UserRole { UserId = actor.UserId, RoleId = admin.RoleId, IsActive = true, AssignedAt = now });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            Task<AddBranchResult> Add(User user) => Service(db, actor.UserId, ticket.Id).AddAsync(ticket.Reference, ticket.Id, user.UserId, branches[1].BranchId, user.RowVersion, ct);
            var target = await User("target"); await Session(target);
            db.Add(new UserRole { UserId = target.UserId, RoleId = hk.RoleId, BranchId = branches[0].BranchId, IsActive = true, AssignedAt = now });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            var before = await Preserved(target.UserId); var primary = await db.UserBranchAccesses.AsNoTracking().SingleAsync(a => a.UserId == target.UserId, ct);
            var success = await Add(target);
            check(success.Status == AddBranchStatus.Success && success.Membership is { AccessLevel: "STANDARD", IsPrimary: false }, "ALL adds only STANDARD nonprimary membership after commit");
            var committedVersion = await Version(target.UserId);
            check(committedVersion.SequenceEqual(success.Version!) && !committedVersion.SequenceEqual(target.RowVersion), "membership transaction advances committed aggregate User.RowVersion");
            check(await Preserved(target.UserId) == before && await db.UserBranchAccesses.CountAsync(a => a.UserId == target.UserId, ct) == 2
                && (await db.UserBranchAccesses.AsNoTracking().SingleAsync(a => a.AccessId == primary.AccessId, ct)).IsPrimary,
                "add preserves roles credentials status primary department and existing sessions/membership");
            var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityType == "user_branch_access" && a.EntityId == success.Membership!.AccessId.ToString(), ct);
            using var auditJson = JsonDocument.Parse(audit.NewValues!);
            check(audit.ActorUserId == actor.UserId && audit.BranchId == branches[1].BranchId && audit.Action == "CREATE"
                && auditJson.RootElement.GetProperty("userId").GetInt64() == target.UserId, "membership audit records backend actor target branch and before-after");
            check((await Add(target)).Status == AddBranchStatus.Stale, "old-token replay after commit is stale without duplicate mutation");
            check((await Service(db, actor.UserId, ticket.Id).AddAsync(ticket.Reference, ticket.Id, target.UserId, branches[1].BranchId, success.Version!, ct)).Status == AddBranchStatus.Duplicate,
                "fresh-token existing membership conflicts");
            await db.UserBranchAccesses.Where(a => a.AccessId == success.Membership!.AccessId).ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, now), ct);
            check((await Service(db, actor.UserId, ticket.Id).AddAsync(ticket.Reference, ticket.Id, target.UserId, branches[1].BranchId, success.Version!, ct)).Status == AddBranchStatus.Duplicate,
                "revoked membership conflicts without regrant");
            check((await Service(db, actor.UserId, ticket.Id).AddAsync(ticket.Reference, ticket.Id, actor.UserId, branches[1].BranchId, await Version(actor.UserId), ct)).Status == AddBranchStatus.Forbidden,
                "self-assignment denied with ALL");
            foreach (var state in new[] { "inactive", "deleted", "latent", "inactiveRole", "expiredRole", "om" })
            {
                var user = await User(state);
                if (state == "inactive") await db.Users.Where(u => u.UserId == user.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "INACTIVE"), ct);
                if (state == "deleted") await db.Users.Where(u => u.UserId == user.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.DeletedAt, now), ct);
                if (state is "latent" or "inactiveRole" or "expiredRole" or "om")
                {
                    db.Add(new UserRole { UserId = user.UserId, RoleId = state == "om" ? om.RoleId : hk.RoleId,
                        BranchId = state == "om" ? branches[0].BranchId : branches[1].BranchId, IsActive = state != "inactiveRole", AssignedAt = now,
                        ExpiresAt = state == "expiredRole" ? now.AddMinutes(-1) : null });
                    await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
                }
                var snapshot = await Preserved(user.UserId); var token = await Version(user.UserId);
                var result = await Service(db, actor.UserId, ticket.Id).AddAsync(ticket.Reference, ticket.Id, user.UserId, branches[1].BranchId, token, ct);
                var expected = state == "inactive" ? AddBranchStatus.Inactive : state == "deleted" ? AddBranchStatus.NotFound
                    : state == "om" ? AddBranchStatus.OperationsManagerConflict : AddBranchStatus.RoleConflict;
                check(result.Status == expected && await Preserved(user.UserId) == snapshot && (await Version(user.UserId)).SequenceEqual(token), state + " denied with no role or aggregate mutation");
            }
            var invalid = await User("invalidBranch");
            check((await Service(db, actor.UserId, ticket.Id).AddAsync(ticket.Reference, ticket.Id, invalid.UserId, long.MaxValue, invalid.RowVersion, ct)).Status == AddBranchStatus.InvalidBranch,
                "nonexistent destination branch rejected");
            await db.Branches.Where(b => b.BranchId == branches[1].BranchId).ExecuteUpdateAsync(s => s.SetProperty(b => b.IsActive, false), ct);
            try { check((await Add(invalid)).Status == AddBranchStatus.InvalidBranch, "inactive destination branch rejected"); }
            finally { await db.Branches.Where(b => b.BranchId == branches[1].BranchId).ExecuteUpdateAsync(s => s.SetProperty(b => b.IsActive, true), CancellationToken.None); }
            var scoped = await User("scoped"); var scopedSession = await Session(scoped);
            var role = new Role { RoleCode = prefix + "scoped", RoleName = "Owned assignment authority", IsActive = true };
            db.Add(role); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); roles.Add(role.RoleId);
            db.Add(new RolePermission { RoleId = role.RoleId, PermissionId = manage.PermissionId, DataScope = "BRANCH" });
            db.Add(new UserRole { UserId = scoped.UserId, RoleId = role.RoleId, BranchId = branches[0].BranchId, IsActive = true, AssignedAt = now });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            var scopedTarget = await User("scopedTarget");
            Task<AddBranchResult> ScopedAdd() => Service(db, scoped.UserId, scopedSession.Id).AddAsync(scopedSession.Reference, scopedSession.Id, scopedTarget.UserId,
                branches[1].BranchId, scopedTarget.RowVersion, ct);
            check((await ScopedAdd()).Status == AddBranchStatus.Forbidden, "managing target at source branch does not authorize destination");
            db.Add(new UserRole { UserId = scoped.UserId, RoleId = role.RoleId, BranchId = branches[1].BranchId, IsActive = true, AssignedAt = now });
            db.Add(new UserBranchAccess { UserId = scoped.UserId, BranchId = branches[1].BranchId, AccessLevel = "STANDARD", GrantedAt = now });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            var scopedSuccess = await ScopedAdd();
            check(scopedSuccess.Status == AddBranchStatus.Success, "BRANCH requires independently eligible source target authority and destination grant");
            var secondTarget = await User("noSource");
            await db.UserRoles.Where(r => r.UserId == scoped.UserId && r.BranchId == branches[0].BranchId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
            check((await Service(db, scoped.UserId, scopedSession.Id).AddAsync(scopedSession.Reference, scopedSession.Id, secondTarget.UserId, branches[1].BranchId, secondTarget.RowVersion, ct)).Status == AddBranchStatus.Forbidden,
                "destination grant alone cannot bootstrap target visibility");
            await db.UserRoles.Where(r => r.UserId == scoped.UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct);
            await db.RolePermissions.Where(r => r.RoleId == role.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.DataScope, "OWN"), ct);
            check((await Service(db, scoped.UserId, scopedSession.Id).AddAsync(scopedSession.Reference, scopedSession.Id, secondTarget.UserId, branches[1].BranchId, secondTarget.RowVersion, ct)).Status == AddBranchStatus.Forbidden, "OWN cannot add membership");
            foreach (var stage in new[] { "audit", "before-commit", "after-commit" })
            {
                var fault = await User(stage.Replace("-", "")); var faultState = await Preserved(fault.UserId);
                await using var faultDb = Open(new Fault(stage), new AuditFault(stage)); var threw = false;
                try { await Service(faultDb, actor.UserId, ticket.Id).AddAsync(ticket.Reference, ticket.Id, fault.UserId, branches[1].BranchId, fault.RowVersion, ct); }
                catch (Exception error) when (error is InjectedFailure || error.InnerException is InjectedFailure) { threw = true; }
                var hasNew = await db.UserBranchAccesses.AnyAsync(a => a.UserId == fault.UserId && a.BranchId == branches[1].BranchId, ct);
                check(threw && (stage == "after-commit" ? hasNew && !(await Version(fault.UserId)).SequenceEqual(fault.RowVersion)
                    : !hasNew && (await Version(fault.UserId)).SequenceEqual(fault.RowVersion)) && await Preserved(fault.UserId) == faultState,
                    stage + " fault preserves complete atomic SQL outcome without business mutation");
                if (stage == "after-commit") check((await Add(fault)).Status == AddBranchStatus.Stale, "postcommit failure retry cannot replay membership result");
            }
            var race = await User("race");
            await using var db1 = Open(); await using var db2 = Open();
            var pair = await Task.WhenAll(Service(db1, actor.UserId, ticket.Id).AddAsync(ticket.Reference, ticket.Id, race.UserId, branches[1].BranchId, race.RowVersion, ct),
                Service(db2, actor.UserId, ticket.Id).AddAsync(ticket.Reference, ticket.Id, race.UserId, branches[1].BranchId, race.RowVersion, ct));
            check(pair.Count(r => r.Status == AddBranchStatus.Success) == 1 && pair.Count(r => r.Status == AddBranchStatus.Stale) == 1, "two SQL Add requests same token yield one success one stale");
        }
        finally
        {
            await db.UserSessions.Where(s => owned.Contains(s.UserId) && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, DateTime.UtcNow), CancellationToken.None);
            await db.UserRoles.Where(r => owned.Contains(r.UserId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.RolePermissions.Where(r => roles.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Roles.Where(r => roles.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
        }
    }
    private sealed class Identity(long userId, long sessionId) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(userId, sessionId, false); return true; } }
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
        { if (stage == "before-commit") throw new InjectedFailure(); return ValueTask.FromResult(result); }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData data, CancellationToken ct = default)
        { if (stage == "after-commit") throw new InjectedFailure(); return Task.CompletedTask; }
    }
}
