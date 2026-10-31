using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class ServiceSqlValidation
{
    internal sealed record LockWait(int WaiterSpid, int OwnerSpid, string WaitType, DateTime ObservedAtUtc);
    internal static List<LockWait> ObservedWaits { get; } = [];
    private static bool IsDeadlock(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is SqlException sql && sql.Number == 1205) return true;
        return false;
    }
    internal static async Task RunAsync(string connection, long actorId, long sessionId, SessionReference ticket,
        long targetId, Action<bool, string> check, CancellationToken ct)
    {
        HotelOpsDbContext Open(params IInterceptor[] hooks) => new(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(connection).AddInterceptors(hooks).Options);
        UserAccountDeactivationService Service(HotelOpsDbContext db, IPermissionDataSource? paths = null)
        {
            var current = new Identity(actorId, sessionId); var data = paths ?? new EfPermissionDataSource(db);
            var permissions = new PermissionService(current, data, new UnresolvedPermissionResourceResolver(), TimeProvider.System);
            return new(db, new(new(db, current, permissions, data, TimeProvider.System)), current,
                new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System), TimeProvider.System);
        }
        await using var observer = Open();
        var expected = await observer.Users.AsNoTracking().Where(u => u.UserId == targetId).Select(u => u.RowVersion).SingleAsync(ct);
        var request = new UserAccountDeactivationRequest("Task52 guarded validation reason");
        Task<AccountDeactivationResult> Run(HotelOpsDbContext db, CancellationToken token) =>
            Service(db).DeactivateAsync(ticket, sessionId, targetId, expected, request, token);
        async Task<string> State() => JsonSerializer.Serialize(new
        {
            user = await observer.Users.AsNoTracking().SingleAsync(u => u.UserId == targetId, ct),
            sessions = await observer.UserSessions.AsNoTracking().Where(s => s.UserId == targetId).OrderBy(s => s.SessionId).ToArrayAsync(ct),
            roles = await observer.UserRoles.AsNoTracking().Where(s => s.UserId == targetId).OrderBy(s => s.UserRoleId).ToArrayAsync(ct),
            memberships = await observer.UserBranchAccesses.AsNoTracking().Where(s => s.UserId == targetId).OrderBy(s => s.AccessId).ToArrayAsync(ct),
            audits = await observer.AuditLogs.AsNoTracking().Where(a => a.EntityType == "users" && a.EntityId == targetId.ToString()).CountAsync(ct)
        }); // Compared only in memory; never emit credential/session material.
        var before = await State();
        foreach (var size in new[] { 0, 7, 9 })
        {
            await using var db = Open(); var rejected = false;
            try { await Service(db).DeactivateAsync(ticket, sessionId, targetId, new byte[size], request, ct); }
            catch (ArgumentException) { rejected = true; }
            check(rejected && db.Database.CurrentTransaction is null, "missing/malformed internal version rejected before transaction: " + size);
        }
        await using (var db = Open())
        {
            var rejected = false;
            try { await Service(db).DeactivateAsync(ticket, sessionId, targetId, expected, new(" short "), ct); }
            catch (ArgumentException) { rejected = true; }
            check(rejected && db.Database.CurrentTransaction is null, "direct invalid reason rejected before transaction");
            await using var owned = await db.Database.BeginTransactionAsync(ct);
            rejected = false;
            try { await Run(db, ct); } catch (InvalidOperationException) { rejected = true; }
            check(rejected && db.Database.CurrentTransaction == owned, "service refuses caller-owned transaction without disposing it");
            await owned.RollbackAsync(ct);
        }
        await using (var db = Open())
        {
            _ = await db.Users.SingleAsync(u => u.UserId == targetId, ct);
            var rejected = false;
            try { await Run(db, ct); } catch (InvalidOperationException) { rejected = true; }
            check(rejected && db.Database.CurrentTransaction is null, "tracked context rejected without transaction or writes");
        }
        await using (var db = Open())
        {
            await db.Database.OpenConnectionAsync(ct);
            check((await Run(db, ct)).Status == AccountDeactivationStatus.ResponsibilitiesUnresolved
                && db.Database.GetDbConnection().State == ConnectionState.Open && db.Database.CurrentTransaction is null,
                "denial preserves caller-open connection and releases owned transaction");
        }
        await using (var db = Open())
        {
            await Run(db, ct);
            check(db.Database.GetDbConnection().State == ConnectionState.Closed, "service closes only its own connection lease");
        }
        await using (var db = Open(new FailResponsibilityRead()))
        {
            var failed = false;
            try { await Run(db, ct); } catch (InjectedReadFailure) { failed = true; }
            check(failed && db.Database.CurrentTransaction is null && !db.ChangeTracker.Entries().Any(),
                "actual SQL responsibility query failure propagates and rolls back cleanly");
        }
        // Explicit SQL fixture manager verifies a known blocker is not hidden by an empty result.
        // Unresolved still takes precedence in the public service result.
        var branch = await observer.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId).Select(b => b.BranchId).FirstAsync(ct);
        var memberships = new[] { actorId, targetId }.Select(id => new UserBranchAccess
        {
            UserId = id, BranchId = branch, AccessLevel = "STANDARD", GrantedAt = DateTime.UtcNow.AddMinutes(-1)
        }).ToArray();
        observer.AddRange(memberships); await observer.SaveChangesAsync(ct); observer.ChangeTracker.Clear();
        try
        {
            foreach (var scope in new[] { "BRANCH", "OWN" })
            {
                await using var db = Open();
                var data = new ScopedPaths(new EfPermissionDataSource(db), branch, scope);
                var result = await Service(db, data).DeactivateAsync(ticket, sessionId, targetId, expected, request, ct);
                check(result.Status == (scope == "BRANCH" ? AccountDeactivationStatus.ResponsibilitiesUnresolved : AccountDeactivationStatus.Forbidden),
                    "synthetic " + scope + " grant with real SQL session/memberships obeys scope and never bypasses responsibility gate");
            }
            await observer.UserBranchAccesses.Where(a => a.AccessId == memberships[1].AccessId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, DateTime.UtcNow), ct);
            await using var deniedDb = Open();
            var denied = await Service(deniedDb, new ScopedPaths(new EfPermissionDataSource(deniedDb), branch, "BRANCH"))
                .DeactivateAsync(ticket, sessionId, targetId, expected, request, ct);
            check(denied.Status == AccountDeactivationStatus.Forbidden, "synthetic BRANCH grant cannot manage target outside effective SQL membership scope");
        }
        finally
        {
            var ids = memberships.Select(a => a.AccessId).ToArray();
            await observer.UserBranchAccesses.Where(a => ids.Contains(a.AccessId)).ExecuteDeleteAsync(CancellationToken.None);
        }
        var code = "T52_" + Guid.NewGuid().ToString("N")[..10];
        var department = new Department { BranchId = branch, DepartmentCode = code, DepartmentName = "Owned Task52 fixture",
            ManagerUserId = targetId, IsActive = true };
        observer.Add(department); await observer.SaveChangesAsync(ct); observer.ChangeTracker.Clear();
        try
        {
            await using var db = Open(); await using var tx = await db.Database.BeginTransactionAsync(ct);
            await new UserEligibilityLock(db).LockAsync([targetId], ct);
            var type = typeof(UserAccountDeactivationService).Assembly.GetType("HotelOps.Api.Services.Users.IntegratedDeactivationResponsibilityQueries", true)!;
            var pending = (Task)type.GetMethod("ReadAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Activator.CreateInstance(type, db), [targetId, ct])!;
            await pending;
            var review = pending.GetType().GetProperty("Result")!.GetValue(pending)!;
            var blockers = (IReadOnlyList<string>)review.GetType().GetProperty("Blockers")!.GetValue(review)!;
            var unresolved = (IReadOnlyList<string>)review.GetType().GetProperty("Unresolved")!.GetValue(review)!;
            check(blockers.Contains("DEPARTMENT_MANAGER") && unresolved.Count > 0, "real SQL integrated review retains known blocker AND unresolved gates");
            await tx.RollbackAsync(ct);
            await using var serviceDb = Open();
            check((await Run(serviceDb, ct)).Status == AccountDeactivationStatus.ResponsibilitiesUnresolved,
                "service denies known manager responsibility without masking incomplete coverage");
        }
        finally { await observer.Departments.Where(d => d.DepartmentId == department.DepartmentId && d.DepartmentCode == code).ExecuteDeleteAsync(CancellationToken.None); }

        async Task<int> Spid(HotelOpsDbContext db) => await db.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
        async Task<bool> Wait(int waiter, int owner, Task pending)
        {
            for (var i = 0; i < 100 && !pending.IsCompleted; i++)
            {
                var wait = await observer.Database.SqlQuery<string>($"SELECT wait_type AS [Value] FROM sys.dm_exec_requests WHERE session_id={waiter} AND blocking_session_id={owner} AND wait_type LIKE 'LCK_M_%'").SingleOrDefaultAsync(ct);
                if (wait is not null)
                {
                    ObservedWaits.Add(new(waiter, owner, wait, DateTime.UtcNow));
                    return true;
                }
                await Task.Delay(50, ct);
            }
            return false;
        }
        foreach (var cancel in new[] { true, false })
        {
            await using var holder = Open(); await using var tx = await holder.Database.BeginTransactionAsync(ct);
            await new GlobalAdminSerialization(holder, TimeProvider.System).AcquireAsync(ct: ct);
            var owner = await Spid(holder);
            await using var db = Open(); await db.Database.OpenConnectionAsync(ct); var waiter = await Spid(db);
            using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var pending = Run(db, cancelled.Token);
            try
            {
                check(await Wait(waiter, owner, pending), cancel ? "cancellable Deactivate has real SQL LCK_M wait on global guard" : "timeout Deactivate has real SQL LCK_M wait on global guard");
                if (cancel)
                {
                    cancelled.Cancel(); var rejected = false;
                    try { await pending; } catch (OperationCanceledException) { rejected = true; }
                    check(rejected, "cancellation propagates instead of returning clear responsibility");
                }
                else check((await pending).Status == AccountDeactivationStatus.SerializationUnavailable, "global lock timeout fails closed");
                check(db.Database.CurrentTransaction is null && db.Database.GetDbConnection().State == ConnectionState.Open,
                    "cancel/timeout releases transaction while preserving caller connection");
            }
            finally { cancelled.Cancel(); try { await pending; } catch (OperationCanceledException) { } await tx.RollbackAsync(CancellationToken.None); }
        }
        // Deliberately inverted test-only writer: User U lock -> global guard. This is not
        // a production writer or proof of ReturnWork safety. DMV proves the service wait.
        await using (var writer = Open())
        await using (var db = Open())
        {
            await using var tx = await writer.Database.BeginTransactionAsync(ct);
            await writer.Database.ExecuteSqlRawAsync("SET DEADLOCK_PRIORITY HIGH", ct);
            await new UserEligibilityLock(writer).LockAsync([targetId], ct);
            var owner = await Spid(writer);
            await db.Database.OpenConnectionAsync(ct); var waiter = await Spid(db);
            await db.Database.ExecuteSqlRawAsync("SET DEADLOCK_PRIORITY LOW", ct);
            var pending = Run(db, ct);
            try
            {
                check(await Wait(waiter, owner, pending), "Deactivate owns global guard and waits on retained User lock in actual SQL");
                await new GlobalAdminSerialization(writer, TimeProvider.System).AcquireAsync(15000, ct);
                var deadlocked = false;
                try { await pending; } catch (Exception e) when (IsDeadlock(e)) { deadlocked = true; }
                check(deadlocked && db.Database.CurrentTransaction is null, "actual SQL deadlock victim propagates provider-wrapped 1205 and releases service transaction");
            }
            finally { await tx.RollbackAsync(CancellationToken.None); try { await pending; } catch (Exception e) when (IsDeadlock(e)) { } }
        }
        await using (var retry = Open())
            check((await Run(retry, ct)).Status == AccountDeactivationStatus.ResponsibilitiesUnresolved,
                "fresh explicit retry after faults still fails closed without bypassing predicates");
        check(await State() == before, "all SQL validation/fault paths preserve target credentials roles memberships sessions rowversion and audit");
    }

    private sealed class InjectedReadFailure : Exception;
    // Catalog has USER_MANAGE/ALL only. These are explicitly composition tests, not
    // claims that SQL BRANCH/OWN grants exist; catalog rows are never changed.
    private sealed class ScopedPaths(IPermissionDataSource source, long branch, string scope) : IPermissionDataSource
    {
        public Task<bool> IsActiveUserAsync(long id, CancellationToken ct) => source.IsActiveUserAsync(id, ct);
        public Task<bool> IsActiveBranchAsync(long id, CancellationToken ct) => source.IsActiveBranchAsync(id, ct);
        public Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long id, string code, DateTime now, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PermissionGrantPath>>([new(code, scope, branch, true, true, now.AddMinutes(-1), null, true, true)]);
    }
    private sealed class FailResponsibilityRead : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("cleaning_assignments", StringComparison.OrdinalIgnoreCase)) throw new InjectedReadFailure();
            return ValueTask.FromResult(result);
        }
    }
}
