using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class AssignmentRaceSuite
{
    internal static async Task Run(string connection, Action<bool, string> check, CancellationToken ct)
    {
        HotelOpsDbContext Open(params IInterceptor[] hooks) => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).AddInterceptors(hooks).Options);
        await using var observer = Open();
        var ownedUsers = new List<long>(); var ownedRoles = new List<long>(); var now = DateTime.UtcNow;
        var prefix = "T38R_" + Guid.NewGuid().ToString("N")[..8];
        var branches = await observer.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId).Take(2).ToArrayAsync(ct);
        var manage = await observer.Permissions.AsNoTracking().SingleAsync(p => p.PermissionCode == "USER_MANAGE", ct);
        var hk = await observer.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "HK_STAFF", ct);
        try
        {
            foreach (var kind in new[] { "assignment", "role", "grant", "membership", "session", "role-insert", "role-revoke", "role-first", "revoke-first", "expiry-bootstrap" })
            {
                // Short keys keep fixture employee_code inside its actual40-byte column.
                User User(string suffix) => new() { EmployeeCode = prefix + kind + suffix, Username = prefix + kind + suffix,
                    Email = prefix + kind + suffix + "@example.invalid", FullName = "Assignment SQL race fixture",
                    AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = now, PrimaryBranchId = branches[0].BranchId };
                var actor = User("a"); var target = User("t");
                observer.AddRange(actor, target); await observer.SaveChangesAsync(ct); observer.ChangeTracker.Clear(); ownedUsers.AddRange([actor.UserId, target.UserId]);
                var role = new Role { RoleCode = prefix + kind, RoleName = "Owned Add authority", IsActive = true };
                observer.Add(role); await observer.SaveChangesAsync(ct); observer.ChangeTracker.Clear(); ownedRoles.Add(role.RoleId);
                observer.Add(new RolePermission { RoleId = role.RoleId, PermissionId = manage.PermissionId, DataScope = "BRANCH" });
                foreach (var branch in branches)
                {
                    observer.Add(new UserBranchAccess { UserId = actor.UserId, BranchId = branch.BranchId, AccessLevel = "STANDARD", GrantedAt = now });
                    observer.Add(new UserRole { UserId = actor.UserId, RoleId = role.RoleId, BranchId = branch.BranchId, AssignedAt = now, IsActive = true,
                        ExpiresAt = kind == "expiry-bootstrap" && branch.BranchId == branches[0].BranchId ? now.AddMinutes(1) : null });
                }
                observer.Add(new UserBranchAccess { UserId = target.UserId, BranchId = branches[0].BranchId, AccessLevel = "STANDARD", IsPrimary = true, GrantedAt = now });
                if (kind == "role-revoke") observer.Add(new UserRole { UserId = target.UserId, RoleId = hk.RoleId,
                    BranchId = branches[0].BranchId, AssignedAt = now, IsActive = true });
                var raw = SessionCredential.Generate();
                var session = new UserSession { UserId = actor.UserId, SessionKeyHash = SessionCredential.Hash(raw), CreatedAt = now, LastUsedAt = now, ExpiresAt = now.AddMinutes(20) };
                observer.Add(session); await observer.SaveChangesAsync(ct); observer.ChangeTracker.Clear();
                var ticket = SessionReference.FromProtectedTicket(actor.UserId, raw, false, now);
                var clock = new Clock(now);
                UserBranchAssignmentService Service(HotelOpsDbContext db)
                {
                    var identity = new Identity(actor.UserId, session.SessionId); var data = new EfPermissionDataSource(db);
                    var permission = new PermissionService(identity, data, new UnresolvedPermissionResourceResolver(), clock);
                    return new(db, new(db, identity, permission, data, new(db, identity, permission, data, clock), clock), identity,
                        new SqlSessionService(db, new SessionRevocationService(db), clock), clock);
                }
                if (kind == "expiry-bootstrap")
                {
                    await using var expiringDb = Open(new AdvanceOnUpdate(clock));
                    var result = await Service(expiringDb).AddAsync(ticket, session.SessionId, target.UserId, branches[1].BranchId, target.RowVersion, ct);
                    check(result.Status == AddBranchStatus.Forbidden, "source authority expiring during actual SQL update cannot bootstrap final gate through new destination membership");
                    var version = await observer.Users.Where(u => u.UserId == target.UserId).Select(u => u.RowVersion).SingleAsync(ct);
                    check(version.SequenceEqual(target.RowVersion) && !await observer.UserBranchAccesses.AnyAsync(a => a.UserId == target.UserId && a.BranchId == branches[1].BranchId, ct),
                        "expired source authority rolls back membership aggregate token and audit");
                    continue;
                }
                await using var addDb = Open(); await addDb.Database.OpenConnectionAsync(ct);
                await using var writerDb = Open(); await writerDb.Database.OpenConnectionAsync(ct);
                var addSpid = await addDb.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                var writerSpid = await writerDb.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                async Task<bool> WaitForLock(int waiting, int blocking, Task operation)
                {
                    for (var attempt = 0; attempt < 100 && !operation.IsCompleted; attempt++)
                    {
                        if (await observer.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.dm_exec_requests WHERE session_id={waiting} AND blocking_session_id={blocking} AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct) > 0) return true;
                        await Task.Delay(50, ct);
                    }
                    return false;
                }
                if (kind is "role-first" or "revoke-first")
                {
                    await using var writerTransaction = await writerDb.Database.BeginTransactionAsync(ct);
                    if (kind == "role-first")
                    {
                        writerDb.Add(new UserRole { UserId = target.UserId, RoleId = hk.RoleId, BranchId = branches[1].BranchId, AssignedAt = now, IsActive = true });
                        await writerDb.SaveChangesAsync(ct); writerDb.ChangeTracker.Clear();
                    }
                    else await writerDb.UserBranchAccesses.Where(a => a.UserId == target.UserId).ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, now), ct);
                    var pending = Service(addDb).AddAsync(ticket, session.SessionId, target.UserId, branches[1].BranchId, target.RowVersion, ct);
                    var blocked = await WaitForLock(addSpid, writerSpid, pending);
                    await writerTransaction.CommitAsync(ct);
                    var result = await pending;
                    check(blocked, kind + " Add actually waits on prior SQL writer SPID");
                    check(result.Status == (kind == "role-first" ? AddBranchStatus.RoleConflict : AddBranchStatus.Forbidden)
                        && !await observer.UserBranchAccesses.AnyAsync(a => a.UserId == target.UserId && a.BranchId == branches[1].BranchId, ct),
                        kind + " committed state rejects Add without new membership");
                    continue;
                }
                var pause = new PauseCommit();
                // A separate context is required to install the final-commit interceptor before use.
                await using var heldDb = Open(pause); await heldDb.Database.OpenConnectionAsync(ct);
                var heldSpid = await heldDb.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                var add = Service(heldDb).AddAsync(ticket, session.SessionId, target.UserId, branches[1].BranchId, target.RowVersion, ct);
                await pause.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
                Task writer;
                if (kind == "role-insert")
                {
                    writerDb.Add(new UserRole { UserId = target.UserId, RoleId = hk.RoleId, BranchId = branches[1].BranchId, AssignedAt = now, IsActive = true });
                    writer = writerDb.SaveChangesAsync(ct);
                }
                else writer = kind switch
                {
                    "assignment" => writerDb.UserRoles.Where(r => r.UserId == actor.UserId && r.BranchId == branches[1].BranchId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct),
                    "role" => writerDb.Roles.Where(r => r.RoleId == role.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct),
                    "grant" => writerDb.RolePermissions.Where(r => r.RoleId == role.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.DataScope, "OWN"), ct),
                    "membership" => writerDb.UserBranchAccesses.Where(a => a.UserId == actor.UserId && a.BranchId == branches[1].BranchId).ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, now), ct),
                    "session" => writerDb.UserSessions.Where(s => s.SessionId == session.SessionId).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, now), ct),
                    _ => writerDb.UserRoles.Where(r => r.UserId == target.UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct)
                };
                bool waits;
                try { waits = await WaitForLock(writerSpid, heldSpid, writer); }
                finally { pause.Release.TrySetResult(); }
                var first = await add; await writer;
                check(waits, kind + " SQL writer has LCK_M wait on Add SPID after final gate");
                check(first.Status == AddBranchStatus.Success, kind + " writer serializes after complete authorized Add");
                if (kind is not ("role-insert" or "role-revoke"))
                {
                    await using var next = Open();
                    var result = await Service(next).AddAsync(ticket, session.SessionId, target.UserId, branches[1].BranchId, first.Version!, ct);
                    check(result.Status == (kind == "session" ? AddBranchStatus.SessionInvalid : AddBranchStatus.Forbidden), kind + " committed authority revocation denies next Add");
                }
            }
        }
        finally
        {
            await observer.UserSessions.Where(s => ownedUsers.Contains(s.UserId) && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, DateTime.UtcNow), CancellationToken.None);
            await observer.UserRoles.Where(r => ownedUsers.Contains(r.UserId)).ExecuteDeleteAsync(CancellationToken.None);
            await observer.RolePermissions.Where(r => ownedRoles.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
            await observer.Roles.Where(r => ownedRoles.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
        }
    }
    private sealed class Identity(long userId, long sessionId) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(userId, sessionId, false); return true; } }
    private sealed class Clock(DateTime now) : TimeProvider
    { public DateTimeOffset Value { get; set; } = new(now); public override DateTimeOffset GetUtcNow() => Value; }
    private sealed class AdvanceOnUpdate(Clock clock) : DbCommandInterceptor
    {
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData data, int result, CancellationToken ct = default)
        { if (command.CommandText.Contains("UPDATE [u]")) clock.Value = clock.Value.AddMinutes(2); return ValueTask.FromResult(result); }
    }
    private sealed class PauseCommit : DbTransactionInterceptor
    {
        public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction tx, TransactionEventData data, InterceptionResult result, CancellationToken ct = default)
        { Arrived.TrySetResult(); await Release.Task.WaitAsync(ct); return result; }
    }
}
