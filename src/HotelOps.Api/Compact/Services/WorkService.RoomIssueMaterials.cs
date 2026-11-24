using System.Text.Json;
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
        return await db.InventoryDocuments.AnyAsync(d=>d.DocumentId==id&&(d.Status=="REJECTED"||d.Status=="CANCELLED"||d.Status=="REMAINDER_CLOSED"),ct)
            ||(await db.InventoryDocuments.AnyAsync(d=>d.DocumentId==id&&d.Status=="FULLY_ISSUED",ct)&&!await MaterialDelivered(id,ct));
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
