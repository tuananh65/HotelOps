using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class InsightService
{
    private sealed record StockRow(long WarehouseId,string Warehouse,long ItemId,string Item,string Code,string Unit,
        decimal BaseQty,decimal? MinimumQty,decimal? TargetQty,DateTime? LastMovement,bool IsActive,decimal AvailableQty=0,string Classification="UNCLASSIFIED",string ExpiryPolicy="UNKNOWN",decimal BatchQuantity=0);

    private async Task<StockRow[]> StockRows(long branchId,CancellationToken ct)
    {
        await access.Require("INVENTORY_VIEW",branchId,ct:ct);
        // Only ledger/policy pairs establish branch stock. Items themselves are a shared catalog.
        var pairs=db.VStockBalances.Where(b=>b.BranchId==branchId).Select(b=>new{b.WarehouseId,b.ItemId})
            .Union(db.InventoryPolicies.Where(p=>p.BranchId==branchId).Select(p=>new{p.WarehouseId,p.ItemId}));
        var rows=await (from pair in pairs
            join w in db.Warehouses on pair.WarehouseId equals w.WarehouseId
            join i in db.InventoryItems on pair.ItemId equals i.ItemId
            join u in db.ReferenceValues on i.BaseUnitId equals u.ReferenceId
            where i.IsActive
            orderby w.Name,i.Name
            select new StockRow(w.WarehouseId,w.Name,i.ItemId,i.Name,i.Code,u.Name,
                db.StockMovements.Where(m=>m.WarehouseId==w.WarehouseId&&m.ItemId==i.ItemId).Sum(m=>(decimal?)m.SignedBaseQty)??0,
                db.InventoryPolicies.Where(p=>p.BranchId==branchId&&p.WarehouseId==w.WarehouseId&&p.ItemId==i.ItemId).Select(p=>(decimal?)p.MinimumQty).SingleOrDefault(),
                db.InventoryPolicies.Where(p=>p.BranchId==branchId&&p.WarehouseId==w.WarehouseId&&p.ItemId==i.ItemId).Select(p=>(decimal?)p.TargetQty).SingleOrDefault(),
                db.StockMovements.Where(m=>m.WarehouseId==w.WarehouseId&&m.ItemId==i.ItemId).Max(m=>(DateTime?)m.OccurredAt),i.IsActive,0,"UNCLASSIFIED","UNKNOWN",0)).ToArrayAsync(ct);
        var profiles=await db.ItemLifecycleProfiles.AsNoTracking().Where(p=>p.BranchId==branchId).ToDictionaryAsync(p=>p.ItemId,ct);
        var batchQuantities=await db.StockBatches.AsNoTracking().Where(b=>b.BranchId==branchId)
            .GroupBy(b=>new{b.WarehouseId,b.ItemId}).Select(g=>new{g.Key.WarehouseId,g.Key.ItemId,Quantity=g.Sum(b=>b.WarehouseQuantity)}).ToArrayAsync(ct);
        for(var index=0;index<rows.Length;index++){
            var row=rows[index];profiles.TryGetValue(row.ItemId,out var profile);var available=profile?.Classification=="REUSABLE"
                ?await db.ReusablePositions.Where(p=>p.BranchId==branchId&&p.ItemId==row.ItemId&&p.WarehouseId==row.WarehouseId&&p.Status=="READY").SumAsync(p=>(decimal?)p.Quantity,ct)??0
                :await StockBatchAccounting.Available(db,row.WarehouseId,row.ItemId,clock.GetUtcNow().UtcDateTime,ct);
            rows[index]=row with{AvailableQty=available,Classification=profile?.Classification??"UNCLASSIFIED",ExpiryPolicy=profile?.ExpiryPolicy??"UNKNOWN",
                BatchQuantity=batchQuantities.FirstOrDefault(b=>b.WarehouseId==row.WarehouseId&&b.ItemId==row.ItemId)?.Quantity??0};
        }
        return rows;
    }

    public async Task<object> Inventory(long branchId,CancellationToken ct)
    {
        var stock=await StockRows(branchId,ct);
        var documents=db.InventoryDocuments.AsNoTracking().Where(d=>d.BranchId==branchId);
        var start=clock.GetUtcNow().UtcDateTime.Date.AddDays(-29);
        var canMr=await access.Allows("MR_VIEW",branchId,ct:ct);
        var canReplenishment=await access.Allows("REPLENISHMENT_VIEW",branchId,ct:ct);
        var pending=canMr?await documents.Where(d=>d.Kind=="MATERIAL_REQUEST"&&(d.Status=="APPROVED"||d.Status=="PARTIALLY_ISSUED"))
            .OrderBy(d=>d.CreatedAt).Take(10).Select(d=>new{d.DocumentId,d.DocumentNo,d.Kind,d.Status,d.CreatedAt}).ToArrayAsync(ct):null;
        var recent=await documents.Where(d=>d.PostedAt!=null).OrderByDescending(d=>d.PostedAt).Take(10)
            .Select(d=>new{d.DocumentId,d.DocumentNo,d.Kind,d.Status,d.PostedAt}).ToArrayAsync(ct);
        var timeline=await documents.Where(d=>d.PostedAt>=start&&(d.Kind=="RECEIPT"||d.Kind=="ISSUE"))
            .GroupBy(d=>new{day=d.PostedAt!.Value.Date,d.Kind}).Select(g=>new{g.Key.day,g.Key.Kind,count=g.Count()}).ToArrayAsync(ct);
        return new{branchId,periodFrom=start,metrics=new{
            trackedItems=stock.Select(x=>x.ItemId).Distinct().Count(),
            stockedItems=stock.Where(x=>x.BaseQty>0).Select(x=>x.ItemId).Distinct().Count(),
            emptyItems=stock.GroupBy(x=>x.ItemId).Count(g=>g.Sum(x=>x.BaseQty)==0),
            lowStock=stock.Count(x=>x.MinimumQty!=null&&x.AvailableQty<x.MinimumQty),
            pendingRequests=canMr?(int?)await documents.CountAsync(d=>d.Kind=="MATERIAL_REQUEST"&&(d.Status=="APPROVED"||d.Status=="PARTIALLY_ISSUED"),ct):null,
            receipts=await documents.CountAsync(d=>d.Kind=="RECEIPT"&&d.PostedAt>=start,ct),
            issues=await documents.CountAsync(d=>d.Kind=="ISSUE"&&d.PostedAt>=start,ct),
            transfers=await documents.CountAsync(d=>d.Kind=="TRANSFER"&&d.PostedAt>=start,ct),
            openStocktakes=await documents.CountAsync(d=>d.Kind=="STOCKTAKE"&&(d.Status=="IN_PROGRESS"||d.Status=="COUNTED"),ct),
            openReplenishments=canReplenishment?(int?)await documents.CountAsync(d=>d.Kind=="REPLENISHMENT"&&(d.Status=="REQUESTED"||d.Status=="PARTIALLY_RECEIVED"),ct):null},
            lowStock=stock.Where(x=>x.MinimumQty!=null&&x.AvailableQty<x.MinimumQty).OrderBy(x=>x.AvailableQty).Take(10),pending,recent,timeline};
    }

    public async Task<object> Ledger(long branchId,long warehouseId,long itemId,int page,CancellationToken ct)
    {
        await access.Require("INVENTORY_VIEW",branchId,ct:ct);
        Output.Require(await db.Warehouses.AsNoTracking().AnyAsync(w=>w.WarehouseId==warehouseId&&w.BranchId==branchId,ct),"WAREHOUSE_UNAVAILABLE",404);
        page=Math.Max(1,page);const int size=25;
        var query=from m in db.StockMovements.AsNoTracking()
            join l in db.InventoryDocumentLines on m.LineId equals l.LineId
            join d in db.InventoryDocuments on l.DocumentId equals d.DocumentId
            join u in db.Users on m.CreatedBy equals u.UserId
            where m.BranchId==branchId&&m.WarehouseId==warehouseId&&m.ItemId==itemId
            orderby m.MovementId descending
            select new{m.MovementId,m.SignedBaseQty,m.OccurredAt,m.ReversalOfMovementId,d.DocumentId,d.DocumentNo,d.Kind,actor=u.FullName};
        return new PageDto<object>((await query.Skip((page-1)*size).Take(size).ToArrayAsync(ct)).Cast<object>().ToArray(),await query.CountAsync(ct),page,size);
    }
}
