using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class DocumentService
{
    private static bool TransferKind(string kind) => kind is "TRANSFER_REQUEST" or "TRANSFER" or "TRANSFER_RECEIPT";
    private static bool CrossBranch(InventoryDocument d) => d.DestinationBranchId is long destination && destination != d.BranchId;

    // The only cross-branch read exception: related transfer documents, never arbitrary stock ledgers.
    // NotificationService can call this static helper without a circular DI dependency.
    public static async Task<bool> CanReadTransfer(HotelOpsCompactDbContext db,Access access,InventoryDocument d,CancellationToken ct)
    {
        if(!TransferKind(d.Kind))return false;
        var source=d.Kind=="TRANSFER_RECEIPT"&&d.SourceDocumentId!=null
            ? await db.InventoryDocuments.AsNoTracking().SingleOrDefaultAsync(x=>x.DocumentId==d.SourceDocumentId&&x.Kind=="TRANSFER",ct):d;
        if(source==null)return false;
        foreach(var branch in new[]{source.BranchId,source.DestinationBranchId??source.BranchId}.Distinct())
            if(await access.Allows("INVENTORY_VIEW",branch,ct:ct)
                &&(await access.HasRole("OPERATION_MANAGER",branch,ct)||await access.HasRole("STOREKEEPER",branch,ct)))return true;
        return false;
    }

    private async Task TransferAuthority(InventoryDocument d,string action,CancellationToken ct)
    {
        if(action=="read") { Output.Require(await CanReadTransfer(db,access,d,ct),"PERMISSION_DENIED",403);return; }
        if(d.Kind=="TRANSFER_REQUEST"){
            var approval=action is "approve" or "reject";
            var branch=approval?d.BranchId:d.DestinationBranchId??d.BranchId;
            await access.RequireRolePermission("OPERATION_MANAGER",approval?"TRANSFER_REQUEST_APPROVE":"TRANSFER_REQUEST_CREATE",branch,ct);
            Output.Require(approval?d.CreatedBy!=actor.Id:d.CreatedBy==actor.Id,approval?"INDEPENDENT_APPROVER_REQUIRED":"DOCUMENT_OWNER_REQUIRED",403);
            Output.Require(action is "create" or "submit" or "cancel" or "approve" or "reject","INVALID_DOCUMENT_TRANSITION",400);
        }else await access.RequireRolePermission("STOREKEEPER","STOCK_TRANSFER_POST",d.BranchId,ct);
    }

    public async Task<object> TransferCatalog(long branchId,CancellationToken ct)
    {
        await access.RequireRolePermission("OPERATION_MANAGER","TRANSFER_REQUEST_CREATE",branchId,ct);
        // Catalog deliberately exposes only names, item units and available quantities needed to request a transfer.
        var warehouses=await (from w in db.Warehouses.AsNoTracking() join b in db.Branches on w.BranchId equals b.BranchId
            where w.IsActive&&b.IsActive select new{w.WarehouseId,w.BranchId,w.Name,branchName=b.Name}).ToArrayAsync(ct);
        var ids=warehouses.Select(w=>w.WarehouseId).ToArray();
        var items=await db.InventoryItems.AsNoTracking().Where(i=>i.IsActive).Select(i=>new{i.ItemId,i.Code,i.Name,i.BaseUnitId}).ToArrayAsync(ct);
        var itemIds=items.Select(i=>i.ItemId).ToArray();
        var balances=await db.StockMovements.AsNoTracking().Where(m=>ids.Contains(m.WarehouseId)&&itemIds.Contains(m.ItemId))
            .GroupBy(m=>new{m.WarehouseId,m.ItemId}).Select(g=>new{g.Key.WarehouseId,g.Key.ItemId,availableBaseQty=g.Sum(m=>m.SignedBaseQty)}).ToArrayAsync(ct);
        var units=await db.ReferenceValues.AsNoTracking().Where(u=>u.Kind=="UOM"&&u.IsActive)
            .Select(u=>new{u.ReferenceId,u.Name,u.BaseUnitId,u.ConversionFactor}).ToArrayAsync(ct);
        return new{warehouses,items,units,balances};
    }

    private async Task ValidateTransferCreate(CreateDocumentRequest r,CancellationToken ct)
    {
        var destination=r.DestinationBranchId??r.BranchId;
        if(r.Kind=="TRANSFER_REQUEST"){
            await access.RequireRolePermission("OPERATION_MANAGER","TRANSFER_REQUEST_CREATE",destination,ct);
            Output.Require(destination!=r.BranchId&&r.SourceWarehouseId!=null&&r.DestinationWarehouseId!=null,"CROSS_BRANCH_WAREHOUSES_REQUIRED",400);
            Output.Require(r.DepartmentId==null&&r.SourceWorkId==null&&r.SourceDocumentId==null,"TRANSFER_REQUEST_LINKS_INVALID",400);
        }else{
            await access.RequireRolePermission("STOREKEEPER","STOCK_TRANSFER_POST",r.BranchId,ct);
            if(r.Kind=="TRANSFER"&&destination!=r.BranchId){
                var source=await db.InventoryDocuments.SingleOrDefaultAsync(x=>x.DocumentId==r.SourceDocumentId,ct);
                Output.Require(source!=null&&source.Kind=="TRANSFER_REQUEST"&&source.Status=="APPROVED"&&source.ApprovedBy!=null&&source.ClosedAt==null
                    &&source.BranchId==r.BranchId&&source.DestinationBranchId==destination&&source.SourceWarehouseId==r.SourceWarehouseId&&source.DestinationWarehouseId==r.DestinationWarehouseId,"APPROVED_TRANSFER_REQUEST_REQUIRED");
            }
            if(r.Kind=="TRANSFER_RECEIPT"){
                var source=await db.InventoryDocuments.SingleOrDefaultAsync(x=>x.DocumentId==r.SourceDocumentId,ct);
                Output.Require(source!=null&&source.Kind=="TRANSFER"&&source.PostedAt!=null&&source.DestinationBranchId==r.BranchId&&source.DestinationWarehouseId==r.DestinationWarehouseId
                    &&r.SourceWarehouseId==null&&destination==r.BranchId,"TRANSFER_DESTINATION_MISMATCH");
            }
        }
        Output.Require(await db.Branches.AnyAsync(b=>b.BranchId==r.BranchId&&b.IsActive,ct)
            &&await db.Branches.AnyAsync(b=>b.BranchId==destination&&b.IsActive,ct),"BRANCH_UNAVAILABLE");
    }

    private async Task ValidateTransferLines(InventoryDocument d,InventoryDocumentLine[] lines,CancellationToken ct)
    {
        Output.Require(await db.Branches.AnyAsync(b=>b.BranchId==d.BranchId&&b.IsActive,ct)
            &&await db.Branches.AnyAsync(b=>b.BranchId==d.DestinationBranchId&&b.IsActive,ct),"BRANCH_UNAVAILABLE");
        Output.Require(d.SourceWarehouseId!=null&&d.DestinationWarehouseId!=null
            &&await db.Warehouses.AnyAsync(w=>w.WarehouseId==d.SourceWarehouseId&&w.BranchId==d.BranchId&&w.IsActive,ct)
            &&await db.Warehouses.AnyAsync(w=>w.WarehouseId==d.DestinationWarehouseId&&w.BranchId==d.DestinationBranchId&&w.IsActive,ct),"WAREHOUSE_UNAVAILABLE");
        foreach(var line in lines)await ValidateTransferUnit(line,ct);
    }

    private async Task ValidateTransferApprovalStock(InventoryDocument d,InventoryDocumentLine[] lines,CancellationToken ct)
    {
        foreach(var group in lines.GroupBy(l=>l.ItemId)){
            var available=await db.StockMovements.Where(m=>m.WarehouseId==d.SourceWarehouseId&&m.ItemId==group.Key).SumAsync(m=>(decimal?)m.SignedBaseQty,ct)??0;
            Output.Require(group.Sum(l=>(l.ApprovedQty??0)*l.ConversionFactor)<=available,"STOCK_INSUFFICIENT");
        }
    }

    private async Task<bool> CrossBranchTransfer(InventoryDocument d,CancellationToken ct)
        => TransferKind(d.Kind)&&(CrossBranch(d)||d.Kind=="TRANSFER_RECEIPT"&&await db.InventoryDocuments.AnyAsync(x=>x.DocumentId==d.SourceDocumentId&&x.DestinationBranchId!=x.BranchId,ct));

    private async Task ValidateTransferUnit(InventoryDocumentLine line,CancellationToken ct)
    {
        var item=await db.InventoryItems.AsNoTracking().SingleOrDefaultAsync(i=>i.ItemId==line.ItemId&&i.IsActive,ct);
        var unit=await db.ReferenceValues.AsNoTracking().SingleOrDefaultAsync(u=>u.ReferenceId==line.UnitId&&u.Kind=="UOM"&&u.IsActive,ct);
        Output.Require(item!=null,"ITEM_INACTIVE");
        Output.Require(unit!=null&&(unit.ReferenceId==item!.BaseUnitId||unit.BaseUnitId==item.BaseUnitId)
            &&line.ConversionFactor==(unit.ReferenceId==item!.BaseUnitId?1:unit.ConversionFactor)&&line.ConversionFactor>0,"UNIT_MISMATCH");
    }

    private async Task<decimal> TransferRemaining(InventoryDocumentLine line,CancellationToken ct)
    {
        var dispatched=await (from m in db.StockMovements join l in db.InventoryDocumentLines on m.LineId equals l.LineId
            join d in db.InventoryDocuments on l.DocumentId equals d.DocumentId
            where l.SourceLineId==line.LineId&&d.Kind=="TRANSFER" select (decimal?)m.SignedBaseQty).SumAsync(ct)??0;
        return Math.Max(0,(line.ApprovedQty??0)+dispatched/line.ConversionFactor);
    }

    private async Task ValidateTransferPost(InventoryDocument d,InventoryDocument? source,InventoryDocumentLine[] lines,CancellationToken ct)
    {
        await access.RequireRolePermission("STOREKEEPER","STOCK_TRANSFER_POST",d.BranchId,ct);
        foreach(var line in lines)await ValidateTransferUnit(line,ct);
        if(d.Kind=="TRANSFER"){
            await ValidateTransferLines(d,lines,ct);
            if(CrossBranch(d)){
                Output.Require(source!=null&&source.Kind=="TRANSFER_REQUEST"&&source.Status=="APPROVED"&&source.ClosedAt==null&&source.ApprovedBy!=null
                    &&source.BranchId==d.BranchId&&source.DestinationBranchId==d.DestinationBranchId&&source.SourceWarehouseId==d.SourceWarehouseId&&source.DestinationWarehouseId==d.DestinationWarehouseId,"APPROVED_TRANSFER_REQUEST_REQUIRED");
                foreach(var group in lines.GroupBy(l=>l.SourceLineId)){
                    var original=await db.InventoryDocumentLines.SingleOrDefaultAsync(l=>l.LineId==group.Key&&l.DocumentId==source!.DocumentId,ct);
                    Output.Require(original!=null&&group.All(l=>l.ItemId==original.ItemId),"SOURCE_LINE_REQUIRED");
                    Output.Require(group.Sum(l=>l.ActualQty*l.ConversionFactor)<=await TransferRemaining(original!,ct)*original!.ConversionFactor,"SOURCE_QUANTITY_EXCEEDED");
                }
            }
        }else{
            Output.Require(source!=null&&source.Kind=="TRANSFER"&&source.PostedAt!=null&&source.DestinationBranchId==d.BranchId
                &&source.DestinationWarehouseId==d.DestinationWarehouseId,"TRANSFER_DESTINATION_MISMATCH");
            Output.Require(!await db.InventoryDocuments.AnyAsync(x=>x.SourceDocumentId==source!.DocumentId&&x.Kind=="REVERSAL"&&x.PostedAt!=null,ct),"TRANSFER_REVERSED");
        }
    }

    private async Task NotifyTransfer(InventoryDocument d,string action,Guid key,CancellationToken ct)
    {
        var root=d;
        if(d.Kind=="TRANSFER_RECEIPT")root=await db.InventoryDocuments.SingleAsync(x=>x.DocumentId==d.SourceDocumentId,ct);
        var sourceBranch=root.BranchId;var destination=root.DestinationBranchId??sourceBranch;
        var now=clock.GetUtcNow().UtcDateTime;
        // Keep notifications.branch_id equal to the linked document's branch: composite FK is preserved.
        // Receiver visibility is checked against either transfer participant, not the notification branch.
        var recipients=await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.RoleId
            join user in db.Users on ur.UserId equals user.UserId
            where (ur.BranchId==sourceBranch||ur.BranchId==destination)&&ur.AssignedAt<=now&&ur.RevokedAt==null&&(ur.ExpiresAt==null||ur.ExpiresAt>now)
                &&role.IsActive&&(role.Code=="OPERATION_MANAGER"||role.Code=="STOREKEEPER")&&user.AccountStatus=="ACTIVE"&&user.EmploymentStatus=="ACTIVE"&&user.DeletedAt==null
                &&db.Memberships.Any(m=>m.UserId==ur.UserId&&m.BranchId==ur.BranchId&&m.Kind=="BRANCH_ACCESS"&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now))
            select ur.UserId).Distinct().ToArrayAsync(ct);
        foreach(var recipient in recipients)notifications.Add(d.BranchId,recipient,"TRANSFER_"+action.ToUpperInvariant(),d.DocumentNo,null,d.DocumentId,key);
    }

    private async Task<object> TransferContext(InventoryDocument d,CancellationToken ct)
    {
        var read=await CanReadTransfer(db,access,d,ct);Output.Require(read,"PERMISSION_DENIED",403);
        var source=d.Kind=="TRANSFER_RECEIPT"?await db.InventoryDocuments.AsNoTracking().SingleAsync(x=>x.DocumentId==d.SourceDocumentId,ct):d;
        var canCreate=d.Kind=="TRANSFER_REQUEST"&&d.CreatedBy==actor.Id&&await access.HasRole("OPERATION_MANAGER",d.DestinationBranchId??d.BranchId,ct)
            &&await access.Allows("TRANSFER_REQUEST_CREATE",d.DestinationBranchId,ct:ct);
        var approve=d.Kind=="TRANSFER_REQUEST"&&d.Status=="PENDING_APPROVAL"&&d.CreatedBy!=actor.Id
            &&await access.HasRole("OPERATION_MANAGER",d.BranchId,ct)&&await access.Allows("TRANSFER_REQUEST_APPROVE",d.BranchId,ct:ct);
        var postedChild=await db.InventoryDocuments.AnyAsync(x=>x.SourceDocumentId==d.DocumentId&&x.PostedAt!=null,ct);
        var children=await db.InventoryDocuments.AsNoTracking().Where(x=>x.SourceDocumentId==d.DocumentId).OrderBy(x=>x.DocumentId).ToArrayAsync(ct);
        var statuses=await TransferStatusProjection.Read(db,children.Where(x=>x.Kind=="TRANSFER").Select(x=>x.DocumentId).ToArray(),ct);
        var workflow=d.Status;
        if(d.Kind=="TRANSFER_REQUEST"&&d.Status=="APPROVED"){
            var requestLines=await db.InventoryDocumentLines.AsNoTracking().Where(l=>l.DocumentId==d.DocumentId).ToArrayAsync(ct);
            var remaining=false;foreach(var line in requestLines)remaining|=await TransferRemaining(line,ct)>0;
            workflow=postedChild?(!remaining&&statuses.Count>0&&statuses.Values.All(s=>s=="RECEIVED")?"COMPLETED":"IN_TRANSIT")
                :children.Any(x=>x.Status=="DRAFT")?"PREPARING":"APPROVED";
        }
        return new{sourceBranchId=source.BranchId,destinationBranchId=source.DestinationBranchId,workflowStatus=workflow,
            canApprove=approve,canReject=approve,canSubmit=canCreate&&d.Status=="DRAFT",canEdit=canCreate&&d.Status=="DRAFT",
            canCancel=canCreate&&!postedChild&&d.Status is "DRAFT" or "PENDING_APPROVAL" or "APPROVED",
            children=children.Select(x=>TransferStatusProjection.Record(x,x.DocumentId,statuses)).ToArray()};
    }
}
