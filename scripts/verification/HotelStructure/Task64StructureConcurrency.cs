using System.Data.Common;
using System.Net;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

internal static class Task64StructureConcurrency
{
    internal static List<object> Waits { get; } = [];
    internal static List<object> Failures { get; } = [];
    internal static void ObserveReadErrors(DbContextOptionsBuilder<HotelOpsDbContext> options, DbCommandInterceptor? hook)
    {
        // SQL may return the reader before a later row blocks. Those errors surface during
        // materialization, beyond CommandFailedAsync. Observe EF's typed error payload too;
        // retain only provider error numbers, never SQL text, parameters or exception messages.
        if (hook is Probe probe) options.LogTo((id, _) => id == CoreEventId.QueryIterationFailed,
            data => { if (data is DbContextTypeErrorEventData error) probe.Record(error.Exception); });
    }
    internal static async Task RunAsync(HotelOpsDbContext setup, Func<DbCommandInterceptor?, HotelOpsDbContext> open,
        Func<string, Task<CookieJar>> login, IReadOnlyDictionary<string, User> identities, long branch, long floor,
        Func<string, CookieJar, CancellationToken, Task<HttpResponseMessage>> get, Action<bool, string> check, CancellationToken ct)
    {
        var actor = identities["admin"].UserId; var route = $"/api/branches/{branch}/structure";
        async Task<int> Spid(HotelOpsDbContext db)
        { await db.Database.OpenConnectionAsync(ct); return await db.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct); }
        async Task<int> Wait(string scenario, int owner, Task pending, string? tag = null)
        {
            for (var i = 0; i < 160 && !pending.IsCompleted; i++)
            {
                var rows = await setup.Database.SqlQuery<SqlWait>($"SELECT CONVERT(int,r.session_id) AS SessionId,r.wait_type AS Kind,t.text AS Query FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE r.blocking_session_id={owner} AND r.wait_type LIKE 'LCK_M_%'").ToArrayAsync(ct);
                var row = rows.FirstOrDefault(x => tag == null || x.Query.Contains(tag, StringComparison.Ordinal));
                if (row != null) { Waits.Add(new { scenario, owner, waiter = row.SessionId, row.Kind }); return row.SessionId; }
                await Task.Delay(40, ct);
            }
            if (pending.IsFaulted) await pending;
            throw new InvalidOperationException("No DMV-confirmed UC013 wait: " + scenario);
        }
        foreach (var scenario in new[] { "writer-first", "permission", "session", "expiry", "downgrade", "cancel" })
        {
            UserRole? extra = null; UserBranchAccess? member = null;
            if (scenario == "downgrade")
            {
                var role = await setup.Roles.Where(x => x.RoleCode == "OPERATIONS_MANAGER").Select(x => x.RoleId).SingleAsync(ct);
                extra = new() { UserId = actor, RoleId = role, BranchId = branch, IsActive = true, AssignedAt = DateTime.UtcNow.AddHours(-1) };
                member = new() { UserId = actor, BranchId = branch, AccessLevel = "STANDARD", GrantedAt = DateTime.UtcNow.AddHours(-1) };
                setup.AddRange(extra, member); await setup.SaveChangesAsync(ct); setup.ChangeTracker.Clear();
            }
            var jar = await login("admin");
            var sid = await setup.UserSessions.Where(x => x.UserId == actor).OrderByDescending(x => x.SessionId).Select(x => x.SessionId).FirstAsync(ct);
            var masterRole = await setup.Roles.Where(x => x.RoleCode == "SYS_ADMIN").Select(x => x.RoleId).SingleAsync(ct);
            if (scenario is "expiry" or "downgrade") await setup.UserRoles.Where(x => x.UserId == actor && x.RoleId == masterRole)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddSeconds(5)), ct);
            await using var holder = open(null); var owner = await Spid(holder);
            await using var tx = await holder.Database.BeginTransactionAsync(ct);
            await holder.Floors.Where(x => x.FloorId == floor).ExecuteUpdateAsync(s => s.SetProperty(x => x.Description, "T64_AUTHORITATIVE_DESC"), ct);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var pending = get(route, jar, stop.Token); Task? revoke = null;
            await using var revoker = open(null);
            try
            {
                var api = await Wait("http-" + scenario, owner, pending, "UC013:Floors:Page");
                if (scenario is "permission" or "session")
                {
                    revoke = Revoke();
                    async Task Revoke()
                    {
                        await using var rtx = await revoker.Database.BeginTransactionAsync(ct);
                        await new UserEligibilityLock(revoker).LockAsync([actor], ct);
                        if (scenario == "permission") await revoker.UserRoles.Where(x => x.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
                        else await revoker.UserSessions.Where(x => x.SessionId == sid).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
                        await rtx.CommitAsync(ct);
                    }
                    await Wait("http-" + scenario + "-revoker", api, revoke);
                    check(!revoke.IsCompleted, "UC013 real HTTPS retains actor/session/grants until read ends: " + scenario);
                }
                if (scenario == "cancel")
                {
                    stop.Cancel(); var canceled = false;
                    try { using var response = await pending; } catch (OperationCanceledException) { canceled = true; }
                    var released = false;
                    for (var i = 0; i < 160; i++)
                    {
                        if (!await setup.Database.SqlQuery<int>($"SELECT CONVERT(int,request_session_id) AS [Value] FROM sys.dm_tran_locks WHERE request_session_id={api} AND resource_database_id=DB_ID() AND request_owner_type='TRANSACTION'").AnyAsync(ct)) { released = true; break; }
                        await Task.Delay(40, ct);
                    }
                    check(canceled && released, "UC013 actual HTTPS RequestAborted releases SQL transaction before blocker release");
                    await tx.RollbackAsync(ct);
                }
                else
                {
                    if (scenario is "expiry" or "downgrade") await Task.Delay(TimeSpan.FromSeconds(6), ct);
                    await tx.CommitAsync(ct); using var response = await pending;
                    var body = await response.Content.ReadAsStringAsync(ct);
                    if (scenario is "expiry" or "downgrade")
                    {
                        check(response.StatusCode == HttpStatusCode.Forbidden && !body.Contains("totalCount") && !body.Contains("T64_AUTHORITATIVE_DESC"),
                            "UC013 final gate discards materialized management data: " + scenario);
                        if (scenario == "downgrade")
                        {
                            using var basic = await get(route, jar, ct); var text = await basic.Content.ReadAsStringAsync(ct);
                            check(basic.StatusCode == HttpStatusCode.OK && text.Contains("\"visibility\":\"basic\"") && !text.Contains("T64_AUTHORITATIVE_DESC"),
                                "UC013 surviving ROOM_VIEW grants basic data only after management expiry");
                        }
                    }
                    else
                    {
                        using var json = JsonDocument.Parse(body);
                        check(response.StatusCode == HttpStatusCode.OK && json.RootElement.GetProperty("floors").GetProperty("items").EnumerateArray()
                            .Single(x => x.GetProperty("id").GetInt64() == floor).GetProperty("description").GetString() == "T64_AUTHORITATIVE_DESC",
                            "UC013 actual HTTPS reads authoritative committed Floor state after SQL wait: " + scenario);
                    }
                    if (revoke != null)
                    {
                        await revoke; using var denied = await get(route, jar, ct);
                        check((int)denied.StatusCode == (scenario == "session" ? 401 : 403), "UC013 committed authority revocation applies to next HTTPS read");
                    }
                }
            }
            finally
            {
                stop.Cancel(); if (holder.Database.CurrentTransaction != null) await tx.RollbackAsync(CancellationToken.None);
                try { using var response = await pending; } catch { }
                if (revoke != null) try { await revoke; } catch { }
                await setup.UserRoles.Where(x => x.UserId == actor && x.RoleId == masterRole)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true).SetProperty(x => x.ExpiresAt, (DateTime?)null), ct);
                await setup.Floors.Where(x => x.FloorId == floor).ExecuteUpdateAsync(s => s.SetProperty(x => x.Description, "T64_ADMIN_ONLY"), ct);
                if (extra != null) await setup.UserRoles.Where(x => x.UserRoleId == extra.UserRoleId).ExecuteDeleteAsync(ct);
                if (member != null) await setup.UserBranchAccesses.Where(x => x.AccessId == member.AccessId).ExecuteDeleteAsync(ct);
            }
        }
        // Direct service sessions are persisted fixtures, independently SQL-revalidated.
        var tickets = new Dictionary<string, (SessionReference Ticket, SessionSnapshot Session)>();
        foreach (var key in new[] { "admin", "other" })
        {
            var raw = SessionCredential.Generate(); var now = DateTime.UtcNow;
            var row = new UserSession { UserId = identities[key].UserId, SessionKeyHash = SessionCredential.Hash(raw), CreatedAt = now, LastUsedAt = now, ExpiresAt = now.AddMinutes(30) };
            setup.Add(row); await setup.SaveChangesAsync(ct); setup.ChangeTracker.Clear();
            tickets[key] = (SessionReference.FromProtectedTicket(row.UserId, raw, false, now), new(row.SessionId, row.UserId, now, row.ExpiresAt, now.AddHours(12), false));
        }
        (BranchStructureService Read, HotelStructureCreationService Create) Services(HotelOpsDbContext db, string key)
        {
            var ticket = tickets[key]; var context = new DefaultHttpContext { User = HotelOpsCookieTicket.Create(new(ticket.Ticket, ticket.Session), TimeProvider.System).Principal };
            context.Items[typeof(SessionSnapshot)] = ticket.Session;
            // Direct concurrent calls have distinct request contexts. HttpContextAccessor's
            // shared AsyncLocal holder would clear the first fixture when constructing the
            // second in this console execution context, unlike separate real HTTP requests.
            var current = new CurrentUserAccessor(new RequestContext { HttpContext = context });
            var session = new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System);
            var permissions = new PermissionService(current, new EfPermissionDataSource(db), new UnresolvedPermissionResourceResolver(), TimeProvider.System);
            return (new(db, current, session, permissions), new(db, current, permissions, session, TimeProvider.System));
        }
        Task<BranchStructureResult> Read(HotelOpsDbContext db, CancellationToken token) => Services(db, "admin").Read.ReadAsync(tickets["admin"].Ticket, branch, new() { PageSize = 100 }, token);
        var capture = new Probe();
        await using (var reader = open(capture))
        {
            var result = await Read(reader, ct);
            check(result.Status == BranchStructureStatus.Success && capture.Queries.Count(x => x.Contains("UC013:")) == 3,
                "UC013 root data projection is branch + SQL count/page (3 commands), independent of child count");
            check(capture.Queries.Where(x => x.Contains("UC013:")).All(x => !x.Contains("[password_hash]") && !x.Contains("[manager_user_id]") && !x.Contains("[operational_status]")),
                "UC013 feature SQL does not select credentials, managers or operational status");
            check(!reader.ChangeTracker.Entries().Any() && reader.Database.CurrentTransaction == null, "UC013 production read leaves clean tracker and transaction");
        }
        var beforeCount = await setup.Floors.CountAsync(x => x.BranchId == branch, ct);
        var barrier = new Probe { PausePage = true };
        await using (var reader = open(barrier))
        await using (var creator = open(null))
        {
            var readerSpid = await Spid(reader); var pending = Read(reader, ct); Task<StructureCreationResult>? write = null;
            try
            {
                await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
                write = Services(creator, "other").Create.CreateFloorAsync(tickets["other"].Ticket, tickets["other"].Session.SessionId,
                    branch, new("T64_PHANTOM", "Concurrent production UC014"), ct);
                await Wait("count-page-actual-UC014-create", readerSpid, write);
                barrier.Release.TrySetResult(); var result = await pending; var created = await write;
                check(result.Status == BranchStructureStatus.Success && result.Structure!.Floors!.TotalCount == beforeCount
                    && result.Structure.Floors.Items.Count == beforeCount && created.Status == StructureCreationStatus.Success,
                    $"UC013 count/page snapshot remains consistent while actual UC014 create waits and commits after read (reader={result.Status}, total={result.Structure?.Floors?.TotalCount}, items={result.Structure?.Floors?.Items.Count}, before={beforeCount}, writer={created.Status})");
                check(await setup.Floors.CountAsync(x => x.BranchId == branch, ct) == beforeCount + 1,
                    "UC014 concurrent create persists once after read boundary ends");
            }
            finally { barrier.Release.TrySetResult(); try { await pending; } catch { } if (write != null) try { await write; } catch { } }
        }
        async Task<string> AllRows()
        {
            var hashes = new List<string>();
            foreach (var entity in setup.Model.GetEntityTypes().Where(x => x.GetTableName() != null).OrderBy(x => x.GetTableName()))
            {
                static string Quote(string value) => "[" + value.Replace("]", "]]") + "]";
                var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                var order = string.Join(',', entity.FindPrimaryKey()!.Properties.Select(x => Quote(x.GetColumnName(store)!)));
                await using var cmd = setup.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "SELECT * FROM " + Quote(entity.GetSchema() ?? "dbo") + "." + Quote(entity.GetTableName()!) + " ORDER BY " + order + " FOR JSON PATH,INCLUDE_NULL_VALUES";
                await using var rows = await cmd.ExecuteReaderAsync(ct); var text = new System.Text.StringBuilder();
                while (await rows.ReadAsync(ct)) text.Append(rows.GetString(0));
                hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.ToString()))));
            }
            return string.Join(':', hashes);
        }
        var before = await AllRows();
        foreach (var scenario in new[] { "lock1222", "timeout", "query-fault" })
        {
            var probe = new Probe { FailPage = scenario == "query-fault" };
            await using var reader = open(probe);
            await Spid(reader);
            if (scenario == "lock1222") await reader.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT 1200", ct);
            if (scenario == "timeout") reader.Database.SetCommandTimeout(2);
            await using var holder = open(null); var owner = await Spid(holder);
            await using var tx = await holder.Database.BeginTransactionAsync(ct);
            if (scenario != "query-fault") await holder.Floors.Where(x => x.FloorId == floor).ExecuteUpdateAsync(s => s.SetProperty(x => x.Description, "Uncommitted fault fixture"), ct);
            var pending = Read(reader, ct);
            try
            {
                if (scenario != "query-fault") await Wait("read-" + scenario, owner, pending, "UC013:Floors:Page");
                var faulted = false; BranchStructureStatus? status = null;
                try { var result = await pending; status = result.Status; faulted = result.Status == BranchStructureStatus.Unavailable && result.Structure == null; }
                catch (SqlException e) when (scenario == "query-fault" && e.Number == 51064) { faulted = true; }
                var expected = scenario == "query-fault" ? 51064 : scenario == "lock1222" ? 1222 : -2;
                check(faulted && probe.Errors.Contains(expected) && reader.Database.CurrentTransaction == null && !reader.ChangeTracker.Entries().Any(),
                    $"UC013 actual SQL {expected} fails closed, no partial DTO or retained transaction (status={status}, observed={string.Join(',', probe.Errors)})");
                Failures.Add(new { scenario, numbers = probe.Errors });
            }
            finally { if (holder.Database.CurrentTransaction != null) await tx.RollbackAsync(CancellationToken.None); try { await pending; } catch { } }
        }
        check(await AllRows() == before, "UC013 production SQL reads/faults preserve every mapped table row, including sessions/audits/credentials");
        await using (var recovery = open(null)) check((await Read(recovery, ct)).Status == BranchStructureStatus.Success, "UC013 normal read recovers after SQL contention/faults");
    }

    private sealed class RequestContext : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
    private sealed class SqlWait
    {
        public int SessionId { get; set; }
        public string Kind { get; set; } = "";
        public string Query { get; set; } = "";
    }
    private sealed class Probe : DbCommandInterceptor
    {
        internal bool PausePage, FailPage;
        internal List<string> Queries { get; } = [];
        internal List<int> Errors { get; } = [];
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Record(Exception error)
        {
            for (Exception? inner = error; inner != null; inner = inner.InnerException)
                if (inner is SqlException sql && !Errors.Contains(sql.Number)) Errors.Add(sql.Number);
        }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            Queries.Add(command.CommandText);
            if (command.CommandText.Contains("UC013:Floors:Page"))
            {
                if (FailPage) command.CommandText = ";THROW 51064, 'Task64 deliberate read SQL failure', 1;";
                if (PausePage) { Entered.TrySetResult(); await Release.Task.WaitAsync(ct); }
            }
            return result;
        }
        public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData data, CancellationToken ct = default)
        { Record(data.Exception); return Task.CompletedTask; }
    }
}
