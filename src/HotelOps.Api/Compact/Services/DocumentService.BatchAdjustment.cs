using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;
public sealed partial class DocumentService
{
    private async Task<bool> ConsumableReduction(InventoryDocument document,CancellationToken ct)
    {
        if(document.Kind!="ADJUSTMENT")return false;
        return await (from line in db.InventoryDocumentLines join source in db.InventoryDocumentLines on line.SourceLineId equals source.LineId
            where line.DocumentId==document.DocumentId && source.CountedQty<source.ExpectedQty
                && db.ItemLifecycleProfiles.Any(p=>p.BranchId==document.BranchId && p.ItemId==line.ItemId && p.Classification=="CONSUMABLE")
            select line.LineId).AnyAsync(ct);
    }
    public Task<object> SelectAdjustmentBatches(long id,BatchAdjustmentRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        var document=await Load(id,"write",ct);await StaffSupplyService.Lock(db,ct);
        Output.Require(document.Kind=="ADJUSTMENT"&&document.Status=="DRAFT"&&document.ApprovedBy==null&&document.PostedAt==null,"ADJUSTMENT_PLAN_LOCKED");
        Output.Version(document.RowVersion,request.RowVersion);Output.Key(request.OperationKey);Output.Reason(request.Reason);
        Output.Require(!await db.DocumentEvents.AnyAsync(e=>e.DocumentId==id&&e.OperationKey==request.OperationKey&&e.ActorUserId==actor.Id,ct),"OPERATION_ALREADY_APPLIED");
        await ValidateBatchSelection(document,request.Lines,ct);
        db.DocumentEvents.Add(new DocumentEvent{BranchId=document.BranchId,DocumentId=id,EventType="BATCH_ADJUSTMENT_SELECTED",ActorUserId=actor.Id,
            OperationKey=request.OperationKey,Reason=request.Reason.Trim(),SnapshotJson=JsonSerializer.Serialize(request.Lines),OccurredAt=clock.GetUtcNow().UtcDateTime,RevisionNo=1});
        db.Entry(document).Property(x=>x.Reason).IsModified=true;
        if(await ConsumableReduction(document,ct))await notifications.ForPermission(document.BranchId,"ROOM_SUPPLY_CONFIG","CONSUMABLE_REDUCTION_REQUESTED","Đề nghị duyệt điều chỉnh giảm vật tư tiêu hao",null,id,request.OperationKey,ct);
        await db.SaveChangesAsync(ct);return (object)Output.Record(document);
    },ct);

    private async Task ValidateBatchSelection(InventoryDocument document,BatchAdjustmentLine[] selections,CancellationToken ct)
    {
        var expected=await (from line in db.InventoryDocumentLines join source in db.InventoryDocumentLines on line.SourceLineId equals source.LineId
            where line.DocumentId==document.DocumentId&&source.CountedQty<source.ExpectedQty&&db.ItemLifecycleProfiles.Any(p=>p.BranchId==document.BranchId&&p.ItemId==line.ItemId&&p.Classification=="CONSUMABLE")
            select new{line.LineId,line.ItemId,Quantity=line.ActualQty*line.ConversionFactor}).ToArrayAsync(ct);
        Output.Require(expected.Length>0&&selections.Length>0&&selections.All(x=>x.Quantity>0&&SupplyMath.ValidQuantity(x.Quantity))
            &&selections.Select(x=>new{x.LineId,x.BatchId}).Distinct().Count()==selections.Length,"ADJUSTMENT_BATCH_PLAN_INVALID",400);
        Output.Require(expected.All(l=>selections.Where(x=>x.LineId==l.LineId).Sum(x=>x.Quantity)==l.Quantity)
            &&selections.All(x=>expected.Any(l=>l.LineId==x.LineId)),"ADJUSTMENT_BATCH_TOTAL_MISMATCH",400);
        foreach(var selection in selections.GroupBy(x=>x.BatchId)){
            var batch=await db.StockBatches.SingleOrDefaultAsync(b=>b.BatchId==selection.Key&&b.BranchId==document.BranchId&&b.WarehouseId==document.SourceWarehouseId,ct)
                ??throw new ApiException(409,"BATCH_SOURCE_MISMATCH");
            Output.Require(selection.All(x=>expected.Any(l=>l.LineId==x.LineId&&l.ItemId==batch.ItemId)),"BATCH_SOURCE_MISMATCH");
            Output.Require(await db.ItemLifecycleProfiles.AnyAsync(p=>p.BranchId==batch.BranchId&&p.ItemId==batch.ItemId&&p.Classification=="CONSUMABLE",ct),"CONSUMABLE_CLASSIFICATION_REQUIRED");
            Output.Require(selection.Sum(x=>x.Quantity)<=batch.WarehouseQuantity-batch.ReservedQuantity,"BATCH_STOCK_INSUFFICIENT");
        }
    }
    private async Task ValidateSavedAdjustmentBatches(InventoryDocument document,CancellationToken ct)
    {
        var json=await db.DocumentEvents.AsNoTracking().Where(e=>e.DocumentId==document.DocumentId&&e.EventType=="BATCH_ADJUSTMENT_SELECTED")
            .OrderByDescending(e=>e.DocumentEventId).Select(e=>e.SnapshotJson).FirstOrDefaultAsync(ct);
        Output.Require(json!=null,"ADJUSTMENT_BATCH_SELECTION_REQUIRED");
        BatchAdjustmentLine[] selections;
        try { selections=JsonSerializer.Deserialize<BatchAdjustmentLine[]>(json!)??[]; }
        catch(JsonException){throw new ApiException(409,"ADJUSTMENT_BATCH_PLAN_INVALID");}
        await ValidateBatchSelection(document,selections,ct);
    }

    private async Task<object?> AdjustmentBatches(InventoryDocument document,CancellationToken ct)
    {
        if(document.Kind!="ADJUSTMENT")return null;
        var json=await db.DocumentEvents.AsNoTracking().Where(e=>e.DocumentId==document.DocumentId&&e.EventType=="BATCH_ADJUSTMENT_SELECTED")
            .OrderByDescending(e=>e.DocumentEventId).Select(e=>e.SnapshotJson).FirstOrDefaultAsync(ct);
        var lines=await (from l in db.InventoryDocumentLines join s in db.InventoryDocumentLines on l.SourceLineId equals s.LineId
            join i in db.InventoryItems on l.ItemId equals i.ItemId
            where l.DocumentId==document.DocumentId&&s.CountedQty<s.ExpectedQty&&db.ItemLifecycleProfiles.Any(p=>p.BranchId==document.BranchId&&p.ItemId==l.ItemId&&p.Classification=="CONSUMABLE")
            select new{l.LineId,l.ItemId,i.Name,Quantity=l.ActualQty*l.ConversionFactor}).ToArrayAsync(ct);
        var selected=json==null?Array.Empty<BatchAdjustmentLine>():JsonSerializer.Deserialize<BatchAdjustmentLine[]>(json)??[];
        var selectedIds=selected.Select(s=>s.BatchId).ToArray();
        var ids=lines.Select(l=>l.ItemId).ToArray();
        var batches=await db.StockBatches.AsNoTracking().Where(b=>b.WarehouseId==document.SourceWarehouseId&&ids.Contains(b.ItemId)&&(b.WarehouseQuantity>b.ReservedQuantity||selectedIds.Contains(b.BatchId)))
            .Select(b=>new{b.BatchId,b.ItemId,b.LotNumber,b.ExpiresOn,b.Status,Available=b.WarehouseQuantity-b.ReservedQuantity}).ToArrayAsync(ct);
        return new{lines,batches,selected};
    }
}
