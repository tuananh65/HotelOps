using System.Data;
using System.Globalization;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed class StocktakeWorkspaceService(HotelOpsDbContext db, StocktakeAuthority authority, InsightAccess access, InventoryService inventory)
{
    public async Task<object> ListAsync(int page, CancellationToken ct)
    {
        if (page is < 1 or > 1000000) throw new InsightException(400, "INVALID_PAGE", "Invalid stocktake page.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var candidates = await db.Warehouses.AsNoTracking().Where(w => w.Branch.IsActive).OrderBy(w => w.WarehouseId)
            .Select(w => new { id = w.WarehouseId, name = w.WarehouseName, w.IsActive }).ToArrayAsync(ct);
        var allowed = new List<long>(); var writable = new List<long>();
        foreach (var warehouse in candidates)
        {
            try { await authority.RequireAsync(access.UserId, warehouse.id, "read", ct); allowed.Add(warehouse.id); }
            catch (InventoryAccessException e) when (e.Status == 403) { continue; }
            catch (InsightException e) when (e.Status == 403) { continue; }
            if (!warehouse.IsActive) continue;
            try { await authority.RequireAsync(access.UserId, warehouse.id, "perform", ct); writable.Add(warehouse.id); }
            catch (InventoryAccessException e) when (e.Status == 403) { }
            catch (InsightException e) when (e.Status == 403) { }
        }
        if (allowed.Count == 0) throw new InventoryAccessException(403);
        var query = db.Stocktakes.AsNoTracking().Where(s => allowed.Contains(s.WarehouseId));
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(s => s.StocktakeId).Skip((page - 1) * 25).Take(25)
            .Select(s => new { s.StocktakeId, s.StocktakeNo, s.Status, s.WarehouseId, s.SnapshotAt }).ToArrayAsync(ct);
        foreach (var warehouse in allowed) await authority.RequireAsync(access.UserId, warehouse, writable.Contains(warehouse) ? "perform" : "read", ct);
        await tx.CommitAsync(ct);
        return new { warehouses = candidates.Where(w => allowed.Contains(w.id)).Select(w => new { w.id, w.name, w.IsActive, canPerform = writable.Contains(w.id) }),
            rows = rows.Select(s => new { s.StocktakeId, s.StocktakeNo, s.Status, s.WarehouseId, snapshotAt = DateTime.SpecifyKind(s.SnapshotAt, DateTimeKind.Utc) }), count, page, pageSize = 25 };
    }

    public async Task<object> DetailAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var warehouse = await db.Stocktakes.AsNoTracking().Where(s => s.StocktakeId == id).Select(s => (long?)s.WarehouseId).SingleOrDefaultAsync(ct)
            ?? throw new InventoryAccessException(403);
        await authority.RequireAsync(access.UserId, warehouse, "read", ct);
        var detail = (await inventory.GetStocktake(id, ct))!;
        var names = await db.StocktakeLines.AsNoTracking().Where(l => l.StocktakeId == id)
            .Select(l => new { l.StocktakeLineId, l.Item.ItemName, l.Item.BaseUom.UomName }).ToDictionaryAsync(l => l.StocktakeLineId, ct);
        var canPerform = false;
        try { await authority.RequireAsync(access.UserId, warehouse, "perform", ct); canPerform = true; }
        catch (InventoryAccessException e) when (e.Status == 403) { }
        catch (InsightException e) when (e.Status == 403) { }
        await authority.RequireAsync(access.UserId, warehouse, canPerform ? "perform" : "read", ct);
        await tx.CommitAsync(ct);
        return new { detail.StocktakeId, detail.StocktakeNo, detail.WarehouseId, detail.Status, detail.RowVersion,
            snapshotAt = DateTime.SpecifyKind(detail.SnapshotAt, DateTimeKind.Utc), detail.Note, canPerform,
            lines = detail.Lines.Select(l => new { l.StocktakeLineId, l.ItemId, names[l.StocktakeLineId].ItemName, names[l.StocktakeLineId].UomName,
                systemQty = l.SystemQty.ToString(CultureInfo.InvariantCulture), countedQty = l.CountedQty?.ToString(CultureInfo.InvariantCulture),
                varianceQty = l.VarianceQty?.ToString(CultureInfo.InvariantCulture), l.DiscrepancyReason, l.CountedBy,
                countedAt = l.CountedAt.HasValue ? (DateTime?)DateTime.SpecifyKind(l.CountedAt.Value, DateTimeKind.Utc) : null }) };
    }
}
