using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

// Every method joins the caller's serializable transaction and the shared supply lock.
// The aggregate stock ledger remains authoritative; batch entries decompose each new movement.
internal sealed class StockBatchAccounting(HotelOpsCompactDbContext db,TimeProvider clock)
{
    private DateTime Now=>clock.GetUtcNow().UtcDateTime;
    private DateOnly Today=>DateOnly.FromDateTime(Now.AddHours(7));
    internal static async Task<decimal> Available(HotelOpsCompactDbContext db,long warehouse,long item,DateTime now,CancellationToken ct)
    {
        if(await (from w in db.Warehouses join p in db.ItemLifecycleProfiles on w.BranchId equals p.BranchId
            where w.WarehouseId==warehouse&&p.ItemId==item&&p.Classification=="ASSET" select p.ItemId).AnyAsync(ct))
            return await db.StockMovements.Where(m=>m.WarehouseId==warehouse&&m.ItemId==item).SumAsync(m=>(decimal?)m.SignedBaseQty,ct)??0;
        if(await (from w in db.Warehouses join p in db.ItemLifecycleProfiles on w.BranchId equals p.BranchId
            where w.WarehouseId==warehouse&&p.ItemId==item&&p.Classification=="REUSABLE" select p.ItemId).AnyAsync(ct))
            {
            var branch=await db.Warehouses.Where(w=>w.WarehouseId==warehouse).Select(w=>w.BranchId).SingleAsync(ct);
            if(await ReusableShelfLifePolicy.Overdue(db,branch,item,now,ct))return 0;
            return await db.ReusablePositions.AsNoTracking().Where(p=>p.WarehouseId==warehouse&&p.ItemId==item&&p.Status=="READY").SumAsync(p=>(decimal?)p.Quantity,ct)??0;
        }
        var today=DateOnly.FromDateTime(now.AddHours(7));
        return await (from b in db.StockBatches.AsNoTracking() join p in db.ItemLifecycleProfiles on new{b.BranchId,b.ItemId} equals new{p.BranchId,p.ItemId}
            where b.WarehouseId==warehouse&&b.ItemId==item&&b.Status=="READY"&&p.Classification=="CONSUMABLE"&&p.ExpiryPolicy!="UNKNOWN"
                &&(p.ExpiryPolicy!="REQUIRED"||b.ExpiresOn!=null)&&(b.ExpiresOn==null||b.ExpiresOn>=today)
            select (decimal?)(b.WarehouseQuantity-b.ReservedQuantity)).SumAsync(ct)??0;
    }
    private bool Usable(StockBatch batch,ItemLifecycleProfile profile)=>batch.Status=="READY"&&profile.ExpiryPolicy!="UNKNOWN"
        &&(profile.ExpiryPolicy!="REQUIRED"||batch.ExpiresOn!=null)&&(batch.ExpiresOn==null||batch.ExpiresOn>=Today);
    internal static async Task<decimal> StaffAvailable(HotelOpsCompactDbContext db,StaffSupplyBalance balance,DateTime now,CancellationToken ct)
    {
        var today=DateOnly.FromDateTime(now.AddHours(7));
        var valid=await (from h in db.StaffBatchBalances join b in db.StockBatches on h.BatchId equals b.BatchId
            join p in db.ItemLifecycleProfiles on new{b.BranchId,b.ItemId} equals new{p.BranchId,p.ItemId}
            where h.BalanceId==balance.BalanceId&&b.Status=="READY"&&p.Classification=="CONSUMABLE"&&p.ExpiryPolicy!="UNKNOWN"
                &&(p.ExpiryPolicy!="REQUIRED"||b.ExpiresOn!=null)&&(b.ExpiresOn==null||b.ExpiresOn>=today)
            select (decimal?)h.Quantity).SumAsync(ct)??0;
        return Math.Max(0,Math.Min(SupplyMath.Remaining(balance),valid)-balance.Reserved);
    }
    private static IEnumerable<StockBatch> Ordered(IEnumerable<StockBatch> batches,ItemLifecycleProfile profile)=>profile.ExpiryPolicy=="REQUIRED"
        ?batches.OrderBy(x=>x.ExpiresOn).ThenBy(x=>x.ReceivedAt).ThenBy(x=>x.BatchId)
        :batches.OrderBy(x=>x.ReceivedAt).ThenBy(x=>x.BatchId);
    internal async Task Post(InventoryDocument document,StockMovement movement,CancellationToken ct,long? staffBalanceId=null)
    {
        Output.Require(db.Database.CurrentTransaction!=null,"INVENTORY_TRANSACTION_REQUIRED");await StaffSupplyService.Lock(db,ct);
        var profile=await db.ItemLifecycleProfiles.SingleOrDefaultAsync(p=>p.BranchId==movement.BranchId&&p.ItemId==movement.ItemId,ct);
        // Asset-classified stock uses the quantity ledger. Individual asset records
        // are registered separately; receiving stock must not invent serials or lots.
        if(profile?.Classification=="ASSET")return;
        if(profile?.Classification=="REUSABLE"){await new ReusableAccounting(db,clock).Receipt(document,movement,ct);return;}
        Output.Require(profile?.Classification=="CONSUMABLE","CONSUMABLE_CLASSIFICATION_REQUIRED",409);
        Output.Require(!await db.StockBatchEntries.AnyAsync(e=>e.MovementId==movement.MovementId,ct),"BATCH_MOVEMENT_ALREADY_POSTED");
        var amount=Math.Abs(movement.SignedBaseQty);
        if(document.Kind=="SUPPLY_RETURN"){
            Output.Require(staffBalanceId!=null,"STAFF_BATCH_SOURCE_REQUIRED");
            var held=await db.StaffBatchBalances.Where(h=>h.BalanceId==staffBalanceId&&h.Quantity>0).OrderBy(h=>h.BatchId).ToArrayAsync(ct);
            Output.Require(held.Sum(x=>x.Quantity)==amount,"STAFF_BATCH_RECONCILIATION_REQUIRED");
            foreach(var hold in held){var batch=await db.StockBatches.SingleAsync(b=>b.BatchId==hold.BatchId,ct);
                Output.Require(batch.WarehouseId==movement.WarehouseId&&batch.ItemId==movement.ItemId,"BATCH_SOURCE_MISMATCH");
                AddEntry(batch,movement,hold.Quantity);batch.WarehouseQuantity+=hold.Quantity;hold.Quantity=0;
                // Expired/quarantined returns remain unavailable. Returning never invents new expiry.
                if(batch.ExpiresOn<Today)batch.Status="EXPIRED";
            }
        }else if(movement.ReversalOfMovementId!=null){
            var originals=await db.StockBatchEntries.Where(e=>e.MovementId==movement.ReversalOfMovementId).ToArrayAsync(ct);
            Output.Require(originals.Length>0&&Math.Abs(originals.Sum(x=>x.Quantity))==amount,"LEGACY_BATCH_RECONCILIATION_REQUIRED");
            foreach(var entry in originals){var batch=await db.StockBatches.SingleAsync(b=>b.BatchId==entry.BatchId,ct);
                Output.Require(batch.WarehouseQuantity-batch.ReservedQuantity-entry.Quantity>=0,"BATCH_STOCK_INSUFFICIENT");
                batch.WarehouseQuantity-=entry.Quantity;AddEntry(batch,movement,-entry.Quantity);}
        }else if(movement.SignedBaseQty<0&&document.Kind=="ADJUSTMENT"){
            // An adjustment is a physical count/disposal, never an implicit issue from good stock.
            var json=await db.DocumentEvents.AsNoTracking().Where(e=>e.DocumentId==document.DocumentId&&e.EventType=="BATCH_ADJUSTMENT_SELECTED")
                .OrderByDescending(e=>e.DocumentEventId).Select(e=>e.SnapshotJson).FirstOrDefaultAsync(ct);
            Output.Require(json!=null,"ADJUSTMENT_BATCH_SELECTION_REQUIRED");
            var selections=System.Text.Json.JsonSerializer.Deserialize<HotelOps.Api.Compact.Contracts.BatchAdjustmentLine[]>(json!)!
                .Where(x=>x.LineId==movement.LineId).ToArray();
            Output.Require(selections.Length>0&&selections.All(x=>x.Quantity>0)&&selections.Sum(x=>x.Quantity)==amount,"ADJUSTMENT_BATCH_TOTAL_MISMATCH");
            foreach(var selection in selections){
                var batch=await db.StockBatches.SingleOrDefaultAsync(b=>b.BatchId==selection.BatchId&&b.WarehouseId==movement.WarehouseId&&b.ItemId==movement.ItemId,ct)
                    ??throw new ApiException(409,"BATCH_SOURCE_MISMATCH");
                Output.Require(batch.WarehouseQuantity-batch.ReservedQuantity>=selection.Quantity,"BATCH_STOCK_INSUFFICIENT");
                batch.WarehouseQuantity-=selection.Quantity;AddEntry(batch,movement,-selection.Quantity);
            }
        }else if(movement.SignedBaseQty<0){
            var batches=await db.StockBatches.Where(b=>b.WarehouseId==movement.WarehouseId&&b.ItemId==movement.ItemId&&b.WarehouseQuantity>b.ReservedQuantity).ToArrayAsync(ct);
            var candidates=Ordered(batches.Where(b=>Usable(b,profile!)),profile!).ToArray();
            Output.Require(candidates.Sum(b=>b.WarehouseQuantity-b.ReservedQuantity)>=amount,"BATCH_AVAILABLE_INSUFFICIENT");
            foreach(var batch in candidates){if(amount==0)break;var take=Math.Min(amount,batch.WarehouseQuantity-batch.ReservedQuantity);
                batch.WarehouseQuantity-=take;amount-=take;AddEntry(batch,movement,-take);
                if(staffBalanceId!=null){var hold=await db.StaffBatchBalances.SingleOrDefaultAsync(h=>h.BalanceId==staffBalanceId&&h.BatchId==batch.BatchId,ct);
                    if(hold==null){hold=new StaffBatchBalance{BalanceId=staffBalanceId.Value,BatchId=batch.BatchId};db.StaffBatchBalances.Add(hold);}hold.Quantity+=take;}
            }
        }else if(document.Kind is "RETURN" or "TRANSFER_RECEIPT"){
            var line=await db.InventoryDocumentLines.SingleAsync(l=>l.LineId==movement.LineId,ct);
            var originalMovement=await db.StockMovements.SingleOrDefaultAsync(m=>m.LineId==line.SourceLineId&&m.SignedBaseQty<0,ct);
            Output.Require(originalMovement!=null,"BATCH_SOURCE_MOVEMENT_REQUIRED");
            var originals=await db.StockBatchEntries.Where(e=>e.MovementId==originalMovement!.MovementId).OrderBy(e=>e.EntryId).ToArrayAsync(ct);
            Output.Require(originals.Length>0,"LEGACY_BATCH_RECONCILIATION_REQUIRED");
            foreach(var entry in originals){if(amount==0)break;var originalBatch=await db.StockBatches.SingleAsync(b=>b.BatchId==entry.BatchId,ct);
                var delivered=await (from e in db.StockBatchEntries join m in db.StockMovements on e.MovementId equals m.MovementId
                    join l in db.InventoryDocumentLines on m.LineId equals l.LineId join b in db.StockBatches on e.BatchId equals b.BatchId
                    where l.SourceLineId==line.SourceLineId&&m.SignedBaseQty>0&&(b.BatchId==originalBatch.BatchId||b.OriginBatchId==originalBatch.BatchId)
                        &&!db.StockMovements.Any(rev=>rev.ReversalOfMovementId==m.MovementId)
                    select (decimal?)e.Quantity).SumAsync(ct)??0;
                var take=Math.Min(amount,-entry.Quantity-delivered);if(take<=0)continue;
                StockBatch target=originalBatch;
                if(document.Kind=="TRANSFER_RECEIPT"){
                    target=new StockBatch{BranchId=movement.BranchId,WarehouseId=movement.WarehouseId,ItemId=movement.ItemId,OriginBatchId=originalBatch.BatchId,
                        LotNumber=originalBatch.LotNumber,ReceivedAt=originalBatch.ReceivedAt,ExpiresOn=originalBatch.ExpiresOn,Status=originalBatch.Status,
                        ReceiptLineId=movement.LineId,ReceivedQuantity=take,CreatedAt=Now};db.StockBatches.Add(target);await db.SaveChangesAsync(ct);
                }
                Output.Require(target.WarehouseId==movement.WarehouseId,"BATCH_RETURN_WAREHOUSE_MISMATCH");
                target.WarehouseQuantity+=take;AddEntry(target,movement,take);amount-=take;
            }
            Output.Require(amount==0,"BATCH_SOURCE_QUANTITY_EXCEEDED");
        }else{
            // A posted receipt establishes quantity, not an invented supplier lot or expiry.
            var batch=new StockBatch{BranchId=movement.BranchId,WarehouseId=movement.WarehouseId,ItemId=movement.ItemId,LotNumber=$"RECEIPT-{movement.LineId}",
                ReceivedAt=movement.OccurredAt,ReceivedQuantity=amount,WarehouseQuantity=amount,ReceiptLineId=movement.LineId,Status="QUARANTINED",CreatedAt=Now};
            db.StockBatches.Add(batch);await db.SaveChangesAsync(ct);AddEntry(batch,movement,amount);
        }
        await db.SaveChangesAsync(ct);
        var ledger=await db.StockMovements.Where(m=>m.WarehouseId==movement.WarehouseId&&m.ItemId==movement.ItemId).SumAsync(m=>(decimal?)m.SignedBaseQty,ct)??0;
        var split=await db.StockBatches.Where(b=>b.WarehouseId==movement.WarehouseId&&b.ItemId==movement.ItemId).SumAsync(b=>(decimal?)b.WarehouseQuantity,ct)??0;
        Output.Require(ledger==split,"BATCH_LEDGER_RECONCILIATION_FAILED");
    }
    private void AddEntry(StockBatch batch,StockMovement movement,decimal quantity)=>db.StockBatchEntries.Add(new StockBatchEntry{BatchId=batch.BatchId,MovementId=movement.MovementId,Kind="WAREHOUSE",Quantity=quantity,OccurredAt=Now});
    internal async Task Consume(StaffSupplyLedger ledger,CancellationToken ct)
    {
        var balance=await db.StaffSupplyBalances.SingleAsync(b=>b.BalanceId==ledger.BalanceId,ct);
        var allocation=await db.StaffSupplyAllocations.SingleAsync(a=>a.AllocationId==balance.AllocationId,ct);
        var profile=await db.ItemLifecycleProfiles.SingleAsync(p=>p.BranchId==allocation.BranchId&&p.ItemId==balance.ItemId,ct);
        Output.Require(profile.Classification=="CONSUMABLE","CONSUMABLE_CLASSIFICATION_REQUIRED");
        var holds=await db.StaffBatchBalances.Where(h=>h.BalanceId==balance.BalanceId&&h.Quantity>0).ToArrayAsync(ct);
        var ids=holds.Select(h=>h.BatchId).ToArray();var batches=await db.StockBatches.Where(b=>ids.Contains(b.BatchId)).ToArrayAsync(ct);
        var candidates=Ordered(batches.Where(b=>Usable(b,profile)),profile).ToArray();var amount=ledger.Quantity;
        Output.Require(candidates.Sum(b=>holds.Single(h=>h.BatchId==b.BatchId).Quantity)>=amount,"STAFF_BATCH_AVAILABLE_INSUFFICIENT");
        foreach(var batch in candidates){if(amount==0)break;var hold=holds.Single(h=>h.BatchId==batch.BatchId);var take=Math.Min(amount,hold.Quantity);hold.Quantity-=take;amount-=take;
            db.StockBatchEntries.Add(new StockBatchEntry{BatchId=batch.BatchId,LedgerId=ledger.LedgerId,Kind="STAFF_USE",Quantity=-take,OccurredAt=Now});}
        await db.SaveChangesAsync(ct);
    }
}
