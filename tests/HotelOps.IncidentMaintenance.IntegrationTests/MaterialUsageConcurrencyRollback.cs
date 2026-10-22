using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.DTOs.Replenishments;
using HotelOps.Api.Services.Inventory;
using HotelOps.Api.Services.MaterialRequests;
using HotelOps.Api.Services.Replenishments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

sealed partial class IntegritySuite
{
    Func<HotelOpsDbContext, Task> UsageAction(IssuedSource s, decimal qty = 6) => async d => { await Maintenance(d).CreateLogAsync(s.Work, UsageInput(s.Document.Lines[0].LineId, qty), default); };
    async Task MaterialOrdered(string name, Func<HotelOpsDbContext, Task> first, Func<HotelOpsDbContext, Task> second, Func<bool[], bool> valid)
    {
        var barrier = new HoldSqlRead("FROM dbo.stock_documents WITH (UPDLOCK,HOLDLOCK)");
        await using var a = Db(barrier); await using var b = Db(); await using var observer = Db();
        await a.Database.OpenConnectionAsync(); await b.Database.OpenConnectionAsync();
        var aId = await a.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS Value").SingleAsync(); var bId = await b.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS Value").SingleAsync();
        var one = Outcome(() => first(a)); Task<bool>? two = null;
        try
        {
            await barrier.Locked.Task.WaitAsync(TimeSpan.FromSeconds(15)); two = Outcome(() => second(b)); var waiting = 0;
            for (var n = 0; n < 150 && waiting == 0; n++) { waiting = await observer.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.dm_exec_requests WHERE session_id={bId} AND blocking_session_id={aId} AND wait_type LIKE 'LCK_M_%'").SingleAsync(); if (waiting == 0) await Task.Delay(50); }
            Check(waiting == 1 && !one.IsCompleted && !two.IsCompleted, name + " SQL Catalog/source-document blocking proof");
        }
        finally { barrier.Release.TrySetResult(); }
        var results = await Task.WhenAll(one, two ?? Task.FromResult(false)); Check(valid(results), name + " serialized outcomes"); races++; Console.WriteLine("Material SQL race PASS: " + name);
    }
    async Task MaterialConcurrency()
    {
        var s = await IssueSource(); await MaterialOrdered("usage/usage remaining 10 competing 6+6", UsageAction(s), UsageAction(s), x => x[0] && !x[1]);
        Check((await Available(s)).Items.Single().ConsumedBaseQty == 6, "Concurrent usage cap");
        foreach (var returnFirst in new[] { false, true })
        {
            s = await IssueSource(); var draft = await ReturnDraft(s, 6);
            Func<HotelOpsDbContext, Task> use = UsageAction(s);
            Func<HotelOpsDbContext, Task> post = async d => { await new InventoryService(d).Post("RETURN", draft.DocumentId, new(actor, draft.RowVersion, [new(draft.Lines[0].LineId, 6)]), default); };
            await MaterialOrdered("usage/RETURN; " + (returnFirst ? "RETURN" : "usage") + " first", returnFirst ? post : use, returnFirst ? use : post, x => x[0] && !x[1]);
            var available = (await Available(s)).Items.Single(); Check(available.ConsumedBaseQty + available.ReturnedBaseQty == 6 && available.RemainingBaseQty == 4, "Usage/RETURN no combined over-allocation");
        }
        s = await IssueSource(10, multi: true);
        Func<HotelOpsDbContext, Task> a = async d => { await Maintenance(d).CreateLogAsync(s.Work, UsageInput(s.Document.Lines[0].LineId, 2), default); };
        Func<HotelOpsDbContext, Task> b = async d => { await Maintenance(d).CreateLogAsync(s.Work, UsageInput(s.Document.Lines[1].LineId, 2), default); };
        await MaterialOrdered("different lines same ISSUE document", a, b, x => x.All(v => v));
        var second = await IssueSource(10, sameWork: s);
        var lines = new[] { s.Document.Lines[0].LineId, second.Document.Lines[0].LineId };
        a = async d => { await Maintenance(d).CreateLogAsync(s.Work, new() { ActorUserId = actor, WorkNote = prefix, MaterialUsages = lines.Select(id => new MaintenanceMaterialUsageWrite(id, usageUnit.UomId, 2)).ToArray() }, default); };
        b = async d => { await Maintenance(d).CreateLogAsync(s.Work, new() { ActorUserId = actor, WorkNote = prefix, MaterialUsages = lines.Reverse().Select(id => new MaintenanceMaterialUsageWrite(id, usageUnit.UomId, 2)).ToArray() }, default); };
        await MaterialOrdered("inverted multi-document payloads", a, b, x => x.All(v => v));
        foreach (var action in new[] { "verify", "cancel", "plain-log", "return" }) foreach (var usageFirst in new[] { true, false })
        {
            s = await IssueSource(); if (action is "verify" or "return") await Command(s.Work, "submit-result", new { ActorUserId = actor, ResolutionNote = prefix });
            var source = s; Func<HotelOpsDbContext, Task> use = UsageAction(source, 2);
            Func<HotelOpsDbContext, Task> command = async d =>
            {
                var service = Maintenance(d);
                if (action == "verify") await service.VerifyWorkAsync(source.Work, new() { ActorUserId = verifier, Note = prefix }, default);
                else if (action == "return") await service.ReturnWorkAsync(source.Work, new() { ActorUserId = verifier, Reason = prefix }, default);
                else if (action == "cancel") await service.CancelWorkAsync(source.Work, new() { ActorUserId = verifier, Reason = prefix }, default);
                else await service.CreateLogAsync(source.Work, new() { ActorUserId = actor, WorkNote = prefix }, default);
            };
            await OrderedRace("usage/" + action + "; " + (usageFirst ? "usage" : action) + " first", usageFirst ? use : command, usageFirst ? command : use, (one, two) =>
                Check(usageFirst ? (action is "verify" or "return" ? !one && two : one && two) : (action is "verify" or "cancel" ? one && !two : one && two), "Maintenance/material serialized outcomes"));
            await using var d = Db(); var consumed = await d.MaintenanceMaterialUsages.Where(x => x.MaintenanceWorkId == s.Work).SumAsync(x => (decimal?)x.UsedBaseQty) ?? 0;
            var expected = action == "verify" || action == "return" && usageFirst || action == "cancel" && !usageFirst ? 0 : 2;
            Check(consumed == expected, "Consumption survives/does not cross terminal boundary");
        }
        foreach (var module in new[] { "Inventory", "MR", "Replenishment" })
        {
            s = await IssueSource(); var source = s;
            long threshold = 0;
            if (module == "Replenishment") threshold = (await Send<InventoryThresholdDto>("PUT", $"inventory/warehouses/{usageWarehouse.WarehouseId}/items/{usageItem.ItemId}/threshold", new InventoryThresholdWrite(actor, 50000))).ThresholdId;
            Func<HotelOpsDbContext, Task> writer = async d =>
            {
                var inv = new InventoryService(d);
                if (module == "Inventory") await inv.CreateAdjustment(new(actor, usageWarehouse.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(usageItem.ItemId, 1)]), default);
                else if (module == "MR") await new MaterialRequestService(d, inv, inv).Save(null, new(actor, branch, usageDepartment, usageWarehouse.WarehouseId, prefix, DateTime.UtcNow, null, null, null, source.Incident, [new(null, 1, usageItem.ItemId, usageUnit.UomId, 1, null)]), default);
                else await new ReplenishmentService(d, inv, inv).Create(new(actor, usageWarehouse.WarehouseId, prefix, null, [new(1, usageItem.ItemId, "LOW_STOCK", threshold, null, 1)]), default);
            };
            await MaterialOrdered("usage/" + module + " Catalog writer", UsageAction(s, 1), writer, x => x.All(v => v));
        }
    }
    async Task MaterialFaults()
    {
        var s = await IssueSource();
        foreach (var boundary in new[] { 1, 2, 3 })
        {
            await using var d = Db(new SaveFailure(boundary)); await Rollback("material log/usage/audit save " + boundary, async () => { await Maintenance(d).CreateLogAsync(s.Work, UsageInput(s.Document.Lines[0].LineId, 2), default); });
        }
        await using (var d = Db(new CommitFailure())) await Rollback("material before commit", async () => { await Maintenance(d).CreateLogAsync(s.Work, UsageInput(s.Document.Lines[0].LineId, 2), default); });
        var draft = await ReturnDraft(s, 2);
        foreach (var boundary in new[] { 1, 2 })
        {
            await using var d = Db(new SaveFailure(boundary)); await Rollback("RETURN state/detail/ledger/audit save " + boundary, async () => { await new InventoryService(d).Post("RETURN", draft.DocumentId, new(actor, draft.RowVersion, [new(draft.Lines[0].LineId, 2)]), default); });
        }
        await using (var d = Db(new CommitFailure())) await Rollback("RETURN before commit with shared cap", async () => { await new InventoryService(d).Post("RETURN", draft.DocumentId, new(actor, draft.RowVersion, [new(draft.Lines[0].LineId, 2)]), default); });
        foreach (var resource in new[] { "Catalog", "Document", "Incident", "Work" })
        {
            await using var blocker = Db(); await using var tx = await blocker.Database.BeginTransactionAsync();
            if (resource == "Catalog") await blocker.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='HotelOps:Inventory:Catalog',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;");
            else if (resource == "Document") await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT document_id FROM dbo.stock_documents WITH (UPDLOCK,HOLDLOCK) WHERE document_id={s.Document.DocumentId}");
            else if (resource == "Incident") await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT incident_id FROM dbo.incidents WITH (UPDLOCK,HOLDLOCK) WHERE incident_id={s.Incident}");
            else await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT maintenance_work_id FROM dbo.maintenance_works WITH (UPDLOCK,HOLDLOCK) WHERE maintenance_work_id={s.Work}");
            await using var worker = Db(); await worker.Database.OpenConnectionAsync(); var spid = await worker.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS Value").SingleAsync(); using var cancellation = new CancellationTokenSource();
            await Rollback("material cancellation waiting " + resource, async () =>
            {
                var task = Maintenance(worker).CreateLogAsync(s.Work, UsageInput(s.Document.Lines[0].LineId, 2), cancellation.Token);
                try
                {
                    var waiting = 0; for (var n = 0; n < 150 && waiting == 0; n++) { waiting = await blocker.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.dm_exec_requests WHERE session_id={spid} AND blocking_session_id<>0 AND wait_type LIKE 'LCK_M_%'").SingleAsync(); if (waiting == 0) await Task.Delay(50); }
                    Check(waiting == 1, "Actual material cancellation SQL wait");
                }
                finally { cancellation.Cancel(); }
                await task;
            });
            await tx.RollbackAsync();
        }
        await using (var d = Db())
        {
            var inv = new InventoryService(d); var denied = false; try { await inv.Capacity(s.Document.Lines[0].LineId, null, default); } catch (InvalidOperationException) { denied = true; } Check(denied, "Capacity requires caller transaction");
            await using var tx = await d.Database.BeginTransactionAsync(); denied = false; try { await inv.Capacity(s.Document.Lines[0].LineId, null, default); } catch (InvalidOperationException) { denied = true; } Check(denied, "Capacity refuses late/missing Catalog");
            await inv.AcquireCatalog(default); var before = await Footprint(); await inv.Capacity(s.Document.Lines[0].LineId, null, default); Check(d.Database.CurrentTransaction == tx && before == await Footprint(), "Capacity neither commits nor writes caller state"); await tx.RollbackAsync();
        }
    }
}
sealed class HoldSqlRead(string marker) : DbCommandInterceptor
{
    public TaskCompletionSource Locked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    bool held;
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (!held && command.CommandText.Contains(marker, StringComparison.OrdinalIgnoreCase)) { held = true; Locked.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken); }
        return result;
    }
}
