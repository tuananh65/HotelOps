using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.Services.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

sealed partial class MaterialSuite
{
    async Task<bool> Outcome(Func<Task> action)
    {
        try { await action(); return true; } catch (ValidationException) { return false; }
    }
    async Task Race(Func<Task> first, Func<Task> second, string name, bool bothMaySucceed = false)
    {
        await using var blocker = Db(); await using var tx = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='HotelOps:Inventory:Catalog',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,'Test blocker failed',1;");
        var a = Outcome(first); var b = Outcome(second);
        // SQL lock-manager barrier proves both connections are actually waiting,
        // including Inventory's direct ADO commands (outside EF interceptors).
        var waiting = 0;
        for (var attempt = 0; attempt < 150 && waiting < 2; attempt++)
        {
            waiting = await blocker.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sys.dm_tran_locks WHERE resource_database_id=DB_ID() AND resource_type='APPLICATION' AND request_status='WAIT' AND resource_description LIKE '%HotelOps:Inventory:Catalog%'").SingleAsync();
            if (waiting < 2) await Task.Delay(50);
        }
        Check(waiting >= 2, name + " SQL wait barrier not reached");
        Check(!a.IsCompleted && !b.IsCompleted, name + " operations did not overlap under SQL lock");
        await tx.CommitAsync(); var result = await Task.WhenAll(a, b);
        Check(bothMaySucceed ? result.Any(x => x) : result.Count(x => x) == 1, name + " unexpected winners"); races++;
    }
    async Task Races()
    {
        var r = await Create();
        await Race(async () => { await using var d = Db(); await Service(d).Save(r.RequestId, Edit(r) with { Purpose = prefix + " race" }, default); },
            async () => { await using var d = Db(); await Service(d).Command(r.RequestId, "submit", new(actor, r.RowVersion, AssignedApproverUserId: approver), default); }, "edit vs submit");
        r = await Create();
        await Race(async () => { await using var d = Db(); await Service(d).Command(r.RequestId, "submit", new(actor, r.RowVersion, AssignedApproverUserId: approver), default); },
            async () => { await using var d = Db(); await Service(d).Command(r.RequestId, "cancel", new(actor, r.RowVersion, prefix), default); }, "submit vs cancel");
        r = await Create(Write(4, true));
        await Race(async () => { await using var d = Db(); await Service(d).Command(r.RequestId, "approve", new(approver, r.RowVersion), default, new(approver, r.RowVersion, [new(r.Lines[0].RequestLineId, 4)])); },
            async () => { await using var d = Db(); await Service(d).Command(r.RequestId, "reject", new(approver, r.RowVersion, prefix), default); }, "approve vs reject");
        r = await Create(Write(4, true));
        await Race(async () => { await using var d = Db(); await Service(d).Command(r.RequestId, "approve", new(approver, r.RowVersion), default, new(approver, r.RowVersion, [new(r.Lines[0].RequestLineId, 4)])); },
            async () => { await using var d = Db(); await Service(d).Command(r.RequestId, "cancel", new(actor, r.RowVersion, prefix), default); }, "approve vs cancel");
        r = await Approved(4);
        await Race(async () => { await using var d = Db(); await Service(d).Issue(r.RequestId, new(issuer, r.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)), default); },
            async () => { await using var d = Db(); await Service(d).Command(r.RequestId, "cancel", new(actor, r.RowVersion, prefix), default); }, "fulfill vs cancel");
        r = await Approved(4);
        Func<Task> partial = async () => { await using var d = Db(); await Service(d).Issue(r.RequestId, new(issuer, r.RowVersion, "Partial", DateOnly.FromDateTime(DateTime.UtcNow), [new(r.Lines[0].RequestLineId, 3)]), default); };
        await Race(partial, partial, "two partial same remaining"); Check((await Get(r.RequestId)).Lines[0].FulfilledQty == 3, "Overissued remaining");
        r = await Approved(4); await Issue(r, 1); r = await Get(r.RequestId);
        await Race(async () => { await using var d = Db(); await Service(d).Issue(r.RequestId, new(issuer, r.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)), default); },
            async () => { await using var d = Db(); await Service(d).Command(r.RequestId, "close-remainder", new(approver, r.RowVersion, prefix), default); }, "issue vs close");
        await Race(async () => { await using var d = Db(); await Service(d).Save(null, Write(), default); }, async () => { await using var d = Db(); await Service(d).Save(null, Write(), default); }, "MR number", true);
        var fa = await Approved(1); var fb = await Approved(1);
        await Race(async () => { await using var d = Db(); await Service(d).Issue(fa.RequestId, new(issuer, fa.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)), default); },
            async () => { await using var d = Db(); await Service(d).Issue(fb.RequestId, new(issuer, fb.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)), default); }, "FUL concurrent allocation", true);
        var s = await Send<InventoryStockDto>("GET", $"inventory/warehouses/{warehouse.WarehouseId}/items/{item.ItemId}/stock");
        var a = await Approved(s.OnHand); var b = await Approved(s.OnHand);
        await Race(async () => { await using var d = Db(); await Service(d).Issue(a.RequestId, new(issuer, a.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)), default); },
            async () => { await using var d = Db(); await Service(d).Issue(b.RequestId, new(issuer, b.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)), default); }, "two requests same stock and FUL allocation");
        await Send<InventoryDocumentDto>("POST", "inventory/adjustments", new InventoryAdjustment(actor, warehouse.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(item.ItemId, 1000)]), 201);
        r = await Approved(1000);
        await Race(async () => { await using var d = Db(); await Service(d).Issue(r.RequestId, new(issuer, r.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)), default); },
            async () => { await using var d = Db(); await new InventoryService(d).CreateAdjustment(new(actor, warehouse.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(item.ItemId, -1000)]), default); }, "FE09 vs Inventory writer");
        await Send<InventoryDocumentDto>("POST", "inventory/adjustments", new InventoryAdjustment(actor, warehouse.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(item.ItemId, 1000)]), 201);
    }
    async Task Rollbacks()
    {
        var r = await Approved(5);
        string? rolledBackNumber = null;
        // Save boundaries: header, fulfillment lines, ISSUE/movements, Inventory audit, FE09 totals/audits.
        foreach (var boundary in Enumerable.Range(1, 5))
        {
            var failure = new SaveFailure(boundary);
            await using var context = Db(failure);
            await AssertRollback(r, async () => await Service(context).Issue(r.RequestId, new(issuer, r.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)), default), $"after SaveChanges {boundary}");
            rolledBackNumber ??= failure.Number;
            Check(!context.ChangeTracker.Entries().Any(), "Failed transaction must discard tracked state");
        }
        await using (var context = Db()) await AssertRollback(r, async () => await Service(context, new PostingFailure(new InventoryService(context), false)).Issue(r.RequestId, new(issuer, r.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)), default), "primitive entry");
        await using (var context = Db()) await AssertRollback(r, async () => await Service(context, new PostingFailure(new InventoryService(context), true)).Issue(r.RequestId, new(issuer, r.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)), default), "primitive completed");
        await using (var context = Db(new CommitFailure())) await AssertRollback(r, async () => await Service(context).Issue(r.RequestId, new(issuer, r.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)), default), "before commit");
        await using (var context = Db(new SaveFailure(5, before: true))) await AssertRollback(r, async () => await Service(context).Issue(r.RequestId, new(issuer, r.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)), default), "during FE09 audit save");
        using var cts = new CancellationTokenSource();
        await using (var context = Db(new SaveFailure(2, cts))) await AssertRollback(r, async () => await Service(context).Issue(r.RequestId, new(issuer, r.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)), cts.Token), "cancellation during transaction");
        await using (var blocker = Db())
        {
            await using var tx = await blocker.Database.BeginTransactionAsync();
            await blocker.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='HotelOps:Inventory:Catalog',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;");
            using var waitingCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            await using var context = Db();
            var cancelled = false;
            try { await Service(context).Issue(r.RequestId, new(issuer, r.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)), waitingCancellation.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && !context.ChangeTracker.Entries().Any(), "Cancellation while waiting for Catalog lock must propagate and discard state");
            await tx.RollbackAsync();
        }
        var committed = await Issue(r); Check(committed.FulfillmentNo == rolledBackNumber, "Rolled-back FUL number should not be consumed");
    }
    async Task AssertRollback(MaterialRequestDto r, Func<Task> action, string boundary)
    {
        async Task<(int D, int L, int F, int FL, int M, int A, decimal Q)> Counts()
        {
            await using var d = Db(); return (await d.StockDocuments.CountAsync(), await d.StockDocumentLines.CountAsync(), await d.MaterialFulfillments.CountAsync(), await d.MaterialFulfillmentLines.CountAsync(), await d.StockMovements.CountAsync(), await d.AuditLogs.CountAsync(), await d.StockMovements.Where(x => x.WarehouseId == warehouse.WarehouseId && x.ItemId == item.ItemId).SumAsync(x => x.SignedBaseQty));
        }
        var before = await Counts(); var thrown = false;
        try { await action(); } catch (InjectedFailure) { thrown = true; } catch (OperationCanceledException) { thrown = true; }
        Check(thrown, boundary + " fault did not fire"); var after = await Counts(); Check(before == after, boundary + " leaked documents/movements/audits/stock");
        var fresh = await Get(r.RequestId); Check(fresh.RawStatus == r.RawStatus && fresh.Lines[0].FulfilledQty == 0 && fresh.Fulfillments.Count == 0, boundary + " leaked FE09 state"); faults++;
    }
}
sealed class InjectedFailure : Exception;
sealed class SaveFailure(int boundary, CancellationTokenSource? cancellation = null, bool before = false) : SaveChangesInterceptor
{
    int count;
    public string? Number { get; private set; }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (before && count + 1 == boundary) throw new InjectedFailure();
        return ValueTask.FromResult(result);
    }
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (++count == boundary) { Number = eventData.Context?.ChangeTracker.Entries<HotelOps.Api.Entities.MaterialFulfillment>().Select(x => x.Entity.FulfillmentNo).FirstOrDefault(); if (cancellation != null) { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); } throw new InjectedFailure(); }
        return ValueTask.FromResult(result);
    }
}
sealed class PostingFailure(IInventoryIssuePosting inner, bool after) : IInventoryIssuePosting
{
    public async Task<InventoryIssueResult> PostIssue(InventoryIssueRequest request, CancellationToken ct) { if (after) await inner.PostIssue(request, ct); throw new InjectedFailure(); }
}
sealed class CommitFailure : DbTransactionInterceptor
{
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) => throw new InjectedFailure();
}
