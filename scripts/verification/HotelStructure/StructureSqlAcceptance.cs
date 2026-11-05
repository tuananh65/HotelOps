using System.Data.Common;
using System.Net;
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

internal static class StructureSqlAcceptance
{
    internal static List<object> Waits { get; } = [];
    internal static List<object> Failures { get; } = [];
    internal static async Task RunAsync(HotelOpsDbContext setup, Func<DbCommandInterceptor?, HotelOpsDbContext> open,
        IReadOnlyDictionary<string, User> identities, long branch, long floor,
        Func<string, CookieJar, object, CancellationToken, Task<HttpResponseMessage>> post,
        Func<string, Task<CookieJar>> login, Action<bool, string> check, CancellationToken ct)
    {
        var actor = identities["admin"].UserId;
        async Task<int> Spid(HotelOpsDbContext db)
        { await db.Database.OpenConnectionAsync(ct); return await db.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct); }
        async Task<int> Wait(string scenario, int owner, Task pending, string? tag = null)
        {
            for (var i = 0; i < 180 && !pending.IsCompleted; i++)
            {
                var rows = await setup.Database.SqlQuery<SqlWait>($"SELECT CONVERT(int,r.session_id) AS SessionId,r.wait_type AS Kind,t.text AS Query FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE r.blocking_session_id={owner} AND r.wait_type LIKE 'LCK_M_%'").ToArrayAsync(ct);
                var row = rows.FirstOrDefault(x => tag == null || x.Query.Contains(tag, StringComparison.OrdinalIgnoreCase));
                if (row != null) { Waits.Add(new { scenario, owner, waiter = row.SessionId, row.Kind }); return row.SessionId; }
                await Task.Delay(40, ct);
            }
            if (pending.IsFaulted) await pending;
            throw new InvalidOperationException("No observed SQL wait: " + scenario);
        }
        async Task<int> Effects(string code) => await setup.Floors.CountAsync(x => x.FloorCode == code, ct)
            + await setup.Areas.CountAsync(x => x.AreaCode == code, ct)
            + await setup.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.audit_logs WHERE entity_type IN ('floors','areas') AND JSON_VALUE(new_values,'$.code')={code}").SingleAsync(ct);
        string Route(bool area = false) => $"/api/branches/{branch}/{(area ? "areas" : "floors")}";

        // Parent-first: authoritative read after a real production HTTPS lock wait.
        foreach (var scenario in new[] { "branch-inactive", "floor-inactive", "floor-deleted", "branch-deleted" })
        {
            var jar = await login("admin"); var code = "T63_" + scenario.Replace('-', '_').ToUpperInvariant();
            var targetFloor = floor; var targetBranch = branch;
            if (scenario == "floor-deleted")
            {
                var extra = new Floor { BranchId = branch, FloorCode = "DELETE_FIXTURE", FloorName = "SQL fixture", IsActive = true };
                setup.Add(extra); await setup.SaveChangesAsync(ct); setup.ChangeTracker.Clear(); targetFloor = extra.FloorId;
            }
            if (scenario == "branch-deleted")
            {
                var extra = new Branch { BranchCode = "T63_DELETE", BranchName = "Disposable SQL fixture", IsActive = true };
                setup.Add(extra); await setup.SaveChangesAsync(ct); setup.ChangeTracker.Clear(); targetBranch = extra.BranchId;
            }
            await using var holder = open(null); var owner = await Spid(holder);
            await using var tx = await holder.Database.BeginTransactionAsync(ct);
            if (scenario == "branch-inactive") await holder.Branches.Where(x => x.BranchId == targetBranch).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
            if (scenario == "floor-inactive") await holder.Floors.Where(x => x.FloorId == targetFloor).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
            if (scenario == "floor-deleted") await holder.Floors.Where(x => x.FloorId == targetFloor).ExecuteDeleteAsync(ct);
            if (scenario == "branch-deleted") await holder.Branches.Where(x => x.BranchId == targetBranch).ExecuteDeleteAsync(ct);
            var area = scenario.StartsWith("floor", StringComparison.Ordinal);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var pending = post($"/api/branches/{targetBranch}/{(area ? "areas" : "floors")}", jar,
                area ? (object)new { code, name = "Parent race", floorId = targetFloor } : new { code, name = "Parent race" }, stop.Token);
            try
            {
                await Wait(scenario, owner, pending, area ? "UC014:ParentFloor" : "UC014:BranchLock");
                await tx.CommitAsync(ct); using var response = await pending;
                check(response.StatusCode == HttpStatusCode.BadRequest && await Effects(code) == 0,
                    "actual HTTPS " + scenario + " rereads committed parent; no business/audit partial effect");
            }
            finally
            {
                stop.Cancel(); if (holder.Database.CurrentTransaction != null) await tx.RollbackAsync(CancellationToken.None);
                try { using var response = await pending; } catch { }
                if (scenario == "branch-inactive") await setup.Branches.Where(x => x.BranchId == branch).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true), ct);
                if (scenario == "floor-inactive") await setup.Floors.Where(x => x.FloorId == floor).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true), ct);
            }
        }

        // At this point the real API has staged its entity INSERT and is waiting to audit.
        // The blocker is test-only SQL, with no production hooks or DDL/schema changes.
        foreach (var scenario in new[] { "revoke-permission", "revoke-session", "parent-retained", "final-expiry", "cancel-after-insert" })
        {
            var jar = await login("admin"); var code = "T63_" + scenario.Replace('-', '_').ToUpperInvariant();
            var session = await setup.UserSessions.Where(x => x.UserId == actor).OrderByDescending(x => x.SessionId).Select(x => x.SessionId).FirstAsync(ct);
            if (scenario == "final-expiry") await setup.UserRoles.Where(x => x.UserId == actor)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddSeconds(5)), ct);
            await using var holder = open(null); var owner = await Spid(holder);
            await using var tx = await holder.Database.BeginTransactionAsync(ct);
            await holder.Database.SqlQueryRaw<long>("SELECT TOP(1) audit_id AS [Value] FROM dbo.audit_logs WITH (TABLOCKX,HOLDLOCK)").ToArrayAsync(ct);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var pending = post(Route(scenario == "parent-retained"), jar, scenario == "parent-retained"
                ? (object)new { code, name = "Retained parent", floorId = floor } : new { code, name = "Retained authority" }, stop.Token);
            await using var writer = open(null); Task? competing = null;
            try
            {
                var api = await Wait(scenario + "-audit", owner, pending, "INSERT INTO [audit_logs]");
                if (scenario is "revoke-permission" or "revoke-session" or "parent-retained")
                {
                    competing = Change();
                    async Task Change()
                    {
                        await using var wtx = await writer.Database.BeginTransactionAsync(ct);
                        if (scenario == "parent-retained")
                            await writer.Floors.Where(x => x.FloorId == floor).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
                        else
                        {
                            await new UserEligibilityLock(writer).LockAsync([actor], ct);
                            if (scenario == "revoke-permission") await writer.UserRoles.Where(x => x.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
                            else await writer.UserSessions.Where(x => x.SessionId == session).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
                        }
                        await wtx.CommitAsync(ct);
                    }
                    await Wait(scenario + "-writer-waits", api, competing);
                    check(!competing.IsCompleted, "production HTTP retains User/authority/parent through audit: " + scenario);
                }
                if (scenario == "cancel-after-insert")
                {
                    stop.Cancel(); var canceled = false;
                    try { using var response = await pending; } catch (OperationCanceledException) { canceled = true; }
                    var released = false;
                    for (var i = 0; i < 160; i++)
                    {
                        if (!await setup.Database.SqlQuery<int>($"SELECT CONVERT(int,request_session_id) AS [Value] FROM sys.dm_tran_locks WHERE request_session_id={api} AND resource_database_id=DB_ID() AND request_owner_type='TRANSACTION'").AnyAsync(ct)) { released = true; break; }
                        await Task.Delay(40, ct);
                    }
                    check(canceled && released, "HTTPS cancellation after entity INSERT rolls back and releases transaction locks before blocker release");
                    await tx.RollbackAsync(ct);
                    check(await Effects(code) == 0, "canceled creation leaves neither entity nor audit");
                }
                else
                {
                    if (scenario == "final-expiry") await Task.Delay(TimeSpan.FromSeconds(6), ct);
                    await tx.RollbackAsync(ct); using var response = await pending;
                    check((int)response.StatusCode == (scenario == "final-expiry" ? 403 : 201), "final production HTTPS result: " + scenario);
                    check(await Effects(code) == (scenario == "final-expiry" ? 0 : 2), "entity/audit atomic poststate: " + scenario);
                    if (competing != null)
                    {
                        await competing;
                        if (scenario != "parent-retained")
                        {
                            using var denied = await post(Route(), jar, new { code = "DENIED_AFTER_REVOKE", name = "Denied" }, ct);
                            check((int)denied.StatusCode == (scenario == "revoke-session" ? 401 : 403), "revocation applies to next HTTPS creation: " + scenario);
                        }
                    }
                }
            }
            finally
            {
                stop.Cancel(); if (holder.Database.CurrentTransaction != null) await tx.RollbackAsync(CancellationToken.None);
                try { using var response = await pending; } catch { }
                if (competing != null) try { await competing; } catch { }
                await setup.UserRoles.Where(x => x.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true).SetProperty(x => x.ExpiresAt, (DateTime?)null), ct);
                await setup.Floors.Where(x => x.FloorId == floor).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true), ct);
            }
        }

        // Dedicated persisted SQL fixture session for direct production-service fault tests.
        // HTTPS cases above use real credential login; these exercise the exact same service.
        var raw = SessionCredential.Generate(); var created = DateTime.UtcNow;
        var rowSession = new UserSession { UserId = actor, SessionKeyHash = SessionCredential.Hash(raw), CreatedAt = created,
            LastUsedAt = created, ExpiresAt = created.AddMinutes(30) };
        setup.Add(rowSession); await setup.SaveChangesAsync(ct); setup.ChangeTracker.Clear();
        var reference = SessionReference.FromProtectedTicket(actor, raw, false, created);
        var snapshot = new SessionSnapshot(rowSession.SessionId, actor, created, rowSession.ExpiresAt, created.AddHours(12), false);
        HotelStructureCreationService Service(HotelOpsDbContext db)
        {
            var context = new DefaultHttpContext { User = HotelOpsCookieTicket.Create(new(reference, snapshot), TimeProvider.System).Principal };
            context.Items[typeof(SessionSnapshot)] = snapshot;
            var current = new CurrentUserAccessor(new HttpContextAccessor { HttpContext = context });
            var permissions = new PermissionService(current, new EfPermissionDataSource(db), new UnresolvedPermissionResourceResolver(), TimeProvider.System);
            return new(db, current, permissions, new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System), TimeProvider.System);
        }
        Task<StructureCreationResult> Invoke(HotelOpsDbContext db, string code, bool area, CancellationToken token) => area
            ? Service(db).CreateAreaAsync(reference, rowSession.SessionId, branch, new(code, "SQL acceptance", floor), token)
            : Service(db).CreateFloorAsync(reference, rowSession.SessionId, branch, new(code, "SQL acceptance"), token);

        foreach (var area in new[] { false, true })
        {
            var suffix = area ? "AREA" : "FLOOR";
            // Actual SQL THROW at the audit command, not an exception fabricated in C#.
            var fault = new FaultCapture { FailAudit = true }; await using (var writer = open(fault))
            {
                var code = "T63_FAULT_" + suffix; var failed = false;
                try { await Invoke(writer, code, area, ct); } catch (DbUpdateException e) when (SqlNumber(e) == 51063) { failed = true; }
                check(failed && fault.Numbers.Contains(51063) && await Effects(code) == 0
                    && writer.Database.CurrentTransaction == null && !writer.ChangeTracker.Entries().Any(),
                    "real SQL audit failure rolls back " + suffix + ", audit and tracker/transaction");
                Failures.Add(new { scenario = "audit-rollback-" + suffix, numbers = fault.Numbers });
            }
            foreach (var scenario in new[] { "lock1222", "command-timeout", "deadlock1205" })
            {
                var code = "T63_" + (scenario == "command-timeout" ? "TIMEOUT" : scenario.ToUpperInvariant()) + "_" + suffix;
                var capture = new FaultCapture();
                await using var writer = open(capture); var writerSpid = await Spid(writer);
                await writer.Database.ExecuteSqlRawAsync("SET DEADLOCK_PRIORITY LOW", ct);
                if (scenario == "lock1222") await writer.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT 1200", ct);
                if (scenario == "command-timeout") writer.Database.SetCommandTimeout(2);
                await using var holder = open(null); var owner = await Spid(holder);
                await holder.Database.ExecuteSqlRawAsync("SET DEADLOCK_PRIORITY HIGH", ct);
                await using var tx = await holder.Database.BeginTransactionAsync(ct);
                await holder.Database.SqlQueryRaw<long>("SELECT TOP(1) audit_id AS [Value] FROM dbo.audit_logs WITH (TABLOCKX,HOLDLOCK)").ToArrayAsync(ct);
                var pending = Invoke(writer, code, area, ct); Task? inverse = null;
                try
                {
                    await Wait(scenario + "-" + suffix, owner, pending, "INSERT INTO [audit_logs]");
                    if (scenario == "deadlock1205")
                    {
                        // Deliberately inverse TEST blocker: Audit -> User versus production
                        // User -> Audit. Forces a real post-insert victim, not canonical safety proof.
                        inverse = new UserEligibilityLock(holder).LockAsync([actor], ct);
                        await Wait("deadlock-inverse-" + suffix, writerSpid, inverse);
                    }
                    var result = await pending;
                    if (inverse != null) await inverse;
                    await tx.RollbackAsync(ct);
                    var expected = scenario == "lock1222" ? 1222 : scenario == "deadlock1205" ? 1205 : -2;
                    check(result.Status == StructureCreationStatus.Unavailable && capture.Numbers.Contains(expected)
                        && await Effects(code) == 0 && writer.Database.CurrentTransaction == null && !writer.ChangeTracker.Entries().Any(),
                        "actual SQL " + expected + " after " + suffix + " INSERT fails closed, no automatic replay or partial write");
                    Failures.Add(new { scenario = scenario + "-" + suffix, numbers = capture.Numbers });
                }
                finally
                {
                    if (holder.Database.CurrentTransaction != null) await tx.RollbackAsync(CancellationToken.None);
                    try { await pending; } catch { } if (inverse != null) try { await inverse; } catch { }
                }
                await using var retry = open(null);
                check((await Invoke(retry, code, area, ct)).Status == StructureCreationStatus.Success && await Effects(code) == 2,
                    "explicit fresh-context retry succeeds once after " + scenario + "-" + suffix);
                await using var duplicate = open(null);
                check((await Invoke(duplicate, code, area, ct)).Status == StructureCreationStatus.DuplicateCode && await Effects(code) == 2,
                    "replayed successful request returns duplicate without second audit/entity: " + suffix);
            }
        }

        // Authorization/session changes that win the User lock are visible to the real service.
        foreach (var scenario in new[] { "actor-inactive", "grant-revoked", "session-expired" })
        {
            var code = "T63_" + scenario.Replace('-', '_').ToUpperInvariant();
            await using var holder = open(null); var owner = await Spid(holder);
            await using var tx = await holder.Database.BeginTransactionAsync(ct);
            await new UserEligibilityLock(holder).LockAsync([actor], ct);
            if (scenario == "actor-inactive") await holder.Users.Where(x => x.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountStatus, "INACTIVE"), ct);
            if (scenario == "grant-revoked") await holder.UserRoles.Where(x => x.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
            if (scenario == "session-expired") await holder.UserSessions.Where(x => x.SessionId == rowSession.SessionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)), ct);
            await using var writer = open(null); var pending = Invoke(writer, code, false, ct);
            try
            {
                await Wait(scenario, owner, pending); await tx.CommitAsync(ct); var result = await pending;
                check(result.Status == (scenario == "grant-revoked" ? StructureCreationStatus.Forbidden : StructureCreationStatus.SessionInvalid)
                    && await Effects(code) == 0, "SQL authority reread after User wait rejects " + scenario);
            }
            finally
            {
                if (holder.Database.CurrentTransaction != null) await tx.RollbackAsync(CancellationToken.None);
                try { await pending; } catch { }
                await setup.Users.Where(x => x.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountStatus, "ACTIVE"), ct);
                await setup.UserRoles.Where(x => x.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true), ct);
                await setup.UserSessions.Where(x => x.SessionId == rowSession.SessionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, rowSession.ExpiresAt), ct);
            }
        }

        var expiredJar = await login("admin");
        var expiredId = await setup.UserSessions.Where(x => x.UserId == actor).OrderByDescending(x => x.SessionId).Select(x => x.SessionId).FirstAsync(ct);
        await setup.UserSessions.Where(x => x.SessionId == expiredId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)), ct);
        using var expiredResponse = await post(Route(), expiredJar, new { code = "DENIED_EXPIRED", name = "Expired" }, ct);
        check(expiredResponse.StatusCode == HttpStatusCode.Unauthorized && await Effects("DENIED_EXPIRED") == 0, "expired SQL session denies actual HTTPS creation");
        var scopedJar = await login("branch");
        await setup.UserBranchAccesses.Where(x => x.UserId == identities["branch"].UserId).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
        try
        {
            using var response = await post(Route(), scopedJar, new { code = "DENIED_MEMBERSHIP", name = "Revoked branch" }, ct);
            check(response.StatusCode == HttpStatusCode.Forbidden && await Effects("DENIED_MEMBERSHIP") == 0, "revoked SQL branch membership invalidates branch-bound management grant");
        }
        finally { await setup.UserBranchAccesses.Where(x => x.UserId == identities["branch"].UserId).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, (DateTime?)null), ct); }
    }

    private static int? SqlNumber(Exception error)
    { for (Exception? e = error; e != null; e = e.InnerException) if (e is SqlException sql) return sql.Number; return null; }
    private sealed class SqlWait
    {
        public int SessionId { get; set; }
        public string Kind { get; set; } = "";
        public string Query { get; set; } = "";
    }
    private sealed class FaultCapture : DbCommandInterceptor
    {
        internal bool FailAudit;
        internal List<int> Numbers { get; } = [];
        private void Inspect(DbCommand command)
        {
            if (FailAudit && command.CommandText.Contains("INSERT INTO [audit_logs]", StringComparison.Ordinal))
                command.CommandText = ";THROW 51063, 'Task63 deliberately failed audit SQL', 1;";
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        { Inspect(command); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        { Inspect(command); return ValueTask.FromResult(result); }
        public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData data, CancellationToken ct = default)
        { if (SqlNumber(data.Exception) is { } number) Numbers.Add(number); return Task.CompletedTask; }
    }
}
