using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed partial class InventoryService
{
    private bool Changed(object entity)
    {
        db.ChangeTracker.DetectChanges();
        return db.Entry(entity).State is EntityState.Added or EntityState.Modified;
    }
    public async Task<InventoryItemDto?> SaveItem(long? id, InventoryItemWrite r, CancellationToken ct)
    {
        var code = Required(r.ItemCode, "ItemCode", 50); var name = Required(r.ItemName, "ItemName", 200);
        await using var tx = await Begin(r.ActorUserId, ct);
        var x = id.HasValue ? await db.InventoryItems.FromSqlInterpolated($"SELECT * FROM dbo.inventory_items WITH (UPDLOCK,HOLDLOCK) WHERE item_id={id}").SingleOrDefaultAsync(ct) : new InventoryItem();
        if (x is null) return null; if (id.HasValue) Version(x.RowVersion, r.ExpectedRowVersion);
        var before = id.HasValue ? Scalars(x) : null;
        if (await db.InventoryItems.AnyAsync(i => i.ItemCode == code && i.ItemId != id, ct)) throw Invalid("ItemCode already exists.");
        if (!await db.ItemCategories.AnyAsync(c => c.ItemCategoryId == r.CategoryId && c.IsActive, ct) || !await db.UnitsOfMeasures.AnyAsync(u => u.UomId == r.BaseUomId && u.IsActive, ct)) throw Invalid("Active Category and BaseUOM are required.");
        if (id.HasValue && x.BaseUomId != r.BaseUomId && await db.StockMovements.AnyAsync(m => m.ItemId == id, ct)) throw Invalid("Base UOM is immutable after the first stock effect.");
        x.ItemCode = code; x.ItemName = name; x.CategoryId = r.CategoryId; x.BaseUomId = r.BaseUomId; x.IsConsumable = r.IsConsumable; x.IsActive = r.IsActive; x.Description = r.Description; x.Specification = r.Specification;
        var now = DateTime.UtcNow; if (!id.HasValue) { x.CreatedAt = now; db.InventoryItems.Add(x); }
        if (Changed(x))
        {
            if (id.HasValue) x.UpdatedAt = now;
            await Audit(x, "inventory_items", x.ItemId, r.ActorUserId, id.HasValue ? "UPDATE" : "CREATE", "ITEM_SAVED", before, [null], now, ct);
        }
        await tx.CommitAsync(ct); return Dto(x);
    }
    public async Task<InventoryUnitDto?> SaveUnit(long? id, InventoryUnitWrite r, CancellationToken ct)
    {
        var code = Required(r.UomCode, "UomCode", 30); var name = Required(r.UomName, "UomName", 100);
        await using var tx = await Begin(r.ActorUserId, ct);
        var x = id.HasValue ? await db.UnitsOfMeasures.FromSqlInterpolated($"SELECT * FROM dbo.units_of_measure WITH (UPDLOCK,HOLDLOCK) WHERE uom_id={id}").SingleOrDefaultAsync(ct) : new UnitsOfMeasure();
        if (x is null) return null; if (id.HasValue) Version(x.RowVersion, r.ExpectedRowVersion); var before = id.HasValue ? Scalars(x) : null;
        if (await db.UnitsOfMeasures.AnyAsync(u => u.UomCode == code && u.UomId != id, ct)) throw Invalid("UomCode already exists.");
        x.UomCode = code; x.UomName = name; x.IsActive = r.IsActive; x.Description = r.Description; if (!id.HasValue) db.UnitsOfMeasures.Add(x);
        if (Changed(x)) await Audit(x, "units_of_measure", x.UomId, r.ActorUserId, id.HasValue ? "UPDATE" : "CREATE", "UNIT_SAVED", before, [null], DateTime.UtcNow, ct);
        await tx.CommitAsync(ct); return Dto(x);
    }
    public async Task<InventoryConversionDto?> SaveConversion(long item, long? id, InventoryConversionWrite r, CancellationToken ct)
    {
        if (r.ConversionFactor <= 0 || r.ConversionFactor > 999999999999.999999m || decimal.Round(r.ConversionFactor, 6) != r.ConversionFactor) throw Invalid("ConversionFactor must be positive decimal(18,6).");
        await using var tx = await Begin(r.ActorUserId, ct); var catalog = await Item(item, ct);
        if (r.FromUomId == catalog.BaseUomId || !await db.UnitsOfMeasures.AnyAsync(u => u.UomId == r.FromUomId && u.IsActive, ct)) throw Invalid("Conversion requires a different active source UOM.");
        var x = id.HasValue ? await db.UomConversions.FromSqlInterpolated($"SELECT * FROM dbo.uom_conversions WITH (UPDLOCK,HOLDLOCK) WHERE conversion_id={id}").SingleOrDefaultAsync(ct) : new UomConversion();
        if (x is null) return null; if (id.HasValue) { Version(x.RowVersion, r.ExpectedRowVersion); if (x.ItemId != item) throw Invalid("Conversion does not belong to this item."); }
        var before = id.HasValue ? Scalars(x) : null;
        if (await db.UomConversions.AnyAsync(c => c.ItemId == item && c.FromUomId == r.FromUomId && c.ToUomId == catalog.BaseUomId && c.ConversionId != id, ct)) throw Invalid("Conversion already exists.");
        x.ItemId = item; x.FromUomId = r.FromUomId; x.ToUomId = catalog.BaseUomId; x.ConversionFactor = r.ConversionFactor; x.IsActive = r.IsActive;
        if (!id.HasValue) db.UomConversions.Add(x);
        if (Changed(x)) await Audit(x, "uom_conversions", x.ConversionId, r.ActorUserId, id.HasValue ? "UPDATE" : "CREATE", "CONVERSION_SAVED", before, [null], DateTime.UtcNow, ct);
        await tx.CommitAsync(ct); return Dto(x);
    }
    private async Task<bool> WarehouseHistory(long id, CancellationToken ct) =>
        await db.StockDocuments.AnyAsync(d => d.SourceWarehouseId == id || d.DestinationWarehouseId == id, ct) ||
        await db.StockMovements.AnyAsync(m => m.WarehouseId == id, ct) || await db.Stocktakes.AnyAsync(s => s.WarehouseId == id, ct) ||
        await db.StockThresholds.AnyAsync(t => t.WarehouseId == id, ct) || await db.MaterialRequests.AnyAsync(m => m.SourceWarehouseId == id, ct) ||
        await db.MaterialFulfillments.AnyAsync(m => m.WarehouseId == id, ct);
    public async Task<InventoryWarehouseDto?> SaveWarehouse(long? id, InventoryWarehouseWrite r, CancellationToken ct)
    {
        var code = Required(r.WarehouseCode, "WarehouseCode", 40); var name = Required(r.WarehouseName, "WarehouseName", 150);
        await using var tx = await Begin(r.ActorUserId, ct);
        if (id.HasValue && !await db.Warehouses.AnyAsync(w => w.WarehouseId == id, ct)) return null;
        var x = id.HasValue ? (await Warehouses([id.Value], false, ct)).Single() : new Warehouse();
        if (id.HasValue) Version(x.RowVersion, r.ExpectedRowVersion); var before = id.HasValue ? Scalars(x) : null;
        if ((!id.HasValue || x.BranchId != r.BranchId) && !await db.Branches.AnyAsync(b => b.BranchId == r.BranchId && b.IsActive, ct)) throw Invalid("A new warehouse or branch change requires an active branch.");
        if ((!id.HasValue || r.DepartmentId != x.DepartmentId) && !r.DepartmentId.HasValue) throw Invalid("DepartmentId is required for a new warehouse or department change.");
        if (r.DepartmentId.HasValue && !await db.Departments.AnyAsync(d => d.DepartmentId == r.DepartmentId && d.BranchId == r.BranchId && d.IsActive, ct)) throw Invalid("Department must be active in the selected branch.");
        if (id.HasValue && x.BranchId != r.BranchId && await WarehouseHistory(id.Value, ct)) throw Invalid("Warehouse branch is immutable after dependent history.");
        if (r.ManagerUserId.HasValue && !await db.Users.AnyAsync(u => u.UserId == r.ManagerUserId && u.DeletedAt == null && u.AccountStatus == "ACTIVE", ct)) throw Invalid("ManagerUser must be active and not deleted.");
        if (await db.Warehouses.AnyAsync(w => w.BranchId == r.BranchId && w.WarehouseCode == code && w.WarehouseId != id, ct)) throw Invalid("WarehouseCode already exists in this branch.");
        if (id.HasValue && x.IsActive && !r.IsActive)
        {
            if (await db.StockMovements.Where(m => m.WarehouseId == id).GroupBy(m => m.ItemId).AnyAsync(g => g.Sum(m => m.SignedBaseQty) != 0, ct) ||
                await db.StockDocuments.AnyAsync(d => (d.SourceWarehouseId == id || d.DestinationWarehouseId == id) && !TerminalDocuments.Contains(d.Status), ct) ||
                await db.Stocktakes.AnyAsync(s => s.WarehouseId == id && !TerminalStocktakes.Contains(s.Status), ct)) throw Invalid("Warehouse has nonzero stock or documents/stocktakes whose closure cannot be confirmed.");
        }
        x.BranchId = r.BranchId; x.DepartmentId = r.DepartmentId; x.WarehouseCode = code; x.WarehouseName = name; x.ManagerUserId = r.ManagerUserId; x.LocationDescription = r.LocationDescription; x.IsActive = r.IsActive;
        var now = DateTime.UtcNow; if (!id.HasValue) { x.CreatedAt = now; db.Warehouses.Add(x); }
        if (Changed(x)) await Audit(x, "warehouses", x.WarehouseId, r.ActorUserId, id.HasValue ? "UPDATE" : "CREATE", "WAREHOUSE_SAVED", before, [x.BranchId], now, ct);
        await tx.CommitAsync(ct); return Dto(x);
    }
    public async Task<InventoryThresholdDto> SaveThreshold(long warehouse, long item, InventoryThresholdWrite r, CancellationToken ct)
    {
        Qty(r.MinimumQty); await using var tx = await Begin(r.ActorUserId, ct); await Item(item, ct);
        var w = (await Warehouses([warehouse], true, ct)).Single();
        await StockGates([(warehouse, item)], ct);
        var x = await db.StockThresholds.FromSqlInterpolated($"SELECT * FROM dbo.stock_thresholds WITH (UPDLOCK,HOLDLOCK) WHERE warehouse_id={warehouse} AND item_id={item}").SingleOrDefaultAsync(ct);
        var create = x is null; object? before = null;
        if (create) { if (r.ExpectedRowVersion is not null) throw Invalid("Threshold no longer exists."); x = new() { WarehouseId = warehouse, ItemId = item, AlertEnabled = true }; db.StockThresholds.Add(x); }
        else { Version(x!.RowVersion, r.ExpectedRowVersion); before = Scalars(x); }
        x!.MinimumQty = r.MinimumQty; var now = DateTime.UtcNow;
        if (Changed(x)) { x.UpdatedAt = now; await Audit(x, "stock_thresholds", x.ThresholdId, r.ActorUserId, create ? "CREATE" : "UPDATE", "THRESHOLD_SAVED", before, [w.BranchId], now, ct); }
        await tx.CommitAsync(ct); return Dto(x);
    }
    public async Task<IReadOnlyList<InventoryThresholdDto>> GetThresholds(long warehouse, CancellationToken ct) =>
        (await db.StockThresholds.AsNoTracking().Where(x => x.WarehouseId == warehouse).OrderBy(x => x.ItemId).ToListAsync(ct)).Select(Dto).ToList();
}
