using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Assets;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Assets;
using HotelOps.Api.Services.Inventory;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

// Explicit opt-in only. Direct production service tests, not HTTP/Authz tests.
internal sealed class ResponsibilityCoordinationChecks
{
    readonly string connection;
    readonly CancellationToken ct;
    readonly long actor, target, source, inactive, unemployed, work, warehouse, createAsset, handover, returnWork;
    long returnIncident;
    string inactiveStatus = "";
    const string FixturePrefix = "RC_D4_";
    ResponsibilityCoordinationChecks(string connection, CancellationToken ct)
    {
        this.connection = connection; this.ct = ct;
        actor = Id("ACTOR"); target = Id("TARGET"); source = Id("SOURCE"); inactive = Id("INACTIVE"); unemployed = Id("EMPLOYMENT_INACTIVE");
        work = Id("WORK"); warehouse = Id("WAREHOUSE"); createAsset = Id("CREATE_ASSET"); handover = Id("HANDOVER");
        returnWork = Id("RETURN_WORK");
    }
    static long Id(string name) => long.TryParse(Environment.GetEnvironmentVariable("HOTEL_OPS_COORD_" + name + "_ID"), out var id) && id > 0
        ? id : throw new InvalidOperationException("Supply positive HOTEL_OPS_COORD_" + name + "_ID for a dedicated external fixture.");
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    HotelOpsDbContext Db(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<HotelOpsDbContext>()
        .UseSqlServer(connection).AddInterceptors(interceptors).Options);

    public static async Task Run()
    {
        if (Environment.GetEnvironmentVariable("HOTEL_OPS_COORD_RUNTIME_APPROVED") != "RC-D4-AUDITED")
            throw new InvalidOperationException("RC-D4 SQL runtime is disabled pending independent audit. No SQL was accessed.");
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__HotelOpsDb")
            ?? throw new InvalidOperationException("Supply ConnectionStrings__HotelOpsDb externally.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var checks = new ResponsibilityCoordinationChecks(connection, timeout.Token);
        await checks.Preflight();
        await checks.HelperContract();
        foreach (var writer in new Func<HotelOpsDbContext, long, Task>[] { checks.Assign, checks.Manage, checks.Propose })
        {
            await using (var db = checks.Db(new RollbackAtCommit())) await Reject(() => writer(db, checks.inactive));
            await using (var db = checks.Db(new RollbackAtCommit())) await Reject(() => writer(db, checks.unemployed));
            await checks.WriterFirst(writer);
            await checks.DeactivateFirst(writer);
        }
        await checks.WarehouseDiscovery();
        await checks.PendingReceiver();
        await checks.AssetFinalization();
        await checks.ReturnWorkCoordination();
        Console.WriteLine("PASS RC-D4 responsibility coordination; ReturnWork-first committed and restored; other service writes rolled back; dedicated fixture status/manager restored.");
    }
    async Task Preflight()
    {
        await using var db = Db();
        Check(await db.Database.SqlQueryRaw<string>("SELECT CONVERT(nvarchar(128),@@SERVERNAME)+'/'+DB_NAME() AS Value").SingleAsync(ct)
            == "KIENPC/HotelOps_Canonical_DEV2_Regression", "Wrong SQL target; no fixture writes permitted.");
        Check(new[] { actor, target, source, inactive, unemployed }.Distinct().Count() == 5, "Fixture Users must be distinct.");
        var users = await db.Users.AsNoTracking()
    .Where(x => new[] { actor, target, source, inactive, unemployed }.Contains(x.UserId))
    .Select(x => new { x.UserId, x.Username, x.DeletedAt, x.AccountStatus, x.EmploymentStatus })
    .ToListAsync(ct);

        Check(users.Count == 5 &&
              users.All(x => x.Username.StartsWith(FixturePrefix, StringComparison.Ordinal)),
            "Use only dedicated RC_D4_ Users.");
        Check(users.Where(x => x.UserId == target || x.UserId == source).All(x => x.DeletedAt == null && x.AccountStatus == "ACTIVE" && x.EmploymentStatus == "ACTIVE"), "Source and target must initially be eligible.");
        var inactiveUser = users.Single(x => x.UserId == inactive);
        Check(inactiveUser.DeletedAt == null && inactiveUser.AccountStatus != "ACTIVE" && inactiveUser.EmploymentStatus == "ACTIVE", "INACTIVE fixture must isolate account eligibility.");
        inactiveStatus = inactiveUser.AccountStatus; // Existing externally approved status; never invent an enum value.
        Check(users.Single(x => x.UserId == unemployed) is { DeletedAt: null, AccountStatus: "ACTIVE" } employment && employment.EmploymentStatus != "ACTIVE", "EMPLOYMENT_INACTIVE fixture must isolate employment eligibility.");
        var w = await db.Warehouses.AsNoTracking().SingleAsync(x => x.WarehouseId == warehouse, ct);
        Check(w.WarehouseCode.StartsWith(FixturePrefix, StringComparison.Ordinal) && w.ManagerUserId == source && w.IsActive, "Warehouse fixture needs SOURCE manager.");
        var mw = await db.MaintenanceWorks.AsNoTracking().Include(x => x.Incident).SingleAsync(x => x.MaintenanceWorkId == work, ct);
        Check(mw.Title.StartsWith(FixturePrefix, StringComparison.Ordinal) && mw.Status == "PENDING" && mw.Incident?.Status == "OPEN", "Work fixture must be a dedicated synchronized corrective PENDING/OPEN pair.");
        var returned = await db.MaintenanceWorks.AsNoTracking().Include(x => x.Incident).SingleAsync(x => x.MaintenanceWorkId == returnWork, ct);
        Check(returnWork != work && returned.Title.StartsWith(FixturePrefix, StringComparison.Ordinal)
            && returned.Status == "COMPLETED" && returned.Incident is { Status: "RESOLVED" }
            && returned.Incident.IncidentTitle.StartsWith(FixturePrefix, StringComparison.Ordinal)
            && returned.AssigneeId == target && actor != target
            && returned.ActualStartAt != null && returned.ResolvedAt != null && returned.Incident.ResolvedAt != null,
            "RETURN_WORK must be a separate dedicated COMPLETED/RESOLVED corrective pair retaining TARGET; ACTOR must be a different verifier.");
        returnIncident = returned.IncidentId!.Value;
        Check(!await db.PreventiveMaintenanceRuns.AnyAsync(x => x.MaintenanceWorkId == returnWork, ct), "RETURN_WORK must be corrective only.");
        Check(!await db.MaintenanceWorks.AsNoTracking().AnyAsync(x => x.AssigneeId == target
            && (x.Status == "PENDING" || x.Status == "IN_PROGRESS" || x.Status == "ON_HOLD"), ct),
            "TARGET must initially have no current Maintenance responsibility; do not ignore or edit unrelated blockers.");
        var asset = await db.Assets.AsNoTracking().SingleAsync(x => x.AssetId == createAsset, ct);
        Check(asset.AssetCode.StartsWith(FixturePrefix, StringComparison.Ordinal) && asset.LifecycleStatus is "IN_USE" or "UNDER_REPAIR"
            && asset.CurrentDepartmentId != null && asset.CustodianUserId == source, "Create Asset fixture needs operational source custody and department.");
        Check(!await db.AssetHandovers.AnyAsync(x => x.AssetId == createAsset && x.HandoverStatus == "PENDING", ct), "Create fixture must not already have pending handover.");
        var h = await db.AssetHandovers.AsNoTracking().Include(x => x.Asset).SingleAsync(x => x.HandoverId == handover, ct);
        Check(h.AssetId != createAsset && h.Asset.AssetCode.StartsWith(FixturePrefix, StringComparison.Ordinal)
            && h.Asset.LifecycleStatus is "IN_USE" or "UNDER_REPAIR" && h.HandoverStatus == "PENDING" && h.ToUserId == target && h.FromUserId == source
            && h.Asset.CustodianUserId == source && h.Asset.CurrentDepartmentId == h.FromDepartmentId, "Accept fixture requires a separate operational Asset and matching PENDING source snapshot.");
    }
    async Task Assign(HotelOpsDbContext db, long receiver)
    {
        var service = new MaintenanceWorkService(db, new InventoryService(db));
        Check(await service.AssignWorkAsync(work, new AssignMaintenanceWorkRequest { ActorUserId = actor, AssigneeId = receiver }, ct) is not null, "Work fixture disappeared.");
    }
    async Task Manage(HotelOpsDbContext db, long receiver)
    {
        var w = await db.Warehouses.AsNoTracking().SingleAsync(x => x.WarehouseId == warehouse, ct);
        Check(await new InventoryService(db).SaveWarehouse(warehouse, actor, new InventoryWarehouseWrite(w.BranchId, w.DepartmentId,
            w.WarehouseCode, w.WarehouseName, w.IsActive, receiver, w.LocationDescription, Convert.ToBase64String(w.RowVersion)), ct) is not null, "Warehouse fixture disappeared.");
    }
    async Task Propose(HotelOpsDbContext db, long receiver)
    {
        var department = await db.Assets.AsNoTracking().Where(x => x.AssetId == createAsset).Select(x => x.CurrentDepartmentId).SingleAsync(ct);
        Check(await new AssetService(db).CreateHandoverAsync(createAsset, new CreateAssetHandoverRequest
        { ActorUserId = actor, ToUserId = receiver, ToDepartmentId = department!.Value }, ct) is not null, "Asset fixture disappeared.");
    }
    async Task Accept(HotelOpsDbContext db, long receiver) => Check(await new AssetService(db).AcceptHandoverAsync(handover,
        new AcceptAssetHandoverRequest { ActorUserId = receiver }, ct) is not null, "Handover fixture disappeared.");
    static async Task Reject(Func<Task> operation, string? message = null)
    {
        try { await operation(); }
        catch (ValidationException e) when (message is null || e.Message.Contains(message, StringComparison.Ordinal)) { return; }
        throw new InvalidOperationException("Expected business/eligibility rejection was not observed.");
    }
    static async Task Rollback(Func<Task> operation)
    {
        try { await operation(); }
        catch (RollbackSignal) { return; }
        throw new InvalidOperationException("Writer did not reach the rollback-at-commit barrier.");
    }
    async Task HelperContract()
    {
        await using var db = Db();
        try { await new UserEligibilityLock(db).LockAsync([target], ct); throw new InvalidOperationException("Missing caller transaction was accepted."); }
        catch (InvalidOperationException e) when (e.Message.Contains("existing caller transaction")) { }
        var trace = new UserTrace();
        await using var ordered = Db(trace); await using var tx = await ordered.Database.BeginTransactionAsync(ct);
        await new UserEligibilityLock(ordered).LockAsync([target, source, target], ct);
        Check(trace.Ids.SequenceEqual(new[] { target, source }.Order()), "User lock IDs must be distinct and numerically ascending.");
        await tx.RollbackAsync(ct);
        var warehouseTrace = new UserTrace();
        await using var manager = Db(warehouseTrace, new RollbackAtCommit());
        await Rollback(() => Manage(manager, target));
        Check(warehouseTrace.Ids.SequenceEqual(new[] { target, source }.Order()), "Warehouse change must lock old/new Users in sorted order.");
    }
    async Task<int> Session(HotelOpsDbContext db)
    {
        await db.Database.OpenConnectionAsync(ct);
        return await db.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS Value").SingleAsync(ct);
    }
    async Task WaitForBlock(HotelOpsDbContext observer, int waiter, int blocker)
    {
        for (var i = 0; i < 100; i++)
        {
            if (await observer.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.dm_exec_requests WHERE session_id={waiter} AND blocking_session_id={blocker} AND wait_type LIKE 'LCK%'").SingleAsync(ct) > 0) return;
            await Task.Delay(50, ct);
        }
        throw new InvalidOperationException("SQL lock-manager evidence was not observed.");
    }
    async Task WriterFirst(Func<HotelOpsDbContext, long, Task> writer)
    {
        var barrier = new RollbackAtCommit(wait: true);
        var trace = new UserTrace();
        await using var worker = Db(trace, barrier); var workerId = await Session(worker);
        await using var contender = Db(); var contenderId = await Session(contender);
        await using var observer = Db();
        var running = Rollback(() => writer(worker, target));
        Task<int>? update = null;
        await using var tx = await contender.Database.BeginTransactionAsync(ct);
        try
        {
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            Check(trace.Ids.Contains(target), "Writer must retain a target User lock through commit.");
            update = contender.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET account_status=account_status WHERE user_id={target}", ct);
            await WaitForBlock(observer, contenderId, workerId);
            Check(!update.IsCompleted, "Concurrent deactivation lock probe must wait behind writer.");
        }
        finally
        {
            barrier.Release.TrySetResult();
            try { await running; if (update is not null) await update; }
            finally { await tx.RollbackAsync(CancellationToken.None); }
        }
    }
    async Task DeactivateFirst(Func<HotelOpsDbContext, long, Task> writer)
    {
        await using var contender = Db(); var contenderId = await Session(contender);
        await using var worker = Db(new RollbackAtCommit()); var workerId = await Session(worker);
        // Deliberately attach a stale ACTIVE instance; production helper must read SQL anew.
        _ = await worker.Users.SingleAsync(x => x.UserId == target, ct);
        await using var observer = Db();
        await using var tx = await contender.Database.BeginTransactionAsync(ct);
        await new UserEligibilityLock(contender).LockAsync([target], ct);
        await contender.Users.Where(x => x.UserId == target).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountStatus, inactiveStatus), ct);
        var running = Reject(() => writer(worker, target));
        try
        {
            await WaitForBlock(observer, workerId, contenderId);
            await tx.CommitAsync(ct);
            await running;
        }
        finally
        {
            // If proof failed before commit, release the blocker before awaiting writer cleanup.
            if (contender.Database.CurrentTransaction is not null) await tx.RollbackAsync(CancellationToken.None);
            try { await running; }
            finally { await RestoreTarget(); }
        }
    }
    async Task RestoreTarget()
    {
        await using var restore = Db();
        await restore.Users.Where(x => x.UserId == target).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountStatus, "ACTIVE"), CancellationToken.None);
    }
    async Task WarehouseDiscovery()
    {
        var trace = new UserTrace(pause: true);
        await using var worker = Db(trace, new RollbackAtCommit());
        var running = Reject(() => Manage(worker, target), "manager changed after discovery");
        try
        {
            await trace.Attempted.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            await using var concurrent = Db();
            // Dedicated fixture only: simulate a committed competing manager change.
            await concurrent.Warehouses.Where(x => x.WarehouseId == warehouse).ExecuteUpdateAsync(s => s.SetProperty(x => x.ManagerUserId, (long?)target), ct);
        }
        finally
        {
            trace.Resume.TrySetResult();
            try { await running; }
            finally
            {
                await using var restore = Db();
                await restore.Warehouses.Where(x => x.WarehouseId == warehouse).ExecuteUpdateAsync(s => s.SetProperty(x => x.ManagerUserId, (long?)source), CancellationToken.None);
            }
        }
    }
    async Task PendingReceiver()
    {
        var pending = new RollbackAtCommit(inspect: async (transaction, token) =>
        {
            using var command = transaction.Connection!.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "SELECT COUNT(*) FROM dbo.asset_handovers WHERE asset_id=@asset AND to_user_id=@receiver AND handover_status='PENDING'";
            var asset = command.CreateParameter(); asset.ParameterName = "@asset"; asset.Value = createAsset; command.Parameters.Add(asset);
            var receiver = command.CreateParameter(); receiver.ParameterName = "@receiver"; receiver.Value = target; command.Parameters.Add(receiver);
            Check(Convert.ToInt32(await command.ExecuteScalarAsync(token)) == 1, "PENDING receiver must be visible as future responsibility in the writer transaction.");
        });
        var trace = new UserTrace();
        await using var db = Db(trace, pending);
        await Rollback(() => Propose(db, target));
        Check(trace.Ids.SequenceEqual(new[] { target }), "Proposal locks only the receiver, not actor merely as actor.");
    }
    async Task AssetFinalization()
    {
        // Both rows must already have changed atomically before the commit barrier.
        var accepted = new RollbackAtCommit(inspect: async (transaction, token) =>
        {
            using var command = transaction.Connection!.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "SELECT COUNT(*) FROM dbo.asset_handovers h JOIN dbo.assets a ON a.asset_id=h.asset_id WHERE h.handover_id=@id AND h.handover_status='ACCEPTED' AND a.custodian_user_id=h.to_user_id AND a.current_department_id=h.to_department_id";
            var parameter = command.CreateParameter(); parameter.ParameterName = "@id"; parameter.Value = handover; command.Parameters.Add(parameter);
            Check(Convert.ToInt32(await command.ExecuteScalarAsync(token)) == 1, "Accept must atomically transfer current custody and department.");
        });
        var trace = new UserTrace();
        await using (var db = Db(trace, accepted)) await Rollback(() => Accept(db, target));
        Check(trace.Ids.SequenceEqual(new[] { target, source }.Order()), "Accept must lock both custody Users, not actor merely as actor.");
        await DeactivateFirst(Accept);
        await using var inactiveReceiver = Db();
        await inactiveReceiver.Users.Where(x => x.UserId == target).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountStatus, inactiveStatus), ct);
        try
        {
            await using (var db = Db(new RollbackAtCommit())) await Reject(() => Accept(db, target));
            var rejected = new RollbackAtCommit(inspect: async (transaction, token) =>
            {
                using var command = transaction.Connection!.CreateCommand(); command.Transaction = transaction;
                command.CommandText = "SELECT COUNT(*) FROM dbo.asset_handovers h JOIN dbo.assets a ON a.asset_id=h.asset_id WHERE h.handover_id=@id AND h.handover_status='REJECTED' AND a.custodian_user_id=h.from_user_id";
                var p = command.CreateParameter(); p.ParameterName = "@id"; p.Value = handover; command.Parameters.Add(p);
                Check(Convert.ToInt32(await command.ExecuteScalarAsync(token)) == 1, "Reject must resolve PENDING without creating custody.");
            });
            var rejectTrace = new UserTrace();
            await using (var db = Db(rejectTrace, rejected)) await Rollback(async () =>
                Check(await new AssetService(db).RejectHandoverAsync(handover, new RejectAssetHandoverRequest { ActorUserId = target, Reason = "RC_D4_rejection" }, ct) is not null, "Reject fixture missing."));
            Check(rejectTrace.Ids.Count == 0, "Reject must not acquire future-responsibility User locks.");
        }
        finally { await RestoreTarget(); }
    }

    Task Return(HotelOpsDbContext db, CancellationToken token) => new MaintenanceWorkService(db, new InventoryService(db))
        .ReturnWorkAsync(returnWork, new ReturnMaintenanceWorkRequest { ActorUserId = actor, Reason = "RC_D4_return_for_rework" }, token);

    // No callable production/shared Maintenance blocker is exposed in this baseline.
    // This test-only query mirrors current responsibility exactly:
    // AssigneeId == TARGET AND Status IN (PENDING, IN_PROGRESS, ON_HOLD).
    // No HTTP Deactivate or production account-lifecycle implementation is introduced.
    async Task RejectMaintenanceResponsibility(HotelOpsDbContext db, CancellationToken token)
    {
        if (await db.MaintenanceWorks.AsNoTracking().AnyAsync(x => x.AssigneeId == target
            && (x.Status == "PENDING" || x.Status == "IN_PROGRESS" || x.Status == "ON_HOLD"), token))
            throw new ValidationException("Current Maintenance responsibility prevents deactivation.");
    }

    async Task<string> ReturnSnapshot(HotelOpsDbContext db, CancellationToken token)
    {
        await db.Database.OpenConnectionAsync(token);
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT (SELECT
              (SELECT * FROM dbo.maintenance_works WHERE maintenance_work_id=@work FOR JSON PATH, INCLUDE_NULL_VALUES) AS work,
              (SELECT * FROM dbo.incidents WHERE incident_id=@incident FOR JSON PATH, INCLUDE_NULL_VALUES) AS incident,
              (SELECT * FROM dbo.maintenance_work_logs WHERE maintenance_work_id=@work ORDER BY log_id FOR JSON PATH, INCLUDE_NULL_VALUES) AS logs,
              (SELECT * FROM dbo.incident_events WHERE incident_id=@incident ORDER BY event_id FOR JSON PATH, INCLUDE_NULL_VALUES) AS events,
              (SELECT * FROM dbo.audit_logs WHERE (entity_type='maintenance_works' AND entity_id=CONVERT(nvarchar(40),@work))
                OR (entity_type='incidents' AND entity_id=CONVERT(nvarchar(40),@incident))
                OR (entity_type='maintenance_work_logs' AND entity_id IN
                  (SELECT CONVERT(nvarchar(40),log_id) FROM dbo.maintenance_work_logs WHERE maintenance_work_id=@work))
                ORDER BY audit_id FOR JSON PATH, INCLUDE_NULL_VALUES) AS audits
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER) AS snapshot
            """;
        foreach (var (name, value) in new[] { ("@work", returnWork), ("@incident", returnIncident) })
        {
            var p = command.CreateParameter(); p.ParameterName = name; p.Value = value; command.Parameters.Add(p);
        }
        return (string)(await command.ExecuteScalarAsync(token))!;
    }

    async Task<(int Logs, int Events, int WorkAudits, int IncidentAudits)> ReturnHistory(HotelOpsDbContext db)
    {
        var workKey = returnWork.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var incidentKey = returnIncident.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return (await db.MaintenanceWorkLogs.CountAsync(x => x.MaintenanceWorkId == returnWork, ct),
            await db.IncidentEvents.CountAsync(x => x.IncidentId == returnIncident, ct),
            await db.AuditLogs.CountAsync(x => x.EntityType == "maintenance_works" && x.EntityId == workKey, ct),
            await db.AuditLogs.CountAsync(x => x.EntityType == "incidents" && x.EntityId == incidentKey, ct));
    }

    async Task InspectReturn(DbTransaction transaction, CancellationToken token,
        (int Logs, int Events, int WorkAudits, int IncidentAudits) before)
    {
        // Read SQL, not tracked DTOs, in the caller's transaction: either before
        // writer rollback or in the serialized Deactivate waiter after commit.
        using var command = transaction.Connection!.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            SELECT COUNT(*) FROM dbo.maintenance_works w JOIN dbo.incidents i ON i.incident_id=w.incident_id
            WHERE w.maintenance_work_id=@work AND i.incident_id=@incident AND w.assignee_id=@target
              AND w.status='IN_PROGRESS' AND i.status='IN_PROGRESS'
              AND w.resolved_at IS NULL AND w.resolution_note IS NULL AND w.reviewed_by IS NULL
              AND w.reviewed_at IS NULL AND i.resolved_at IS NULL
              AND (SELECT COUNT(*) FROM dbo.maintenance_work_logs WHERE maintenance_work_id=@work)=@logs
              AND (SELECT COUNT(*) FROM dbo.incident_events WHERE incident_id=@incident)=@events
              AND (SELECT COUNT(*) FROM dbo.audit_logs WHERE entity_type='maintenance_works' AND entity_id=CONVERT(nvarchar(40),@work))=@workAudits
              AND (SELECT COUNT(*) FROM dbo.audit_logs WHERE entity_type='incidents' AND entity_id=CONVERT(nvarchar(40),@incident))=@incidentAudits
            """;
        foreach (var (name, value) in new[] { ("@work", returnWork), ("@incident", returnIncident), ("@target", target),
            ("@logs", before.Logs + 1L), ("@events", before.Events + 1L),
            ("@workAudits", before.WorkAudits + 1L), ("@incidentAudits", before.IncidentAudits + 1L) })
        {
            var p = command.CreateParameter(); p.ParameterName = name; p.Value = value; command.Parameters.Add(p);
        }
        Check(Convert.ToInt32(await command.ExecuteScalarAsync(token)) == 1, "Return must atomically restore responsibility, clear result/review metadata and add one log/event plus both audits.");
    }

    async Task ReturnWorkCoordination()
    {
        await using var observer = Db();
        var before = await ReturnSnapshot(observer, ct);
        await ReturnFirst();
        Check(before == await ReturnSnapshot(observer, ct), "Return-first restoration changed the baseline Work/Incident/log/event/audit snapshot.");
        var history = await ReturnHistory(observer);
        await using (var writer = Db(new RollbackAtCommit(inspect: (transaction, token) => InspectReturn(transaction, token, history))))
            await Rollback(() => Return(writer, ct));
        Check(before == await ReturnSnapshot(observer, ct), "Return rollback before commit leaked Work/Incident/log/event/audit changes.");
        await ReturnDeactivateFirst();
        Check(before == await ReturnSnapshot(observer, ct), "Deactivate-first rejection changed the completed pair or history.");
        await ReturnCancellation();
        Check(before == await ReturnSnapshot(observer, ct), "Cancelled User-lock wait mutated Return state/history.");
        await ReturnStaleContext();
        Check(before == await ReturnSnapshot(observer, ct), "Stale-context rejection leaked mutations.");
        Console.WriteLine("PASS ReturnWork: Users -> Incident -> Work; SQL waits in both orders; committed blocker rejection and exact fixture restoration; cancellation; stale context; rollback. SQL 1205 is never accepted.");
    }

    async Task ReturnFirst()
    {
        await using var observer = Db();
        var baseline = await CaptureReturnBaseline(observer);
        var trace = new ReturnLockTrace();
        var barrier = new ReturnCommitBarrier();
        await using var writer = Db(trace, barrier); var writerId = await Session(writer);
        await using var contender = Db(); var contenderId = await Session(contender);
        // Track both entities before the wait to exercise authoritative reloads.
        _ = await writer.MaintenanceWorks.Include(x => x.Incident).SingleAsync(x => x.MaintenanceWorkId == returnWork, ct);
        await using var tx = await contender.Database.BeginTransactionAsync(ct);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task<MaintenanceWorkDetailDto?>? running = null;
        Task<IReadOnlyDictionary<long, UserEligibilityLock.LockedUser?>>? locking = null;
        var failures = new List<Exception>();
        async Task Cleanup(Func<Task> action)
        {
            try { await action(); }
            catch (Exception e) { if (!failures.Contains(e)) failures.Add(e); }
        }
        try
        {
            running = new MaintenanceWorkService(writer, new InventoryService(writer)).ReturnWorkAsync(returnWork,
                new() { ActorUserId = actor, Reason = "RC_D4_return_for_rework" }, cancellation.Token);
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            Check(trace.Order.SequenceEqual(new[] { "User", "Incident", "Work" }), "Return lock order must be User -> Incident -> Work exactly.");
            locking = new UserEligibilityLock(contender).LockAsync([target], cancellation.Token);
            await WaitForBlock(observer, contenderId, writerId);
            Check(!locking.IsCompleted, "Deactivation must wait for Return's retained User UPDLOCK,HOLDLOCK.");

            barrier.Release.TrySetResult(); // Permit the actual production commit.
            var result = await running;
            Check(barrier.Committed && result is { Status: "IN_PROGRESS" } && result.AssigneeId == target,
                "Production ReturnWork must complete successfully with a real SQL commit.");
            var locked = await locking;
            Check(locked.TryGetValue(target, out var user) && user is not null, "Serialized Deactivate must acquire TARGET's User lock after commit.");
            // Both queries use the SAME waiting transaction, now holding User.
            // TARGET is never updated/deactivated by this Return-first race.
            await InspectReturn(tx.GetDbTransaction(), ct, baseline.History);
            await Reject(() => RejectMaintenanceResponsibility(contender, ct), "Current Maintenance responsibility");
            var created = CreatedReturnRows(writer, baseline.Rows);
            Check(created.Logs.Length == 1 && created.Events.Length == 1 && created.Audits.Length == 2,
                "Return must create exactly one log/event and two audits.");
            CheckReturnRowOwnership(baseline.Rows, created, await ReadReturnRows(contender, ct), requireCreated: true);
            Check(trace.Order.SequenceEqual(new[] { "User", "Incident", "Work" }), "Committed Return changed its canonical lock order.");
        }
        catch (Exception e) { failures.Add(e); }
        finally
        {
            barrier.Release.TrySetResult();
            if (failures.Count != 0) cancellation.Cancel();
            // Drain both sessions and release the waiting User lock before cleanup.
            // A failed assertion, cancellation or SQL 1205 must still restore the
            // fixture; retain every failure, including any restoration failure.
            await Cleanup(async () => { if (running is not null) await running; });
            await Cleanup(async () => { if (locking is not null) await locking; });
            await Cleanup(() => tx.RollbackAsync(CancellationToken.None));
            await Cleanup(() => tx.DisposeAsync().AsTask());
            await Cleanup(() => RestoreReturnFixture(baseline, CreatedReturnRows(writer, baseline.Rows)));
        }
        if (failures.Count != 0) throw new AggregateException("Return-first commit/serialization/restoration failed.", failures);
    }

    sealed record ReturnRows(long[] Logs, long[] Events, long[] Audits);
    sealed record ReturnBaseline(MaintenanceWork Work, Incident Incident, ReturnRows Rows, string Snapshot,
        (int Logs, int Events, int WorkAudits, int IncidentAudits) History);

    IQueryable<AuditLog> ReturnAudits(HotelOpsDbContext db)
    {
        var workKey = returnWork.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var incidentKey = returnIncident.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "maintenance_works" && x.EntityId == workKey
            || x.EntityType == "incidents" && x.EntityId == incidentKey
            || x.EntityType == "maintenance_work_logs" && db.MaintenanceWorkLogs.Any(log => log.MaintenanceWorkId == returnWork && log.LogId.ToString() == x.EntityId));
    }

    async Task<ReturnRows> ReadReturnRows(HotelOpsDbContext db, CancellationToken token) => new(
        await db.MaintenanceWorkLogs.AsNoTracking().Where(x => x.MaintenanceWorkId == returnWork).Select(x => x.LogId).ToArrayAsync(token),
        await db.IncidentEvents.AsNoTracking().Where(x => x.IncidentId == returnIncident).Select(x => x.EventId).ToArrayAsync(token),
        await ReturnAudits(db).Select(x => x.AuditId).ToArrayAsync(token));

    ReturnRows CreatedReturnRows(HotelOpsDbContext writer, ReturnRows baseline)
    {
        var workKey = returnWork.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var incidentKey = returnIncident.ToString(System.Globalization.CultureInfo.InvariantCulture);
        // IDs come from this exact production call's tracker after SQL SaveChanges,
        // even if a later assertion/commit fails. Never infer ownership from MAX(id).
        var logs = writer.ChangeTracker.Entries<MaintenanceWorkLog>().Select(x => x.Entity)
            .Where(x => x.MaintenanceWorkId == returnWork && x.LogId > 0).Select(x => x.LogId).Except(baseline.Logs).ToArray();
        var logKeys = baseline.Logs.Concat(logs).Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return new(
            logs,
            writer.ChangeTracker.Entries<IncidentEvent>().Select(x => x.Entity)
                .Where(x => x.IncidentId == returnIncident && x.EventId > 0).Select(x => x.EventId).Except(baseline.Events).ToArray(),
            writer.ChangeTracker.Entries<AuditLog>().Select(x => x.Entity)
                .Where(x => x.AuditId > 0 && (x.EntityType == "maintenance_works" && x.EntityId == workKey
                    || x.EntityType == "incidents" && x.EntityId == incidentKey
                    || x.EntityType == "maintenance_work_logs" && logKeys.Contains(x.EntityId))).Select(x => x.AuditId).Except(baseline.Audits).ToArray());
    }

    static void CheckReturnRowOwnership(ReturnRows baseline, ReturnRows created, ReturnRows current, bool requireCreated = false)
    {
        foreach (var (before, added, now) in new[] { (baseline.Logs, created.Logs, current.Logs),
            (baseline.Events, created.Events, current.Events), (baseline.Audits, created.Audits, current.Audits) })
        {
            Check(!before.Except(now).Any() && !now.Except(before.Concat(added)).Any(),
                "RETURN_WORK history changed outside this test; refusing to delete unknown rows or overwrite missing baseline history.");
            if (requireCreated) Check(!added.Except(now).Any(), "Committed Return history IDs were not persisted.");
        }
    }

    void RequireReturnOwnership(MaintenanceWork w, Incident i)
    {
        Check(w.MaintenanceWorkId == returnWork && returnWork != work && w.IncidentId == returnIncident && i.IncidentId == returnIncident
            && w.Title.StartsWith(FixturePrefix, StringComparison.Ordinal) && i.IncidentTitle.StartsWith(FixturePrefix, StringComparison.Ordinal),
            "Refusing Return fixture capture/restoration outside the dedicated RC_D4_ Work/Incident pair.");
    }

    static async Task RequireReturnRegression(HotelOpsDbContext db, CancellationToken token) =>
        Check(await db.Database.SqlQueryRaw<string>("SELECT CONVERT(nvarchar(128),@@SERVERNAME)+'/'+DB_NAME() AS Value").SingleAsync(token)
            == "KIENPC/HotelOps_Canonical_DEV2_Regression", "Wrong SQL target; no Return fixture capture/restoration permitted.");

    async Task<ReturnBaseline> CaptureReturnBaseline(HotelOpsDbContext db)
    {
        await RequireReturnRegression(db, ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var i = await db.Incidents.AsNoTracking().SingleAsync(x => x.IncidentId == returnIncident, ct);
        var w = await db.MaintenanceWorks.AsNoTracking().SingleAsync(x => x.MaintenanceWorkId == returnWork, ct);
        RequireReturnOwnership(w, i);
        Check(w.Status == "COMPLETED" && i.Status == "RESOLVED" && w.AssigneeId == target && actor != target,
            "Return baseline must still be the retained-TARGET COMPLETED/RESOLVED pair.");
        var baseline = new ReturnBaseline(w, i, await ReadReturnRows(db, ct), await ReturnSnapshot(db, ct), await ReturnHistory(db));
        await tx.CommitAsync(ct);
        return baseline;
    }

    async Task RestoreReturnFixture(ReturnBaseline baseline, ReturnRows created)
    {
        // Independent cleanup token: an expired/cancelled race cannot skip reset.
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = cleanup.Token;
        await using var db = Db();
        await RequireReturnRegression(db, token);
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, token))
        {
            var i = await db.Incidents.FromSqlInterpolated($"SELECT * FROM incidents WITH (UPDLOCK, HOLDLOCK) WHERE incident_id={returnIncident}")
                .SingleAsync(token);
            var w = await db.MaintenanceWorks.FromSqlInterpolated($"SELECT * FROM maintenance_works WITH (UPDLOCK, HOLDLOCK) WHERE maintenance_work_id={returnWork}")
                .SingleAsync(token);
            RequireReturnOwnership(baseline.Work, baseline.Incident);
            RequireReturnOwnership(w, i);
            Check(w.WorkNo == baseline.Work.WorkNo && i.IncidentNo == baseline.Incident.IncidentNo,
                "Dedicated Return fixture identity changed; refusing restoration.");
            CheckReturnRowOwnership(baseline.Rows, created, await ReadReturnRows(db, token));
            // Exact captured IDs AND dedicated parent IDs; preserve every baseline
            // row. No blanket history delete, table reset, reseed or User update.
            await ReturnAudits(db).Where(x => created.Audits.Contains(x.AuditId) && !baseline.Rows.Audits.Contains(x.AuditId)).ExecuteDeleteAsync(token);
            await db.IncidentEvents.Where(x => x.IncidentId == returnIncident && created.Events.Contains(x.EventId)
                && !baseline.Rows.Events.Contains(x.EventId)).ExecuteDeleteAsync(token);
            await db.MaintenanceWorkLogs.Where(x => x.MaintenanceWorkId == returnWork && created.Logs.Contains(x.LogId)
                && !baseline.Rows.Logs.Contains(x.LogId)).ExecuteDeleteAsync(token);
            db.Entry(w).CurrentValues.SetValues(baseline.Work);
            db.Entry(i).CurrentValues.SetValues(baseline.Incident);
            await db.SaveChangesAsync(token);
            Check(baseline.Snapshot == await ReturnSnapshot(db, token), "Return restoration did not reproduce the complete baseline snapshot exactly.");
            await tx.CommitAsync(token);
        }
        Check(baseline.Snapshot == await ReturnSnapshot(db, token), "Committed Return fixture restoration differs from the baseline snapshot.");
    }

    sealed class ReturnCommitBarrier : DbTransactionInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Committed { get; private set; }
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return result; // No injected exception or suppression: perform the real commit.
        }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }
    }

    async Task ReturnDeactivateFirst()
    {
        await using var blocker = Db(); var blockerId = await Session(blocker);
        var trace = new ReturnLockTrace();
        await using var writer = Db(trace, new RollbackAtCommit()); var writerId = await Session(writer);
        _ = await writer.Users.SingleAsync(x => x.UserId == target, ct); // Stale ACTIVE tracking must not bypass SQL eligibility.
        _ = await writer.MaintenanceWorks.Include(x => x.Incident).SingleAsync(x => x.MaintenanceWorkId == returnWork, ct);
        await using var observer = Db();
        await using var tx = await blocker.Database.BeginTransactionAsync(ct);
        await new UserEligibilityLock(blocker).LockAsync([target], ct);
        await RejectMaintenanceResponsibility(blocker, ct);
        await blocker.Users.Where(x => x.UserId == target).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountStatus, inactiveStatus), ct);
        var running = Reject(() => Return(writer, ct), "Future responsibility requires");
        try
        {
            await WaitForBlock(observer, writerId, blockerId);
            Check(trace.Order.SequenceEqual(new[] { "User" }) && !running.IsCompleted, "Return must wait on User before taking either business lock.");
            await tx.CommitAsync(ct);
            await running;
            Check(trace.Order.SequenceEqual(new[] { "User" }), "Ineligible Return must reject before Incident/Work locks or writes.");
            Check(!await observer.MaintenanceWorks.AnyAsync(x => x.MaintenanceWorkId == returnWork && x.AssigneeId == target
                && (x.Status == "PENDING" || x.Status == "IN_PROGRESS" || x.Status == "ON_HOLD"), ct), "Inactive target acquired current Maintenance responsibility.");
        }
        finally
        {
            if (blocker.Database.CurrentTransaction is not null) await tx.RollbackAsync(CancellationToken.None);
            try { await running; }
            finally { await RestoreTarget(); }
        }
    }

    async Task ReturnCancellation()
    {
        await using var blocker = Db(); var blockerId = await Session(blocker);
        await using var tx = await blocker.Database.BeginTransactionAsync(ct);
        await new UserEligibilityLock(blocker).LockAsync([target], ct);
        var trace = new ReturnLockTrace();
        await using var writer = Db(trace, new RollbackAtCommit()); var writerId = await Session(writer);
        await using var observer = Db();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var running = Return(writer, cancellation.Token);
        try
        {
            await WaitForBlock(observer, writerId, blockerId);
            Check(trace.Order.SequenceEqual(new[] { "User" }), "Cancelled Return acquired business locks before User lock.");
        }
        finally
        {
            cancellation.Cancel();
            try
            {
                try { await running; throw new InvalidOperationException("Waiting Return ignored cancellation."); }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            }
            finally { await tx.RollbackAsync(CancellationToken.None); }
        }
    }

    async Task ReturnStaleContext()
    {
        // Inject changed SQL state after discovery, inside the writer's own
        // rollback-only transaction. No committed fixture edits are needed.
        foreach (var change in new[] { "assignee", "linkage", "state", "incident", "missing" })
        {
            var injection = new ReturnContextChange(returnWork, returnIncident, source, change);
            await using var writer = Db(injection, new RollbackAtCommit());
            _ = await writer.MaintenanceWorks.Include(x => x.Incident).SingleAsync(x => x.MaintenanceWorkId == returnWork, ct);
            if (change == "missing")
                Check(await new MaintenanceWorkService(writer, new InventoryService(writer)).ReturnWorkAsync(returnWork,
                    new() { ActorUserId = actor, Reason = "RC_D4_stale_missing" }, ct) is null, "Disappeared Work must fail closed.");
            else await Reject(() => Return(writer, ct), change switch
            {
                "assignee" or "state" => "changed after discovery", "linkage" => "relationship changed", _ => "inconsistent"
            });
            Check(injection.Applied, "Stale-context injection did not run after discovery.");
        }
    }

    sealed class ReturnLockTrace : DbCommandInterceptor
    {
        public List<string> Order { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("HOLDLOCK", StringComparison.OrdinalIgnoreCase))
                Order.Add(command.CommandText.Contains("dbo.users", StringComparison.OrdinalIgnoreCase) ? "User"
                    : command.CommandText.Contains("FROM incidents", StringComparison.OrdinalIgnoreCase) ? "Incident"
                    : command.CommandText.Contains("FROM maintenance_works", StringComparison.OrdinalIgnoreCase) ? "Work" : "Unexpected");
            return ValueTask.FromResult(result);
        }
    }

    sealed class ReturnContextChange(long workId, long incidentId, long otherAssignee, string change) : DbCommandInterceptor
    {
        public bool Applied { get; private set; }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!Applied && command.CommandText.Contains("FROM incidents WITH (UPDLOCK", StringComparison.OrdinalIgnoreCase))
            {
                Applied = true;
                using var inject = command.Connection!.CreateCommand(); inject.Transaction = command.Transaction;
                inject.CommandText = change switch
                {
                    "assignee" => "UPDATE dbo.maintenance_works SET assignee_id=@other WHERE maintenance_work_id=@work",
                    "linkage" => "UPDATE dbo.maintenance_works SET incident_id=NULL WHERE maintenance_work_id=@work",
                    "state" => "UPDATE dbo.maintenance_works SET status='CLOSED' WHERE maintenance_work_id=@work; UPDATE dbo.incidents SET status='CLOSED' WHERE incident_id=@incident",
                    "incident" => "UPDATE dbo.incidents SET status='IN_PROGRESS' WHERE incident_id=@incident",
                    // Filter the locked read instead of deleting FK-referenced history.
                    _ => "SELECT 1"
                };
                foreach (var (name, value) in new[] { ("@work", workId), ("@incident", incidentId), ("@other", otherAssignee) })
                {
                    var p = inject.CreateParameter(); p.ParameterName = name; p.Value = value; inject.Parameters.Add(p);
                }
                await inject.ExecuteNonQueryAsync(cancellationToken);
            }
            if (Applied && change == "missing" && command.CommandText.Contains("FROM maintenance_works WITH (UPDLOCK", StringComparison.OrdinalIgnoreCase))
                command.CommandText = command.CommandText.Replace("WHERE maintenance_work_id =", "WHERE 1=0 AND maintenance_work_id =", StringComparison.Ordinal);
            return result;
        }
    }
    sealed class RollbackSignal : Exception;
    sealed class RollbackAtCommit(bool wait = false, Func<DbTransaction, CancellationToken, Task>? inspect = null) : DbTransactionInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (inspect is not null) await inspect(transaction, cancellationToken);
            Entered.TrySetResult();
            if (wait) await Release.Task.WaitAsync(cancellationToken);
            throw new RollbackSignal(); // Never commit a production writer's fixture mutations.
        }
    }
    sealed class UserTrace(bool pause = false) : DbCommandInterceptor
    {
        public List<long> Ids { get; } = [];
        public TaskCompletionSource Attempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("dbo.users", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("HOLDLOCK", StringComparison.OrdinalIgnoreCase))
            {
                Ids.Add(Convert.ToInt64(command.Parameters[0].Value));
                if (pause && Ids.Count == 1) { Attempted.TrySetResult(); await Resume.Task.WaitAsync(cancellationToken); }
            }
            return result;
        }
    }
}
