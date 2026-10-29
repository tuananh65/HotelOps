using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.DTOs.Replenishments;
using HotelOps.Api.Services.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

sealed partial class ReplenishmentSuite
{
    async Task<string> Footprint()
    {
        await using var d = Db();
        // Fresh context, owned IDs plus global counts detect leaked atomic business/audit writes.
        var totals = new[] { await d.MaterialReplenishments.CountAsync(), await d.MaterialReplenishmentLines.CountAsync(), await d.MaterialReplenishmentReceiptLines.CountAsync(), await d.StockDocuments.CountAsync(), await d.StockDocumentLines.CountAsync(), await d.StockReceiptLineDetails.CountAsync(), await d.StockMovements.CountAsync(), await d.AuditLogs.CountAsync() };
        var roots = await d.MaterialReplenishments.AsNoTracking().Where(x => warehouses.Contains(x.WarehouseId)).OrderBy(x => x.ReplenishmentId).Select(x => new { x.ReplenishmentId, x.Status, x.RowVersion, x.UpdatedAt }).ToArrayAsync();
        var lines = await d.MaterialReplenishmentLines.AsNoTracking().Where(x => warehouses.Contains(x.WarehouseId)).OrderBy(x => x.ReplenishmentLineId).Select(x => new { x.ReplenishmentLineId, x.ReceivedBaseQty, x.IsOpenCoverage }).ToArrayAsync();
        var docs = await d.StockDocuments.AsNoTracking().Where(x => x.StockDocumentLines.Any(l => items.Contains(l.ItemId))).OrderBy(x => x.DocumentId).Select(x => new { x.DocumentId, x.Status, x.RowVersion, x.PostedAt }).ToArrayAsync();
        var stock = await d.StockMovements.Where(x => items.Contains(x.ItemId)).SumAsync(x => (decimal?)x.SignedBaseQty) ?? 0;
        return System.Text.Json.JsonSerializer.Serialize(new { totals, roots, lines, docs, stock });
    }
    async Task Rollback(string name, Func<Task> action)
    {
        var before = await Footprint(); var thrown = false;
        try { await action(); } catch (InjectedFailure) { thrown = true; } catch (OperationCanceledException) { thrown = true; }
        Check(thrown, name + " injection did not fire"); Check(before == await Footprint(), name + " partial persistence leak"); faults++; Console.WriteLine("Rollback PASS: " + name);
    }
    async Task Faults()
    {
        await ReleaseAll(); await SetStock(0);
        foreach (var point in new[] { 1, 2, 3 })
        {
            await using var d = Db(new SaveFailure(point)); await Rollback("create save " + point, async () => { await Service(d).Create(Input(), default); }); Check(!d.ChangeTracker.Entries().Any(), "Create failure clears tracker");
        }
        var r = await Create(10); var receipt = await Draft(5); var input = Posting(r, receipt, 5);
        foreach (var stage in new[] { (1, "Inventory state"), (1, "receipt detail"), (1, "movement"), (2, "Inventory audit"), (3, "receipt link"), (4, "received cache"), (4, "status"), (4, "coverage"), (4, "FE10 audit") })
        {
            await using var d = Db(new SaveFailure(stage.Item1));
            await Rollback(stage.Item2, async () => { await Service(d).Receive(r.ReplenishmentId, new(actor, r.RowVersion, input.Lines), default, input); });
        }
        await using (var d = Db(new CommitFailure())) await Rollback("coordinated before commit", async () => { await Service(d).Receive(r.ReplenishmentId, new(actor, r.RowVersion, input.Lines), default, input); });
        r = await Receive(r, receipt, 5); receipt = await Send<InventoryDocumentDto>("GET", $"inventory/documents/{receipt.DocumentId}");
        foreach (var point in new[] { 1, 2, 4 })
        {
            await using var d = Db(new SaveFailure(point)); await Rollback("reversal save " + point, async () => { await new InventoryService(d).Reverse(receipt.DocumentId, actor, new(receipt.RowVersion, prefix), default); });
        }
        await using (var d = Db(new CommitFailure())) await Rollback("reversal before commit", async () => { await new InventoryService(d).Reverse(receipt.DocumentId, actor, new(receipt.RowVersion, prefix), default); });
        foreach (var resource in new[] { "HotelOps:Inventory:Catalog", $"HotelOps:Replenishment:Owner:{r.ReplenishmentId}", $"HotelOps:Replenishment:Shortage:{warehouse.WarehouseId}:{item.ItemId}" })
        {
            await using var blocker = Db(); await using var tx = await blocker.Database.BeginTransactionAsync();
            await blocker.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;");
            using var cancellation = new CancellationTokenSource(250); await using var d = Db();
            await Rollback("cancellation waiting " + resource, async () => { await Service(d).Close(r.ReplenishmentId, new(actor, r.RowVersion, prefix), cancellation.Token); });
            await tx.RollbackAsync();
        }
        await Close(r);
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
