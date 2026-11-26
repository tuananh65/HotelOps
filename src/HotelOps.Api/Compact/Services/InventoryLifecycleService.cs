using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;
public sealed class InventoryLifecycleService(HotelOpsCompactDbContext db,Access access,Actor actor,Atomic atomic,Audit audit,TimeProvider clock,LowStockService lowStock,IConfiguration? configuration=null)
{
    private DateTime Now=>clock.GetUtcNow().UtcDateTime;
    public async Task<object> Profiles(long branch,CancellationToken ct){
        if(!await access.Allows("ROOM_SUPPLY_CONFIG",branch,ct:ct))await access.Require("INVENTORY_VIEW",branch,ct:ct);
        return await (from i in db.InventoryItems.AsNoTracking()
            join profile in db.ItemLifecycleProfiles.Where(p=>p.BranchId==branch) on i.ItemId equals profile.ItemId into profiles
            from p in profiles.DefaultIfEmpty() where i.IsActive||p!=null
            orderby i.Name select new{BranchId=branch,i.ItemId,i.Name,i.Code,Classification=p==null?"UNCLASSIFIED":p.Classification,
                ExpiryPolicy=p==null?"UNKNOWN":p.ExpiryPolicy,rowVersion=p==null?null:Convert.ToBase64String(p.RowVersion)}).ToArrayAsync(ct);
    }
    public Task<object> Configure(ItemLifecycleRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        await access.RequireRolePermission("OPERATION_MANAGER","ROOM_SUPPLY_CONFIG",r.BranchId,ct);await StaffSupplyService.Lock(db,ct);
        if(r.Classification!="REUSABLE")Output.Require(!await db.SupplyConfigurations.AnyAsync(c=>c.BranchId==r.BranchId&&c.ItemId==r.ItemId&&c.IsCurrent&&c.SupplyGroup=="B"&&c.InitialQuantity>0,ct),"REUSABLE_SHIFT_CONFIGURATION_MUST_BE_CLEARED");
        Output.Require(r.Classification is "CONSUMABLE" or "REUSABLE" or "ASSET" or "UNCLASSIFIED","INVALID_ITEM_CLASSIFICATION",400);
        Output.Require(r.ExpiryPolicy is "REQUIRED" or "NONE" or "UNKNOWN","INVALID_EXPIRY_POLICY",400);Output.Reason(r.Reason);
        var p=await db.ItemLifecycleProfiles.SingleOrDefaultAsync(x=>x.BranchId==r.BranchId&&x.ItemId==r.ItemId,ct);
        if(p==null){
            Output.Require(r.RowVersion==null,"STALE_ROW_VERSION");
            Output.Require(await db.InventoryItems.AnyAsync(i=>i.ItemId==r.ItemId&&i.IsActive,ct),"ITEM_UNAVAILABLE",404);
            p=new ItemLifecycleProfile{BranchId=r.BranchId,ItemId=r.ItemId};db.ItemLifecycleProfiles.Add(p);
        }else {Output.Require(r.RowVersion!=null,"STALE_ROW_VERSION");Output.Version(p.RowVersion,r.RowVersion!);}
        if(p.Classification!=r.Classification){
            // Changing the meaning of a live balance needs an explicit reconciliation, never a dropdown rewrite.
            Output.Require(!await db.StockMovements.AnyAsync(m=>m.BranchId==r.BranchId&&m.ItemId==r.ItemId,ct)
                &&!await db.StaffSupplyBalances.AnyAsync(b=>b.ItemId==r.ItemId&&db.StaffSupplyAllocations.Any(a=>a.AllocationId==b.AllocationId&&a.BranchId==r.BranchId),ct),"CLASSIFICATION_RECONCILIATION_REQUIRED");
        }
        var before=new{p.Classification,p.ExpiryPolicy};p.Classification=r.Classification;p.ExpiryPolicy=r.ExpiryPolicy;p.UpdatedBy=actor.Id;p.UpdatedAt=Now;
        audit.Add("ItemLifecycleProfile",r.ItemId,"CONFIGURE",r.BranchId,before,new{r.Classification,r.ExpiryPolicy,r.Reason});await db.SaveChangesAsync(ct);
        await lowStock.Reconcile(ct,r.BranchId);
        return (object)new{p.ItemId,p.Classification,p.ExpiryPolicy,rowVersion=Convert.ToBase64String(p.RowVersion)};
    },ct);
    public async Task<object> Batches(long branch,long? item,CancellationToken ct,int? page=null,int pageSize=25,string? search=null){
        await access.Require("INVENTORY_VIEW",branch,ct:ct);var today=DateOnly.FromDateTime(Now.AddHours(7));
        if(page!=null)Output.Require(page>0&&pageSize is >=1 and <=100&&page<=(int.MaxValue/pageSize),"INVALID_PAGE",400);
        search=search?.Trim();
        var warningDays=ExpiryWarningPolicy.Days(configuration,branch);
        var query=from b in db.StockBatches.AsNoTracking() join p in db.ItemLifecycleProfiles on new{b.BranchId,b.ItemId} equals new{p.BranchId,p.ItemId}
            join i in db.InventoryItems on b.ItemId equals i.ItemId join w in db.Warehouses on b.WarehouseId equals w.WarehouseId
            where b.BranchId==branch&&(item==null||b.ItemId==item)&&(search==null||search==""||i.Name.Contains(search)||b.LotNumber.Contains(search)||w.Name.Contains(search)) orderby b.ExpiresOn,b.ReceivedAt,b.BatchId
            select new{b.BatchId,b.BranchId,b.WarehouseId,b.ItemId,item=i.Name,warehouse=w.Name,b.LotNumber,b.ReceivedAt,b.ExpiresOn,b.ReceivedQuantity,b.WarehouseQuantity,b.ReservedQuantity,
                b.Status,b.IsLegacy,b.ReceiptLineId,b.OriginBatchId,p.ExpiryPolicy,
                staffQuantity=db.StaffBatchBalances.Where(h=>h.BatchId==b.BatchId).Sum(h=>(decimal?)h.Quantity)??0,
                available=b.Status=="READY"&&p.ExpiryPolicy!="UNKNOWN"&&(p.ExpiryPolicy!="REQUIRED"||b.ExpiresOn!=null)&&(b.ExpiresOn==null||b.ExpiresOn>=today)?b.WarehouseQuantity-b.ReservedQuantity:0,
                expired=b.ExpiresOn!=null&&b.ExpiresOn<today,expiring=b.ExpiresOn!=null&&b.ExpiresOn>=today&&b.ExpiresOn<=today.AddDays(warningDays),rowVersion=Convert.ToBase64String(b.RowVersion)};
        // Preserve the array contract for allocation callers; the interactive list requests pages.
        if(page==null)return await query.ToArrayAsync(ct);
        var total=await query.CountAsync(ct);
        return new{items=await query.Skip((page.Value-1)*pageSize).Take(pageSize).ToArrayAsync(ct),total,page=page.Value,pageSize};
    }
    public Task<object> VerifyBatch(long id,BatchMetadataRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        await StaffSupplyService.Lock(db,ct);var batch=await db.StockBatches.SingleOrDefaultAsync(b=>b.BatchId==id,ct)??throw new ApiException(404,"BATCH_NOT_FOUND");
        await access.RequireRolePermission("STOREKEEPER","INVENTORY_VIEW",batch.BranchId,ct);Output.Version(batch.RowVersion,r.RowVersion);Output.Reason(r.Reason);
        await ShiftCommandAccess.Require(db,actor.Id,batch.BranchId,Now,ct);
        Output.Require(r.Status is "READY" or "QUARANTINED" or "EXPIRED","INVALID_BATCH_STATUS",400);
        Output.Require(r.ReceivedAt<=Now&&r.ReceivedAt.Year>=2000&&!string.IsNullOrWhiteSpace(r.LotNumber),"BATCH_METADATA_REQUIRED",400);
        var p=await db.ItemLifecycleProfiles.SingleAsync(p=>p.BranchId==batch.BranchId&&p.ItemId==batch.ItemId,ct);var today=DateOnly.FromDateTime(Now.AddHours(7));
        Output.Require(r.ExpiresOn==null||r.ExpiresOn>=DateOnly.FromDateTime(r.ReceivedAt),"EXPIRY_BEFORE_RECEIPT",400);
        if(r.Status=="READY")Output.Require(p.Classification=="CONSUMABLE"&&p.ExpiryPolicy!="UNKNOWN"&&(p.ExpiryPolicy!="REQUIRED"||r.ExpiresOn!=null)&&(r.ExpiresOn==null||r.ExpiresOn>=today),"BATCH_NOT_SAFE_TO_ISSUE",409);
        if(await db.StockBatchEntries.AnyAsync(e=>e.BatchId==id&&e.Quantity<0,ct))
            Output.Require(batch.LotNumber==r.LotNumber.Trim()&&batch.ReceivedAt==r.ReceivedAt&&batch.ExpiresOn==r.ExpiresOn,"ISSUED_BATCH_IDENTITY_IMMUTABLE");
        var before=new{batch.LotNumber,batch.ReceivedAt,batch.ExpiresOn,batch.Status};
        batch.LotNumber=r.LotNumber.Trim();batch.ReceivedAt=r.ReceivedAt;batch.ExpiresOn=r.ExpiresOn;batch.Status=r.Status;
        audit.Add("StockBatch",id,"METADATA_VERIFIED",batch.BranchId,before,new{batch.LotNumber,batch.ReceivedAt,batch.ExpiresOn,batch.Status,r.Reason});await db.SaveChangesAsync(ct);
        await lowStock.Reconcile(ct,batch.BranchId);
        return (object)new{batch.BatchId,batch.Status,rowVersion=Convert.ToBase64String(batch.RowVersion)};
    },ct);
    public async Task<object> BatchHistory(long id,CancellationToken ct,int? page=null,int pageSize=25){
        var batch=await db.StockBatches.AsNoTracking().SingleOrDefaultAsync(b=>b.BatchId==id,ct)??throw new ApiException(404,"BATCH_NOT_FOUND");
        await access.Require("INVENTORY_VIEW",batch.BranchId,ct:ct);
        if(page!=null)Output.Require(page>0&&pageSize is >=1 and <=100&&page<=(int.MaxValue/pageSize),"INVALID_PAGE",400);
        var query=db.StockBatchEntries.AsNoTracking().Where(e=>e.BatchId==id).OrderByDescending(e=>e.EntryId)
            .Select(e=>new{e.EntryId,e.MovementId,e.LedgerId,e.Kind,e.Quantity,e.OccurredAt});
        if(page==null)return await query.ToArrayAsync(ct);
        var total=await query.CountAsync(ct);
        return new{items=await query.Skip((page.Value-1)*pageSize).Take(pageSize).ToArrayAsync(ct),total,page=page.Value,pageSize};
    }
}
