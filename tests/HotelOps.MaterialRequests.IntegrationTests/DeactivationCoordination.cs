using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Inventory;
using HotelOps.Api.Services.MaterialRequests;
using HotelOps.Api.Services.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Service-level real-SQL harness only. No HTTP, API process, Inventory fixture
// setup or production Deactivate implementation is exercised here.
// Adapted to BASE DTO ActorUserId and explicit approver selection; no MR Authz.
internal sealed class DeactivationCoordination(string connection)
{
    const string DraftNo = "RC-D16-MR-DRAFT", PendingNo = "RC-D16-MR-PENDING";
    const string Catalog = "HotelOps:Inventory:Catalog";
    const long Requester = 4, Approver = 1;
    readonly List<Fixture> fixtures = [];
    readonly string runId = Guid.NewGuid().ToString("N")[..8];

    sealed record Fixture(State Original)
    {
        public string Name => Original.Request.RequestNo;
        public long Id => Original.Request.RequestId;
        // IDs captured from these exact service calls, never a blanket audit
        // deletion based only on request ID or a database-wide high-water mark.
        public HashSet<long> GeneratedAudits { get; } = [];
        public HashSet<long> GeneratedDecisions { get; } = [];
        public HashSet<long> GeneratedCycles { get; } = [];
        public HashSet<long> GeneratedSnapshots { get; } = [];
    }
    sealed record State(MaterialRequest Request, List<MaterialRequestLine> Lines,
        List<MaterialRequestApprovalCycle> Cycles, List<MaterialRequestApprovalLineSnapshot> Snapshots,
        List<MaterialRequestApproval> Decisions, List<AuditLog> Audits);
    enum ProbeResult { Blocked, Deactivated }
    sealed record WriterResult(MaterialRequestDto? Request, Exception? Failure);

    internal static async Task Run()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__HotelOpsDb");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Supply ConnectionStrings__HotelOpsDb; no other connection source is accepted.");
        await new DeactivationCoordination(connection).Execute();
    }

    HotelOpsDbContext Db() => new(new DbContextOptionsBuilder<HotelOpsDbContext>()
        .UseSqlServer(connection, sql => sql.CommandTimeout(30)).Options);
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("RC-D16: " + message);
    }

    // Keep every worker connection explicitly open so its captured SPID remains
    // the SPID executing the operation, even across multiple EF commands.
    sealed class Session(HotelOpsDbContext db, int spid) : IAsyncDisposable
    {
        public HotelOpsDbContext Db { get; } = db;
        public int Spid { get; } = spid;
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    async Task<Session> Open(CancellationToken ct = default)
    {
        var db = Db();
        try
        {
            await db.Database.OpenConnectionAsync(ct);
            var server = await db.Database.SqlQueryRaw<string>("SELECT CONVERT(nvarchar(128), @@SERVERNAME) AS Value").SingleAsync(ct);
            var database = await db.Database.SqlQueryRaw<string>("SELECT CONVERT(nvarchar(128), DB_NAME()) AS Value").SingleAsync(ct);
            Check(server == "KIENPC" && database == "HotelOps_Canonical_DEV2_Regression", "Forbidden SQL target; no fixture writes permitted.");
            var spid = await db.Database.SqlQueryRaw<int>("SELECT CONVERT(int, @@SPID) AS Value").SingleAsync(ct);
            return new(db, spid);
        }
        catch { await db.DisposeAsync(); throw; }
    }

    static async Task AppLock(HotelOpsDbContext db, string resource, CancellationToken ct, int timeout = 15000)
    {
        Check(db.Database.CurrentTransaction != null, "Application lock requires its caller transaction.");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive',
                @LockOwner='Transaction', @LockTimeout={timeout};
            IF @result < 0 THROW 51000, 'RC-D16 application lock unavailable', 1;
            """, ct);
    }

    async Task Execute()
    {
        await using var lease = await Open();
        await using var leaseTx = await lease.Db.Database.BeginTransactionAsync();
        // Prevent two copies of this harness from resetting each other's fixtures.
        await AppLock(lease.Db, "RC-D16:MR-Deactivation:Harness", default, timeout: 0);
        await using var observer = await Open();
        foreach (var name in new[] { DraftNo, PendingNo })
        {
            var state = await ReadState(observer.Db, name, default);
            await ValidateFixture(observer.Db, state);
            fixtures.Add(new(state));
        }
        await AssertUsers(observer.Db, null, default);
        foreach (var user in new[] { Approver, Requester })
            Check(!await HasResponsibility(observer.Db, user, excludeDedicated: true, default),
                $"User {user} has an unrelated MR blocker. Use an isolated regression fixture; this harness cannot ignore or edit that blocker.");
        var untouched = await Fingerprints(observer.Db);

        // Cleanup becomes armed only after BOTH existing fixtures and the SQL
        // target have passed preflight. A preflight failure never resets data.
        var failures = new List<Exception>();
        try
        {
            await Race(fixtures[0], submit: true, deactivationWins: true, "A");
            await Race(fixtures[0], submit: true, deactivationWins: false, "B");
            await Race(fixtures[1], submit: false, deactivationWins: true, "C");
            await Race(fixtures[1], submit: false, deactivationWins: false, "D");
        }
        catch (Exception e) { failures.Add(e); }
        finally
        {
            // Race workers have been cancelled, drained and disposed before any
            // reset. Use new sessions/tokens so cancellation cannot skip cleanup.
            try { await RestoreAndAssert(); } catch (Exception e) { failures.Add(e); }
            try
            {
                await using var final = await Open();
                var after = await Fingerprints(final.Db);
                Check(untouched.Count == after.Count && untouched.All(x => after.TryGetValue(x.Key, out var value) && value == x.Value),
                    "Unrelated SQL state or protected User fields changed; run only against an idle regression database.");
            }
            catch (Exception e) { failures.Add(e); }
        }
        if (failures.Count != 0) throw new AggregateException("RC-D16 coordination failed; see assertion/restoration errors.", failures);
        Console.WriteLine("PASS RC-D16 MR deactivation coordination; races=4; SQL waits proven; fixtures restored.");
    }

    async Task Race(Fixture fixture, bool submit, bool deactivationWins, string letter)
    {
        var name = (submit ? "submit" : "request-information") + (deactivationWins ? " deactivation-wins" : " writer-wins");
        var failures = new List<Exception>();
        try
        {
            // Explicitly authorized TEST-ONLY isolation: the sibling fixture is
            // itself a blocker for the other race's target. Cancel it through the
            // production service, OUTSIDE DeactivationProbe, then restore it in
            // finally. The probe still checks ALL MR responsibilities globally.
            var sibling = fixtures.Single(x => x.Id != fixture.Id);
            await using (var setup = await Open())
            {
                var isolated = await Write(setup.Db, sibling, Requester, "cancel", default);
                Check(isolated.Failure is null && isolated.Request?.RawStatus == "CANCELLED", "Sibling fixture isolation failed.");
            }
            await ConcurrentRace(fixture, submit, deactivationWins, letter);
        }
        catch (Exception e) { failures.Add(e); }
        finally
        {
            try { await RestoreAndAssert(); } catch (Exception e) { failures.Add(e); }
        }
        if (failures.Count != 0) throw new AggregateException("RC-D16 " + name + " failed.", failures);
        Console.WriteLine("PASS RC-D16 " + name + "; SQL waits proven; fixtures restored.");
    }

    async Task ConcurrentRace(Fixture fixture, bool submit, bool deactivationWins, string letter)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var ct = cancellation.Token;
        await using var blocker = await Open(ct);
        await using var writer = await Open(ct);
        await using var probe = await Open(ct);
        await using var observer = await Open(ct);
        Check(new[] { blocker.Spid, writer.Spid, probe.Spid, observer.Spid }.Distinct().Count() == 4, "Race sessions must be distinct.");
        Console.WriteLine($"SQL SESSIONS race={letter}; blocker={blocker.Spid}; writer={writer.Spid}; probe={probe.Spid}; observer={observer.Spid}");
        var target = submit ? Approver : Requester;
        var resource = deactivationWins ? $"RC-D16:{runId}:{letter}" : Catalog;
        await using var blockerTx = await blocker.Db.Database.BeginTransactionAsync(ct);
        await AppLock(blocker.Db, resource, ct);
        Task<WriterResult>? writing = null;
        Task<ProbeResult>? deactivating = null;
        var released = false;
        var failures = new List<Exception>();
        try
        {
            if (deactivationWins)
            {
                deactivating = DeactivationProbe(probe.Db, target, resource, ct);
                await WaitForSqlLock(observer, probe.Spid, blocker.Spid, resource, null, deactivating, ct);
                writing = Write(writer.Db, fixture, submit ? Requester : Approver, submit ? "submit" : "request-information", ct);
                await WaitForSqlLock(observer, writer.Spid, probe.Spid, null, target, writing, ct);
            }
            else
            {
                writing = Write(writer.Db, fixture, submit ? Requester : Approver, submit ? "submit" : "request-information", ct);
                await WaitForSqlLock(observer, writer.Spid, blocker.Spid, Catalog, null, writing, ct);
                deactivating = DeactivationProbe(probe.Db, target, null, ct);
                await WaitForSqlLock(observer, probe.Spid, writer.Spid, null, target, deactivating, ct);
            }
            await blockerTx.CommitAsync(ct);
            released = true;
            var result = await writing;
            var deactivation = await deactivating;
            Check(deactivation == (deactivationWins ? ProbeResult.Deactivated : ProbeResult.Blocked), "Unexpected deactivation winner/blocker.");
            if (deactivationWins)
            {
                Check(result.Request is null, "Writer committed a responsibility after target deactivation.");
                // BASE service keeps its ValidationException/rollback contract;
                // no Authz routing exception or routing-failure audit is imported.
                Check(result.Failure is ValidationException validation && validation.Message.StartsWith("Future responsibility requires", StringComparison.Ordinal),
                    "Writer must fail User eligibility, not an unrelated validation.");
            }
            else Check(result.Failure is null && result.Request != null, "Expected exactly one committed production writer.");
            await AssertDurable(observer.Db, fixture, submit, deactivationWins, ct);
        }
        catch (Exception e) { failures.Add(e); }
        finally
        {
            cancellation.Cancel();
            if (!released)
                try { await blockerTx.RollbackAsync(CancellationToken.None); } catch (Exception e) { failures.Add(e); }
            // Always observe both tasks, including an operation whose SQL wait
            // assertion failed. Never restore while a worker can still commit.
            foreach (var task in new Task?[] { writing, deactivating })
            {
                if (task is null) continue;
                try { await task; }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception e) { failures.Add(e); }
            }
        }
        if (failures.Count != 0) throw new AggregateException("RC-D16 SQL race failed.", failures);
    }

    async Task<WriterResult> Write(HotelOpsDbContext db, Fixture fixture, long actor, string command, CancellationToken ct)
    {
        // Fetch the actual current token in this writer's own SQL session.
        var version = await db.MaterialRequests.AsNoTracking()
            .Where(x => x.RequestNo == fixture.Name && x.RequestId == fixture.Id).Select(x => x.RowVersion).SingleAsync(ct);
        var input = new MaterialCommand(actor, Convert.ToBase64String(version),
            command == "submit" ? null : "RC-D16 coordination fixture operation", command == "submit" ? Approver : null);
        var inv = new InventoryService(db);
        try { return new(await new MaterialRequestService(db, inv, inv).Command(fixture.Id, command, input, ct), null); }
        catch (ValidationException e) { return new(null, e); }
        finally
        {
            var entityId = fixture.Id.ToString(CultureInfo.InvariantCulture);
            foreach (var entry in db.ChangeTracker.Entries<MaterialRequestApprovalCycle>())
                if (entry.Entity.ApprovalCycleId > 0 && entry.Entity.RequestId == fixture.Id
                    && !fixture.Original.Cycles.Any(x => x.ApprovalCycleId == entry.Entity.ApprovalCycleId))
                    fixture.GeneratedCycles.Add(entry.Entity.ApprovalCycleId);
            foreach (var entry in db.ChangeTracker.Entries<MaterialRequestApprovalLineSnapshot>())
                if (entry.Entity.SnapshotId > 0 && fixture.GeneratedCycles.Contains(entry.Entity.ApprovalCycleId)
                    && !fixture.Original.Snapshots.Any(x => x.SnapshotId == entry.Entity.SnapshotId))
                    fixture.GeneratedSnapshots.Add(entry.Entity.SnapshotId);
            foreach (var entry in db.ChangeTracker.Entries<MaterialRequestApproval>())
                if (entry.Entity.ApprovalId > 0 && entry.Entity.RequestId == fixture.Id
                    && !fixture.Original.Decisions.Any(x => x.ApprovalId == entry.Entity.ApprovalId))
                    fixture.GeneratedDecisions.Add(entry.Entity.ApprovalId);
            foreach (var entry in db.ChangeTracker.Entries<AuditLog>())
                if (entry.Entity.AuditId > 0 && entry.Entity.EntityType == "material_requests" && entry.Entity.EntityId == entityId
                    && !fixture.Original.Audits.Any(x => x.AuditId == entry.Entity.AuditId))
                    fixture.GeneratedAudits.Add(entry.Entity.AuditId);
        }
    }

    // TEST-ONLY contractual protocol emulator. This is NOT DEV1 production
    // Deactivate Account and proves only the agreed MR responsibility predicates.
    static async Task<ProbeResult> DeactivationProbe(HotelOpsDbContext db, long target, string? barrier, CancellationToken ct)
    {
        Check(target is Approver or Requester, "Probe may change only Users 1/4.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var locked = await new UserEligibilityLock(db).LockAsync([target], ct);
        UserEligibilityLock.RequireEligible(target, locked);
        if (barrier != null) await AppLock(db, barrier, ct);
        // No discovery/cache/fixture filter: reevaluate fresh SQL AFTER the
        // retained User lock and after any test barrier has been released.
        if (await HasResponsibility(db, target, excludeDedicated: false, ct))
        {
            await tx.RollbackAsync(ct);
            return ProbeResult.Blocked;
        }
        var affected = await db.Users.Where(x => (x.UserId == Approver || x.UserId == Requester) && x.UserId == target)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.AccountStatus, "INACTIVE"), ct);
        Check(affected == 1, "Probe must update exactly its target User.");
        await tx.CommitAsync(ct);
        return ProbeResult.Deactivated;
    }

    static Task<bool> HasResponsibility(HotelOpsDbContext db, long user, bool excludeDedicated, CancellationToken ct) =>
        db.MaterialRequests.AsNoTracking().Where(r => !excludeDedicated || r.RequestNo != DraftNo && r.RequestNo != PendingNo)
            .AnyAsync(r => r.RequesterId == user && (r.Status == "DRAFT" || r.Status == "NEEDS_INFORMATION")
                || r.Status == "PENDING_APPROVAL" && db.MaterialRequestApprovalCycles.Any(c => c.RequestId == r.RequestId
                    && c.Status == "PENDING" && c.AssignedApproverId == user
                    && !db.MaterialRequestApprovalCycles.Any(newer => newer.RequestId == r.RequestId && newer.RevisionNo > c.RevisionNo)), ct);

    sealed class WaitEvidence
    {
        public string WaitType { get; set; } = "";
        public string ResourceType { get; set; } = "";
        public string ResourceDescription { get; set; } = "";
        public string RequestedMode { get; set; } = "";
        public string RetainedMode { get; set; } = "";
    }
    static async Task WaitForSqlLock(Session observer, int waiter, int blocker, string? application, long? target,
        Task operation, CancellationToken ct)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            // Match the actual waiting session AND its blocking session AND
            // their identical lock-manager resource, not a global WAIT count.
            // HoBT -> sys.partitions identifies KEY/PAGE/RID locks on dbo.users.
            var evidence = await observer.Db.Database.SqlQuery<WaitEvidence>($"""
                SELECT TOP (1) r.wait_type AS WaitType, w.resource_type AS ResourceType,
                    COALESCE(w.resource_description, '') AS ResourceDescription,
                    w.request_mode AS RequestedMode, g.request_mode AS RetainedMode
                FROM sys.dm_exec_requests AS r
                JOIN sys.dm_tran_locks AS w ON w.request_session_id = r.session_id
                JOIN sys.dm_tran_locks AS g ON g.request_session_id = {blocker}
                    AND g.resource_database_id = w.resource_database_id
                    AND g.resource_type = w.resource_type
                    AND COALESCE(g.resource_subtype, '') = COALESCE(w.resource_subtype, '')
                    AND g.resource_associated_entity_id = w.resource_associated_entity_id
                    AND g.resource_lock_partition = w.resource_lock_partition
                    AND COALESCE(g.resource_description, '') = COALESCE(w.resource_description, '')
                WHERE r.session_id = {waiter} AND r.blocking_session_id = {blocker}
                    AND r.wait_type LIKE 'LCK_M_%' AND w.resource_database_id = DB_ID()
                    AND w.request_status IN ('WAIT', 'CONVERT') AND g.request_status = 'GRANT'
                    AND w.request_owner_type = 'TRANSACTION' AND g.request_owner_type = 'TRANSACTION'
                    AND (({application} IS NOT NULL AND w.resource_type = 'APPLICATION'
                            AND w.resource_description LIKE '%' + {application} + '%' AND g.request_mode = 'X')
                        OR ({application} IS NULL AND w.request_mode IN ('U', 'X', 'RangeS-U', 'RangeX-X')
                            AND g.request_mode IN ('U', 'X', 'RangeS-U', 'RangeX-X')
                            AND ((w.resource_type IN ('KEY', 'PAGE', 'RID') AND EXISTS
                                (SELECT 1 FROM sys.partitions p WHERE p.hobt_id = w.resource_associated_entity_id
                                    AND p.object_id = OBJECT_ID(N'dbo.users')))
                                OR (w.resource_type = 'OBJECT' AND w.resource_associated_entity_id = OBJECT_ID(N'dbo.users')))))
                """).ToListAsync(ct);
            if (evidence.Count != 0)
            {
                var proof = evidence[0];
                Check(!operation.IsCompleted, "Operation finished while its SQL barrier was being observed.");
                Console.WriteLine($"SQL WAIT waiter={waiter}; blocker={blocker}; {proof.WaitType}; {proof.ResourceType}; " +
                    $"requested={proof.RequestedMode}; retained={proof.RetainedMode}; resource={application ?? "dbo.users target=" + target}; {proof.ResourceDescription}");
                return;
            }
            if (operation.IsCompleted)
            {
                await operation; // Surface unexpected SQL/worker exceptions.
                throw new InvalidOperationException($"RC-D16: session {waiter} completed without the required SQL wait on session {blocker}.");
            }
            await Task.Delay(50, ct); // Poll cadence only; never accepted as proof.
        }
        throw new TimeoutException($"RC-D16: no SQL lock-manager proof for waiter {waiter}, blocker {blocker}, resource {application ?? "dbo.users"}. Check DMV permissions and fixture prerequisites.");
    }

    static async Task<State> ReadState(HotelOpsDbContext db, string name, CancellationToken ct)
    {
        Check(name is DraftNo or PendingNo, "Only dedicated fixture names may be read/reset as fixtures.");
        var request = await db.MaterialRequests.AsNoTracking().SingleAsync(x => x.RequestNo == name, ct);
        var lines = await db.MaterialRequestLines.AsNoTracking().Where(x => x.RequestId == request.RequestId).OrderBy(x => x.LineId).ToListAsync(ct);
        var cycles = await db.MaterialRequestApprovalCycles.AsNoTracking().Where(x => x.RequestId == request.RequestId).OrderBy(x => x.RevisionNo).ToListAsync(ct);
        var snapshots = await db.MaterialRequestApprovalLineSnapshots.AsNoTracking().Where(x => x.ApprovalCycle.RequestId == request.RequestId).OrderBy(x => x.SnapshotId).ToListAsync(ct);
        var decisions = await db.MaterialRequestApprovals.AsNoTracking().Where(x => x.RequestId == request.RequestId).OrderBy(x => x.ApprovalId).ToListAsync(ct);
        var entityId = request.RequestId.ToString(CultureInfo.InvariantCulture);
        var audits = await db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "material_requests" && x.EntityId == entityId).OrderBy(x => x.AuditId).ToListAsync(ct);
        return new(request, lines, cycles, snapshots, decisions, audits);
    }

    static string? Event(AuditLog audit)
    {
        using var json = JsonDocument.Parse(audit.NewValues ?? "{}");
        return json.RootElement.TryGetProperty("BusinessEvent", out var value) ? value.GetString() : null;
    }
    static async Task ValidateFixture(HotelOpsDbContext db, State state)
    {
        var r = state.Request;
        Check(r.RequesterId == Requester && r.SourceWarehouseId == 3 && state.Lines.Count(x => x.IsActive) == 1, r.RequestNo + " requester/warehouse/active-line mismatch.");
        Check(state.Decisions.Count == 0 && !await db.MaterialFulfillments.AnyAsync(x => x.RequestId == r.RequestId), r.RequestNo + " must have no decisions/fulfillments.");
        Check(state.Audits.Count(x => Event(x) == "REQUEST_CREATED") == 1, r.RequestNo + " must retain exactly one REQUEST_CREATED seed audit.");
        if (r.RequestNo == DraftNo)
            Check(r.Status == "DRAFT" && r.SubmittedAt == null && state.Cycles.Count == 0 && state.Snapshots.Count == 0, "DRAFT initial state mismatch.");
        else
        {
            Check(r.Status == "PENDING_APPROVAL" && state.Cycles.Count == 1, "PENDING initial state mismatch.");
            var cycle = state.Cycles.Single();
            Check(cycle.RevisionNo == 1 && cycle.SubmittedBy == Requester && cycle.AssignedApproverId == Approver
                && cycle.Status == "PENDING" && cycle.ResolvedAt == null, "PENDING initial cycle mismatch.");
            Check(state.Snapshots.Count == 1 && state.Snapshots[0].ApprovalCycleId == cycle.ApprovalCycleId
                && state.Snapshots[0].RequestLineId == state.Lines.Single(x => x.IsActive).LineId, "PENDING seed snapshot mismatch.");
        }
    }

    static async Task AssertUsers(HotelOpsDbContext db, long? inactive, CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().Where(x => x.UserId == Approver || x.UserId == Requester)
            .Select(x => new { x.UserId, x.DeletedAt, x.AccountStatus, x.EmploymentStatus }).ToListAsync(ct);
        Check(users.Count == 2 && users.All(x => x.DeletedAt == null && x.EmploymentStatus == "ACTIVE"
            && x.AccountStatus == (x.UserId == inactive ? "INACTIVE" : "ACTIVE")), "Unexpected User eligibility state.");
    }

    // Compare mapped scalar values only: no navigation fixup/cycles, and no
    // attempt to restore SQL-generated rowversion tokens to their old bytes.
    static string Scalars<T>(HotelOpsDbContext db, IEnumerable<T> rows) where T : class => JsonSerializer.Serialize(
        rows.Select(row => db.Model.FindEntityType(typeof(T))!.GetProperties().Where(p => p.Name != "RowVersion")
            .OrderBy(p => p.Name).Select(p => new { p.Name, Value = p.PropertyInfo!.GetValue(row) }).ToArray()).ToArray());
    static void SameState(HotelOpsDbContext db, State original, State current, bool audits)
    {
        Check(Scalars(db, new[] { original.Request }) == Scalars(db, new[] { current.Request })
            && Scalars(db, original.Lines) == Scalars(db, current.Lines)
            && Scalars(db, original.Cycles) == Scalars(db, current.Cycles)
            && Scalars(db, original.Snapshots) == Scalars(db, current.Snapshots)
            && Scalars(db, original.Decisions) == Scalars(db, current.Decisions)
            && (!audits || Scalars(db, original.Audits) == Scalars(db, current.Audits)), original.Request.RequestNo + " was not preserved/restored exactly (excluding rowversion).");
    }

    static async Task AssertDurable(HotelOpsDbContext db, Fixture fixture, bool submit, bool deactivationWins, CancellationToken ct)
    {
        var current = await ReadState(db, fixture.Name, ct);
        await AssertUsers(db, deactivationWins ? submit ? Approver : Requester : null, ct);
        var newAudits = current.Audits.Where(x => !fixture.Original.Audits.Any(a => a.AuditId == x.AuditId)).ToArray();
        Check(newAudits.All(x => fixture.GeneratedAudits.Contains(x.AuditId)), "Unexpected audit writer touched the dedicated fixture.");
        if (deactivationWins)
        {
            SameState(db, fixture.Original, current, audits: true);
            Check(newAudits.Length == 0, "BASE eligibility rejection must roll back all request/cycle/audit changes.");
            return;
        }
        Check(current.Cycles.Count == 1, "Expected exactly one current cycle / submit winner.");
        var cycle = current.Cycles.Single();
        Check(cycle.RevisionNo == 1 && cycle.AssignedApproverId == Approver && cycle.SubmittedBy == Requester, "Wrong current cycle ownership.");
        if (submit)
        {
            Check(current.Request.Status == "PENDING_APPROVAL" && current.Request.SubmittedAt != null
                && cycle.Status == "PENDING" && cycle.ResolvedAt == null && current.Decisions.Count == 0, "Submit winner state mismatch.");
            var line = current.Lines.Single(x => x.IsActive);
            Check(current.Snapshots.Count == 1 && current.Snapshots[0].RequestLineId == line.LineId
                && current.Snapshots[0].ApprovalCycleId == cycle.ApprovalCycleId && line.ApprovedQty == null
                && current.Snapshots[0].RequestedQty == line.RequestedQty, "Submit winner snapshot mismatch.");
        }
        else
        {
            Check(current.Request.Status == "NEEDS_INFORMATION" && cycle.Status == "NEEDS_INFORMATION"
                && cycle.ResolvedAt != null && current.Decisions.Count == 1, "Request-information winner state mismatch.");
            var decision = current.Decisions.Single();
            Check(decision.Decision == "RETURNED" && decision.ApproverId == Approver && decision.ApprovalCycleId == cycle.ApprovalCycleId,
                "Expected exactly one RETURNED decision for the current assigned approver.");
            Check(Scalars(db, fixture.Original.Snapshots) == Scalars(db, current.Snapshots), "Request-information changed the seed snapshot.");
            Check(Scalars(db, fixture.Original.Lines) == Scalars(db, current.Lines), "Request-information changed request line values.");
        }
        Check(newAudits.Length == 1 && Event(newAudits[0]) == (submit ? "REQUEST_SUBMITTED" : "REQUEST_INFORMATION_REQUESTED")
            && newAudits[0].ActorUserId == (submit ? Requester : Approver), "Winner audit mismatch.");
        Check(await HasResponsibility(db, submit ? Approver : Requester, excludeDedicated: false, ct),
            "Committed writer must leave a fresh MR blocker visible to deactivation.");
    }

    async Task RestoreAndAssert()
    {
        await using var session = await Open();
        var db = session.Db;
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            // Restoration uses the same Users -> resources order. Users may be
            // INACTIVE because deactivation won; restore only AccountStatus.
            await new UserEligibilityLock(db).LockAsync([Approver, Requester], CancellationToken.None);
            await AppLock(db, Catalog, CancellationToken.None);
            foreach (var fixture in fixtures)
            {
                Check(fixture.Name is DraftNo or PendingNo, "Refusing reset outside dedicated request names.");
                var r = await db.MaterialRequests.SingleAsync(x => x.RequestNo == fixture.Name && x.RequestId == fixture.Id);
                var original = fixture.Original;
                var cycleIds = original.Cycles.Select(x => x.ApprovalCycleId).ToArray();
                var snapshotIds = original.Snapshots.Select(x => x.SnapshotId).ToArray();
                var newCycles = fixture.GeneratedCycles.ToArray();
                var newSnapshots = fixture.GeneratedSnapshots.ToArray();
                var newDecisions = fixture.GeneratedDecisions.ToArray();
                // Initially both fixtures have zero decisions. Every mutation is
                // constrained by request_no, the originally discovered ID AND
                // row IDs captured from these exact production service calls.
                await db.MaterialRequestApprovals.Where(x => x.RequestId == fixture.Id && x.Request.RequestNo == fixture.Name
                    && newDecisions.Contains(x.ApprovalId)).ExecuteDeleteAsync();
                await db.MaterialRequestApprovalLineSnapshots.Where(x => x.ApprovalCycle.RequestId == fixture.Id
                    && x.ApprovalCycle.Request.RequestNo == fixture.Name && newSnapshots.Contains(x.SnapshotId)
                    && !snapshotIds.Contains(x.SnapshotId)).ExecuteDeleteAsync();
                await db.MaterialRequestApprovalCycles.Where(x => x.RequestId == fixture.Id && x.Request.RequestNo == fixture.Name
                    && newCycles.Contains(x.ApprovalCycleId) && !cycleIds.Contains(x.ApprovalCycleId)).ExecuteDeleteAsync();
                foreach (var old in original.Cycles)
                {
                    var cycle = await db.MaterialRequestApprovalCycles.SingleAsync(x => x.ApprovalCycleId == old.ApprovalCycleId
                        && x.RequestId == fixture.Id && x.Request.RequestNo == fixture.Name);
                    cycle.Status = old.Status; cycle.ResolvedAt = old.ResolvedAt;
                }
                foreach (var old in original.Lines)
                {
                    var line = await db.MaterialRequestLines.SingleAsync(x => x.LineId == old.LineId && x.RequestId == fixture.Id && x.Request.RequestNo == fixture.Name);
                    line.ApprovedQty = old.ApprovedQty; line.FulfilledQty = old.FulfilledQty;
                }
                r.Status = original.Request.Status; r.SubmittedAt = original.Request.SubmittedAt;
                r.UpdatedAt = original.Request.UpdatedAt; r.CancellationReason = original.Request.CancellationReason;
                var ids = fixture.GeneratedAudits.ToArray();
                var entityId = fixture.Id.ToString(CultureInfo.InvariantCulture);
                await db.AuditLogs.Where(x => x.EntityType == "material_requests" && x.EntityId == entityId && ids.Contains(x.AuditId)
                    && db.MaterialRequests.Any(request => request.RequestId == fixture.Id && request.RequestNo == fixture.Name)).ExecuteDeleteAsync();
            }
            await db.Users.Where(x => (x.UserId == Approver || x.UserId == Requester) && x.AccountStatus == "INACTIVE")
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.AccountStatus, "ACTIVE"));
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        await AssertUsers(db, null, default);
        foreach (var fixture in fixtures)
        {
            var after = await ReadState(db, fixture.Name, default);
            await ValidateFixture(db, after);
            SameState(db, fixture.Original, after, audits: true);
        }
    }

    async Task<Dictionary<string, string>> Fingerprints(HotelOpsDbContext db)
    {
        // SQL hashes protect unrelated rows without retrieving User credentials.
        // Comparing database-wide protected rows also detects interference from
        // external writers; this harness requires an idle regression database.
        var draft = fixtures.Single(x => x.Name == DraftNo).Id;
        var pending = fixtures.Single(x => x.Name == PendingNo).Id;
        var queries = new Dictionary<string, string>
        {
            ["users (all non-rowversion fields, including restored targets)"] = "SELECT u.user_id, u.employee_code, u.username, u.email, u.password_hash, u.full_name, u.phone, u.avatar_url, u.primary_branch_id, u.department_id, u.manager_user_id, u.employment_status, u.account_status, u.last_login_at, u.created_at, u.updated_at, u.deleted_at, u.failed_login_count, u.locked_until, u.temporary_password_expires_at FROM dbo.users u ORDER BY u.user_id",
            ["other requests"] = "SELECT r.* FROM dbo.material_requests r WHERE r.request_no NOT IN ('RC-D16-MR-DRAFT','RC-D16-MR-PENDING') ORDER BY r.request_id",
            ["other lines"] = "SELECT l.* FROM dbo.material_request_lines l WHERE l.request_id NOT IN (@draft,@pending) ORDER BY l.line_id",
            ["other cycles"] = "SELECT c.* FROM dbo.material_request_approval_cycles c WHERE c.request_id NOT IN (@draft,@pending) ORDER BY c.approval_cycle_id",
            ["other snapshots"] = "SELECT s.* FROM dbo.material_request_approval_line_snapshots s JOIN dbo.material_request_approval_cycles c ON c.approval_cycle_id=s.approval_cycle_id WHERE c.request_id NOT IN (@draft,@pending) ORDER BY s.snapshot_id",
            ["other decisions"] = "SELECT a.* FROM dbo.material_request_approvals a WHERE a.request_id NOT IN (@draft,@pending) ORDER BY a.approval_id",
            ["other audits"] = "SELECT a.* FROM dbo.audit_logs a WHERE a.entity_type <> 'material_requests' OR a.entity_id NOT IN (CONVERT(varchar(30),@draft),CONVERT(varchar(30),@pending)) ORDER BY a.audit_id",
            ["fulfillments"] = "SELECT f.* FROM dbo.material_fulfillments f ORDER BY f.fulfillment_id",
            ["fulfillment lines"] = "SELECT l.* FROM dbo.material_fulfillment_lines l ORDER BY l.fulfillment_line_id"
        };
        var result = new Dictionary<string, string>();
        foreach (var (name, query) in queries)
        {
            var sql = "SELECT CONVERT(varchar(64), HASHBYTES('SHA2_256', (" + query + " FOR JSON PATH, INCLUDE_NULL_VALUES)), 2) AS Value";
            result[name] = await db.Database.SqlQueryRaw<string>(sql, new SqlParameter("@draft", draft), new SqlParameter("@pending", pending)).SingleAsync();
        }
        return result;
    }
}
