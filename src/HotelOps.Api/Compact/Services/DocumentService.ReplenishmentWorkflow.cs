using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class DocumentService
{
    internal async Task<long> CreateWorkflowReplenishment(RequestWorkflow flow,WorkItem root,RequestWorkflowLine[] items,Guid key,CancellationToken ct)
    {
        Output.Require(db.Database.CurrentTransaction!=null&&flow.RequestType==RequestWorkflowService.ReplenishmentType&&flow.WorkId==root.WorkId&&flow.WarehouseId!=null,"REPLENISHMENT_WORKFLOW_REQUIRED");
        await access.RequireRolePermission("STOREKEEPER","REPLENISHMENT_CREATE",flow.BranchId,ct);
        Output.Require(items.Length>0&&items.All(l=>l.WorkId==root.WorkId),"REQUEST_LINES_REQUIRED");
        var document=new InventoryDocument{BranchId=flow.BranchId,DocumentNo=Guid.NewGuid().ToString("N"),Kind="REPLENISHMENT",Status="REQUESTED",DestinationWarehouseId=flow.WarehouseId,DestinationBranchId=flow.BranchId,SourceWorkId=root.WorkId,CreatedBy=actor.Id,CreatedAt=clock.GetUtcNow().UtcDateTime,Reason=root.Description};
        db.InventoryDocuments.Add(document);await db.SaveChangesAsync(ct);var number=0;
        foreach(var item in items){
            var line=new InventoryDocumentLine{BranchId=flow.BranchId,DocumentId=document.DocumentId,LineNo=++number,ItemId=item.ItemId,UnitId=item.UnitId,UnitKind="UOM",ConversionFactor=1,ActualQty=item.RequestedQuantity,RequestedQty=item.RequestedQuantity};
            db.InventoryDocumentLines.Add(line);await db.SaveChangesAsync(ct);item.DocumentLineId=line.LineId;
        }
        await Event(document,"WORKFLOW_REQUESTED",key,root.Description,ct);return document.DocumentId;
    }

    internal async Task ApproveWorkflowReplenishment(RequestWorkflow flow,RequestWorkflowLine[] items,Guid key,CancellationToken ct)
    {
        Output.Require(db.Database.CurrentTransaction!=null&&flow.RequestType==RequestWorkflowService.ReplenishmentType&&flow.Status=="PENDING_OPERATIONS_MANAGER"&&flow.DocumentId!=null,"REPLENISHMENT_WORKFLOW_REQUIRED");
        await access.RequireRolePermission("OPERATION_MANAGER","REPLENISHMENT_APPROVE",flow.BranchId,ct);
        var document=await db.InventoryDocuments.SingleAsync(d=>d.DocumentId==flow.DocumentId&&d.BranchId==flow.BranchId,ct);
        Output.Require(document.Kind=="REPLENISHMENT"&&document.Status=="REQUESTED"&&document.ApprovedBy==null&&document.ClosedAt==null&&document.CreatedBy!=actor.Id,"INDEPENDENT_REPLENISHMENT_APPROVAL_REQUIRED");
        var lines=await db.InventoryDocumentLines.Where(l=>l.DocumentId==document.DocumentId).ToArrayAsync(ct);
        Output.Require(lines.Length==items.Length&&items.All(i=>i.ApprovedQuantity>0&&i.ApprovedQuantity<=i.RequestedQuantity),"APPROVED_LINES_REQUIRED");
        foreach(var item in items){var line=lines.Single(l=>l.LineId==item.DocumentLineId&&l.ItemId==item.ItemId);line.ApprovedQty=item.ApprovedQuantity;line.ActualQty=item.ApprovedQuantity!.Value;}
        document.ApprovedBy=actor.Id;document.ApprovedAt=clock.GetUtcNow().UtcDateTime;
        await Event(document,"WORKFLOW_APPROVED",key,null,ct);
    }

    internal async Task<long> ReceiveWorkflowReplenishment(RequestWorkflow flow,ReceiveReplenishmentWorkflowRequest input,CancellationToken ct)
    {
        Output.Require(db.Database.CurrentTransaction!=null&&flow.RequestType==RequestWorkflowService.ReplenishmentType&&flow.Status=="PENDING_RECEIPT"&&flow.DocumentId!=null&&flow.WarehouseId!=null,"APPROVED_REPLENISHMENT_REQUIRED");
        await access.RequireRolePermission("STOREKEEPER","REPLENISHMENT_RECEIVE",flow.BranchId,ct);
        await access.RequireRolePermission("STOREKEEPER","STOCK_RECEIPT_POST",flow.BranchId,ct);
        var source=await db.InventoryDocuments.SingleAsync(d=>d.DocumentId==flow.DocumentId&&d.BranchId==flow.BranchId,ct);
        Output.Require(source.Kind=="REPLENISHMENT"&&source.ApprovedBy!=null&&source.ClosedAt==null&&source.Status is "REQUESTED" or "PARTIALLY_RECEIVED","APPROVED_REPLENISHMENT_REQUIRED");
        Output.Require(source.DestinationWarehouseId==flow.WarehouseId&&source.DestinationBranchId==flow.BranchId,"WAREHOUSE_SCOPE_MISMATCH");
        var originals=await db.InventoryDocumentLines.Where(l=>l.DocumentId==source.DocumentId).ToArrayAsync(ct);
        Output.Require(input.Items.Count>0&&input.Items.Select(i=>i.ItemId).Distinct().Count()==input.Items.Count&&input.Items.All(i=>i.Quantity>0&&SupplyMath.ValidQuantity(i.Quantity)&&originals.Any(l=>l.ItemId==i.ItemId)),"INVALID_RECEIPT_LINES",400);
        Output.Require(input.SupplierName==null||input.SupplierName.Length<=200,"SUPPLIER_NAME_TOO_LONG",400);
        var receipt=new InventoryDocument{BranchId=flow.BranchId,DocumentNo=Guid.NewGuid().ToString("N"),Kind="RECEIPT",Status="DRAFT",SourceDocumentId=source.DocumentId,SourceWorkId=flow.WorkId,DestinationWarehouseId=flow.WarehouseId,DestinationBranchId=flow.BranchId,CreatedBy=actor.Id,CreatedAt=clock.GetUtcNow().UtcDateTime,SupplierName=input.SupplierName,Reason=input.Note};
        db.InventoryDocuments.Add(receipt);await db.SaveChangesAsync(ct);var number=0;
        var lines=new List<InventoryDocumentLine>();
        foreach(var item in input.Items){var original=originals.Single(l=>l.ItemId==item.ItemId);var line=new InventoryDocumentLine{BranchId=flow.BranchId,DocumentId=receipt.DocumentId,LineNo=++number,ItemId=item.ItemId,UnitId=original.UnitId,UnitKind="UOM",ConversionFactor=1,ActualQty=item.Quantity,SourceLineId=original.LineId};db.InventoryDocumentLines.Add(line);lines.Add(line);}
        await db.SaveChangesAsync(ct);
        // Reuse the existing ledger writer: positive quantities, warehouse, remaining amount,
        // posting immutability, partial receipts and stock concurrency remain enforced there.
        await Post(receipt,lines.ToArray(),new ActionRequest(Convert.ToBase64String(receipt.RowVersion),input.OperationKey,input.Note),ct);
        await Event(receipt,"WORKFLOW_RECEIVED",input.OperationKey,input.Note,ct);return receipt.DocumentId;
    }

    // Parent wires this into generic document Load/Line/Act/Derive/Post paths. Typed methods above
    // intentionally use their private writer directly after the narrower workflow authorization.
    internal async Task GuardWorkflowDocumentMutation(InventoryDocument document,CancellationToken ct)
    {
        var flow=document.SourceWorkId==null?null:await db.RequestWorkflows.SingleOrDefaultAsync(r=>r.WorkId==document.SourceWorkId&&r.ParentRequestWorkId==null,ct);
        flow??=await db.RequestWorkflows.SingleOrDefaultAsync(r=>r.ParentRequestWorkId==null&&r.DocumentId!=null&&(r.DocumentId==document.DocumentId
            ||document.SourceDocumentId!=null&&r.DocumentId==document.SourceDocumentId),ct);
        if(flow==null)return;
        // Room replacement uses the existing authorized material issue UI. Refill and
        // warehouse replenishment keep their dedicated writers and cannot use this path.
        Output.Require(flow.RequestType=="ROOM_DURABLE_ITEM_MISSING"&&flow.BranchId==document.BranchId
            &&flow.Status is "PENDING_STOREKEEPER" or "ACCEPTED" or "IN_PROGRESS"
            &&document.Kind is "MATERIAL_REQUEST" or "ISSUE","USE_REQUEST_WORKFLOW",403);
        await access.RequireRolePermission("STOREKEEPER","MR_FULFILL",flow.BranchId,ct);
        await WorkdayAuthority.Require(db,access,actor,clock,flow.BranchId,"STOREKEEPER",ct);
        Output.Require(flow.HandlerUserId==null||flow.HandlerUserId==actor.Id,"REQUEST_HANDLED_BY_ANOTHER_USER",403);
        Output.Require(await db.Roles.AnyAsync(r=>r.RoleId==flow.QueueRoleId&&r.Code=="STOREKEEPER"&&r.IsActive,ct),"REQUEST_WRONG_HANDLER",403);
        var sourceId=document.Kind=="MATERIAL_REQUEST"?document.DocumentId:document.SourceDocumentId;
        var source=await db.InventoryDocuments.AsNoTracking().SingleOrDefaultAsync(d=>d.DocumentId==sourceId,ct)
            ??throw new ApiException(409,"ROOM_ISSUE_MATERIAL_NOT_FULFILLABLE");
        Output.Require(source.Kind=="MATERIAL_REQUEST"&&source.SourceWorkId==flow.WorkId&&source.Status is "APPROVED" or "PARTIALLY_ISSUED"
            &&source.ReceivingRoomId!=null&&document.ReceivingRoomId==source.ReceivingRoomId,"ROOM_ISSUE_MATERIAL_NOT_FULFILLABLE");
    }
}
