using HotelOps.Api.Data;
using HotelOps.Api.Services.Incidents;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.MaterialRequests;

internal static class MaterialCreationCatalog
{
    internal sealed record Choice(long Id, string Name);
    internal sealed record Item(long Id, string Code, string Name, Choice[] Units);
    internal sealed record Catalog(long BranchId, Choice[] Departments, Choice[] Warehouses, Choice[] Rooms,
        Choice[] Areas, Choice[] Priorities, Choice[] Incidents, Item[] Items);

    // Minimal selection data for an authorized material creator; never stock,
    // prices, supplier data or unrelated incident details.
    internal static async Task<Catalog> ReadAsync(HotelOpsDbContext db, long actor, long branch, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Catalog requires a scoped read transaction.");
        var departments = await db.Departments.AsNoTracking().Where(d => d.BranchId == branch && d.IsActive)
            .OrderBy(d => d.DepartmentName).ThenBy(d => d.DepartmentId).Select(d => new Choice(d.DepartmentId, d.DepartmentName)).ToArrayAsync(ct);
        var warehouses = await db.Warehouses.AsNoTracking().Where(w => w.BranchId == branch && w.IsActive)
            .OrderBy(w => w.WarehouseName).ThenBy(w => w.WarehouseId).Select(w => new Choice(w.WarehouseId, w.WarehouseName)).ToArrayAsync(ct);
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.BranchId == branch && r.IsActive && r.Floor.BranchId == branch
            && r.Floor.IsActive && (r.Area == null || r.Area.BranchId == branch && r.Area.IsActive))
            .OrderBy(r => r.RoomNumber).ThenBy(r => r.RoomId).Select(r => new Choice(r.RoomId, r.RoomNumber)).ToArrayAsync(ct);
        var areas = await db.Areas.AsNoTracking().Where(a => a.BranchId == branch && a.IsActive)
            .OrderBy(a => a.AreaName).ThenBy(a => a.AreaId).Select(a => new Choice(a.AreaId, a.AreaName)).ToArrayAsync(ct);
        var priorities = await db.ReferenceValues.AsNoTracking().Where(r => r.GroupCode == "PRIORITY" && r.IsActive)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.ReferenceId).Select(r => new Choice(r.ReferenceId, r.DisplayName)).ToArrayAsync(ct);
        var incidentScope = await IncidentReadAccess.ReadAsync(db, actor, branch, ct);
        var incidents = await incidentScope.Apply(db.Incidents.AsNoTracking()).Where(i => i.Status == "OPEN" || i.Status == "IN_PROGRESS" || i.Status == "RESOLVED")
            .OrderBy(i => i.IncidentNo).ThenBy(i => i.IncidentId).Select(i => new Choice(i.IncidentId, i.IncidentNo + ": " + i.IncidentTitle)).ToArrayAsync(ct);
        var inventory = await db.InventoryItems.AsNoTracking().Where(i => i.IsActive && i.Category.IsActive && i.BaseUom.IsActive)
            .OrderBy(i => i.ItemName).ThenBy(i => i.ItemId).Select(i => new { i.ItemId, i.ItemCode, i.ItemName, i.BaseUomId, i.BaseUom.UomName }).ToArrayAsync(ct);
        var ids = inventory.Select(i => i.ItemId).ToArray();
        var conversions = await db.UomConversions.AsNoTracking().Where(c => c.ItemId != null && ids.Contains(c.ItemId.Value)
            && c.IsActive && c.ConversionFactor > 0 && c.FromUom.IsActive && c.ToUomId == c.Item!.BaseUomId)
            .Select(c => new { c.ItemId, c.FromUomId, c.FromUom.UomName }).ToArrayAsync(ct);
        return new(branch, departments, warehouses, rooms, areas, priorities, incidents, inventory.Select(i =>
            new Item(i.ItemId, i.ItemCode, i.ItemName, conversions.Where(c => c.ItemId == i.ItemId)
                .Select(c => new Choice(c.FromUomId, c.UomName)).Prepend(new(i.BaseUomId, i.UomName))
                .DistinctBy(u => u.Id).ToArray())).ToArray());
    }
}
