using HotelOps.Api.Compact.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class InsightService
{
    private IQueryable<InventoryDocumentLine> StocktakeDifferenceLines(long branchId)
        =>db.InventoryDocumentLines.AsNoTracking().Where(l=>l.BranchId==branchId&&l.CountedQty!=null&&l.ExpectedQty!=null&&l.CountedQty!=l.ExpectedQty
            &&db.InventoryDocuments.Any(d=>d.DocumentId==l.DocumentId&&d.BranchId==branchId&&d.Kind=="STOCKTAKE"&&d.Status=="COUNTED")
            &&!db.InventoryDocumentLines.Any(a=>a.SourceLineId==l.LineId&&db.InventoryDocuments.Any(d=>d.DocumentId==a.DocumentId&&d.Kind=="ADJUSTMENT"&&d.PostedAt!=null)
                &&db.StockMovements.Any(m=>m.LineId==a.LineId&&!db.StockMovements.Any(r=>r.ReversalOfMovementId==m.MovementId))));

    private async Task<object> StocktakeDifferencesReport(long branchId,DateTime start,DateTime end,CancellationToken ct)
    {
        await access.Require("INVENTORY_VIEW",branchId,ct:ct);
        var unresolved=StocktakeDifferenceLines(branchId).Select(l=>l.LineId);
        return await (from l in db.InventoryDocumentLines.AsNoTracking()
            join d in db.InventoryDocuments on l.DocumentId equals d.DocumentId
            join i in db.InventoryItems on l.ItemId equals i.ItemId
            join u in db.ReferenceValues on i.BaseUnitId equals u.ReferenceId
            where d.BranchId==branchId&&l.BranchId==branchId&&d.Kind=="STOCKTAKE"&&d.CreatedAt>=start&&d.CreatedAt<=end
                &&l.CountedQty!=null&&l.ExpectedQty!=null&&l.CountedQty!=l.ExpectedQty
            orderby d.CreatedAt descending,l.LineNo
            select new{d.DocumentId,d.DocumentNo,d.Status,d.CreatedAt,l.LineId,i.ItemId,item=i.Name,unit=u.Name,
                expectedBaseQty=l.ExpectedQty*l.ConversionFactor,countedBaseQty=l.CountedQty*l.ConversionFactor,
                differenceBaseQty=(l.CountedQty-l.ExpectedQty)*l.ConversionFactor,needsAdjustment=unresolved.Contains(l.LineId)})
            .Take(10000).ToArrayAsync(ct);
    }

    private async Task<object> TransferReport(long branchId,DateTime start,DateTime end,CancellationToken ct)
    {
        await access.Require("INVENTORY_VIEW",branchId,ct:ct);
        // The selected branch must participate; this never exposes an unrelated branch's ledger.
        var documents=db.InventoryDocuments.AsNoTracking().Where(d=>(d.Kind=="TRANSFER_REQUEST"||d.Kind=="TRANSFER"||d.Kind=="TRANSFER_RECEIPT")
            &&(d.BranchId==branchId||d.DestinationBranchId==branchId
                ||d.Kind=="TRANSFER_RECEIPT"&&db.InventoryDocuments.Any(s=>s.DocumentId==d.SourceDocumentId&&s.Kind=="TRANSFER"&&s.BranchId==branchId))
            &&d.CreatedAt>=start&&d.CreatedAt<=end);
        var headers=await documents.OrderByDescending(d=>d.CreatedAt).ThenBy(d=>d.DocumentId).Take(10000)
            .Select(d=>new{d.DocumentId,d.DocumentNo,d.Kind,d.Status,d.BranchId,d.DestinationBranchId,d.SourceDocumentId,d.CreatedAt,d.ApprovedAt,d.PostedAt,d.ClosedAt,
                sourceBranchId=d.Kind=="TRANSFER_RECEIPT"?db.InventoryDocuments.Where(s=>s.DocumentId==d.SourceDocumentId&&s.Kind=="TRANSFER").Select(s=>s.BranchId).FirstOrDefault():d.BranchId}).ToArrayAsync(ct);
        var ids=headers.Select(d=>d.DocumentId).ToArray();
        var states=await TransferStatusProjection.Read(db,headers.Where(d=>d.Kind=="TRANSFER").Select(d=>d.DocumentId).ToArray(),ct);
        var lines=await (from l in db.InventoryDocumentLines.AsNoTracking() join i in db.InventoryItems on l.ItemId equals i.ItemId
            join unit in db.ReferenceValues on i.BaseUnitId equals unit.ReferenceId
            where ids.Contains(l.DocumentId) orderby l.DocumentId,l.LineNo
            select new{l.DocumentId,l.LineId,l.ItemId,item=i.Name,unit=unit.Name,
                requestedBaseQty=l.RequestedQty*l.ConversionFactor,approvedBaseQty=l.ApprovedQty*l.ConversionFactor,
                actualBaseQty=l.ActualQty*l.ConversionFactor,
                receivedBaseQty=(from received in db.InventoryDocumentLines join receipt in db.InventoryDocuments on received.DocumentId equals receipt.DocumentId
                    join movement in db.StockMovements on received.LineId equals movement.LineId
                    where received.SourceLineId==l.LineId&&receipt.Kind=="TRANSFER_RECEIPT"&&receipt.PostedAt!=null
                        &&!db.StockMovements.Any(r=>r.ReversalOfMovementId==movement.MovementId)
                    select (decimal?)movement.SignedBaseQty).Sum()??0}).ToArrayAsync(ct);
        return headers.Select(d=>new{d.DocumentId,d.DocumentNo,d.Kind,recordedStatus=d.Status,status=states.GetValueOrDefault(d.DocumentId,d.Status),
            d.sourceBranchId,destinationBranchId=d.Kind=="TRANSFER_RECEIPT"?d.BranchId:d.DestinationBranchId,d.SourceDocumentId,d.CreatedAt,d.ApprovedAt,d.PostedAt,d.ClosedAt,
            lines=lines.Where(l=>l.DocumentId==d.DocumentId).ToArray()}).ToArray();
    }
}
