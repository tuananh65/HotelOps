using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;
public sealed partial class DocumentService
{
    internal async Task<long> CreateRoomRemediation(WorkItem work,RoomIssueActionRequest request,CancellationToken ct)
    {
        Output.Require(db.Database.CurrentTransaction!=null,"WORKFLOW_TRANSACTION_REQUIRED");
        await access.RequireRolePermission("HOUSEKEEPING_SUPERVISOR","IR_PROCESS",work.BranchId,ct);
        Output.Require(work.Kind=="INCIDENT"&&work.RoomId!=null&&work.Status=="IN_PROGRESS","ROOM_ISSUE_WRONG_STAGE");
        Output.Require(request.ItemId!=null&&request.UnitId!=null&&request.WarehouseId!=null&&request.Quantity>0&&SupplyMath.ValidQuantity(request.Quantity.Value),"ROOM_ISSUE_MATERIAL_REQUIRED",400);
        var item=await db.InventoryItems.SingleOrDefaultAsync(i=>i.ItemId==request.ItemId&&i.IsActive,ct)??throw new ApiException(409,"ITEM_UNAVAILABLE");
        Output.Require(item.BaseUnitId==request.UnitId&&await db.ReferenceValues.AnyAsync(u=>u.ReferenceId==request.UnitId&&u.Kind=="UOM"&&u.IsActive,ct),"BASE_UNIT_REQUIRED",400);
        Output.Require(await db.Warehouses.AnyAsync(w=>w.WarehouseId==request.WarehouseId&&w.BranchId==work.BranchId&&w.IsActive,ct),"WAREHOUSE_UNAVAILABLE");
        Output.Require(await db.SupplyConfigurations.AnyAsync(c=>c.BranchId==work.BranchId&&c.ItemId==item.ItemId&&c.SupplyGroup=="B"&&c.IsCurrent&&c.IsActive,ct),"DURABLE_SUPPLY_CONFIGURATION_REQUIRED");
        var department=await db.Departments.Where(d=>d.BranchId==work.BranchId&&d.Code=="HK"&&d.IsActive).Select(d=>(long?)d.DepartmentId).SingleOrDefaultAsync(ct);
        Output.Require(department!=null,"HOUSEKEEPING_DEPARTMENT_REQUIRED");
        var document=new InventoryDocument{BranchId=work.BranchId,DocumentNo=Guid.NewGuid().ToString("N"),Kind="MATERIAL_REQUEST",Status="DRAFT",DepartmentId=department,
            SourceWarehouseId=request.WarehouseId,SourceWorkId=work.WorkId,ReceivingRoomId=work.RoomId,CreatedBy=actor.Id,CreatedAt=clock.GetUtcNow().UtcDateTime,Reason=request.Note};
        db.InventoryDocuments.Add(document);await db.SaveChangesAsync(ct);
        db.InventoryDocumentLines.Add(new InventoryDocumentLine{DocumentId=document.DocumentId,BranchId=work.BranchId,LineNo=1,ItemId=item.ItemId,UnitId=item.BaseUnitId,
            ConversionFactor=1,ActualQty=request.Quantity!.Value,RequestedQty=request.Quantity.Value});
        await db.SaveChangesAsync(ct);
        await AuthorizeRoomRemediation(document.DocumentId,ct);
        return document.DocumentId;
    }
    // Called only inside the room-issue transaction, never exposed as a document action.
    internal async Task AuthorizeRoomRemediation(long documentId,CancellationToken ct)
    {
        var d=await db.InventoryDocuments.SingleAsync(x=>x.DocumentId==documentId,ct);
        await access.RequireRolePermission("HOUSEKEEPING_SUPERVISOR","IR_PROCESS",d.BranchId,ct);
        Output.Require(d.Kind=="MATERIAL_REQUEST"&&d.Status=="DRAFT"&&d.CreatedBy==actor.Id&&d.ReceivingRoomId!=null,"ROOM_ISSUE_WRONG_STAGE");
        var work=await db.WorkItems.SingleAsync(w=>w.WorkId==d.SourceWorkId,ct);
        var stage=await db.WorkEvents.Where(e=>e.WorkId==work.WorkId&&e.EventType.StartsWith("ROOM_ISSUE_"))
            .OrderByDescending(e=>e.WorkEventId).Select(e=>e.EventType).FirstOrDefaultAsync(ct);
        Output.Require(work.Kind=="INCIDENT"&&work.BranchId==d.BranchId&&work.RoomId==d.ReceivingRoomId
            &&stage is "ROOM_ISSUE_APPROVED" or "ROOM_ISSUE_MATERIAL_REQUESTED" or "ROOM_ISSUE_INSPECTION_FAILED","ROOM_ISSUE_WRONG_STAGE");
        var report=await db.WorkEvents.SingleAsync(e=>e.WorkId==work.WorkId&&e.EventType=="ROOM_ISSUE_REPORTED",ct);
        using var info=System.Text.Json.JsonDocument.Parse(report.SnapshotJson!);
        Output.Require(info.RootElement.GetProperty("Kind").GetString()=="MISSING","ROOM_ISSUE_WRONG_STAGE");
        var lines=await db.InventoryDocumentLines.Where(l=>l.DocumentId==documentId).ToArrayAsync(ct);
        Output.Require(lines.Length>0&&lines.All(l=>l.RequestedQty>0),"DOCUMENT_LINES_REQUIRED");
        // Persist authorization evidence before the guarded approval update, in the same transaction.
        await Event(d,"ROOM_REMEDIATION_AUTHORIZED",Guid.NewGuid(),"Giám sát buồng phòng xác nhận thiếu vật dụng; chuyển kho tiếp nhận",ct);
        foreach(var line in lines)line.ApprovedQty=line.RequestedQty;
        d.ApprovedBy=actor.Id;d.ApprovedAt=clock.GetUtcNow().UtcDateTime;d.Status="APPROVED";
        await db.SaveChangesAsync(ct);
        await Event(d,"ROOM_REMEDIATION_APPROVED",Guid.NewGuid(),null,ct);
    }
}
