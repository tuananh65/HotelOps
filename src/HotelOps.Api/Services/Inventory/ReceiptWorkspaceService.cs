using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed record ReceiptItem(long Id, string Code, string Name, long BaseUnitId);
public sealed record ReceiptConversion(long ItemId, long UnitId);
public sealed record ReceiptWorkspace(InventoryLookup[] Warehouses, ReceiptItem[] Items, InventoryLookup[] Units,
    ReceiptConversion[] Conversions, InventoryPaged<InventoryDocumentDto> Documents);
public sealed record ReceiptDetail(InventoryDocumentDto Document, ReceiptSource? Source, string[] ExpectedQuantities);

public sealed class ReceiptWorkspaceService(HotelOpsDbContext db, ReceiptDraftAuthority authority, InsightAccess access,
    InventoryService inventory, IPermissionService permissions)
{
    public async Task<ReceiptWorkspace> ReadAsync(int page, CancellationToken ct)
    {
        if (page is < 1 or > 1000000) throw new InsightException(400, "INVALID_PAGE", "Invalid receipt page.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await authority.InstalledAsync(ct);
        var candidates = await db.Warehouses.AsNoTracking().Where(w => w.Branch.IsActive).OrderBy(w => w.WarehouseId).Select(w => w.WarehouseId).ToArrayAsync(ct);
        var allowed = new List<long>();
        foreach (var id in candidates)
        {
            try { await authority.RequireAsync(access.UserId, id, ct); allowed.Add(id); }
            catch (InsightException e) when (e.Status == 403) { }
        }
        if (allowed.Count == 0) throw new InsightException(403, "ACCESS_DENIED", "No authorized receiving warehouses.");
        var warehouses = await db.Warehouses.AsNoTracking().Where(w => allowed.Contains(w.WarehouseId))
            .OrderBy(w => w.WarehouseCode).Select(w => new InventoryLookup(w.WarehouseId, w.WarehouseCode, w.WarehouseName, w.IsActive)).ToArrayAsync(ct);
        var source = db.InventoryItems.AsNoTracking().Where(i => i.IsActive && i.Category.IsActive && i.BaseUom.IsActive);
        if (await source.CountAsync(ct) > 50000) throw new InsightException(503, "CATALOG_LIMIT", "Receipt catalogue exceeds the supported selection size.");
        var items = await source.OrderBy(i => i.ItemCode).Select(i => new ReceiptItem(i.ItemId, i.ItemCode, i.ItemName, i.BaseUomId)).ToArrayAsync(ct);
        var units = await db.UnitsOfMeasures.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.UomCode)
            .Select(u => new InventoryLookup(u.UomId, u.UomCode, u.UomName, u.IsActive)).ToArrayAsync(ct);
        var conversions = await db.UomConversions.AsNoTracking().Where(c => c.IsActive && c.ItemId != null && c.Item!.IsActive && c.FromUom.IsActive && c.ToUomId == c.Item.BaseUomId)
            .Select(c => new ReceiptConversion(c.ItemId!.Value, c.FromUomId)).ToArrayAsync(ct);
        var docs = db.StockDocuments.AsNoTracking().Where(d => d.DocumentType == "RECEIPT" && d.SourceWarehouseId == null
            && d.DestinationWarehouseId != null && allowed.Contains(d.DestinationWarehouseId.Value));
        var count = await docs.CountAsync(ct);
        var ids = await docs.OrderByDescending(d => d.DocumentId).Skip((page - 1) * 25).Take(25).Select(d => d.DocumentId).ToArrayAsync(ct);
        var rows = new List<InventoryDocumentDto>();
        foreach (var id in ids) rows.Add((await inventory.GetDocument(id, ct))!);
        foreach (var id in allowed) await authority.RequireAsync(access.UserId, id, ct);
        await tx.CommitAsync(ct);
        return new(warehouses, items, units, conversions, new(rows, count, page, 25));
    }

    public async Task<ReceiptDetail> DetailAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await authority.InstalledAsync(ct);
        var warehouse = await db.StockDocuments.AsNoTracking().Where(d => d.DocumentId == id && d.DocumentType == "RECEIPT" && d.SourceWarehouseId == null)
            .Select(d => d.DestinationWarehouseId).SingleOrDefaultAsync(ct) ?? throw new InsightException(404, "NOT_FOUND", "Receipt unavailable.");
        await authority.RequireAsync(access.UserId, warehouse, ct);
        var document = (await inventory.GetDocument(id, ct))!;
        var source = await authority.SourceAsync(id, ct);
        if (source?.ReplenishmentId != null)
        {
            var branch = await db.Warehouses.Where(w => w.WarehouseId == warehouse).Select(w => w.BranchId).SingleAsync(ct);
            InsightAccess.Require(await permissions.EvaluateBranchAsync("REPLENISHMENT_VIEW", branch, ct));
        }
        await authority.RequireAsync(access.UserId, warehouse, ct);
        await tx.CommitAsync(ct);
        return new(document, source, document.Lines.Select(l => l.RequestedQty?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "").ToArray());
    }
}
