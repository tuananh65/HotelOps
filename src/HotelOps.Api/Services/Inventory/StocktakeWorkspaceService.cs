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
        var canDecide = false;
        try { await authority.RequireAsync(access.UserId, warehouse, "approve-adjustment", ct); canDecide = true; }
        catch (InventoryAccessException e) when (e.Status == 403) { }
        catch (InsightException e) when (e.Status == 403) { }
        var balances = await StockBalanceReadService.Posted(db).Where(m => m.WarehouseId == warehouse)
            .GroupBy(m => m.ItemId).Select(g => new { ItemId = g.Key, Quantity = g.Sum(m => m.SignedBaseQty) })
            .ToDictionaryAsync(g => g.ItemId, g => g.Quantity, ct);
        await authority.RequireAsync(access.UserId, warehouse, canDecide ? "approve-adjustment" : canPerform ? "perform" : "read", ct);
        await tx.CommitAsync(ct);
        return new { detail.StocktakeId, detail.StocktakeNo, detail.WarehouseId, detail.Status, detail.RowVersion,
            snapshotAt = DateTime.SpecifyKind(detail.SnapshotAt, DateTimeKind.Utc), detail.Note, canPerform, canDecide, detail.AdjustmentDocumentId,
            lines = detail.Lines.Select(l => new { l.StocktakeLineId, l.ItemId, names[l.StocktakeLineId].ItemName, names[l.StocktakeLineId].UomName,
                currentQty = balances.GetValueOrDefault(l.ItemId).ToString(CultureInfo.InvariantCulture),
                resultingQty = detail.Status == "PENDING_ADJUSTMENT" ? (balances.GetValueOrDefault(l.ItemId) + l.VarianceQty)?.ToString(CultureInfo.InvariantCulture) : null,
                systemQty = l.SystemQty.ToString(CultureInfo.InvariantCulture), countedQty = l.CountedQty?.ToString(CultureInfo.InvariantCulture),
                varianceQty = l.VarianceQty?.ToString(CultureInfo.InvariantCulture), previewVariance = (l.CountedQty - l.SystemQty)?.ToString(CultureInfo.InvariantCulture), l.DiscrepancyReason, l.CountedBy,
                countedAt = l.CountedAt.HasValue ? (DateTime?)DateTime.SpecifyKind(l.CountedAt.Value, DateTimeKind.Utc) : null }) };
    }

    public async Task<object> AdjustmentsAsync(int page, CancellationToken ct)
    {
        if (page is < 1 or > 1000000) throw new InsightException(400, "INVALID_PAGE", "Invalid adjustment page.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var warehouses = await db.Warehouses.AsNoTracking().Where(w => w.IsActive && w.Branch.IsActive)
            .Select(w => new { id = w.WarehouseId, name = w.WarehouseName }).ToArrayAsync(ct);
        var allowed = new List<long>();
        foreach (var warehouse in warehouses)
        {
            try { await authority.RequireAsync(access.UserId, warehouse.id, "adjust", ct); allowed.Add(warehouse.id); }
            catch (InventoryAccessException e) when (e.Status == 403) { }
            catch (InsightException e) when (e.Status == 403) { }
        }
        if (allowed.Count == 0) throw new InventoryAccessException(403);
        var items = await db.InventoryItems.AsNoTracking().Where(i => i.IsActive && i.Category.IsActive && i.BaseUom.IsActive).OrderBy(i => i.ItemId)
            .Select(i => new { id = i.ItemId, name = i.ItemName, unit = i.BaseUom.UomName }).Take(50001).ToArrayAsync(ct);
        if (items.Length > 50000) throw new InsightException(503, "CATALOG_TOO_LARGE", "Item catalogue exceeds the safe workspace limit.");
        var query = db.StockDocuments.AsNoTracking().Where(d => d.DocumentType == "ADJUSTMENT" && d.DestinationWarehouseId != null && allowed.Contains(d.DestinationWarehouseId.Value));
        var count = await query.CountAsync(ct);
        var documents = await query.OrderByDescending(d => d.DocumentId).Skip((page - 1) * 25).Take(25)
            .Include(d => d.StockDocumentLines).ThenInclude(l => l.Item).ThenInclude(i => i.BaseUom).ToArrayAsync(ct);
        var itemIds = documents.SelectMany(d => d.StockDocumentLines).Select(l => l.ItemId).Distinct().ToArray();
        var balances = await StockBalanceReadService.Posted(db).Where(m => allowed.Contains(m.WarehouseId) && itemIds.Contains(m.ItemId))
            .GroupBy(m => new { m.WarehouseId, m.ItemId }).Select(g => new { g.Key.WarehouseId, g.Key.ItemId, Quantity = g.Sum(m => m.SignedBaseQty) })
            .ToDictionaryAsync(g => (g.WarehouseId, g.ItemId), g => g.Quantity, ct);
        foreach (var warehouse in allowed) await authority.RequireAsync(access.UserId, warehouse, "adjust", ct);
        await tx.CommitAsync(ct);
        return new { warehouses = warehouses.Where(w => allowed.Contains(w.id)), items, count, pageSize = 25,
            documents = documents.Select(d => new { d.DocumentId, d.DocumentNo, d.Status, d.DestinationWarehouseId, d.Reason,
                rowVersion = Convert.ToBase64String(d.RowVersion), canPost = d.Status == "DRAFT" && d.CreatedBy != access.UserId,
                lines = d.StockDocumentLines.OrderBy(l => l.LineNo).Select(l => new { l.Item.ItemName, unit = l.Item.BaseUom.UomName,
                    delta = l.RequestedQty?.ToString(CultureInfo.InvariantCulture),
                    currentQty = balances.GetValueOrDefault((d.DestinationWarehouseId!.Value, l.ItemId)).ToString(CultureInfo.InvariantCulture),
                    resultingQty = d.Status == "DRAFT" ? (balances.GetValueOrDefault((d.DestinationWarehouseId!.Value, l.ItemId))
                        + d.StockDocumentLines.Where(other => other.ItemId == l.ItemId).Sum(other => other.RequestedQty ?? 0)).ToString(CultureInfo.InvariantCulture) : null }) }) };
    }
}
