using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class DocumentService
{
    private async Task<string[]> Derivations(InventoryDocument d,CancellationToken ct)
    {
        if(d.Kind is "REUSABLE_ISSUE" or "REUSABLE_RETURN")return [];
        var kinds=new List<string>();
        if(d.Kind=="TRANSFER_REQUEST"&&d.Status=="APPROVED"&&d.ClosedAt==null
            &&await access.HasRole("STOREKEEPER",d.BranchId,ct))kinds.Add("TRANSFER");
        if(d.Kind=="MATERIAL_REQUEST"&&d.Status is "APPROVED" or "PARTIALLY_ISSUED"&&d.ApprovedBy!=actor.Id&&d.ClosedAt==null
            &&await access.Allows("MR_FULFILL",d.BranchId,ct:ct)&&await access.HasRole("STOREKEEPER",d.BranchId,ct))kinds.Add("ISSUE");
        if(d.Kind=="REPLENISHMENT"&&d.Status is "REQUESTED" or "PARTIALLY_RECEIVED"&&d.ClosedAt==null
            &&await access.Allows("REPLENISHMENT_RECEIVE",d.BranchId,ct:ct))kinds.Add("RECEIPT");
        if(d.Kind=="ISSUE"&&d.PostedAt!=null)kinds.Add("RETURN");
        if(d.Kind=="TRANSFER"&&d.PostedAt!=null)kinds.Add("TRANSFER_RECEIPT");
        if(d.Kind=="STOCKTAKE"&&d.Status=="COUNTED")kinds.Add("ADJUSTMENT");
        if(d.PostedAt!=null&&d.Kind!="REVERSAL"&&!await CrossBranchTransfer(d,ct)&&!await db.InventoryDocuments.AnyAsync(x=>x.SourceDocumentId==d.DocumentId&&x.PostedAt!=null&&x.Kind!="REVERSAL",ct))kinds.Add("REVERSAL");
        var allowed=new List<string>();
        foreach(var kind in kinds){var branch=kind=="TRANSFER_RECEIPT"?d.DestinationBranchId??d.BranchId:d.BranchId;
            if(await access.Allows(Permission(kind,"create"),branch,ct:ct)&&(!TransferKind(kind)||await access.HasRole("STOREKEEPER",branch,ct)))allowed.Add(kind);}
        return allowed.ToArray();
    }

    private async Task<decimal> Remaining(InventoryDocument d,InventoryDocumentLine line,string target,CancellationToken ct)
    {
        if(target=="TRANSFER")return await TransferRemaining(line,ct);
        if(target=="ISSUE")return (await db.VMaterialFulfillments.Where(f=>f.LineId==line.LineId).Select(f=>f.MissingBaseQty).SingleOrDefaultAsync(ct)??0)/line.ConversionFactor;
        if(target=="ADJUSTMENT")return await db.InventoryDocumentLines.AnyAsync(l=>l.SourceLineId==line.LineId&&db.StockMovements.Any(m=>m.LineId==l.LineId),ct)?0:Math.Abs((line.CountedQty??0)-(line.ExpectedQty??0));
        if(target=="REVERSAL"){
            var movement=await db.StockMovements.AsNoTracking().SingleOrDefaultAsync(m=>m.LineId==line.LineId,ct);
            return movement==null||await db.StockMovements.AnyAsync(m=>m.ReversalOfMovementId==movement.MovementId,ct)?0:Math.Abs(movement.SignedBaseQty)/line.ConversionFactor;
        }
        var used=await (from m in db.StockMovements join l in db.InventoryDocumentLines on m.LineId equals l.LineId
            join child in db.InventoryDocuments on l.DocumentId equals child.DocumentId
            where l.SourceLineId==line.LineId&&child.Kind==target&&!db.StockMovements.Any(x=>x.ReversalOfMovementId==m.MovementId)
            select (decimal?)m.SignedBaseQty).SumAsync(ct)??0;
        return Math.Max(0,line.ActualQty-used/line.ConversionFactor);
    }

    private async Task<object?> InventoryContext(InventoryDocument d,CancellationToken ct)
    {
        if(!await access.Allows("INVENTORY_VIEW",d.BranchId,ct:ct)&&!(TransferKind(d.Kind)&&await CanReadTransfer(db,access,d,ct)))return null;
        var derivations=await Derivations(d,ct);
        var lines=await db.InventoryDocumentLines.AsNoTracking().Where(l=>l.DocumentId==d.DocumentId).OrderBy(l=>l.LineNo).ToArrayAsync(ct);
        var rows=new List<object>();
        foreach(var line in lines){
            var item=await db.InventoryItems.AsNoTracking().SingleAsync(i=>i.ItemId==line.ItemId,ct);
            var unit=await db.ReferenceValues.AsNoTracking().Where(u=>u.ReferenceId==line.UnitId).Select(u=>u.Name).SingleAsync(ct);
            var warehouse=d.SourceWarehouseId??d.DestinationWarehouseId;
            var balance=warehouse==null?(decimal?)null:await db.StockMovements.Where(m=>m.WarehouseId==warehouse&&m.ItemId==line.ItemId).SumAsync(m=>(decimal?)m.SignedBaseQty,ct)??0;
            var remaining=new Dictionary<string,decimal>();
            foreach(var kind in derivations)remaining[kind]=await Remaining(d,line,kind,ct);
            rows.Add(new{line.LineId,item.Name,item.Code,unit,balance,remaining});
        }
        var write=d.Kind is not ("REUSABLE_ISSUE" or "REUSABLE_RETURN")&&await access.Allows(Permission(d.Kind,"create"),d.Kind=="TRANSFER_REQUEST"?d.DestinationBranchId:d.BranchId,d.CreatedBy==actor.Id,ct:ct);
        if(d.Kind=="TRANSFER_REQUEST")write&=d.CreatedBy==actor.Id&&await access.HasRole("OPERATION_MANAGER",d.DestinationBranchId??d.BranchId,ct);
        else if(TransferKind(d.Kind))write&=await access.HasRole("STOREKEEPER",d.BranchId,ct);
        var independent=d.Kind is "RETURN" or "ADJUSTMENT" or "REVERSAL";
        var consumableReduction=await ConsumableReduction(d,ct);
        var approvalWriter=consumableReduction?await access.HasRole("OPERATION_MANAGER",d.BranchId,ct)&&await access.Allows("ROOM_SUPPLY_CONFIG",d.BranchId,ct:ct):write;
        var canApprove=independent&&approvalWriter&&d.CreatedBy!=actor.Id&&d.ApprovedBy==null&&d.Status=="DRAFT";
        var canPost=write&&d.Status=="DRAFT"&&d.PostedAt==null&&d.Kind is "RECEIPT" or "ISSUE" or "RETURN" or "TRANSFER" or "TRANSFER_RECEIPT" or "ADJUSTMENT" or "REVERSAL"
            &&(!independent||d.ApprovedBy!=null&&d.ApprovedBy!=actor.Id);
        if(d.Kind=="ISSUE")canPost&=await access.Allows("MR_FULFILL",d.BranchId,ct:ct)&&await access.HasRole("STOREKEEPER",d.BranchId,ct);
        if(d.Kind=="RECEIPT"&&d.SourceDocumentId!=null&&await db.InventoryDocuments.AnyAsync(x=>x.DocumentId==d.SourceDocumentId&&x.Kind=="REPLENISHMENT",ct))canPost&=await access.Allows("REPLENISHMENT_RECEIVE",d.BranchId,ct:ct);
        var children=await db.InventoryDocuments.AsNoTracking().Where(x=>x.SourceDocumentId==d.DocumentId).OrderByDescending(x=>x.DocumentId).Take(50)
            .Select(x=>new{x.DocumentId,x.DocumentNo,x.Kind,x.Status,x.PostedAt}).ToArrayAsync(ct);
        var documentCreator=await db.Users.AsNoTracking().Where(u=>u.UserId==d.CreatedBy).Select(u=>u.FullName).SingleAsync(ct);
        var sourceRequester=d.Kind=="MATERIAL_REQUEST"&&d.ReceivingRoomId!=null
            ?await (from w in db.WorkItems.AsNoTracking() join u in db.Users on w.CreatedBy equals u.UserId
                where w.WorkId==d.SourceWorkId&&w.Kind=="INCIDENT"&&w.BranchId==d.BranchId&&w.RoomId==d.ReceivingRoomId
                    &&db.WorkEvents.Any(e=>e.WorkId==w.WorkId&&e.EventType=="ROOM_ISSUE_REPORTED")
                select u.FullName).SingleOrDefaultAsync(ct):null;
        return new{lines=rows,derivations,canPost,adjustmentBatches=await AdjustmentBatches(d,ct),
            receivingRoom=await (from room in db.Rooms.AsNoTracking() join location in db.Locations on room.LocationId equals location.LocationId join branch in db.Branches on room.BranchId equals branch.BranchId where room.RoomId==d.ReceivingRoomId select branch.Name+" · "+location.Name+" · Phòng "+room.Code).SingleOrDefaultAsync(ct),
            canApprove,canReject=canApprove&&consumableReduction,
            canEdit=write&&d.ApprovedBy==null&&d.PostedAt==null&&d.Status is "DRAFT" or "IN_PROGRESS" or "REQUESTED",
            creator=sourceRequester??documentCreator,documentCreator,
            approver=await db.Users.AsNoTracking().Where(u=>u.UserId==d.ApprovedBy).Select(u=>u.FullName).SingleOrDefaultAsync(ct),
            sourceNumber=await db.InventoryDocuments.AsNoTracking().Where(x=>x.DocumentId==d.SourceDocumentId).Select(x=>x.DocumentNo).SingleOrDefaultAsync(ct),
            sourceWarehouse=await db.Warehouses.AsNoTracking().Where(w=>w.WarehouseId==d.SourceWarehouseId).Select(w=>w.Name).SingleOrDefaultAsync(ct),
            destinationWarehouse=await db.Warehouses.AsNoTracking().Where(w=>w.WarehouseId==d.DestinationWarehouseId).Select(w=>w.Name).SingleOrDefaultAsync(ct),children};
    }

    // Create a linked draft and all selected lines atomically; posting remains a separate confirmation.
    public Task<object> Derive(long id,DeriveDocumentRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        var source=await Load(id,"read",ct);Output.Version(source.RowVersion,r.RowVersion);Output.Key(r.OperationKey);
        await GuardWorkflowDocumentMutation(source,ct);
        Output.Require(!await db.DocumentEvents.AnyAsync(e=>e.DocumentId==id&&e.ActorUserId==actor.Id&&e.OperationKey==r.OperationKey,ct),"OPERATION_ALREADY_APPLIED");
        Output.Require((await Derivations(source,ct)).Contains(r.Kind),"DERIVATION_NOT_ALLOWED",403);
        Output.Require(r.Lines is {Count:>0 and <=100}&&r.Lines.Select(x=>x.LineId).Distinct().Count()==r.Lines.Count,"DOCUMENT_LINES_REQUIRED",400);
        Output.Reason(r.Reason);
        long? from=null,to=null;
        if(r.Kind=="ISSUE")from=source.SourceWarehouseId??r.WarehouseId;
        if(r.Kind=="RECEIPT")to=source.DestinationWarehouseId??r.WarehouseId;
        if(r.Kind=="RETURN")to=source.SourceWarehouseId;
        if(r.Kind=="TRANSFER_RECEIPT")to=source.DestinationWarehouseId;
        if(r.Kind=="TRANSFER"){from=source.SourceWarehouseId;to=source.DestinationWarehouseId;}
        if(r.Kind=="ADJUSTMENT")from=source.SourceWarehouseId;
        if(r.Kind=="REVERSAL")from=source.SourceWarehouseId??source.DestinationWarehouseId;
        Output.Require(from!=null||to!=null,"WAREHOUSE_REQUIRED",400);
        var branch=r.Kind=="TRANSFER_RECEIPT"?source.DestinationBranchId??source.BranchId:source.BranchId;
        var child=(ResourceDto)await Create(new CreateDocumentRequest(branch,r.Kind,branch==source.BranchId?source.DepartmentId:null,from,to,id,
            branch==source.BranchId?source.SourceWorkId:null,source.SupplierName,r.Reason,null,null,r.Kind=="TRANSFER"?source.DestinationBranchId:branch),ct);
        var childId=Convert.ToInt64(child.Fields["documentId"]);
        foreach(var selected in r.Lines){
            var line=await db.InventoryDocumentLines.AsNoTracking().SingleOrDefaultAsync(l=>l.DocumentId==id&&l.LineId==selected.LineId,ct)??throw new ApiException(400,"SOURCE_LINE_MISMATCH");
            var remaining=await Remaining(source,line,r.Kind,ct);
            Output.Require(selected.Quantity>0&&selected.Quantity<=remaining,"SOURCE_QUANTITY_EXCEEDED");
            if(r.Kind is "ADJUSTMENT" or "REVERSAL")Output.Require(selected.Quantity==remaining,"EXACT_QUANTITY_REQUIRED");
            var document=await db.InventoryDocuments.SingleAsync(d=>d.DocumentId==childId,ct);
            await Line(childId,null,new DocumentLineRequest(Convert.ToBase64String(document.RowVersion),line.ItemId,line.UnitId,selected.Quantity,line.UnitCost,line.LineId),ct);
        }
        // Posted headers are immutable. Event keys and remaining-quantity checks protect linked drafts.
        if(source.PostedAt==null){db.Entry(source).Property(d=>d.Reason).IsModified=true;await db.SaveChangesAsync(ct);}
        await Event(source,"DERIVED",r.OperationKey,r.Reason,ct);
        return (object)Output.Record(await db.InventoryDocuments.SingleAsync(d=>d.DocumentId==childId,ct));
    },ct);
}
