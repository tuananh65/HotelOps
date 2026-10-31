using System.Data.Common;
using System.Net;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Additional cases in the existing real-Program.cs HTTPS harness. No replacement host,
// authentication doubles, production hooks, catalog mutation or new test framework.
internal static class Task61AcceptanceCases
{
    internal static List<object> Waits { get; } = [];

    internal static async Task RunAsync(HotelOpsDbContext setup,
        Func<DbCommandInterceptor?, HotelOpsDbContext> open, HttpClient http, Func<string, Task<Jar>> login,
        IReadOnlyDictionary<string, User> identities, IReadOnlyList<User> targets, Jar branchOnly,
        long branch, Action<bool, string> check, CancellationToken ct)
    {
        async Task<HttpResponseMessage> Get(string path, Jar jar, CancellationToken token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(x => x.Key + "=" + x.Value)));
            var result = await http.SendAsync(request, token); jar.Read(result); return result;
        }
        async Task<int> Spid(HotelOpsDbContext db)
        {
            await db.Database.OpenConnectionAsync(ct);
            return await db.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
        }
        async Task<int> Wait(string scenario, int owner, Task pending, string? expectedTag = null, int? waiter = null)
        {
            for (var i = 0; i < 160 && !pending.IsCompleted; i++)
            {
                var requests = await setup.Database.SqlQuery<SqlWait>($"SELECT CONVERT(int,r.session_id) AS SessionId, r.wait_type AS Kind, t.text AS Query FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE r.blocking_session_id={owner} AND r.wait_type LIKE 'LCK_M_%'").ToArrayAsync(ct);
                var found = requests.FirstOrDefault(x => (waiter == null || x.SessionId == waiter)
                    && (expectedTag == null || x.Query.Contains(expectedTag, StringComparison.Ordinal)));
                if (found != null)
                {
                    Waits.Add(new { scenario, waiter = found.SessionId, owner, found.Kind });
                    return found.SessionId;
                }
                await Task.Delay(40, ct);
            }
            if (pending.IsFaulted) await pending;
            throw new InvalidOperationException("FAIL: no retained SQL wait for " + scenario);
        }
        using (var denied = await Get("/api/users?branchId=" + branch + "&actorId=" + identities["admin"].UserId, branchOnly, ct))
            check(denied.StatusCode == HttpStatusCode.Forbidden && !(await denied.Content.ReadAsStringAsync(ct)).Contains("totalCount"),
                "actual SQL USER_VIEW BRANCH grant denied even for matching branch/client actor selector");

        // Dedicated cookie for each session case; do not revoke the main acceptance cookie.
        foreach (var expired in new[] { true, false })
        {
            var jar = await login("admin");
            var id = await setup.UserSessions.AsNoTracking().Where(x => x.UserId == identities["admin"].UserId)
                .OrderByDescending(x => x.SessionId).Select(x => x.SessionId).FirstAsync(ct);
            if (expired) await setup.UserSessions.Where(x => x.SessionId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)), ct);
            else await setup.UserSessions.Where(x => x.SessionId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
            using var denied = await Get("/api/users", jar, ct);
            check(denied.StatusCode == HttpStatusCode.Unauthorized
                && !(await denied.Content.ReadAsStringAsync(ct)).Contains("totalCount"), expired
                    ? "real HTTPS expired SQL session denies original cookie" : "real HTTPS revoked SQL session denies original cookie");
        }

        foreach (var scenario in new[] { "session", "permission", "cancellation", "writer-first", "final-expiry" })
        {
            var jar = await login("admin");
            var sessionId = await setup.UserSessions.AsNoTracking().Where(x => x.UserId == identities["admin"].UserId)
                .OrderByDescending(x => x.SessionId).Select(x => x.SessionId).FirstAsync(ct);
            if (scenario == "final-expiry") await setup.UserRoles.Where(x => x.UserId == identities["admin"].UserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddSeconds(5)), ct);
            await using var holder = open(null); await using var revoker = open(null);
            var owner = await Spid(holder); var revokeSpid = await Spid(revoker);
            await using var tx = await holder.Database.BeginTransactionAsync(ct);
            var featureWait = scenario is "session" or "permission" or "final-expiry";
            if (featureWait)
                await holder.UserRoles.Where(x => x.UserId == targets[0].UserId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
            else await holder.Users.Where(x => x.UserId == targets[0].UserId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.FullName, "Task61 authoritative committed name"), ct);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var pending = Get("/api/users?search=" + Uri.EscapeDataString(targets[0].Username), jar, stop.Token);
            Task? revoke = null;
            try
            {
                var apiSpid = await Wait(scenario + "-http-query-wait", owner, pending,
                    featureWait ? "UC141:Page" : "UC141:UserReadUniverse");
                if (scenario is "session" or "permission")
                {
                    revoke = Revoke();
                    async Task Revoke()
                    {
                        await using var revokeTx = await revoker.Database.BeginTransactionAsync(ct);
                        await new UserEligibilityLock(revoker).LockAsync([identities["admin"].UserId], ct);
                        if (scenario == "session") await revoker.UserSessions.Where(x => x.SessionId == sessionId)
                            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
                        else await revoker.UserRoles.Where(x => x.UserId == identities["admin"].UserId)
                            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
                        await revokeTx.CommitAsync(ct);
                    }
                    // The old endpoint lets this revocation commit while its read is in
                    // flight. Acceptance requires retaining SQL authority until read ends.
                    await Wait(scenario + "-revoker-waits-for-inflight-read", apiSpid, revoke, waiter: revokeSpid);
                    check(!revoke.IsCompleted, scenario + " authority retained through pending production HTTPS read");
                }
                if (scenario == "cancellation")
                {
                    stop.Cancel(); var cancelled = false;
                    try { using var result = await pending; } catch (OperationCanceledException) { cancelled = true; }
                    check(cancelled, "HTTPS caller cancellation propagates during actual SQL lock wait");
                    var released = false;
                    for (var i = 0; i < 120; i++)
                    {
                        if (!await setup.Database.SqlQuery<int>($"SELECT session_id AS [Value] FROM sys.dm_exec_requests WHERE session_id={apiSpid} AND blocking_session_id={owner}").AnyAsync(ct))
                        { released = true; break; }
                        await Task.Delay(40, ct);
                    }
                    check(released, "RequestAborted cancels blocked SQL before fixture blocker is released");
                    await tx.RollbackAsync(ct);
                }
                else
                {
                    if (scenario == "final-expiry") await Task.Delay(TimeSpan.FromSeconds(6), ct);
                    await tx.CommitAsync(ct);
                    using var result = await pending;
                    if (scenario == "final-expiry")
                        check(result.StatusCode == HttpStatusCode.Forbidden
                            && !(await result.Content.ReadAsStringAsync(ct)).Contains("totalCount"),
                            "grant expires during real SQL wait: final filter replaces action data with forbidden before serialization");
                    else
                    {
                        check(result.StatusCode == HttpStatusCode.OK, scenario + " inflight read linearizes before queued revocation");
                        using var body = JsonDocument.Parse(await result.Content.ReadAsStringAsync(ct));
                        var item = body.RootElement.GetProperty("items").EnumerateArray().Single();
                        check(featureWait ? item.GetProperty("roles").GetArrayLength() == 0
                            : item.GetProperty("fullName").GetString() == "Task61 authoritative committed name",
                            scenario + " actual HTTP query rereads committed target state after wait");
                    }
                    if (revoke != null)
                    {
                        await revoke; using var denied = await Get("/api/users", jar, ct);
                        check((int)denied.StatusCode == (scenario == "session" ? 401 : 403), scenario + " revocation applies to next real HTTPS request");
                    }
                }
            }
            finally
            {
                stop.Cancel();
                // Release only this fixture's transaction; preserve the primary assertion.
                if (holder.Database.CurrentTransaction != null) await tx.RollbackAsync(CancellationToken.None);
                try { using var response = await pending; } catch { }
                if (revoke != null) { try { await revoke; } catch { } }
                await setup.UserRoles.Where(x => x.UserId == identities["admin"].UserId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true).SetProperty(x => x.ExpiresAt, (DateTime?)null), ct);
                await setup.Users.Where(x => x.UserId == targets[0].UserId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.FullName, targets[0].FullName), ct);
                await setup.UserRoles.Where(x => x.UserId == targets[0].UserId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true), ct);
            }
        }

        // Supplementary SQL boundary tests, not substitutes for the real HTTPS cases.
        var raw = SessionCredential.Generate(); var createdAt = DateTime.UtcNow;
        var rowSession = new UserSession { UserId = identities["admin"].UserId,
            SessionKeyHash = SessionCredential.Hash(raw), CreatedAt = createdAt,
            ExpiresAt = createdAt.AddMinutes(30), LastUsedAt = createdAt };
        setup.UserSessions.Add(rowSession); await setup.SaveChangesAsync(ct); setup.ChangeTracker.Clear();
        var ticket = SessionReference.FromProtectedTicket(rowSession.UserId, raw, false, createdAt);
        var snapshot = new SessionSnapshot(rowSession.SessionId, rowSession.UserId, createdAt,
            rowSession.ExpiresAt, createdAt.AddHours(12), false);
        UserDirectoryReadGuard Guard(HotelOpsDbContext db)
        {
            var context = new DefaultHttpContext { User = HotelOpsCookieTicket.Create(new(ticket, snapshot), TimeProvider.System).Principal };
            context.Items[typeof(SessionSnapshot)] = snapshot;
            var current = new CurrentUserAccessor(new HttpContextAccessor { HttpContext = context });
            return new(db, current,
                new PermissionService(current, new EfPermissionDataSource(db), new UnresolvedPermissionResourceResolver(), TimeProvider.System), TimeProvider.System);
        }
        var barrier = new PageBarrier();
        await using (var reader = open(barrier))
        await using (var writer = open(null))
        {
            var owner = await Spid(reader); var waiter = await Spid(writer);
            var extra = new User { EmployeeCode = "T61_PHANTOM", Username = "ud.target.t61", Email = "t61.phantom@invalid.test",
                FullName = "Task61 phantom", AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = createdAt };
            Task? insert = null;
            var read = Guard(reader).RunAsync(ticket, () => new UserDirectoryService(reader, TimeProvider.System)
                .ListAsync(new() { Search = "ud.target", PageSize = 100 }, ct), ct);
            try
            {
                await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
                writer.Users.Add(extra); insert = writer.SaveChangesAsync(ct);
                await Wait("count-page-phantom-insert", owner, insert, waiter: waiter);
                barrier.Release.TrySetResult(); var result = await read; await insert;
                check(result.Status == UserDirectoryReadStatus.Success && result.Value is { TotalCount: 26, Items.Count: 26 },
                    "production read guard retains serializable count/page ranges; concurrent insert waits and cannot split totals/items");
                check(reader.Database.CurrentTransaction == null && !reader.ChangeTracker.Entries().Any(),
                    "guard read commit releases transaction and leaves no tracked entities");
            }
            finally
            {
                barrier.Release.TrySetResult(); try { await read; } catch { }
                if (insert != null) { try { await insert; } catch { } }
                if (extra.UserId > 0) await setup.Users.Where(x => x.UserId == extra.UserId).ExecuteDeleteAsync(ct);
            }
        }
        await using (var failing = open(new PageBarrier { Fail = true }))
        {
            var rejected = false;
            try { await Guard(failing).RunAsync(ticket, () => new UserDirectoryService(failing, TimeProvider.System).ListAsync(new(), ct), ct); }
            catch (InvalidOperationException e) when (e.Message == "Task61 deliberate page fault") { rejected = true; }
            check(rejected && failing.Database.CurrentTransaction == null && !failing.ChangeTracker.Entries().Any(),
                "actual page command fault rolls back read boundary; no partial directory or retained transaction");
        }
    }
    private sealed class SqlWait
    {
        public int SessionId { get; set; }
        public string Kind { get; set; } = "";
        public string Query { get; set; } = "";
    }
    private sealed class PageBarrier : DbCommandInterceptor
    {
        public bool Fail { get; init; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UC141:Page", StringComparison.Ordinal))
            {
                if (Fail) throw new InvalidOperationException("Task61 deliberate page fault");
                Entered.TrySetResult(); await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
