using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.DTOs.Replenishments;
using HotelOps.Api.Services.Inventory;
using HotelOps.Api.Services.MaterialRequests;
using Microsoft.EntityFrameworkCore;

sealed partial class ReplenishmentSuite
{
    async Task<bool> Outcome(Func<Task> action) { try { await action(); return true; } catch (ValidationException) { return false; } }
    async Task Race(string name, Func<Task> a, Func<Task> b, bool mayBothSucceed = false, bool requireBoth = false)
    {
        await using var blocker = Db(); await using var tx = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='HotelOps:Inventory:Catalog',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,'Test lock failed',1;");
        var one = Outcome(a); var two = Outcome(b); var waiting = 0;
        for (var i = 0; i < 150 && waiting < 2; i++) { waiting = await blocker.Database.SqlQueryRaw<int>("SELECT COUNT(*) Value FROM sys.dm_tran_locks WHERE resource_database_id=DB_ID() AND resource_type='APPLICATION' AND request_status='WAIT' AND resource_description LIKE '%HotelOps:Inventory:Catalog%'").SingleAsync(); if (waiting < 2) await Task.Delay(50); }
        Check(waiting >= 2 && !one.IsCompleted && !two.IsCompleted, name + " SQL overlap barrier");
        await tx.CommitAsync(); var results = await Task.WhenAll(one, two);
        Check(requireBoth ? results.All(x => x) : mayBothSucceed ? results.Any(x => x) : results.Count(x => x) == 1, name + " winners");
        Console.WriteLine("Race PASS: " + name); races++;
    }
    Func<Task> CreateAction(ReplenishmentCreate input) => async () => { await using var d = Db(); await Service(d).Create(input, default); };
    Func<Task> LinkAction(ReplenishmentDto r, InventoryDocumentDto receipt) => async () => { await using var d = Db(); await Service(d).Receive(r.ReplenishmentId, new(actor, r.RowVersion, [new(receipt.Lines[0].LineId, r.Lines[0].ReplenishmentLineId)]), default); };
    Func<Task> ReverseAction(InventoryDocumentDto receipt) => async () => { await using var d = Db(); await new InventoryService(d).Reverse(receipt.DocumentId, new(actor, receipt.RowVersion, prefix), default); };
    async Task Concurrency()
    {
        await ReleaseAll(); await SetStock(0);
        var source = await ApprovedMR(1000);
        await Race("LOW vs LOW", CreateAction(Input()), CreateAction(Input())); await ReleaseAll();
        await Race("LOW vs MR", CreateAction(Input()), CreateAction(Input(10, source.Lines[0].RequestLineId))); await ReleaseAll();
        await Race("MR vs MR", CreateAction(Input(10, source.Lines[0].RequestLineId)), CreateAction(Input(10, source.Lines[0].RequestLineId))); await ReleaseAll();
        await Race("create vs stock above minimum", CreateAction(Input()), async () => { await using var d = Db(); await new InventoryService(d).CreateAdjustment(new(actor, warehouse.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(item.ItemId, 100)]), default); }, true); await ReleaseAll(); await SetStock(0);
        // Full MR can be satisfied while aggregate demand remains physically short due to independent lines.
        var fulfilled = await ApprovedMR(10); await SetStock(10);
        await Race("MR create vs FE09 fulfillment", CreateAction(Input(5, fulfilled.Lines[0].RequestLineId)), async () => { await using var d = Db(); var inv = new InventoryService(d); await new MaterialRequestService(d, inv, inv).Issue(fulfilled.RequestId, new(issuer, fulfilled.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)), default); }, true); await ReleaseAll(); await SetStock(0);
        var r = await Create(5); var receipt = await Post(await Draft(2));
        await Race("same receipt line twice", LinkAction(r, receipt), LinkAction(r, receipt)); await ReleaseAll();
        r = await Create(3); var d1 = await Post(await Draft(2)); var d2 = await Post(await Draft(2));
        await Race("two receipt lines same remaining", LinkAction(r, d1), LinkAction(r, d2)); await ReleaseAll();
        r = await Create(3); receipt = await Post(await Draft(2));
        await Race("link vs close", LinkAction(r, receipt), async () => { await using var d = Db(); await Service(d).Close(r.ReplenishmentId, new(actor, r.RowVersion, prefix), default); }); await ReleaseAll();
        r = await Create(3); receipt = await Draft(2); var request = Posting(r, receipt, 2);
        await Race("coordinated post vs link", async () => { await using var d = Db(); await Service(d).Receive(r.ReplenishmentId, new(actor, r.RowVersion, request.Lines), default, request); }, LinkAction(r, receipt)); await ReleaseAll();
        r = await Create(3); receipt = await Post(await Draft(2));
        await Race("receipt reversal vs link", ReverseAction(receipt), LinkAction(r, receipt), true); await ReleaseAll();
        r = await Create(2); receipt = await Draft(2); r = await Receive(r, receipt, 2); receipt = await Send<InventoryDocumentDto>("GET", $"inventory/documents/{receipt.DocumentId}");
        await Race("reversal vs competing active coverage", ReverseAction(receipt), CreateAction(Input())); await ReleaseAll();
        await Race("RPL allocation", CreateAction(Input()), CreateAction(new(actor, warehouse.WarehouseId, prefix, null, [new(1, second.ItemId, "LOW_STOCK", secondThreshold.ThresholdId, null, 2)])), true, true); await ReleaseAll();
        r = await Create(3);
        Func<Task> close = async () => { await using var d = Db(); await Service(d).Close(r.ReplenishmentId, new(actor, r.RowVersion, prefix), default); };
        await Race("stale version commands", close, close);
        await SetStock(0);
        await Race("FE10 vs Inventory writer", CreateAction(Input()), async () => { await using var d = Db(); await new InventoryService(d).CreateAdjustment(new(actor, warehouse.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(item.ItemId, 1)]), default); }, true, true); await ReleaseAll();
    }
}
