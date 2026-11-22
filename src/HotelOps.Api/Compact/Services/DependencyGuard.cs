using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;
public sealed class DependencyGuard(HotelOpsCompactDbContext db)
{
    public async Task BeforeDeactivate(object row,CancellationToken ct)
    {
        var dependent=row switch {
            Branch b=>await db.Rooms.AnyAsync(x=>x.BranchId==b.BranchId&&x.IsActive,ct)||await db.Memberships.AnyAsync(x=>x.BranchId==b.BranchId&&x.ValidTo==null,ct)||await db.Warehouses.AnyAsync(x=>x.BranchId==b.BranchId&&x.IsActive,ct)||await db.Assets.AnyAsync(x=>x.BranchId==b.BranchId&&x.IsActive,ct),
            Department d=>await db.Memberships.AnyAsync(x=>x.DepartmentId==d.DepartmentId&&x.ValidTo==null,ct)||await db.Rooms.AnyAsync(x=>x.DepartmentId==d.DepartmentId&&x.IsActive,ct)||await db.WorkItems.AnyAsync(x=>x.DepartmentId==d.DepartmentId&&!WorkRules.Terminal.Contains(x.Status),ct),
            Location l=>await db.Rooms.AnyAsync(x=>x.LocationId==l.LocationId&&x.IsActive,ct)||await db.Assets.AnyAsync(x=>x.LocationId==l.LocationId&&x.IsActive,ct)||await db.Locations.AnyAsync(x=>x.ParentLocationId==l.LocationId&&x.IsActive,ct),
            RoomType t=>await db.Rooms.AnyAsync(x=>x.RoomTypeId==t.RoomTypeId&&x.IsActive,ct),
            Room r=>r.OperationalStatus=="OCCUPIED"||await db.WorkItems.AnyAsync(x=>x.RoomId==r.RoomId&&!WorkRules.Terminal.Contains(x.Status),ct)||await db.RoomBlocks.AnyAsync(x=>x.RoomId==r.RoomId&&x.ReleasedAt==null,ct),
            Warehouse w=>await db.StockMovements.Where(x=>x.WarehouseId==w.WarehouseId).GroupBy(x=>x.ItemId).AnyAsync(g=>g.Sum(x=>x.SignedBaseQty)!=0,ct)||await db.InventoryDocuments.AnyAsync(x=>(x.SourceWarehouseId==w.WarehouseId||x.DestinationWarehouseId==w.WarehouseId)&&x.ClosedAt==null,ct),
            InventoryItem i=>await db.InventoryDocumentLines.AnyAsync(x=>x.ItemId==i.ItemId,ct),
            ReferenceValue r=>await db.InventoryItems.AnyAsync(x=>x.BaseUnitId==r.ReferenceId||x.CategoryId==r.ReferenceId,ct)||await db.WorkItems.AnyAsync(x=>x.CategoryId==r.ReferenceId||x.PriorityId==r.ReferenceId||x.SeverityId==r.ReferenceId,ct),
            _=>false};
        Output.Require(!dependent,"ACTIVE_DEPENDENCIES_EXIST");
    }
}
