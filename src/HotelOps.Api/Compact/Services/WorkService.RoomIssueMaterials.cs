using System.Text.Json;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;
public sealed partial class WorkService
{
    private async Task<long> CurrentIssueMaterial(long workId,CancellationToken ct)
    {
        var snapshot=await db.WorkEvents.Where(e=>e.WorkId==workId&&e.EventType=="ROOM_ISSUE_MATERIAL_REQUESTED")
            .OrderByDescending(e=>e.WorkEventId).Select(e=>e.SnapshotJson).FirstAsync(ct);
        using var json=JsonDocument.Parse(snapshot!);
        return json.RootElement.GetProperty("DocumentId").GetInt64();
    }
    private async Task<bool> CanReplaceMaterial(long workId,CancellationToken ct)
    {
        var id=await CurrentIssueMaterial(workId,ct);
        var status=await (from d in db.InventoryDocuments.AsNoTracking() join w in db.WorkItems on d.SourceWorkId equals w.WorkId
            where d.DocumentId==id&&w.WorkId==workId&&d.Kind=="MATERIAL_REQUEST"&&d.BranchId==w.BranchId&&d.ReceivingRoomId==w.RoomId
            select d.Status).SingleOrDefaultAsync(ct);
        if(status is "REJECTED" or "CANCELLED" or "REMAINDER_CLOSED")return true;
        if(status!="FULLY_ISSUED")return false;
        // A missing ledger entry is not a return. Require a real, unreversed posted return
        // against an issue line from this specific request before reopening its shortage.
        var returned=await (from requested in db.InventoryDocumentLines.AsNoTracking()
            join issued in db.InventoryDocumentLines on requested.LineId equals issued.SourceLineId
            join issue in db.InventoryDocuments on issued.DocumentId equals issue.DocumentId
            join returnedLine in db.InventoryDocumentLines on issued.LineId equals returnedLine.SourceLineId
            join returnedDocument in db.InventoryDocuments on returnedLine.DocumentId equals returnedDocument.DocumentId
            join movement in db.StockMovements on returnedLine.LineId equals movement.LineId
            where requested.DocumentId==id&&issue.Kind=="ISSUE"&&issue.PostedAt!=null
                &&returnedDocument.Kind=="RETURN"&&returnedDocument.PostedAt!=null&&returnedDocument.BranchId==issue.BranchId
                &&movement.SignedBaseQty>0&&movement.ReversalOfMovementId==null
                &&!db.StockMovements.Any(r=>r.ReversalOfMovementId==movement.MovementId)
            select movement.MovementId).AnyAsync(ct);
        return returned&&!await MaterialDelivered(id,ct);
    }

    private async Task ReclaimRoomMaterialRequest(WorkItem work,CancellationToken ct)
    {
        await RequireRoomIssueSupervisor(work.BranchId,ct);
        await WorkdayAuthority.Require(db,access,actor,clock,work.BranchId,"HOUSEKEEPING_SUPERVISOR",ct);
        await StaffSupplyService.Lock(db,ct);
        Output.Require(await CanReplaceMaterial(work.WorkId,ct),"ROOM_ISSUE_MATERIAL_NOT_REPLACEABLE");
        var flow=await db.RequestWorkflows.SingleOrDefaultAsync(r=>r.WorkId==work.WorkId,ct)
            ??throw new ApiException(409,"REQUEST_WORKFLOW_NOT_FOUND");
        Output.Require(flow.BranchId==work.BranchId&&flow.ParentRequestWorkId==null&&flow.RequestType=="ROOM_DURABLE_ITEM_MISSING",
            "REQUEST_QUEUE_ROLE_REQUIRED",403);
        var queue=await db.Roles.Where(r=>r.RoleId==flow.QueueRoleId&&r.IsActive).Select(r=>r.Code).SingleOrDefaultAsync(ct);
        if(queue=="HOUSEKEEPING_SUPERVISOR")return;
        Output.Require(queue=="STOREKEEPER"&&flow.Status is "PENDING_STOREKEEPER" or "ACCEPTED" or "IN_PROGRESS" or "REWORK_REQUIRED",
            "REQUEST_QUEUE_ROLE_REQUIRED",403);
        await requests.RecordDomainTransition(work,"ROOM_DURABLE_ITEM_MISSING","PENDING_SUPERVISOR","HOUSEKEEPING_SUPERVISOR",
            "material-request-reclaimed","HOUSEKEEPING_SUPERVISOR",Guid.NewGuid(),
            "Tiếp nhận lại phần thiếu sau khi phiếu cấp vật tư đã đóng hoặc có hoàn trả thực tế.",ct,documentId:await CurrentIssueMaterial(work.WorkId,ct));
    }
    private async Task<bool> MaterialDelivered(long id,CancellationToken ct)
    {
        if(!await db.InventoryDocuments.AnyAsync(d=>d.DocumentId==id&&d.Kind=="MATERIAL_REQUEST"&&d.Status=="FULLY_ISSUED",ct))return false;
        var requested=await db.InventoryDocumentLines.AsNoTracking().Where(l=>l.DocumentId==id).Select(l=>new{l.LineId,Qty=l.ActualQty*l.ConversionFactor}).ToArrayAsync(ct);
        if(requested.Length==0)return false;
        var ids=requested.Select(l=>l.LineId).ToArray();
        var deliveries=await (from l in db.InventoryDocumentLines.AsNoTracking() join d in db.InventoryDocuments on l.DocumentId equals d.DocumentId join m in db.StockMovements on l.LineId equals m.LineId
            where l.SourceLineId!=null&&ids.Contains(l.SourceLineId.Value)&&d.Kind=="ISSUE"&&d.PostedAt!=null&&m.SignedBaseQty<0&&!db.StockMovements.Any(r=>r.ReversalOfMovementId==m.MovementId)
            select new{l.LineId,l.SourceLineId,Qty=-m.SignedBaseQty}).ToArrayAsync(ct);
        var deliveredIds=deliveries.Select(l=>l.LineId).ToArray();
        var returns=await (from l in db.InventoryDocumentLines.AsNoTracking() join d in db.InventoryDocuments on l.DocumentId equals d.DocumentId join m in db.StockMovements on l.LineId equals m.LineId
            where l.SourceLineId!=null&&deliveredIds.Contains(l.SourceLineId.Value)&&d.Kind=="RETURN"&&d.PostedAt!=null&&!db.StockMovements.Any(r=>r.ReversalOfMovementId==m.MovementId)
            select new{l.SourceLineId,m.SignedBaseQty}).ToArrayAsync(ct);
        return requested.All(r=>deliveries.Where(d=>d.SourceLineId==r.LineId).Sum(d=>d.Qty-returns.Where(t=>t.SourceLineId==d.LineId).Sum(t=>t.SignedBaseQty))>=r.Qty);
    }
}
