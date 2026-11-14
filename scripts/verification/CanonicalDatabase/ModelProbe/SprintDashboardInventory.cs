using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

// Called only inside the dashboard scenario after its exact disposable identity guard.
internal sealed class SprintDashboardInventory(HotelOpsDbContext db, string prefix, long actor)
{
    private long[] warehouses = [], items = [], units = [];
    private long category;
    private long ownWarehouse, secondWarehouse, itemA, itemB, unitA, unitB;
    internal async Task SeedAsync(long branch, long foreign, long department, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var cat = new ItemCategory { CategoryCode=prefix, CategoryName="Synthetic dashboard", IsActive=true };
        var u1 = new UnitsOfMeasure { UomCode=prefix+"A", UomName="Synthetic pieces", IsActive=true };
        var u2 = new UnitsOfMeasure { UomCode=prefix+"B", UomName="Synthetic litres", IsActive=true };
        db.ItemCategories.Add(cat); db.UnitsOfMeasures.AddRange(u1,u2);
        await db.SaveChangesAsync(ct);
        var a = new InventoryItem { ItemCode=prefix+"A", ItemName="Synthetic A", CategoryId=cat.ItemCategoryId, BaseUomId=u1.UomId, IsActive=true, IsConsumable=true };
        var b = new InventoryItem { ItemCode=prefix+"B", ItemName="Synthetic B", CategoryId=cat.ItemCategoryId, BaseUomId=u2.UomId, IsActive=true, IsConsumable=true };
        var w1 = new Warehouse { BranchId=branch, DepartmentId=department, WarehouseCode=prefix+"A", WarehouseName="Synthetic HK", IsActive=true };
        var w2 = new Warehouse { BranchId=branch, WarehouseCode=prefix+"B", WarehouseName="Synthetic other", IsActive=true };
        var wf = new Warehouse { BranchId=foreign, WarehouseCode=prefix+"F", WarehouseName="Synthetic foreign", IsActive=true };
        db.InventoryItems.AddRange(a,b); db.Warehouses.AddRange(w1,w2,wf); await db.SaveChangesAsync(ct);
        var day = new DateTime(2026,11,14,0,0,0,DateTimeKind.Utc);
        var sequence=0;
        void Movement(Warehouse warehouse, InventoryItem item, decimal quantity, DateTime at)
        {
            var document = new StockDocument { DocumentNo=prefix+(++sequence), DocumentType=quantity>0?"RECEIPT":"ISSUE", Status="POSTED",
                SourceWarehouseId=quantity<0?warehouse.WarehouseId:null, DestinationWarehouseId=quantity>0?warehouse.WarehouseId:null,
                DocumentDate=DateOnly.FromDateTime(at), CreatedBy=actor, PostedBy=actor, CreatedAt=at, PostedAt=at };
            var line = new StockDocumentLine { Document=document, LineNo=1, ItemId=item.ItemId, UomId=item.BaseUomId,
                ConversionFactor=1, ActualQty=Math.Abs(quantity), BaseQty=Math.Abs(quantity) };
            db.StockMovements.Add(new StockMovement { DocumentLine=line, WarehouseId=warehouse.WarehouseId, ItemId=item.ItemId,
                SignedBaseQty=quantity, OccurredAt=at, CreatedBy=actor });
        }
        Movement(w1,a,10.125m,day); Movement(w1,a,-2.005m,day.AddDays(1).AddSeconds(-1));
        Movement(w1,a,5m,day.AddSeconds(-1)); Movement(w1,a,100m,day.AddDays(1));
        Movement(w1,b,5.5m,day.AddHours(1)); Movement(w2,a,2m,day); Movement(wf,a,999m,day);
        db.StockThresholds.AddRange(
            new StockThreshold { WarehouseId=w1.WarehouseId, ItemId=a.ItemId, MinimumQty=114, AlertEnabled=true },
            new StockThreshold { WarehouseId=w1.WarehouseId, ItemId=b.ItemId, MinimumQty=5.5m, AlertEnabled=true },
            new StockThreshold { WarehouseId=w2.WarehouseId, ItemId=a.ItemId, MinimumQty=2, AlertEnabled=true },
            new StockThreshold { WarehouseId=wf.WarehouseId, ItemId=a.ItemId, MinimumQty=1000, AlertEnabled=true });
        await db.SaveChangesAsync(ct);
        warehouses=[w1.WarehouseId,w2.WarehouseId,wf.WarehouseId]; items=[a.ItemId,b.ItemId]; units=[u1.UomId,u2.UomId]; category=cat.ItemCategoryId;
        ownWarehouse=w1.WarehouseId; secondWarehouse=w2.WarehouseId; itemA=a.ItemId; itemB=b.ItemId; unitA=u1.UomId; unitB=u2.UomId;
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }
    internal async Task VerifyAsync(Func<string,int,Task<JsonElement>> get, long branch, long department, List<object> checks)
    {
        var path=$"/api/dashboards/operations?branchId={branch}&from=2026-11-14&to=2026-11-14";
        void Verify(JsonElement data, bool scoped)
        {
            var rows=data.GetProperty("quantities").EnumerateArray().Where(q=>q.GetProperty("kind").GetString()=="netStockMovement").ToArray();
            if(rows.Length!=(scoped?2:3))throw new Exception("Inventory grouping/scope count mismatch");
            decimal Quantity(long warehouse,long item,long unit)
            {
                var row=rows.Single(q=>q.GetProperty("warehouseId").GetInt64()==warehouse&&q.GetProperty("itemId").GetInt64()==item&&q.GetProperty("uomId").GetInt64()==unit);
                if(row.GetProperty("quantity").ValueKind!=JsonValueKind.String)throw new Exception("Decimal transport lost string contract");
                return decimal.Parse(row.GetProperty("quantity").GetString()!,CultureInfo.InvariantCulture);
            }
            if(Quantity(ownWarehouse,itemA,unitA)!=8.120m||Quantity(ownWarehouse,itemB,unitB)!=5.5m||(!scoped&&Quantity(secondWarehouse,itemA,unitA)!=2m))throw new Exception("Inventory period/sign/precision mismatch");
            var low=data.GetProperty("metrics").EnumerateArray().Single(m=>m.GetProperty("key").GetString()=="lowStockItems").GetProperty("value").GetDecimal();
            if(low!=1)throw new Exception("Low-stock all-time balance, strict minimum or branch scope mismatch");
        }
        Verify(await get(path,200),false);
        Verify(await get(path+"&departmentId="+department,200),true);
        var outside=await get($"/api/dashboards/operations?branchId={branch}&from=2026-11-16&to=2026-11-16",200);
        if(outside.GetProperty("quantities").GetArrayLength()!=0)throw new Exception("Movements outside period included");
        checks.Add(new {inventoryPositiveRows=true,utcInclusiveStartExclusiveEnd=true,signedDecimalStrings=true,separateItemUnitWarehouse=true,foreignWarehouseExcluded=true,departmentScope=true,lowStockStrictMinimum=true});
    }
    // Participates in the outer cleanup transaction; exact IDs from committed synthetic seed only.
    internal async Task CleanupAsync(CancellationToken ct)
    {
        if(warehouses.Length==0)return;
        var docs=db.StockDocuments.Where(d=>d.DocumentNo.StartsWith(prefix)&&d.CreatedBy==actor).Select(d=>d.DocumentId);
        await db.StockMovements.Where(m=>warehouses.Contains(m.WarehouseId)&&m.CreatedBy==actor&&docs.Contains(m.DocumentLine.DocumentId)).ExecuteDeleteAsync(ct);
        await db.StockDocumentLines.Where(l=>docs.Contains(l.DocumentId)).ExecuteDeleteAsync(ct);
        await db.StockDocuments.Where(d=>docs.Contains(d.DocumentId)).ExecuteDeleteAsync(ct);
        await db.StockThresholds.Where(t=>warehouses.Contains(t.WarehouseId)&&items.Contains(t.ItemId)).ExecuteDeleteAsync(ct);
        await db.Warehouses.Where(w=>warehouses.Contains(w.WarehouseId)&&w.WarehouseCode.StartsWith(prefix)).ExecuteDeleteAsync(ct);
        await db.InventoryItems.Where(i=>items.Contains(i.ItemId)&&i.ItemCode.StartsWith(prefix)).ExecuteDeleteAsync(ct);
        await db.UnitsOfMeasures.Where(u=>units.Contains(u.UomId)&&u.UomCode.StartsWith(prefix)).ExecuteDeleteAsync(ct);
        await db.ItemCategories.Where(c=>c.ItemCategoryId==category&&c.CategoryCode==prefix).ExecuteDeleteAsync(ct);
        if(await docs.AnyAsync(ct)||await db.UnitsOfMeasures.AnyAsync(u=>units.Contains(u.UomId),ct)||await db.ItemCategories.AnyAsync(c=>c.ItemCategoryId==category,ct)||await db.StockMovements.AnyAsync(m=>warehouses.Contains(m.WarehouseId),ct)||await db.Warehouses.AnyAsync(w=>warehouses.Contains(w.WarehouseId),ct)||await db.InventoryItems.AnyAsync(i=>items.Contains(i.ItemId),ct))throw new Exception("Inventory fixture residue");
    }
}
