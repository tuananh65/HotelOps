using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class DocumentService
{
    // Narrow internal automation; never reachable through generic Create/Act/Derive APIs.
    // SUPPLY_* documents have dedicated DB provenance guards and still use the shared immutable
    // stock ledger, quantity/sign trigger, posted-document immutability and nonnegative-stock lock.
    internal async Task<Dictionary<long,long>> PostStaffSupply(StaffSupplyAllocation allocation,IReadOnlyList<(StaffSupplyBalance Balance,decimal Quantity)> entries,string purpose,Guid key,long? requestWorkId,CancellationToken ct)
    {
        Output.Require(db.Database.CurrentTransaction!=null,"SUPPLY_TRANSACTION_REQUIRED");Output.Key(key);
        Output.Require(purpose is "INITIAL" or "ADDITIONAL" or "RETURN","INVALID_SUPPLY_PURPOSE",400);
        Output.Require(entries.Count>0&&entries.Select(x=>x.Balance.BalanceId).Distinct().Count()==entries.Count,"SUPPLY_LINES_REQUIRED");
        var result=new Dictionary<long,long>();var incoming=purpose=="RETURN";
        foreach(var group in entries.GroupBy(x=>x.Balance.WarehouseId).OrderBy(x=>x.Key)){
            Output.Require(await db.Warehouses.AnyAsync(w=>w.WarehouseId==group.Key&&w.BranchId==allocation.BranchId&&w.IsActive,ct),"WAREHOUSE_UNAVAILABLE");
            Output.Require(!await db.SupplyStockDocuments.AnyAsync(x=>x.AllocationId==allocation.AllocationId&&x.WarehouseId==group.Key&&x.Purpose==purpose&&x.OperationKey==key,ct),"OPERATION_ALREADY_APPLIED");
            var now=clock.GetUtcNow().UtcDateTime;
            var document=new InventoryDocument{BranchId=allocation.BranchId,DocumentNo=Guid.NewGuid().ToString("N"),Kind=incoming?"SUPPLY_RETURN":"SUPPLY_ISSUE",Status="DRAFT",SourceWarehouseId=incoming?null:group.Key,DestinationWarehouseId=incoming?group.Key:null,DestinationBranchId=incoming?allocation.BranchId:null,SourceWorkId=requestWorkId,CreatedBy=actor.Id,CreatedAt=now,Reason=$"{purpose}; workday {allocation.AssignmentId}; staff {allocation.UserId}"};
            db.InventoryDocuments.Add(document);await db.SaveChangesAsync(ct);
            db.SupplyStockDocuments.Add(new SupplyStockDocument{DocumentId=document.DocumentId,AllocationId=allocation.AllocationId,WarehouseId=group.Key,Purpose=purpose,OperationKey=key,RequestWorkId=requestWorkId});
            var lines=new List<(StaffSupplyBalance Balance,InventoryDocumentLine Line)>();var lineNo=0;
            foreach(var (balance,quantity) in group){
                Output.Require(balance.AllocationId==allocation.AllocationId&&quantity>0&&SupplyMath.ValidQuantity(quantity),"INVALID_SUPPLY_QUANTITY",400);
                Output.Require(await db.InventoryItems.AnyAsync(i=>i.ItemId==balance.ItemId&&i.BaseUnitId==balance.UnitId&&i.IsActive,ct),"SUPPLY_ITEM_OR_UNIT_UNAVAILABLE");
                if(incoming)Output.Require(balance.Reserved==0&&quantity==SupplyMath.Remaining(balance),"SUPPLY_RETURN_MISMATCH");
                else {var stock=await db.StockMovements.Where(m=>m.WarehouseId==group.Key&&m.ItemId==balance.ItemId).SumAsync(m=>(decimal?)m.SignedBaseQty,ct)??0;Output.Require(stock>=quantity,"STOCK_INSUFFICIENT");}
                var line=new InventoryDocumentLine{BranchId=allocation.BranchId,DocumentId=document.DocumentId,LineNo=++lineNo,ItemId=balance.ItemId,UnitId=balance.UnitId,UnitKind="UOM",ConversionFactor=1,ActualQty=quantity};
                db.InventoryDocumentLines.Add(line);lines.Add((balance,line));
            }
            await db.SaveChangesAsync(ct);
            document.Status="POSTED";document.PostedAt=now;document.PostedBy=actor.Id;await db.SaveChangesAsync(ct);
            foreach(var (balance,line) in lines){
                var movement=new StockMovement{BranchId=allocation.BranchId,LineId=line.LineId,WarehouseId=group.Key,ItemId=line.ItemId,SignedBaseQty=(incoming?1:-1)*line.ActualQty,CreatedBy=actor.Id,OccurredAt=now};
                db.StockMovements.Add(movement);await db.SaveChangesAsync(ct);
                await new StockBatchAccounting(db,clock).Post(document,movement,ct,balance.BalanceId);result.Add(balance.BalanceId,line.LineId);
            }
            await db.SaveChangesAsync(ct);
            await Event(document,"SUPPLY_POSTED",key,document.Reason,ct);
            await new LowStockService(db,notifications,clock,configuration).SupplyPosted(allocation,document,purpose,ct);
        }
        return result;
    }
}
