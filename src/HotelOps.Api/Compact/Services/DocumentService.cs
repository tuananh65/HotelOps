using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;
public sealed partial class DocumentService(HotelOpsCompactDbContext db,Access access,Actor actor,Atomic atomic,Audit audit,TimeProvider clock,NotificationService notifications,IConfiguration? configuration=null)
{
    private static string Permission(string kind,string action)=>kind switch{
        "MATERIAL_REQUEST"=>action=="read"?"MR_VIEW":action=="approve"||action=="reject"?"MR_APPROVE_L1":"MR_CREATE",
        "REPLENISHMENT"=>action=="read"?"REPLENISHMENT_VIEW":action=="close"?"REPLENISHMENT_CLOSE":"REPLENISHMENT_CREATE",
        "RECEIPT"=>action=="read"?"INVENTORY_VIEW":"STOCK_RECEIPT_POST",
        "ISSUE"=>action=="read"?"INVENTORY_VIEW":"STOCK_ISSUE_POST",
        "RETURN"=>action=="read"?"INVENTORY_VIEW":"STOCK_RETURN_POST",
        "TRANSFER_REQUEST"=>action=="read"?"INVENTORY_VIEW":action is "approve" or "reject"?"TRANSFER_REQUEST_APPROVE":"TRANSFER_REQUEST_CREATE",
        "TRANSFER" or "TRANSFER_RECEIPT"=>action=="read"?"INVENTORY_VIEW":"STOCK_TRANSFER_POST",
        "STOCKTAKE"=>action=="read"?"INVENTORY_VIEW":"STOCKTAKE_PERFORM",
        "ADJUSTMENT"=>action=="read"?"INVENTORY_VIEW":"STOCK_ADJUST_POST",
        "REVERSAL"=>action=="read"?"INVENTORY_VIEW":"STOCK_REVERSE",
        "REUSABLE_ISSUE" or "REUSABLE_RETURN"=>action=="read"?"INVENTORY_VIEW":throw new ApiException(403,"USE_REUSABLE_CUSTODY_WORKFLOW"),
        _=>throw new ApiException(400,"UNKNOWN_DOCUMENT_KIND")};
    public async Task<InventoryDocument> Load(long id,string action,CancellationToken ct){
        var d=await db.InventoryDocuments.SingleOrDefaultAsync(x=>x.DocumentId==id,ct)??throw new ApiException(404,"DOCUMENT_NOT_FOUND");
        if(action!="read")await GuardWorkflowDocumentMutation(d,ct);
        if(TransferKind(d.Kind)){await TransferAuthority(d,action,ct);return d;}
        if(action is "approve" or "reject"&&await ConsumableReduction(d,ct)){
            await access.RequireRolePermission("OPERATION_MANAGER","ROOM_SUPPLY_CONFIG",d.BranchId,ct);return d;
        }
        Output.Require(d.Kind!="MATERIAL_REQUEST"||action!="post","MATERIAL_REQUEST_NOT_STOCK_DOCUMENT",403);
        if(d.Kind=="MATERIAL_REQUEST"&&action is "create" or "submit" or "cancel")
            Output.Require(d.CreatedBy==actor.Id,"DOCUMENT_OWNER_REQUIRED",403);
        if(d.Kind=="MATERIAL_REQUEST"&&action is "approve" or "reject" or "request-information" or "close")await ApprovalAuthority(d,ct);
        else await access.Require(Permission(d.Kind,action),d.BranchId,d.CreatedBy==actor.Id,ct);return d;
    }
    public async Task<object> List(long? branchId,string kind,int page,int pageSize,CancellationToken ct,string? status=null,string? search=null,string sort="latest",DateTime? from=null,DateTime? to=null){
        var permission=Permission(kind,"read");var all=await access.Allows(permission,branchId,ct:ct);if(!all)await access.Require(permission,branchId,true,ct);
        if(TransferKind(kind))Output.Require(branchId!=null&&all&&(await access.HasRole("OPERATION_MANAGER",branchId.Value,ct)||await access.HasRole("STOREKEEPER",branchId.Value,ct)),"PERMISSION_DENIED",403);
        var query=db.InventoryDocuments.AsNoTracking().Where(d=>d.Kind==kind&&(all||d.CreatedBy==actor.Id));
        query=TransferKind(kind)?query.Where(d=>d.BranchId==branchId||d.DestinationBranchId==branchId
            ||d.Kind=="TRANSFER_RECEIPT"&&db.InventoryDocuments.Any(s=>s.DocumentId==d.SourceDocumentId&&s.Kind=="TRANSFER"&&s.BranchId==branchId))
            :query.Where(d=>branchId==null?db.Branches.Any(b=>b.BranchId==d.BranchId&&b.IsActive):d.BranchId==branchId);
        page=Math.Max(page,1);pageSize=Math.Clamp(pageSize,1,100);
        if(!string.IsNullOrWhiteSpace(status)&&!(kind=="TRANSFER"&&status is "IN_TRANSIT" or "PARTIALLY_RECEIVED" or "RECEIVED"))query=query.Where(d=>d.Status==status);
        if(!string.IsNullOrWhiteSpace(search)){var term=search.Trim();query=query.Where(d=>d.DocumentNo.Contains(term)||(d.Reason!=null&&d.Reason.Contains(term)));}
        Output.Require(sort is "latest" or "oldest" or "number","INVALID_SORT",400);
        Output.Require(from==null||to==null||from<=to,"INVALID_DATE_RANGE",400);
        if(from!=null)query=query.Where(d=>d.CreatedAt>=from);if(to!=null)query=query.Where(d=>d.CreatedAt<=to);
        if(kind=="TRANSFER"&&status is "IN_TRANSIT" or "PARTIALLY_RECEIVED" or "RECEIVED"){
            var projected=await TransferStatusProjection.Read(db,await query.Where(d=>d.PostedAt!=null).Select(d=>d.DocumentId).ToArrayAsync(ct),ct);
            var ids=projected.Where(x=>x.Value==status).Select(x=>x.Key).ToArray();query=query.Where(d=>ids.Contains(d.DocumentId));
        }
        var ordered=sort=="oldest"?query.OrderBy(d=>d.DocumentId):sort=="number"?query.OrderBy(d=>d.DocumentNo).ThenBy(d=>d.DocumentId):query.OrderByDescending(d=>d.DocumentId);
        var rows=await ordered.Skip((page-1)*pageSize).Take(pageSize).ToArrayAsync(ct);
        var statuses=await TransferStatusProjection.Read(db,rows.Where(d=>d.Kind=="TRANSFER").Select(d=>d.DocumentId).ToArray(),ct);
        return new PageDto<ResourceDto>(rows.Select(d=>TransferStatusProjection.Record(d,d.DocumentId,statuses)).ToArray(),await query.CountAsync(ct),page,pageSize);
    }
    public async Task<object> Detail(long id,CancellationToken ct){var d=await Load(id,"read",ct);
        var statuses=await TransferStatusProjection.Read(db,d.Kind=="TRANSFER"?[id]:[],ct);
        var canApprove=false;
        if(d.Kind=="MATERIAL_REQUEST"){
            try{await ApprovalAuthority(d,ct);canApprove=true;}
            catch(ApiException e) when(e.Status is 403 || e.Code=="REQUESTER_DEPARTMENT_REQUIRED"){ }
        }
        return new{document=TransferStatusProjection.Record(d,id,statuses),canApprove,transfer=TransferKind(d.Kind)?await TransferContext(d,ct):null,inventory=await InventoryContext(d,ct),
        lines=(await db.InventoryDocumentLines.AsNoTracking().Where(l=>l.DocumentId==id).OrderBy(l=>l.LineNo).ToArrayAsync(ct)).Select(Output.Record),
        fulfillment=(await db.VMaterialFulfillments.AsNoTracking().Where(f=>f.DocumentId==id).ToArrayAsync(ct)).Select(Output.Record),
        events=(await db.DocumentEvents.AsNoTracking().Where(e=>e.DocumentId==id).OrderByDescending(e=>e.DocumentEventId).Take(200).ToArrayAsync(ct)).Select(Output.Record)};}
    public Task<object> Create(CreateDocumentRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        Output.Require(r.Kind!="REPLENISHMENT","USE_REQUEST_WORKFLOW",403);
        if(r.SourceDocumentId is long workflowSource)await GuardWorkflowDocumentMutation(await db.InventoryDocuments.SingleOrDefaultAsync(d=>d.DocumentId==workflowSource,ct)??throw new ApiException(404,"DOCUMENT_NOT_FOUND"),ct);
        if(TransferKind(r.Kind))await ValidateTransferCreate(r,ct);
        else {await access.Require(Permission(r.Kind,"create"),r.BranchId,true,ct);Output.Require(r.DestinationBranchId==null||r.DestinationBranchId==r.BranchId,"SOURCE_BRANCH_MISMATCH");}
        if(r.OperationKey!=null){Output.Key(r.OperationKey.Value);Output.Require(!await db.DocumentEvents.AnyAsync(e=>e.ActorUserId==actor.Id&&e.OperationKey==r.OperationKey,ct),"OPERATION_ALREADY_APPLIED");}
        if(r.Kind=="MATERIAL_REQUEST"){
            var now=clock.GetUtcNow().UtcDateTime;
            Output.Require(r.DepartmentId!=null&&await db.Memberships.AnyAsync(m=>m.UserId==actor.Id&&m.BranchId==r.BranchId&&m.DepartmentId==r.DepartmentId&&m.Kind=="EMPLOYMENT"&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now),ct),"REQUESTER_DEPARTMENT_MEMBERSHIP_REQUIRED");
        }
        long? receivingRoomId=null;
        if(r.SourceDocumentId!=null){var source=await Load(r.SourceDocumentId.Value,"read",ct);Output.Require(source.BranchId==r.BranchId||r.Kind=="TRANSFER_RECEIPT"&&source.Kind=="TRANSFER"&&source.DestinationBranchId==r.BranchId,"SOURCE_BRANCH_MISMATCH");receivingRoomId=source.BranchId==r.BranchId?source.ReceivingRoomId:null;}
        if(r.SourceWarehouseId!=null)Output.Require(await db.Warehouses.AnyAsync(w=>w.WarehouseId==r.SourceWarehouseId&&w.BranchId==r.BranchId&&w.IsActive,ct),"WAREHOUSE_UNAVAILABLE");
        if(r.DestinationWarehouseId!=null)Output.Require(await db.Warehouses.AnyAsync(w=>w.WarehouseId==r.DestinationWarehouseId&&w.BranchId==(r.DestinationBranchId??r.BranchId)&&w.IsActive,ct),"WAREHOUSE_UNAVAILABLE");
        if(r.SourceWorkId!=null){
            var source=await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w=>w.WorkId==r.SourceWorkId&&w.BranchId==r.BranchId&&!WorkRules.Terminal.Contains(w.Status),ct);
            Output.Require(source!=null,"SOURCE_WORK_UNAVAILABLE");
            if(r.Kind=="MATERIAL_REQUEST"&&source!.Kind=="MAINTENANCE")Output.Require(source.CreatedBy==actor.Id||await db.WorkAssignments.AnyAsync(a=>a.WorkId==source.WorkId&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE",ct)||await access.Allows("MAINTENANCE_ASSIGN",r.BranchId,ct:ct),"SOURCE_WORK_UNAVAILABLE");
        }
        var d=new InventoryDocument{DocumentNo=Guid.NewGuid().ToString("N"),BranchId=r.BranchId,Kind=r.Kind,Status=r.Kind=="REPLENISHMENT"?"REQUESTED":r.Kind=="STOCKTAKE"?"IN_PROGRESS":"DRAFT",
            DepartmentId=r.DepartmentId,SourceWarehouseId=r.SourceWarehouseId,DestinationWarehouseId=r.DestinationWarehouseId,DestinationBranchId=r.DestinationWarehouseId==null?null:r.DestinationBranchId??r.BranchId,
            SourceDocumentId=r.SourceDocumentId,SourceWorkId=r.SourceWorkId,ReceivingRoomId=receivingRoomId,SupplierName=r.SupplierName,Reason=r.Reason,ExpectedOn=r.ExpectedOn,CreatedBy=actor.Id};
        db.InventoryDocuments.Add(d);await db.SaveChangesAsync(ct);await Event(d,"CREATED",r.OperationKey??Guid.NewGuid(),r.Reason,ct);return (object)Output.Record(d);
    },ct);
    public Task<object> Line(long id,long? lineId,DocumentLineRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        var d=await Load(id,"create",ct);Output.Version(d.RowVersion,r.RowVersion);
        Output.Require(d.Status is "DRAFT" or "NEEDS_INFORMATION" or "REQUESTED" or "IN_PROGRESS","DOCUMENT_NOT_EDITABLE");
        Output.Require(d.ApprovedBy==null,"DOCUMENT_APPROVED_IMMUTABLE");
        Output.Require(r.Quantity>0&&r.UnitCost is null or >=0&&r.CountedQty is null or >=0,"INVALID_QUANTITY",400);
        var item=await db.InventoryItems.SingleOrDefaultAsync(x=>x.ItemId==r.ItemId&&x.IsActive,ct)??throw new ApiException(409,"ITEM_UNAVAILABLE");
        var unit=await db.ReferenceValues.SingleOrDefaultAsync(x=>x.ReferenceId==r.UnitId&&x.Kind=="UOM"&&x.IsActive,ct)??throw new ApiException(409,"UNIT_UNAVAILABLE");
        Output.Require(unit.ReferenceId==item.BaseUnitId||unit.BaseUnitId==item.BaseUnitId,"UNIT_MISMATCH",400);
        var factor=unit.ReferenceId==item.BaseUnitId?1:unit.ConversionFactor??0;
        var line=lineId==null?new InventoryDocumentLine{BranchId=d.BranchId,DocumentId=id,LineNo=(await db.InventoryDocumentLines.Where(l=>l.DocumentId==id).MaxAsync(l=>(int?)l.LineNo,ct)??0)+1}
            :await db.InventoryDocumentLines.SingleOrDefaultAsync(l=>l.DocumentId==id&&l.LineId==lineId,ct)??throw new ApiException(404,"LINE_NOT_FOUND");
        Output.Require(factor>0,"UNIT_MISMATCH",400);
        line.ItemId=r.ItemId;line.UnitId=r.UnitId;line.ConversionFactor=factor;line.ActualQty=r.Quantity;line.RequestedQty=d.Kind is "MATERIAL_REQUEST" or "REPLENISHMENT" or "TRANSFER_REQUEST"?r.Quantity:null;
        line.UnitCost=r.UnitCost;line.SourceLineId=r.SourceLineId;line.CountedQty=r.CountedQty;
        if(d.Kind=="STOCKTAKE"){
            Output.Require(d.SourceWarehouseId!=null&&r.CountedQty!=null,"WAREHOUSE_AND_COUNT_REQUIRED",400);
            Output.Require(!await db.InventoryDocumentLines.AnyAsync(l=>l.DocumentId==id&&l.ItemId==r.ItemId&&l.LineId!=line.LineId,ct),"DUPLICATE_STOCKTAKE_ITEM",409);
            // A count edits the observed quantity, never silently refreshes the stock snapshot.
            // Posting adjustments still rejects stock movements made since this snapshot.
            if(lineId==null||line.ExpectedQty==null){
                var balance=await db.StockMovements.Where(m=>m.WarehouseId==d.SourceWarehouseId&&m.ItemId==r.ItemId).SumAsync(m=>(decimal?)m.SignedBaseQty,ct)??0;
                line.ExpectedQty=balance/factor;
            }
        }
        if(r.SourceLineId!=null)Output.Require(await db.InventoryDocumentLines.AnyAsync(l=>l.LineId==r.SourceLineId&&l.DocumentId==d.SourceDocumentId&&l.ItemId==r.ItemId,ct),"SOURCE_LINE_MISMATCH");
        if(lineId==null)db.InventoryDocumentLines.Add(line);db.Entry(d).Property(x=>x.Reason).IsModified=true;
        await db.SaveChangesAsync(ct);await Event(d,"LINE_SAVED",Guid.NewGuid(),null,ct);return (object)new{document=Output.Record(d),line=Output.Record(line)};
    },ct);
    public Task<object> Act(long id,string action,ActionRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        var d=await Load(id,action,ct);Output.Version(d.RowVersion,r.RowVersion);Output.Key(r.OperationKey);
        Output.Require(!await db.DocumentEvents.AnyAsync(e=>e.DocumentId==id&&e.ActorUserId==actor.Id&&e.OperationKey==r.OperationKey,ct),"OPERATION_ALREADY_APPLIED");
        var lines=await db.InventoryDocumentLines.Where(l=>l.DocumentId==id).OrderBy(l=>l.LineNo).ToArrayAsync(ct);var now=clock.GetUtcNow().UtcDateTime;
        Output.Require(lines.Length>0,"DOCUMENT_LINES_REQUIRED");
        switch(action){
            case "submit":
                Output.Require(d.Kind is "MATERIAL_REQUEST" or "TRANSFER_REQUEST"&&d.Status is "DRAFT" or "NEEDS_INFORMATION","INVALID_DOCUMENT_TRANSITION");
                if(d.Kind=="TRANSFER_REQUEST")await ValidateTransferLines(d,lines,ct);
                d.Status="PENDING_APPROVAL";if(d.Kind!="TRANSFER_REQUEST")await notifications.ForPermission(d.BranchId,"MR_APPROVE_L1","PENDING_APPROVAL",d.DocumentNo,null,id,r.OperationKey,ct);break;
            case "approve":
                Output.Require(d.Kind is "MATERIAL_REQUEST" or "TRANSFER_REQUEST" or "ADJUSTMENT" or "RETURN" or "REVERSAL"&&d.Status is "PENDING_APPROVAL" or "DRAFT","INVALID_DOCUMENT_TRANSITION");
                Output.Require(d.CreatedBy!=actor.Id,"INDEPENDENT_APPROVER_REQUIRED",403);
                if(d.Kind=="ADJUSTMENT")Output.Require(d.ApprovedBy==null,"DOCUMENT_ALREADY_APPROVED");
                if(d.Kind=="ADJUSTMENT"&&await (from l in db.InventoryDocumentLines join s in db.InventoryDocumentLines on l.SourceLineId equals s.LineId
                    where l.DocumentId==d.DocumentId&&s.CountedQty<s.ExpectedQty&&db.ItemLifecycleProfiles.Any(p=>p.BranchId==d.BranchId&&p.ItemId==l.ItemId&&p.Classification=="CONSUMABLE") select l.LineId).AnyAsync(ct))
                    await ValidateSavedAdjustmentBatches(d,ct);
                if(d.Kind=="TRANSFER_REQUEST"){Output.Require(d.Status=="PENDING_APPROVAL","INVALID_DOCUMENT_TRANSITION");await ValidateTransferLines(d,lines,ct);}
                if(d.Kind=="MATERIAL_REQUEST")await access.RequireRolePermission("STOREKEEPER","MR_APPROVE_L1",d.BranchId,ct);
                d.ApprovedBy=actor.Id;d.ApprovedAt=now;if(d.Kind is "MATERIAL_REQUEST" or "TRANSFER_REQUEST")d.Status="APPROVED";
                if(r.ApprovedLines!=null)Output.Require(r.ApprovedLines.Count==lines.Length&&r.ApprovedLines.Select(x=>x.LineId).Distinct().Count()==lines.Length,"ALL_APPROVAL_LINES_REQUIRED",400);
                if(d.Kind=="ADJUSTMENT"&&r.ApprovedLines!=null)Output.Require(lines.All(l=>r.ApprovedLines.SingleOrDefault(a=>a.LineId==l.LineId)?.Quantity==l.ActualQty),"ADJUSTMENT_FULL_APPROVAL_REQUIRED",400);
                foreach(var l in lines){var quantity=r.ApprovedLines==null?l.RequestedQty??l.ActualQty:r.ApprovedLines.SingleOrDefault(x=>x.LineId==l.LineId)?.Quantity??-1;
                    Output.Require(quantity>=0&&quantity<=(l.RequestedQty??l.ActualQty),"APPROVED_QUANTITY_EXCEEDS_REQUEST",400);l.ApprovedQty=quantity;}
                if(d.Kind=="TRANSFER_REQUEST"){Output.Require(lines.Any(l=>l.ApprovedQty>0),"APPROVAL_QUANTITY_REQUIRED",400);await ValidateTransferApprovalStock(d,lines,ct);}
                break;
            case "reject":
                Output.Reason(r.Reason);Output.Require(d.Status=="PENDING_APPROVAL"||d.Status=="DRAFT"&&d.ApprovedBy==null&&await ConsumableReduction(d,ct),"INVALID_DOCUMENT_TRANSITION");
                Output.Require(d.CreatedBy!=actor.Id,"INDEPENDENT_APPROVER_REQUIRED",403);d.Status="REJECTED";d.Reason=r.Reason;break;
            case "request-information":
                Output.Reason(r.Reason);Output.Require(d.Kind=="MATERIAL_REQUEST"&&d.Status=="PENDING_APPROVAL","INVALID_DOCUMENT_TRANSITION");d.Status="NEEDS_INFORMATION";break;
            case "count":Output.Require(d.Kind=="STOCKTAKE"&&d.Status=="IN_PROGRESS"&&lines.All(l=>l.CountedQty!=null),"COUNT_INCOMPLETE");d.Status="COUNTED";break;
            case "cancel":Output.Reason(r.Reason);
                Output.Require(d.PostedAt==null&&(d.Status is "DRAFT" or "IN_PROGRESS" or "PENDING_APPROVAL" or "NEEDS_INFORMATION"||d.Kind=="TRANSFER_REQUEST"&&d.Status=="APPROVED"),"DOCUMENT_CANNOT_CANCEL");
                if(d.Kind=="TRANSFER_REQUEST")Output.Require(!await db.InventoryDocuments.AnyAsync(x=>x.SourceDocumentId==id&&x.PostedAt!=null,ct),"TRANSFER_ALREADY_DISPATCHED");
                d.Status="CANCELLED";d.ClosedAt=now;break;
            case "close":Output.Reason(r.Reason);Output.Require(d.Kind is "MATERIAL_REQUEST" or "REPLENISHMENT","INVALID_DOCUMENT_TRANSITION");
                d.Status=d.Kind=="MATERIAL_REQUEST"?"REMAINDER_CLOSED":"CLOSED";d.ClosedAt=now;break;
            case "post":await Post(d,lines,r,ct);break;
            default:throw new ApiException(400,"UNKNOWN_ACTION");
        }
        await db.SaveChangesAsync(ct);await Event(d,action.ToUpperInvariant(),r.OperationKey,r.Reason,ct);
        if(TransferKind(d.Kind))await NotifyTransfer(d,action,r.OperationKey,ct);
        else notifications.Add(d.BranchId,d.CreatedBy,"DOCUMENT_"+action.ToUpperInvariant(),d.DocumentNo,null,id,r.OperationKey);
        await db.SaveChangesAsync(ct);return (object)Output.Record(d);
    },ct);
    private async Task Post(InventoryDocument d,InventoryDocumentLine[] lines,ActionRequest r,CancellationToken ct)
    {
        await StaffSupplyService.Lock(db,ct);
        Output.Require(d.Status=="DRAFT"&&d.PostedAt==null,"DOCUMENT_ALREADY_POSTED");
        Output.Require(d.Kind is "RECEIPT" or "ISSUE" or "RETURN" or "TRANSFER" or "TRANSFER_RECEIPT" or "ADJUSTMENT" or "REVERSAL","DOCUMENT_NOT_POSTABLE");
        if(d.Kind is "ADJUSTMENT" or "RETURN" or "REVERSAL")Output.Require(d.ApprovedBy!=null&&d.ApprovedBy!=actor.Id,"INDEPENDENT_APPROVAL_REQUIRED");
        if(d.Kind=="ISSUE"){
            await access.Require("MR_FULFILL",d.BranchId,ct:ct);
            Output.Require(await access.HasRole("STOREKEEPER",d.BranchId,ct),"STOREKEEPER_REQUIRED",403);
            await access.RequireRolePermission("STOREKEEPER","STOCK_ISSUE_POST",d.BranchId,ct);
            await access.RequireRolePermission("STOREKEEPER","MR_FULFILL",d.BranchId,ct);
        }
        var incoming=d.Kind is "RECEIPT" or "RETURN" or "TRANSFER_RECEIPT";
        var warehouse=incoming?d.DestinationWarehouseId:d.SourceWarehouseId;
        Output.Require(warehouse!=null,"WAREHOUSE_REQUIRED",400);
        Output.Require(await db.Warehouses.AnyAsync(w=>w.WarehouseId==warehouse&&w.BranchId==d.BranchId&&w.IsActive,ct),"WAREHOUSE_UNAVAILABLE");
        InventoryDocument? source=null;
        if(d.SourceDocumentId!=null)source=await db.InventoryDocuments.SingleAsync(x=>x.DocumentId==d.SourceDocumentId,ct);
        if(source?.Kind=="REPLENISHMENT"){
            await access.Require("REPLENISHMENT_RECEIVE",d.BranchId,ct:ct);
            // Read inside the serializable posting transaction, including drafts created before closure.
            // ClosedAt also protects inconsistent source statuses left by older application versions.
            Output.Require(source.Status!="CLOSED"&&source.ClosedAt==null,"REPLENISHMENT_CLOSED");
        }
        if(d.Kind is "ISSUE" or "RETURN" or "TRANSFER_RECEIPT" or "ADJUSTMENT" or "REVERSAL")Output.Require(source!=null,"SOURCE_DOCUMENT_REQUIRED");
        if(d.Kind=="ISSUE"){
            Output.Require(source!.Kind=="MATERIAL_REQUEST"&&source.BranchId==d.BranchId&&source.ApprovedBy!=null&&source.Status is "APPROVED" or "PARTIALLY_ISSUED"&&source.ClosedAt==null,"APPROVED_REQUEST_REQUIRED");
            if(source.ApprovedBy==actor.Id)await access.RequireRolePermission("STOREKEEPER","MR_APPROVE_L1",d.BranchId,ct);
        }
        if(d.Kind is "TRANSFER" or "TRANSFER_RECEIPT")await ValidateTransferPost(d,source,lines,ct);
        if(d.Kind=="TRANSFER")Output.Require(d.SourceWarehouseId!=d.DestinationWarehouseId,"TRANSFER_DESTINATION_MISMATCH");
        if(d.Kind=="REVERSAL"){
            Output.Reason(d.Reason);
            Output.Require(source!.PostedAt!=null&&source.Kind!="REVERSAL","ORIGINAL_MOVEMENT_REQUIRED");
            Output.Require(!await CrossBranchTransfer(source,ct),"CROSS_BRANCH_TRANSFER_REVERSAL_UNSUPPORTED");
            Output.Require(!await db.InventoryDocuments.AnyAsync(x=>x.SourceDocumentId==source.DocumentId&&x.PostedAt!=null&&x.Kind!="REVERSAL",ct),"DOCUMENT_HAS_POSTED_DEPENDENTS");
        }
        if(d.Kind=="TRANSFER_RECEIPT")Output.Require(source!.Kind=="TRANSFER"&&source.PostedAt!=null&&source.DestinationWarehouseId==d.DestinationWarehouseId,"TRANSFER_DESTINATION_MISMATCH");
        // Direct API creation must retain receipt/discrepancy evidence too, not only Derive.
        if(d.Kind=="TRANSFER_RECEIPT")Output.Reason(r.Reason??d.Reason);
        if(d.Kind=="RETURN")Output.Require(source!.Kind=="ISSUE"&&source.PostedAt!=null&&source.SourceWarehouseId==d.DestinationWarehouseId,"RETURN_SOURCE_MISMATCH");
        if(d.Kind=="ADJUSTMENT")Output.Require(source!.Kind=="STOCKTAKE"&&source.Status=="COUNTED"&&source.SourceWarehouseId==warehouse,"STOCKTAKE_REQUIRED");
        var pending=new List<StockMovement>();
        foreach(var l in lines){
            var qty=l.ActualQty*l.ConversionFactor;Output.Require(qty>0,"POSITIVE_QUANTITY_REQUIRED",400);
            Output.Require(await db.InventoryItems.AnyAsync(i=>i.ItemId==l.ItemId&&i.IsActive,ct),"ITEM_INACTIVE");
            var sign=incoming?1m:-1m;long? reverse=null;
            var original=l.SourceLineId==null?null:await db.InventoryDocumentLines.SingleOrDefaultAsync(x=>x.LineId==l.SourceLineId&&x.DocumentId==d.SourceDocumentId&&x.ItemId==l.ItemId,ct);
            if(d.Kind is "RETURN" or "TRANSFER_RECEIPT" or "ADJUSTMENT" or "REVERSAL")Output.Require(original!=null,"SOURCE_LINE_REQUIRED");
            if(d.Kind is "RETURN" or "TRANSFER_RECEIPT"||d.Kind=="RECEIPT"&&source?.Kind=="REPLENISHMENT"){
                Output.Require(original!=null,"SOURCE_LINE_REQUIRED");
                var used=await (from movement in db.StockMovements join line in db.InventoryDocumentLines on movement.LineId equals line.LineId
                    join child in db.InventoryDocuments on line.DocumentId equals child.DocumentId
                    where line.SourceLineId==l.SourceLineId&&child.Kind==d.Kind&&!db.StockMovements.Any(x=>x.ReversalOfMovementId==movement.MovementId)
                    select (decimal?)movement.SignedBaseQty).SumAsync(ct)??0;
                used+=pending.Where(m=>lines.Any(previous=>previous.LineId==m.LineId&&previous.SourceLineId==l.SourceLineId)).Sum(m=>m.SignedBaseQty);
                Output.Require(used+qty<=original!.ActualQty*original.ConversionFactor,"SOURCE_QUANTITY_EXCEEDED");
            }
            if(d.Kind=="ADJUSTMENT"){
                Output.Require(l.ApprovedQty==l.ActualQty,"ADJUSTMENT_FULL_APPROVAL_REQUIRED");
                var difference=(original!.CountedQty-original.ExpectedQty)*original.ConversionFactor;
                Output.Require(difference!=null&&Math.Abs(difference.Value)==qty,"ADJUSTMENT_MUST_MATCH_COUNT");
                Output.Require(!await db.InventoryDocumentLines.AnyAsync(x=>x.SourceLineId==original.LineId&&db.StockMovements.Any(m=>m.LineId==x.LineId),ct),"COUNT_ALREADY_ADJUSTED");
                Output.Require(!pending.Any(m=>lines.Any(previous=>previous.LineId==m.LineId&&previous.SourceLineId==l.SourceLineId)),"COUNT_ALREADY_ADJUSTED");
                var current=await db.StockMovements.Where(m=>m.WarehouseId==warehouse&&m.ItemId==l.ItemId).SumAsync(m=>(decimal?)m.SignedBaseQty,ct)??0;
                Output.Require(current==original.ExpectedQty*original.ConversionFactor,"STOCK_CHANGED_RECOUNT_REQUIRED");sign=difference>0?1:-1;
            }
            if(d.Kind=="REVERSAL"){
                var movement=await db.StockMovements.SingleOrDefaultAsync(m=>m.LineId==original!.LineId&&m.WarehouseId==warehouse,ct)??throw new ApiException(409,"ORIGINAL_MOVEMENT_REQUIRED");
                Output.Require(movement.ReversalOfMovementId==null&&Math.Abs(movement.SignedBaseQty)==qty,"EXACT_REVERSAL_REQUIRED");
                sign=movement.SignedBaseQty>0?-1:1;reverse=movement.MovementId;
            }
            pending.Add(new StockMovement{BranchId=d.BranchId,LineId=l.LineId,WarehouseId=warehouse!.Value,ItemId=l.ItemId,SignedBaseQty=sign*qty,UnitCost=l.UnitCost,ReversalOfMovementId=reverse,CreatedBy=actor.Id,OccurredAt=clock.GetUtcNow().UtcDateTime});
        }
        foreach(var group in pending.GroupBy(m=>new{m.WarehouseId,m.ItemId})){
            var balance=await db.StockMovements.Where(m=>m.WarehouseId==group.Key.WarehouseId&&m.ItemId==group.Key.ItemId).SumAsync(m=>(decimal?)m.SignedBaseQty,ct)??0;
            Output.Require(balance+group.Sum(m=>m.SignedBaseQty)>=0,"STOCK_INSUFFICIENT");
        }
        d.PostedBy=actor.Id;d.PostedAt=clock.GetUtcNow().UtcDateTime;d.Status=d.Kind=="TRANSFER"?"IN_TRANSIT":"POSTED";
        await db.SaveChangesAsync(ct);
        foreach(var movement in pending){db.StockMovements.Add(movement);await db.SaveChangesAsync(ct);await new StockBatchAccounting(db,clock).Post(d,movement,ct);}
        var fulfillmentSource=source;
        if(d.Kind=="REVERSAL"&&source?.SourceDocumentId!=null)
            fulfillmentSource=await db.InventoryDocuments.SingleAsync(x=>x.DocumentId==source.SourceDocumentId,ct);
        if(fulfillmentSource?.Kind=="MATERIAL_REQUEST"&&fulfillmentSource.ClosedAt==null&&fulfillmentSource.Status!="REMAINDER_CLOSED"){
            var remaining=await db.VMaterialFulfillments.AnyAsync(f=>f.DocumentId==fulfillmentSource.DocumentId&&f.MissingBaseQty>0,ct);
            var any=await db.VMaterialFulfillments.AnyAsync(f=>f.DocumentId==fulfillmentSource.DocumentId&&f.IssuedBaseQty>0,ct);
            fulfillmentSource.Status=remaining?(any?"PARTIALLY_ISSUED":"APPROVED"):"FULLY_ISSUED";
        }
        if(fulfillmentSource?.Kind=="REPLENISHMENT"&&fulfillmentSource.ClosedAt==null&&fulfillmentSource.Status!="CLOSED"){
            var originals=await db.InventoryDocumentLines.Where(l=>l.DocumentId==fulfillmentSource.DocumentId).ToArrayAsync(ct);var full=true;var any=false;
            foreach(var line in originals){var received=await (from m in db.StockMovements join l in db.InventoryDocumentLines on m.LineId equals l.LineId where l.SourceLineId==line.LineId&&!db.StockMovements.Any(x=>x.ReversalOfMovementId==m.MovementId) select (decimal?)m.SignedBaseQty).SumAsync(ct)??0;full&=received>=line.ActualQty*line.ConversionFactor;any|=received>0;}
            fulfillmentSource.Status=full?"FULLY_RECEIVED":any?"PARTIALLY_RECEIVED":"REQUESTED";
        }
        // Shipment/receipt changes pending quantities at both ends even though only one
        // warehouse ledger moves. Refresh both branches inside the posting transaction.
        var alertBranches=new HashSet<long>{d.BranchId};
        if(d.Kind=="TRANSFER"&&d.DestinationBranchId is long destinationBranch)alertBranches.Add(destinationBranch);
        if(d.Kind=="TRANSFER_RECEIPT"&&source!=null)alertBranches.Add(source.BranchId);
        var lowStock=new LowStockService(db,notifications,clock,configuration);
        foreach(var branch in alertBranches.Order())await lowStock.Reconcile(ct,branch);
    }
    private async Task ApprovalAuthority(InventoryDocument d,CancellationToken ct)
    {
        Output.Require(d.CreatedBy!=actor.Id,"INDEPENDENT_APPROVER_REQUIRED",403);
        // A storekeeper may approve and issue, but cannot approve their own request.
        await access.RequireRolePermission("STOREKEEPER","MR_APPROVE_L1",d.BranchId,ct);
        Output.Require(d.DestinationBranchId==null||d.DestinationBranchId==d.BranchId,"USE_TRANSFER_REQUEST_FOR_OTHER_BRANCH",400);
    }
    private async Task Event(InventoryDocument d,string action,Guid key,string? reason,CancellationToken ct){
        var revision=(await db.DocumentEvents.Where(e=>e.DocumentId==d.DocumentId).MaxAsync(e=>(int?)e.RevisionNo,ct)??0)+1;
        db.DocumentEvents.Add(new DocumentEvent{BranchId=d.BranchId,DocumentId=d.DocumentId,EventType=action,RevisionNo=revision,ActorUserId=actor.Id,OperationKey=key,Reason=reason,SnapshotJson=System.Text.Json.JsonSerializer.Serialize(Output.Record(d))});
        audit.Add("InventoryDocument",d.DocumentId,action,d.BranchId,key:key);await db.SaveChangesAsync(ct);
    }
}
