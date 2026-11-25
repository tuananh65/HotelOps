using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;
internal sealed class ReusableAccounting(HotelOpsCompactDbContext db,TimeProvider clock)
{
    internal async Task<ReusablePosition> Position(long branch,long item,string status,long? warehouse,long? user,long? room,CancellationToken ct)
    {
        Output.Require(new[]{warehouse,user,room}.Count(x=>x!=null)==1,"CUSTODY_LOCATION_REQUIRED",400);
        var key=warehouse!=null?$"W:{warehouse}":user!=null?$"U:{user}":$"R:{room}";
        var position=await db.ReusablePositions.SingleOrDefaultAsync(p=>p.BranchId==branch&&p.ItemId==item&&p.LocationKey==key&&p.Status==status,ct);
        if(position==null){position=new ReusablePosition{BranchId=branch,ItemId=item,WarehouseId=warehouse,UserId=user,RoomId=room,LocationKey=key,Status=status};db.ReusablePositions.Add(position);await db.SaveChangesAsync(ct);}
        return position;
    }
    internal async Task Move(ReusablePosition? source,ReusablePosition destination,decimal quantity,long actor,Guid key,string kind,string reason,CancellationToken ct,long? work=null,long? documentLine=null,bool postWarehouse=true)
    {
        Output.Require(quantity>0&&SupplyMath.ValidQuantity(quantity),"INVALID_SUPPLY_QUANTITY",400);
        if(ReusableShelfLifePolicy.RequiresUsableStock(source?.Status,destination.Status))
            Output.Require(!await ReusableShelfLifePolicy.Overdue(db,destination.BranchId,destination.ItemId,clock.GetUtcNow().UtcDateTime,ct),"REUSABLE_SHELF_REVIEW_REQUIRED",409);
        if(source!=null){Output.Require(source.BranchId==destination.BranchId&&source.ItemId==destination.ItemId&&source.PositionId!=destination.PositionId,"CUSTODY_SOURCE_MISMATCH");
            var held=await db.ReusableWriteOffs.Where(w=>w.PositionId==source.PositionId&&w.Status=="PENDING").SumAsync(w=>(decimal?)w.Quantity,ct)??0;
            Output.Require(source.Quantity-held>=quantity,"REUSABLE_AVAILABLE_INSUFFICIENT");source.Quantity-=quantity;}
        destination.Quantity+=quantity;
        var entry=new ReusableMovement{BranchId=destination.BranchId,ItemId=destination.ItemId,SourcePositionId=source?.PositionId,DestinationPositionId=destination.PositionId,
            Quantity=quantity,Kind=kind,Reason=reason,ActorUserId=actor,OperationKey=key,OccurredAt=clock.GetUtcNow().UtcDateTime,WorkId=work,DocumentLineId=documentLine};
        db.ReusableMovements.Add(entry);
        if(postWarehouse){
            var from=source?.Status is "WRITTEN_OFF" or "LEGACY_REVIEW"?null:source?.WarehouseId;
            var to=destination.Status is "WRITTEN_OFF" or "LEGACY_REVIEW"?null:destination.WarehouseId;
            if(from!=to){if(from!=null)entry.DocumentLineId=await PostPhysical(destination.BranchId,destination.ItemId,from.Value,-quantity,actor,key,reason,ct);
                if(to!=null)entry.DocumentLineId=await PostPhysical(destination.BranchId,destination.ItemId,to.Value,quantity,actor,key,reason,ct);}
        }
        await db.SaveChangesAsync(ct);
    }
    private async Task<long> PostPhysical(long branch,long item,long warehouse,decimal signedQuantity,long actor,Guid key,string reason,CancellationToken ct)
    {
        var incoming=signedQuantity>0;var now=clock.GetUtcNow().UtcDateTime;
        var document=new InventoryDocument{BranchId=branch,DocumentNo=$"RU-{Guid.NewGuid():N}",Kind=incoming?"REUSABLE_RETURN":"REUSABLE_ISSUE",Status="DRAFT",
            SourceWarehouseId=incoming?null:warehouse,DestinationWarehouseId=incoming?warehouse:null,DestinationBranchId=incoming?branch:null,CreatedBy=actor,CreatedAt=now,Reason=reason};
        db.InventoryDocuments.Add(document);await db.SaveChangesAsync(ct);
        var unit=await db.InventoryItems.Where(i=>i.ItemId==item).Select(i=>i.BaseUnitId).SingleAsync(ct);
        var line=new InventoryDocumentLine{BranchId=branch,DocumentId=document.DocumentId,LineNo=1,ItemId=item,UnitId=unit,UnitKind="UOM",ActualQty=Math.Abs(signedQuantity),ConversionFactor=1};
        db.InventoryDocumentLines.Add(line);await db.SaveChangesAsync(ct);
        document.Status="POSTED";document.PostedBy=actor;document.PostedAt=now;await db.SaveChangesAsync(ct);
        db.StockMovements.Add(new StockMovement{BranchId=branch,LineId=line.LineId,WarehouseId=warehouse,ItemId=item,SignedBaseQty=signedQuantity,CreatedBy=actor,OccurredAt=now});
        db.DocumentEvents.Add(new DocumentEvent{BranchId=branch,DocumentId=document.DocumentId,EventType="REUSABLE_MOVEMENT",RevisionNo=1,ActorUserId=actor,OperationKey=key,Reason=reason});
        await db.SaveChangesAsync(ct);
        return line.LineId;
    }
    internal async Task Receipt(InventoryDocument document,StockMovement movement,CancellationToken ct)
    {
        Output.Require(movement.ReversalOfMovementId==null,"REUSABLE_REVERSAL_REQUIRES_RECONCILIATION",409);
        if(document.Kind=="TRANSFER"){
            Output.Require(movement.SignedBaseQty<0&&document.DestinationBranchId!=null&&document.DestinationWarehouseId!=null,"TRANSFER_DESTINATION_MISMATCH");
            Output.Require(await db.ItemLifecycleProfiles.AnyAsync(p=>p.BranchId==document.DestinationBranchId&&p.ItemId==movement.ItemId&&p.Classification=="REUSABLE",ct),"DESTINATION_CLASSIFICATION_MISMATCH");
            var ready=await Position(movement.BranchId,movement.ItemId,"READY",movement.WarehouseId,null,null,ct);
            var transit=await Position(movement.BranchId,movement.ItemId,"IN_TRANSIT",movement.WarehouseId,null,null,ct);
            await Move(ready,transit,-movement.SignedBaseQty,movement.CreatedBy,Guid.NewGuid(),"HANDOVER",document.Reason??"Điều chuyển vật tư tái sử dụng",ct,documentLine:movement.LineId,postWarehouse:false);
            return;
        }
        if(document.Kind=="TRANSFER_RECEIPT"){
            Output.Require(movement.SignedBaseQty>0,"TRANSFER_RECEIPT_QUANTITY_REQUIRED");
            var line=await db.InventoryDocumentLines.SingleAsync(l=>l.LineId==movement.LineId,ct);
            var sent=await db.ReusableMovements.SingleOrDefaultAsync(m=>m.DocumentLineId==line.SourceLineId&&m.Kind=="HANDOVER",ct);
            Output.Require(sent?.DestinationPositionId!=null,"REUSABLE_TRANSFER_SOURCE_REQUIRED");
            var transit=await db.ReusablePositions.SingleAsync(p=>p.PositionId==sent!.DestinationPositionId,ct);
            Output.Require(transit.Status=="IN_TRANSIT"&&transit.ItemId==movement.ItemId&&transit.Quantity>=movement.SignedBaseQty,"REUSABLE_TRANSFER_QUANTITY_EXCEEDED");
            var prior=await (from r in db.ReusableMovements join l in db.InventoryDocumentLines on r.DocumentLineId equals l.LineId
                where l.SourceLineId==line.SourceLineId&&r.Kind=="RECEIPT" select (decimal?)r.Quantity).SumAsync(ct)??0;
            Output.Require(prior+movement.SignedBaseQty<=sent!.Quantity,"REUSABLE_TRANSFER_QUANTITY_EXCEEDED");
            var ready=await Position(movement.BranchId,movement.ItemId,"READY",movement.WarehouseId,null,null,ct);
            transit.Quantity-=movement.SignedBaseQty;ready.Quantity+=movement.SignedBaseQty;
            // Ownership changes only by the actual accepted quantity. Unreceived units remain in transit at origin.
            var key=Guid.NewGuid();var now=clock.GetUtcNow().UtcDateTime;
            db.ReusableMovements.AddRange(
                new ReusableMovement{BranchId=transit.BranchId,ItemId=movement.ItemId,SourcePositionId=transit.PositionId,Quantity=movement.SignedBaseQty,Kind="HANDOVER",DocumentLineId=movement.LineId,ActorUserId=movement.CreatedBy,OperationKey=key,Reason=document.Reason??"Giao vật tư đến chi nhánh nhận",OccurredAt=now},
                new ReusableMovement{BranchId=ready.BranchId,ItemId=movement.ItemId,DestinationPositionId=ready.PositionId,Quantity=movement.SignedBaseQty,Kind="RECEIPT",DocumentLineId=movement.LineId,ActorUserId=movement.CreatedBy,OperationKey=key,Reason=document.Reason??"Xác nhận vật tư thực nhận",OccurredAt=now});
            await db.SaveChangesAsync(ct);return;
        }
        if(document.Kind=="RECEIPT"&&movement.SignedBaseQty>0){
            var received=await Position(movement.BranchId,movement.ItemId,"READY",movement.WarehouseId,null,null,ct);
            await Move(null,received,movement.SignedBaseQty,movement.CreatedBy,Guid.NewGuid(),"RECEIPT",document.Reason??"Nhận vật tư tái sử dụng",ct,documentLine:movement.LineId,postWarehouse:false);
            return;
        }
        Output.Require(document.Kind is "ISSUE" or "RETURN","USE_REUSABLE_CUSTODY_WORKFLOW",409);
        Output.Require(document.ReceivingRoomId!=null&&await db.Rooms.AnyAsync(r=>r.RoomId==document.ReceivingRoomId&&r.BranchId==document.BranchId,ct),"DURABLE_RECEIVING_ROOM_REQUIRED",400);
        var room=await Position(document.BranchId,movement.ItemId,"IN_USE",null,null,document.ReceivingRoomId,ct);
        var warehouse=await Position(document.BranchId,movement.ItemId,document.Kind=="ISSUE"?"READY":"DIRTY",movement.WarehouseId,null,null,ct);
        // The generic material-request ledger already posts the physical warehouse delta.
        // Custody records the same handover without a second warehouse issue or consumption.
        await Move(document.Kind=="ISSUE"?warehouse:room,document.Kind=="ISSUE"?room:warehouse,Math.Abs(movement.SignedBaseQty),movement.CreatedBy,
            Guid.NewGuid(),"HANDOVER",document.Reason??"Bàn giao vật tư tái sử dụng",ct,documentLine:movement.LineId,postWarehouse:false);
    }
}
