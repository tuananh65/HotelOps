using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

sealed partial class IntegritySuite
{
    async Task<bool> Outcome(Func<Task> action)
    {
        try { await action(); return true; } catch (ValidationException) { return false; }
    }
    async Task OrderedRace(string name, Func<HotelOpsDbContext, Task> first, Func<HotelOpsDbContext, Task> second, Action<bool, bool> outcomes)
    {
        var barrier = new HoldIncidentRead();
        await using var a = Db(barrier); await using var b = Db(); await using var observer = Db();
        await a.Database.OpenConnectionAsync(); await b.Database.OpenConnectionAsync();
        var aSpid = await a.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS Value").SingleAsync(); var bSpid = await b.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS Value").SingleAsync();
        var one = Outcome(() => first(a)); Task<bool>? two = null;
        try
        {
            await barrier.Locked.Task.WaitAsync(TimeSpan.FromSeconds(15));
            two = Outcome(() => second(b)); var blocking = 0;
            for (var i = 0; i < 150 && blocking == 0; i++)
            {
                blocking = await observer.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.dm_exec_requests WHERE session_id={bSpid} AND blocking_session_id={aSpid} AND wait_type LIKE 'LCK_M_%'").SingleAsync();
                if (blocking == 0) await Task.Delay(50);
            }
            Check(blocking == 1 && !one.IsCompleted && !two.IsCompleted, name + " actual Incident lock blocking/order");
        }
        finally { barrier.Release.TrySetResult(); }
        var result = await Task.WhenAll(one, two ?? Task.FromResult(false)); outcomes(result[0], result[1]); races++; Console.WriteLine("SQL ordered race PASS: " + name);
    }
    async Task Concurrency()
    {
        foreach (var genericFirst in new[] { true, false })
        {
            var incident = await Create();
            Func<HotelOpsDbContext, Task> generic = async d => { await new IncidentService(d).ChangeIncidentStatusAsync(incident.IncidentId, Change("IN_PROGRESS"), default); };
            Func<HotelOpsDbContext, Task> create = async d => { await new MaintenanceWorkService(d, new HotelOps.Api.Services.Inventory.InventoryService(d)).CreateWorkAsync(incident.IncidentId, new() { ActorUserId = actor, Title = prefix }, default); };
            var before = await HistoryCounts(incident.IncidentId);
            await OrderedRace("generic/create; " + (genericFirst ? "generic" : "create") + " first", genericFirst ? generic : create, genericFirst ? create : generic, (a, b) => Check(a && !b, "Create race winner follows Incident lock order"));
            await using var check = Db(); var works = await check.MaintenanceWorks.Where(x => x.IncidentId == incident.IncidentId).ToArrayAsync(); var row = await check.Incidents.SingleAsync(x => x.IncidentId == incident.IncidentId);
            Check(genericFirst ? works.Length == 0 && row.Status == "IN_PROGRESS" : works.Length == 1 && works[0].Status == "PENDING" && row.Status == "OPEN", "Create race serialized state");
            var after = await HistoryCounts(incident.IncidentId); Check(after == (before.Events + (genericFirst ? 1 : 0), before.IncidentAudits + (genericFirst ? 1 : 0), before.WorkAudits + (genericFirst ? 0 : 1)), "Create race no duplicate history");
        }
        foreach (var action in new[] { "start", "submit-result", "verify", "return", "cancel" }) foreach (var genericFirst in new[] { true, false })
        {
            var initial = action == "start" ? "PENDING" : action is "verify" or "return" ? "COMPLETED" : "IN_PROGRESS";
            var target = action switch { "start" or "return" => "IN_PROGRESS", "submit-result" => "RESOLVED", "verify" => "CLOSED", _ => "CANCELLED" };
            var pair = await Prepare(initial); var before = await HistoryCounts(pair.Incident);
            Func<HotelOpsDbContext, Task> generic = async d => { await new IncidentService(d).ChangeIncidentStatusAsync(pair.Incident, Change(target, prefix), default); };
            Func<HotelOpsDbContext, Task> command = async d =>
            {
                var m = new MaintenanceWorkService(d, new HotelOps.Api.Services.Inventory.InventoryService(d));
                switch (action)
                {
                    case "start": await m.StartWorkAsync(pair.Work, new() { ActorUserId = actor }, default); break;
                    case "submit-result": await m.SubmitResultAsync(pair.Work, new() { ActorUserId = actor, ResolutionNote = prefix }, default); break;
                    case "verify": await m.VerifyWorkAsync(pair.Work, new() { ActorUserId = verifier, Note = prefix }, default); break;
                    case "return": await m.ReturnWorkAsync(pair.Work, new() { ActorUserId = verifier, Reason = prefix }, default); break;
                    default: await m.CancelWorkAsync(pair.Work, new() { ActorUserId = verifier, Reason = prefix }, default); break;
                }
            };
            await OrderedRace($"generic/{action}; {(genericFirst ? "generic" : action)} first", genericFirst ? generic : command, genericFirst ? command : generic,
                (a, b) => Check(genericFirst ? !a && b : a && b, "Paired command wins; waiting matching generic is no-op"));
            var expectedWork = action switch { "submit-result" => "COMPLETED", "verify" => "CLOSED", "cancel" => "CANCELLED", _ => "IN_PROGRESS" };
            await AssertPair(pair.Work, expectedWork, target);
            var after = await HistoryCounts(pair.Incident); Check(after == (before.Events + 1, before.IncidentAudits + 1, before.WorkAudits + 1), "Race exactly one paired success history");
        }
    }
    async Task Rollback(string name, Func<Task> action)
    {
        var before = await Footprint(); var thrown = false;
        try { await action(); } catch (InjectedFailure) { thrown = true; } catch (OperationCanceledException) { thrown = true; }
        Check(thrown, name + " injection/cancellation did not fire"); Check(before == await Footprint(), name + " business/history persistence leak"); faults++; Console.WriteLine("Rollback PASS: " + name);
    }
    async Task Rollbacks()
    {
        var i = await Create();
        await using (var d = Db(new SaveFailure(1))) await Rollback("generic status/event/audit after SQL save", async () => { await new IncidentService(d).ChangeIncidentStatusAsync(i.IncidentId, Change("IN_PROGRESS"), default); });
        await using (var d = Db(new CommitFailure())) await Rollback("generic before commit", async () => { await new IncidentService(d).ChangeIncidentStatusAsync(i.IncidentId, Change("IN_PROGRESS"), default); });
        foreach (var boundary in new[] { 1, 2 })
        {
            await using var d = Db(new SaveFailure(boundary)); await Rollback("corrective create save " + boundary, async () => { await new MaintenanceWorkService(d, new HotelOps.Api.Services.Inventory.InventoryService(d)).CreateWorkAsync(i.IncidentId, new() { ActorUserId = actor, Title = prefix }, default); });
        }
        var pair = await Prepare("IN_PROGRESS");
        await using (var d = Db(new SaveFailure(1))) await Rollback("paired result/status/event/audits after SQL save", async () => { await new MaintenanceWorkService(d, new HotelOps.Api.Services.Inventory.InventoryService(d)).SubmitResultAsync(pair.Work, new() { ActorUserId = actor, ResolutionNote = prefix }, default); });
        await using (var d = Db(new CommitFailure())) await Rollback("paired result before commit", async () => { await new MaintenanceWorkService(d, new HotelOps.Api.Services.Inventory.InventoryService(d)).SubmitResultAsync(pair.Work, new() { ActorUserId = actor, ResolutionNote = prefix }, default); });
        foreach (var boundary in new[] { 1, 2 })
        {
            await using var d = Db(new SaveFailure(boundary)); await Rollback("technical log/history save " + boundary, async () => { await new MaintenanceWorkService(d, new HotelOps.Api.Services.Inventory.InventoryService(d)).CreateLogAsync(pair.Work, new() { ActorUserId = actor, WorkNote = prefix, SpentMinutes = 1 }, default); });
        }
        await Command(pair.Work, "submit-result", new { ActorUserId = actor, ResolutionNote = prefix });
        await using (var d = Db(new SaveFailure(1))) await Rollback("return responsibility/status/log/event/audits after SQL save", async () => { await new MaintenanceWorkService(d, new HotelOps.Api.Services.Inventory.InventoryService(d)).ReturnWorkAsync(pair.Work, new() { ActorUserId = verifier, Reason = prefix }, default); });
        await using (var d = Db(new CommitFailure())) await Rollback("return responsibility before commit", async () => { await new MaintenanceWorkService(d, new HotelOps.Api.Services.Inventory.InventoryService(d)).ReturnWorkAsync(pair.Work, new() { ActorUserId = verifier, Reason = prefix }, default); });
        await using (var d = Db(new SaveFailure(1))) await Rollback("paired verification/closure/review/audits after SQL save", async () => { await new MaintenanceWorkService(d, new HotelOps.Api.Services.Inventory.InventoryService(d)).VerifyWorkAsync(pair.Work, new() { ActorUserId = verifier, Note = prefix }, default); });
        foreach (var paired in new[] { false, true })
        {
            var target = paired ? pair.Incident : i.IncidentId;
            await using var blocker = Db(); await using var tx = await blocker.Database.BeginTransactionAsync();
            await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT incident_id FROM dbo.incidents WITH (UPDLOCK,HOLDLOCK) WHERE incident_id={target}");
            await using var worker = Db(); await worker.Database.OpenConnectionAsync(); var workerSpid = await worker.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS Value").SingleAsync();
            using var cancellation = new CancellationTokenSource();
            await Rollback("cancellation waiting Incident lock: " + (paired ? "paired" : "generic"), async () =>
            {
                Task task = paired ? new MaintenanceWorkService(worker, new HotelOps.Api.Services.Inventory.InventoryService(worker)).VerifyWorkAsync(pair.Work, new() { ActorUserId = verifier, Note = prefix }, cancellation.Token) : new IncidentService(worker).ChangeIncidentStatusAsync(i.IncidentId, Change("IN_PROGRESS"), cancellation.Token);
                try
                {
                    var waiting = 0;
                    for (var n = 0; n < 150 && waiting == 0; n++) { waiting = await blocker.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.dm_exec_requests WHERE session_id={workerSpid} AND blocking_session_id<>0 AND wait_type LIKE 'LCK_M_%'").SingleAsync(); if (waiting == 0) await Task.Delay(50); }
                    Check(waiting == 1, "Cancellation actual SQL lock wait");
                }
                finally { cancellation.Cancel(); }
                await task;
            });
            await tx.RollbackAsync();
        }
    }
}
sealed class HoldIncidentRead : DbCommandInterceptor
{
    public TaskCompletionSource Locked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    bool held;
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (!held && command.CommandText.Contains("FROM incidents WITH (UPDLOCK, HOLDLOCK)", StringComparison.OrdinalIgnoreCase))
        {
            held = true; Locked.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
        }
        return result;
    }
}
sealed class LinkageQueryEvidence : DbCommandInterceptor
{
    public bool Observed { get; private set; }
    public bool StatusFiltered { get; private set; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("maintenance_works") && command.CommandText.Contains("EXISTS")) { Observed = true; StatusFiltered = command.CommandText.Contains("[status]", StringComparison.OrdinalIgnoreCase); }
        return ValueTask.FromResult(result);
    }
}
sealed class InjectedFailure : Exception;
sealed class SaveFailure(int boundary) : SaveChangesInterceptor
{
    int count;
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (++count == boundary) throw new InjectedFailure(); return ValueTask.FromResult(result);
    }
}
sealed class CommitFailure : DbTransactionInterceptor
{
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) => throw new InjectedFailure();
}
