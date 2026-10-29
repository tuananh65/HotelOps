using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Assets;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Services.Assets;
using HotelOps.Api.Services.Inventory;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Explicit opt-in only. Direct production service tests, not HTTP/Authz tests.
internal sealed class ResponsibilityCoordinationChecks
{
    readonly string connection;
    readonly CancellationToken ct;
    readonly long actor, target, source, inactive, unemployed, work, warehouse, createAsset, handover;
    string inactiveStatus = "";
    const string FixturePrefix = "RC_D4_";
    ResponsibilityCoordinationChecks(string connection, CancellationToken ct)
    {
        this.connection = connection; this.ct = ct;
        actor = Id("ACTOR"); target = Id("TARGET"); source = Id("SOURCE"); inactive = Id("INACTIVE"); unemployed = Id("EMPLOYMENT_INACTIVE");
        work = Id("WORK"); warehouse = Id("WAREHOUSE"); createAsset = Id("CREATE_ASSET"); handover = Id("HANDOVER");
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
        Console.WriteLine("PASS RC-D4 responsibility coordination; service writes rolled back; dedicated fixture status/manager restored.");
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
