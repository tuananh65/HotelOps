using System.Data;
using System.Globalization;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed class TransferWorkspaceService(HotelOpsDbContext db, TransferAuthority authority,
    InsightAccess access, InventoryService inventory)
{
    public async Task<object> ReadAsync(int page, CancellationToken ct)
    {
        if (page is < 1 or > 1000000) throw new InsightException(400, "INVALID_PAGE", "Invalid transfer page.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var candidates = await db.Warehouses.AsNoTracking().Where(w => w.Branch.IsActive).OrderBy(w => w.WarehouseId)
            .Select(w => new { w.WarehouseId, w.WarehouseCode, w.WarehouseName, w.IsActive }).ToArrayAsync(ct);
        var allowed = new List<long>(); var writable = new List<long>();
        foreach (var w in candidates)
        {
            try { await authority.RequireWarehouseAsync(access.UserId, w.WarehouseId, false, ct); allowed.Add(w.WarehouseId); }
            catch (InventoryAccessException e) when (e.Status == 403) { continue; }
            catch (InsightException e) when (e.Status == 403) { continue; }
            if (!w.IsActive) continue;
            try { await authority.RequireWarehouseAsync(access.UserId, w.WarehouseId, true, ct); writable.Add(w.WarehouseId); }
            catch (InventoryAccessException e) when (e.Status == 403) { }
            catch (InsightException e) when (e.Status == 403) { }
        }
        if (allowed.Count == 0) throw new InventoryAccessException(403);
        var source = db.InventoryItems.AsNoTracking().Where(i => i.IsActive && i.Category.IsActive && i.BaseUom.IsActive);
        if (await source.CountAsync(ct) > 50000) throw new InsightException(503, "CATALOG_LIMIT", "Item catalogue exceeds supported selection size.");
        var items = await source.OrderBy(i => i.ItemCode).Select(i => new { id = i.ItemId, name = i.ItemName, baseUnitId = i.BaseUomId }).ToArrayAsync(ct);
        var units = await db.UnitsOfMeasures.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.UomCode)
            .Select(u => new { id = u.UomId, name = u.UomName }).ToArrayAsync(ct);
        var conversions = await db.UomConversions.AsNoTracking().Where(c => c.IsActive && c.ItemId != null && c.Item!.IsActive && c.FromUom.IsActive && c.ToUomId == c.Item.BaseUomId)
            .Select(c => new { itemId = c.ItemId, unitId = c.FromUomId }).ToArrayAsync(ct);
        var docs = db.StockDocuments.AsNoTracking().Where(d => d.DocumentType == "TRANSFER"
            && d.SourceWarehouseId != null && d.DestinationWarehouseId != null && d.SourceWarehouseId != d.DestinationWarehouseId
            && allowed.Contains(d.SourceWarehouseId.Value) && allowed.Contains(d.DestinationWarehouseId.Value));
        var count = await docs.CountAsync(ct);
        var documents = await docs.OrderByDescending(d => d.DocumentId).Skip((page - 1) * 25).Take(25)
            .Select(d => new { d.DocumentId, d.DocumentNo, d.Status, d.SourceWarehouseId, d.DestinationWarehouseId, d.DocumentDate }).ToArrayAsync(ct);
        foreach (var warehouse in allowed) await authority.RequireWarehouseAsync(access.UserId, warehouse, writable.Contains(warehouse), ct);
        await tx.CommitAsync(ct);
        return new { warehouses = candidates.Where(w => allowed.Contains(w.WarehouseId)).Select(w => new { id = w.WarehouseId, name = w.WarehouseName, w.IsActive, canWrite = writable.Contains(w.WarehouseId) }),
            items, units, conversions, documents, count, page, pageSize = 25 };
    }

    public async Task<object> DetailAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var root = await db.StockDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.DocumentId == id && d.DocumentType == "TRANSFER", ct)
            ?? throw new InventoryAccessException(403);
        await authority.RequireAsync(access.UserId, root, "read", ct);
        var document = (await inventory.GetDocument(id, ct))!;
        var names = await db.StockDocumentLines.AsNoTracking().Where(l => l.DocumentId == id)
            .Select(l => new { l.LineId, l.Item.ItemName, l.Uom.UomName }).ToDictionaryAsync(l => l.LineId, ct);
        var quantities = document.Lines.Select(l => new
        {
            l.LineId, names[l.LineId].ItemName, names[l.LineId].UomName,
            planned = l.RequestedQty?.ToString(CultureInfo.InvariantCulture),
            dispatched = l.ActualQty.ToString(CultureInfo.InvariantCulture),
            received = l.ReceivedQty?.ToString(CultureInfo.InvariantCulture),
            damaged = l.DamagedQty?.ToString(CultureInfo.InvariantCulture),
            missing = l.MissingQty?.ToString(CultureInfo.InvariantCulture),
            outstanding = l.InTransitQty?.ToString(CultureInfo.InvariantCulture)
        }).ToArray();
        await authority.RequireAsync(access.UserId, root, "read", ct);
        await tx.CommitAsync(ct);
        return new { document, quantities };
    }
}
