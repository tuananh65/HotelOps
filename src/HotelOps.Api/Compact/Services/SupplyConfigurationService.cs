using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed class SupplyConfigurationService(HotelOpsCompactDbContext db,Access access,Actor actor,Atomic atomic,Audit audit,TimeProvider clock,LowStockService lowStock)
{
    public async Task<IReadOnlyList<SupplyConfigurationDto>> List(long branchId,CancellationToken ct)
    {
        if(!await access.Allows("ROOM_SUPPLY_CONFIG",branchId,ct:ct))await access.Require("STAFF_SUPPLY_VIEW",branchId,true,ct);
        var rows=await db.SupplyConfigurations.AsNoTracking().Where(x=>x.BranchId==branchId&&x.IsCurrent).OrderBy(x=>x.ConfigurationId).ToArrayAsync(ct);
        // Own-scope staff may see only the group-A definitions used by their allocations.
        if(!await access.Allows("STAFF_SUPPLY_VIEW",branchId,ct:ct)&&!await access.Allows("ROOM_SUPPLY_CONFIG",branchId,ct:ct))
            rows=rows.Where(x=>x.SupplyGroup=="A").ToArray();
        var result=new List<SupplyConfigurationDto>();foreach(var row in rows)result.Add(await Dto(row,ct));return result;
    }
    public Task<SupplyConfigurationDto> Save(SaveSupplyConfigurationRequest r,CancellationToken ct)=>atomic.Run(()=>SaveCore(r,true,ct),ct);
    private async Task<SupplyConfigurationDto> SaveCore(SaveSupplyConfigurationRequest r,bool reconcile,CancellationToken ct){
        await access.RequireRolePermission("OPERATION_MANAGER","ROOM_SUPPLY_CONFIG",r.BranchId,ct);
        await StaffSupplyService.Lock(db,ct);
        Output.Require(r.SupplyGroup is "A" or "B" or "C","INVALID_SUPPLY_GROUP",400);
        Output.Require((r.ItemId!=null) != (r.AssetId!=null),"ONE_SUPPLY_SUBJECT_REQUIRED",400);
        Output.Require(r.InitialQuantity>=0&&r.MinimumStock>=0&&r.PerGuestQuantity>=0&&new[]{r.InitialQuantity,r.MinimumStock,r.PerGuestQuantity}.All(SupplyMath.ValidQuantity),"INVALID_SUPPLY_QUANTITY",400);
        Output.Require(r.SupplyGroup=="A"||r.InitialQuantity==0&&r.PerGuestQuantity==0,"NON_FILL_SUPPLY_CANNOT_AUTO_ISSUE",400);
        Output.Require(r.AssetId==null||r.SupplyGroup=="C","ASSET_REQUIRES_EXTERNAL_REPAIR_GROUP",400);
        if(r.ItemId!=null){
            var item=await db.InventoryItems.AsNoTracking().SingleOrDefaultAsync(x=>x.ItemId==r.ItemId&&x.IsActive,ct)??throw new ApiException(409,"ITEM_UNAVAILABLE");
            Output.Require(r.UnitId==item.BaseUnitId,"SUPPLY_BASE_UNIT_REQUIRED",400);
            Output.Require(await db.ReferenceValues.AnyAsync(x=>x.ReferenceId==r.UnitId&&x.Kind=="UOM"&&x.IsActive,ct),"UNIT_UNAVAILABLE");
        }else{
            Output.Require(await db.Assets.AnyAsync(x=>x.AssetId==r.AssetId&&x.BranchId==r.BranchId&&x.IsActive,ct),"ASSET_UNAVAILABLE");
            Output.Require(r.UnitId==null&&r.WarehouseId==null&&r.MinimumStock==0,"EXTERNAL_ASSET_NOT_STOCKED",400);
        }
        long? warehouseId=null;
        if(r.ItemId!=null){
            var warehouseIds=await db.Warehouses.AsNoTracking().Where(x=>x.BranchId==r.BranchId&&x.IsActive).Select(x=>x.WarehouseId).ToArrayAsync(ct);
            Output.Require(warehouseIds.Length==1,"BRANCH_REQUIRES_ONE_ACTIVE_WAREHOUSE");
            // The branch owns one warehouse. Older clients may still send its ID;
            // validate it, but never allow a per-item warehouse override.
            warehouseId=warehouseIds[0];
            Output.Require(r.WarehouseId==null||warehouseId==r.WarehouseId,"WAREHOUSE_UNAVAILABLE");
        }
        if(r.SupplyGroup=="A"&&r.IsActive)Output.Require(r.InitialQuantity>0&&r.PerGuestQuantity>0,"FILL_SUPPLY_CONFIGURATION_INCOMPLETE",400);
        var previous=await db.SupplyConfigurations.SingleOrDefaultAsync(x=>x.BranchId==r.BranchId&&x.ItemId==r.ItemId&&x.AssetId==r.AssetId&&x.IsCurrent,ct);
        if(previous!=null){Output.Version(previous.RowVersion,r.RowVersion??"");previous.IsCurrent=false;await db.SaveChangesAsync(ct);}
        else Output.Require(string.IsNullOrEmpty(r.RowVersion),"SUPPLY_CONFIGURATION_NOT_FOUND",404);
        var row=new SupplyConfiguration{BranchId=r.BranchId,ItemId=r.ItemId,AssetId=r.AssetId,SupplyGroup=r.SupplyGroup,WarehouseId=warehouseId,UnitId=r.UnitId,
            InitialQuantity=r.InitialQuantity,MinimumStock=r.MinimumStock,PerGuestQuantity=r.PerGuestQuantity,IsActive=r.IsActive,Revision=(previous?.Revision??0)+1,IsCurrent=true,UpdatedBy=actor.Id,UpdatedAt=clock.GetUtcNow().UtcDateTime};
        db.SupplyConfigurations.Add(row);await db.SaveChangesAsync(ct);
        if(previous?.ItemId!=null&&previous.WarehouseId!=null&&(!row.IsActive||row.WarehouseId!=previous.WarehouseId)){
            var oldPolicy=await db.InventoryPolicies.SingleOrDefaultAsync(x=>x.BranchId==previous.BranchId&&x.WarehouseId==previous.WarehouseId&&x.ItemId==previous.ItemId,ct);
            if(oldPolicy!=null)oldPolicy.MinimumQty=0;
            await db.SaveChangesAsync(ct);
        }
        if(row.ItemId!=null&&row.WarehouseId!=null&&row.IsActive){
            var policy=await db.InventoryPolicies.SingleOrDefaultAsync(x=>x.BranchId==row.BranchId&&x.WarehouseId==row.WarehouseId&&x.ItemId==row.ItemId,ct);
            if(policy==null){policy=new InventoryPolicy{BranchId=row.BranchId,WarehouseId=row.WarehouseId.Value,ItemId=row.ItemId.Value};db.InventoryPolicies.Add(policy);}
            policy.MinimumQty=row.MinimumStock;policy.TargetQty=Math.Max(policy.TargetQty,policy.MinimumQty);
            await db.SaveChangesAsync(ct);
        }
        if(reconcile)await lowStock.Reconcile(ct,r.BranchId);
        audit.Add("SupplyConfiguration",row.ConfigurationId,"CONFIGURED",r.BranchId,after:new{row.Revision,row.ItemId,row.AssetId,row.SupplyGroup});
        return await Dto(row,ct);
    }
    public async Task<object> Minimum(long branchId,long warehouseId,long itemId,CancellationToken ct)
    {
        await access.Require("INVENTORY_VIEW",branchId,ct:ct);
        Output.Require(await db.Warehouses.AnyAsync(w=>w.WarehouseId==warehouseId&&w.BranchId==branchId&&w.IsActive,ct),"WAREHOUSE_UNAVAILABLE");
        var policy=await db.InventoryPolicies.AsNoTracking().SingleOrDefaultAsync(p=>p.BranchId==branchId&&p.WarehouseId==warehouseId&&p.ItemId==itemId,ct);
        var config=await db.SupplyConfigurations.AsNoTracking().SingleOrDefaultAsync(p=>p.BranchId==branchId&&p.ItemId==itemId&&p.IsCurrent,ct);
        return new { minimumStock=policy?.MinimumQty??0,rowVersion=policy==null?null:Convert.ToBase64String(policy.RowVersion),configurationRowVersion=config==null?null:Convert.ToBase64String(config.RowVersion) };
    }
    public Task<object> SaveMinimum(SaveStockMinimumRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        await access.RequireRolePermission("OPERATION_MANAGER","ROOM_SUPPLY_CONFIG",r.BranchId,ct);
        await access.Require("INVENTORY_VIEW",r.BranchId,ct:ct);
        await StaffSupplyService.Lock(db,ct);
        Output.Require(SupplyMath.ValidQuantity(r.MinimumStock),"INVALID_SUPPLY_QUANTITY",400);
        Output.Require(await db.Warehouses.AnyAsync(w=>w.WarehouseId==r.WarehouseId&&w.BranchId==r.BranchId&&w.IsActive,ct),"WAREHOUSE_UNAVAILABLE");
        Output.Require(await db.InventoryItems.AnyAsync(i=>i.ItemId==r.ItemId&&i.IsActive,ct),"ITEM_UNAVAILABLE");
        var policy=await db.InventoryPolicies.SingleOrDefaultAsync(p=>p.BranchId==r.BranchId&&p.WarehouseId==r.WarehouseId&&p.ItemId==r.ItemId,ct);
        if(policy!=null)Output.Version(policy.RowVersion,r.RowVersion??"");
        else Output.Require(r.RowVersion==null,"STALE_ROW_VERSION");
        var before=policy?.MinimumQty??0;
        var config=await db.SupplyConfigurations.SingleOrDefaultAsync(p=>p.BranchId==r.BranchId&&p.ItemId==r.ItemId&&p.IsCurrent,ct);
        if(config!=null){
            Output.Version(config.RowVersion,r.ConfigurationRowVersion??"");
            // Reuse the outer transaction. Preserve group, allocation amounts
            // and historical revisions; open shifts retain their existing snapshots.
            await SaveCore(new(r.BranchId,r.ItemId,null,config.SupplyGroup,r.WarehouseId,config.UnitId,config.InitialQuantity,r.MinimumStock,config.PerGuestQuantity,config.IsActive,r.ConfigurationRowVersion),false,ct);
        }else Output.Require(r.ConfigurationRowVersion==null,"STALE_ROW_VERSION");
        policy=await db.InventoryPolicies.SingleOrDefaultAsync(p=>p.BranchId==r.BranchId&&p.WarehouseId==r.WarehouseId&&p.ItemId==r.ItemId,ct);
        if(policy==null){policy=new InventoryPolicy{BranchId=r.BranchId,WarehouseId=r.WarehouseId,ItemId=r.ItemId};db.InventoryPolicies.Add(policy);}
        policy.MinimumQty=r.MinimumStock;policy.TargetQty=Math.Max(policy.TargetQty,r.MinimumStock);
        await db.SaveChangesAsync(ct);
        await lowStock.Reconcile(ct,r.BranchId);
        audit.Add("InventoryPolicy",policy.InventoryPolicyId,"MINIMUM_CHANGED",r.BranchId,before:new{minimumStock=before},after:new{minimumStock=r.MinimumStock});
        return await Minimum(r.BranchId,r.WarehouseId,r.ItemId,ct);
    },ct);
    private async Task<SupplyConfigurationDto> Dto(SupplyConfiguration x,CancellationToken ct)
    {
        var item=x.ItemId==null?null:await db.InventoryItems.AsNoTracking().SingleAsync(i=>i.ItemId==x.ItemId,ct);
        var asset=x.AssetId==null?null:await db.Assets.AsNoTracking().SingleAsync(a=>a.AssetId==x.AssetId,ct);
        var unit=x.UnitId==null?null:await db.ReferenceValues.AsNoTracking().Where(u=>u.ReferenceId==x.UnitId).Select(u=>u.Name).SingleAsync(ct);
        return new(x.ConfigurationId,x.BranchId,x.ItemId,x.AssetId,item?.Code??asset!.AssetCode,item?.Name??asset!.Name,x.SupplyGroup,x.WarehouseId,x.UnitId,unit,x.InitialQuantity,x.MinimumStock,x.PerGuestQuantity,x.IsActive,x.Revision,x.UpdatedBy,x.UpdatedAt,Convert.ToBase64String(x.RowVersion));
    }
}

public static class SupplyMath
{
    public static bool ValidQuantity(decimal quantity)=>quantity>=0&&quantity<=99999999999999.9999m&&decimal.Round(quantity,4)==quantity;
    public static decimal Remaining(StaffSupplyBalance b)=>b.InitialIssued+b.AdditionalIssued-b.Used-b.Returned;
    public static decimal Available(StaffSupplyBalance b)=>Remaining(b)-b.Reserved;
    public static decimal Required(int capacity,decimal perGuest){Output.Require(capacity>0&&perGuest>0,"ROOM_SUPPLY_CAPACITY_INVALID");var value=capacity*perGuest;Output.Require(ValidQuantity(value),"INVALID_SUPPLY_QUANTITY",400);return value;}
}
