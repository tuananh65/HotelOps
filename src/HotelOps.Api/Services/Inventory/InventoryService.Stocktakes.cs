using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed partial class InventoryService
{
    private async Task<Stocktake?> Stocktake(long id, CancellationToken ct)
    {
        await Gate($"HotelOps:Inventory:Stocktake:{id}", ct);
        var x = await db.Stocktakes.FromSqlInterpolated($"SELECT * FROM dbo.stocktakes WITH (UPDLOCK,HOLDLOCK) WHERE stocktake_id={id}").SingleOrDefaultAsync(ct);
        if (x is not null) await db.Entry(x).Collection(x => x.StocktakeLines).LoadAsync(ct); return x;
    }
    public async Task<InventoryStocktakeDto> CreateStocktake(long actorUserId, InventoryStocktakeCreate r, CancellationToken ct)
    {
        await using var tx = await Begin(actorUserId, ct); var warehouse = (await Warehouses([r.WarehouseId], true, ct)).Single();
        if (await db.Stocktakes.AnyAsync(x => x.WarehouseId == r.WarehouseId && OpenStocktakes.Contains(x.Status), ct)) throw Invalid("A canonical open stocktake already exists.");
        var now = DateTime.UtcNow;
        var balances = await db.StockMovements.Where(x => x.WarehouseId == r.WarehouseId && x.OccurredAt <= now)
            .GroupBy(x => x.ItemId).Select(g => new { ItemId = g.Key, Qty = g.Sum(x => x.SignedBaseQty) }).ToDictionaryAsync(x => x.ItemId, x => x.Qty, ct);
        var thresholds = await db.StockThresholds.Where(x => x.WarehouseId == r.WarehouseId).Select(x => x.ItemId).ToListAsync(ct);
        var x = new Stocktake
        {
            WarehouseId = r.WarehouseId,
            SnapshotAt = now,
            StartedAt = now,
            CreatedAt = now,
            Status = "IN_PROGRESS",
            Note = r.Note,
            StocktakeNo = await Number("ST", now, true, ct)
        };
        foreach (var item in balances.Where(b => b.Value != 0).Select(b => b.Key).Union(thresholds).Order())
            x.StocktakeLines.Add(new() { ItemId = item, SystemQty = balances.GetValueOrDefault(item) });
        db.Stocktakes.Add(x); await Audit(x, "stocktakes", 0, actorUserId, "CREATE", "STOCKTAKE_STARTED", null, [warehouse.BranchId], now, ct, ["lines"]);
        await tx.CommitAsync(ct); return (await GetStocktake(x.StocktakeId, ct))!;
    }
    public async Task<InventoryStocktakeDto?> SaveCounts(long id, long actorUserId, InventoryCounts r, CancellationToken ct)
    {
        await using var tx = await Begin(actorUserId, ct); var x = await Stocktake(id, ct); if (x is null) return null;
        Version(x.RowVersion, r.ExpectedRowVersion); if (x.Status is not ("IN_PROGRESS" or "COUNTED")) throw Invalid("Stocktake is not open for counts.");
        var warehouse = (await Warehouses([x.WarehouseId], true, ct)).Single();
        if (r.Lines is null || r.Lines.Any(x => x is null) || r.Lines.Count == 0 || r.Lines.Select(l => l.StocktakeLineId).Distinct().Count() != r.Lines.Count) throw Invalid("Supply distinct count lines.");
        var before = new { stocktake = Scalars(x), lines = x.StocktakeLines.Select(Scalars).ToList() }; var now = DateTime.UtcNow; var changed = false;
        foreach (var input in r.Lines)
        {
            var l = x.StocktakeLines.SingleOrDefault(l => l.StocktakeLineId == input.StocktakeLineId) ?? throw Invalid("Count line does not belong to this stocktake.");
            if (input.CountedQty.HasValue) Qty(input.CountedQty.Value);
            if (l.CountedQty == input.CountedQty && l.DiscrepancyReason == input.DiscrepancyReason) continue;
            if (l.CountedQty != input.CountedQty)
            {
                l.CountedQty = input.CountedQty; l.CountedBy = input.CountedQty.HasValue ? actorUserId : null; l.CountedAt = input.CountedQty.HasValue ? now : null;
            }
            l.DiscrepancyReason = input.DiscrepancyReason; l.VarianceQty = null; changed = true;
        }
        if (changed)
        {
            db.Entry(x).Property(s => s.Status).IsModified = true;
            await Audit(x, "stocktakes", id, actorUserId, "UPDATE", "COUNTS_SAVED", before, [warehouse.BranchId], now, ct, ["lines"]);
        }
        await tx.CommitAsync(ct); return await GetStocktake(id, ct);
    }
    public async Task<InventoryStocktakeDto?> StocktakeAction(long id, string action, long actorUserId, InventoryCommand r, CancellationToken ct)
    {
        await using var tx = await Begin(actorUserId, ct); var x = await Stocktake(id, ct); if (x is null) return null;
        Version(x.RowVersion, r.ExpectedRowVersion); var warehouse = (await Warehouses([x.WarehouseId], true, ct)).Single();
        var before = new { stocktake = Scalars(x), lines = x.StocktakeLines.Select(Scalars).ToList() }; var now = DateTime.UtcNow;
        switch (action)
        {
            case "submit":
                if (x.Status != "IN_PROGRESS" || x.StocktakeLines.Any(l => l.CountedQty is null)) throw Invalid("IN_PROGRESS stocktake must have every line counted.");
                x.Status = "COUNTED"; x.CountedBy = actorUserId; x.CompletedAt = now; break;
            case "reconcile":
                if (x.Status != "COUNTED" || x.StocktakeLines.Any(l => l.CountedQty is null)) throw Invalid("A fully counted COUNTED stocktake is required.");
                foreach (var l in x.StocktakeLines) { l.VarianceQty = l.CountedQty - l.SystemQty; if (l.VarianceQty != 0) Reason(l.DiscrepancyReason); }
                x.Status = x.StocktakeLines.All(l => l.VarianceQty == 0) ? "RECONCILED" : "PENDING_ADJUSTMENT"; break;
            case "approve-adjustment":
                if (x.Status != "PENDING_ADJUSTMENT" || x.AdjustmentDocumentId.HasValue) throw Invalid("Stocktake is not awaiting its first adjustment.");
                if (x.CountedBy == actorUserId || x.StocktakeLines.Any(l => l.CountedBy == actorUserId)) throw Invalid("Adjustment approver must differ from counting/performing actors.");
                var deltas = x.StocktakeLines.Where(l => l.VarianceQty != 0).Select(l => new InventoryDelta(l.ItemId, l.VarianceQty ?? throw Invalid("Variance missing."))).ToList();
                if (deltas.Count == 0) throw Invalid("No adjustment is required.");
                var document = await Adjustment(actorUserId, new(x.WarehouseId, DateOnly.FromDateTime(now), Reason(r.Reason), deltas), [warehouse], now, postApprovedStocktake: true, ct);
                x.AdjustmentDocumentId = document.DocumentId; x.Status = "RECONCILED"; x.ApprovedBy = actorUserId; x.ApprovedAt = now; break;
            case "return":
                if (x.Status != "PENDING_ADJUSTMENT") throw Invalid("Only PENDING_ADJUSTMENT may be returned.");
                x.Note = Reason(r.Reason); x.Status = "COUNTED"; break;
            case "cancel":
                if (x.Status != "IN_PROGRESS") throw Invalid("Only IN_PROGRESS may be cancelled.");
                x.Note = Reason(r.Reason); x.Status = "CANCELLED"; break;
            default: throw Invalid("Unknown stocktake action.");
        }
        var lineChanges = db.ChangeTracker.Entries<StocktakeLine>().Any(e => e.Entity.StocktakeId == id &&
            e.State == EntityState.Modified && e.Properties.Any(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)));
        await Audit(x, "stocktakes", id, actorUserId, action == "cancel" ? "CANCEL" : "UPDATE", action, before, [warehouse.BranchId], now, ct, lineChanges ? ["lines"] : null);
        await tx.CommitAsync(ct); return await GetStocktake(id, ct);
    }
}
