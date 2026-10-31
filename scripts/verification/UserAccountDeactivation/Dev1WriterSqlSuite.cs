using System.Data.Common;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Existing account-state writer acceptance. No missing business writer is simulated.
internal static class Dev1WriterSqlSuite
{
    internal sealed record SqlWait(string Case, int Waiter, int Owner, string WaitType, DateTime ObservedUtc);
    internal sealed record Evidence(IReadOnlyList<string> Cases, IReadOnlyList<SqlWait> Waits,
        string LastAdminFoundation, string MissingBusinessWriterRaces);

    internal static async Task<Evidence> RunAsync(string connection, long actor, long target, long branch,
        Func<HotelOpsDbContext, long, byte[], CancellationToken, Task<AccountActivationResult>> activate,
        Action<bool, string> report, CancellationToken ct)
    {
        var cases = new List<string>(); var waits = new List<SqlWait>(); var owned = new List<long>();
        void Check(bool ok, string label) { report(ok, label); cases.Add(label); }
        HotelOpsDbContext Open(params IInterceptor[] hooks) => new(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(connection).AddInterceptors(hooks).Options);
        await using var observer = Open(); var prefix = "T55BW_" + Guid.NewGuid().ToString("N")[..10];
        async Task<User> Person()
        {
            var code = prefix + owned.Count;
            var user = new User { Username = code, EmployeeCode = code, Email = code + "@example.invalid",
                FullName = code, CreatedAt = DateTime.UtcNow, PrimaryBranchId = branch, AccountStatus = "INACTIVE", EmploymentStatus = "ACTIVE" };
            observer.Add(user); await observer.SaveChangesAsync(ct); observer.ChangeTracker.Clear(); owned.Add(user.UserId); return user;
        }
        async Task<int> Spid(HotelOpsDbContext db)
        { await db.Database.OpenConnectionAsync(ct); return await db.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct); }
        async Task Wait(string label, int waiter, int owner, Task pending)
        {
            for (var i = 0; i < 150 && !pending.IsCompleted; i++)
            {
                var kind = await observer.Database.SqlQuery<string>($"SELECT wait_type AS [Value] FROM sys.dm_exec_requests WHERE session_id={waiter} AND blocking_session_id={owner} AND wait_type LIKE 'LCK_M_%'").SingleOrDefaultAsync(ct);
                if (kind != null) { waits.Add(new(label, waiter, owner, kind, DateTime.UtcNow)); return; }
                await Task.Delay(50, ct);
            }
            if (pending.IsFaulted) await pending;
            throw new InvalidOperationException("No actual DEV1 writer SQL wait: " + label);
        }
        async Task<string> State(long id) => JsonSerializer.Serialize(new
        {
            user = await observer.Users.AsNoTracking().SingleAsync(x => x.UserId == id, ct),
            audits = await observer.AuditLogs.AsNoTracking().Where(x => x.EntityType == "users" && x.EntityId == id.ToString()).OrderBy(x => x.AuditId).ToArrayAsync(ct)
        }); // Compare only in memory, do not emit payloads.
        var lastAdmin = "BLOCKED: unrelated eligible administrators cannot be edited";
        try
        {
            var first = await Person(); var commit = new CommitBarrier();
            await using (var writer = Open(commit))
            await using (var contender = Open())
            {
                var owner = await Spid(writer); var waiter = await Spid(contender);
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var activation = activate(writer, first.UserId, first.RowVersion, cancellation.Token);
                Task<IReadOnlyDictionary<long, UserEligibilityLock.LockedUser?>>? locking = null;
                await using var tx = await contender.Database.BeginTransactionAsync(ct);
                try
                {
                    await commit.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
                    locking = new UserEligibilityLock(contender).LockAsync([first.UserId], cancellation.Token);
                    await Wait("Actual DEV1 activation-first", waiter, owner, locking);
                    Check(!locking.IsCompleted, "actual DEV1 activation retains User lock through business and audit flush until commit");
                    commit.Release.TrySetResult(); var result = await activation; var locked = await locking;
                    Check(result.Status == AccountActivationStatus.Success && locked[first.UserId]?.AccountStatus == "ACTIVE"
                        && !result.Version!.AsSpan().SequenceEqual(first.RowVersion)
                        && await observer.AuditLogs.CountAsync(x => x.EntityType == "users" && x.EntityId == first.UserId.ToString(), ct) == 1,
                        "actual DEV1 activation commits one account/version change and one audit; waiter rereads ACTIVE");
                }
                finally { commit.Release.TrySetResult(); cancellation.Cancel(); await tx.RollbackAsync(CancellationToken.None); await Drain(activation); if (locking != null) await Drain(locking); }
            }
            var second = await Person();
            await using (var holder = Open())
            await using (var writer = Open())
            {
                var owner = await Spid(holder); var waiter = await Spid(writer);
                await using var tx = await holder.Database.BeginTransactionAsync(ct);
                await new UserEligibilityLock(holder).LockAsync([second.UserId], ct);
                // Test-only competing edit on a separate owned user, never a Deactivate success simulation.
                await holder.Users.Where(x => x.UserId == second.UserId).ExecuteUpdateAsync(s => s.SetProperty(x => x.FullName, "Owned competing profile edit"), ct);
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var activation = activate(writer, second.UserId, second.RowVersion, cancellation.Token);
                try
                {
                    await Wait("User-lock-first stale DEV1 activation", waiter, owner, activation);
                    await tx.CommitAsync(ct); var before = await State(second.UserId);
                    Check((await activation).Status == AccountActivationStatus.Stale && await State(second.UserId) == before,
                        "actual DEV1 activation after User wait rereads version and rejects committed stale state without partial audit");
                }
                finally { cancellation.Cancel(); await Drain(activation); }
            }
            var third = await Person(); var thirdBefore = await State(third.UserId);
            await using (var holder = Open())
            await using (var writer = Open())
            {
                var owner = await Spid(holder); var waiter = await Spid(writer);
                await using var tx = await holder.Database.BeginTransactionAsync(ct);
                await new UserEligibilityLock(holder).LockAsync([third.UserId], ct);
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var activation = activate(writer, third.UserId, third.RowVersion, cancellation.Token);
                var cancelled = false;
                try
                {
                    await Wait("DEV1 activation cancellation", waiter, owner, activation); cancellation.Cancel();
                    try { await activation; } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { cancelled = true; }
                }
                finally { cancellation.Cancel(); await tx.RollbackAsync(CancellationToken.None); await Drain(activation); }
                Check(cancelled && writer.Database.CurrentTransaction == null && await State(third.UserId) == thirdBefore,
                    "actual DEV1 writer cancellation releases retained User locks and leaves account version and audit unchanged");
            }
            var fourth = await Person(); var fourthBefore = await State(fourth.UserId); var failure = new FailCommit(fourth.UserId);
            await using (var writer = Open(failure))
            {
                var failed = false;
                try { await activate(writer, fourth.UserId, fourth.RowVersion, ct); } catch (CommitFault) { failed = true; }
                Check(failed && failure.AuditFlushed && writer.Database.CurrentTransaction == null && await State(fourth.UserId) == fourthBefore,
                    "actual DEV1 writer failure after persisted account and audit flush rolls both back atomically");
            }
            // Foundation test, not an actual successful Deactivate. Touch only the two owned readiness administrators.
            await using (var db = Open())
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var guard = await new GlobalAdminSerialization(db, TimeProvider.System).AcquireAsync(ct: ct);
                var before = await guard.ReadSnapshotAsync(ct);
                if (before.DurableEligibleUserIds.Count == 2 && before.DurableEligibleUserIds.All(id => id == actor || id == target))
                {
                    await db.Users.Where(x => x.UserId == target).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountStatus, "INACTIVE"), ct);
                    await guard.EnsurePreservedAsync(before, ct);
                    Check(true, "LastAdmin foundation allows one eligible owned administrator remaining under canonical locks");
                    await db.Users.Where(x => x.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountStatus, "INACTIVE"), ct);
                    var rejected = false;
                    try { await guard.EnsurePreservedAsync(before, ct); } catch (GlobalAdministrationInvariantException) { rejected = true; }
                    Check(rejected, "LastAdmin foundation rejects zero eligible administrators; actual Deactivate poststate branch is still BLOCKED");
                    lastAdmin = "PASS foundation only; actual Deactivate branch BLOCKED";
                }
                await tx.RollbackAsync(ct);
            }
        }
        finally
        {
            foreach (var id in owned)
            {
                await observer.AuditLogs.Where(x => x.EntityType == "users" && x.EntityId == id.ToString()).ExecuteDeleteAsync(CancellationToken.None);
                await observer.Users.Where(x => x.UserId == id && x.Username.StartsWith(prefix)).ExecuteDeleteAsync(CancellationToken.None);
            }
        }
        Check(!await observer.Users.AnyAsync(x => owned.Contains(x.UserId), ct), "owned DEV1 writer fixtures removed without RBAC catalog or business assignment writes");
        return new(cases, waits, lastAdmin, "BLOCKED: no DEV1 assignment/workday/handover writer implementation to exercise; external writer inventory unverified");
    }

    private static async Task Drain(Task task) { try { await task; } catch { /* Retain primary error while settling owned workers. */ } }
    private sealed class CommitBarrier : DbTransactionInterceptor
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        { Entered.TrySetResult(); await Release.Task.WaitAsync(cancellationToken); return result; }
    }
    private sealed class CommitFault : Exception;
    private sealed class FailCommit(long userId) : DbTransactionInterceptor
    {
        internal bool AuditFlushed { get; private set; }
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            await using var command = transaction.Connection!.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "SELECT COUNT(*) FROM dbo.audit_logs WHERE entity_type='users' AND entity_id=CONVERT(varchar(30),@id)";
            var p = command.CreateParameter(); p.ParameterName = "@id"; p.Value = userId; command.Parameters.Add(p);
            AuditFlushed = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
            throw new CommitFault();
        }
    }
}
