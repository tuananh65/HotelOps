using HotelOps.Api.DTOs.Replenishments;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Dev2Authorization;

internal static partial class ReplenishmentWriteContract
{
    private static async Task<bool> ValidatePost(Dev2BoundAction a, Dev2AuthorizationPhase phase,
        WarehouseFact warehouse, CancellationToken ct)
    {
        var input = (ReplenishmentPost)Input(a);
        if (input.ReceiptDocumentId <= 0 || input.Lines == null || input.Lines.Count == 0
            || input.Lines.Any(l => l == null || l.StockDocumentLineId <= 0 || l.ReplenishmentLineId <= 0)
            || input.Lines.Select(l => l.StockDocumentLineId).Distinct().Count() != input.Lines.Count
            || input.ReceiptLines == null || input.ReceiptLines.Count == 0
            || input.ReceiptLines.Any(l => l == null || l.LineId <= 0 || l.AcceptedQty < 0 || l.RejectedQty < 0 || l.DamagedQty < 0)
            || input.ReceiptLines.Select(l => l.LineId).Distinct().Count() != input.ReceiptLines.Count) return false;
        var db = Db(a);
        var receipt = await db.StockDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.DocumentId == input.ReceiptDocumentId, ct);
        if (receipt == null || receipt.DocumentType != "RECEIPT" || receipt.DestinationWarehouseId != warehouse.Id
            || receipt.SourceWarehouseId != null || receipt.ReversalOfDocumentId != null || receipt.CancelledAt != null) return false;
        var beforePosting = receipt.Status != "POSTED" && (phase is Dev2AuthorizationPhase.Admission or Dev2AuthorizationPhase.BeforeStockPosting);
        if (beforePosting)
        {
            if (receipt.Status != "DRAFT" || receipt.PostedAt != null || receipt.PostedBy != null
                || await db.StockMovements.AnyAsync(m => m.DocumentLine.DocumentId == receipt.DocumentId, ct)
                || await db.MaterialReplenishmentReceiptLines.AnyAsync(l => l.StockDocumentLine.DocumentId == receipt.DocumentId, ct)) return false;
        }
        else if (receipt.Status != "POSTED" || receipt.PostedAt == null || receipt.PostedBy != a.Actor.UserId) return false;

        // The primitive posts the entire document. Bind every supplied line, including
        // lines not credited to this replenishment, to this one authorized warehouse.
        var lines = await db.StockDocumentLines.AsNoTracking().Where(l => l.DocumentId == receipt.DocumentId).ToArrayAsync(ct);
        var linked = await db.Database.SqlQuery<long?>($"SELECT replenishment_id AS Value FROM dbo.stock_receipt_sources WHERE document_id={receipt.DocumentId}").SingleOrDefaultAsync(ct);
        if (linked != null && (linked != Id(a) || input.Lines.Count != lines.Length)) return false;
        if (lines.Length != input.ReceiptLines.Count || input.ReceiptLines.Any(l => !lines.Any(s => s.LineId == l.LineId))) return false;
        foreach (var mapping in input.Lines)
        {
            var line = lines.SingleOrDefault(l => l.LineId == mapping.StockDocumentLineId);
            if (line == null || !await db.MaterialReplenishmentLines.AsNoTracking().AnyAsync(r =>
                r.ReplenishmentLineId == mapping.ReplenishmentLineId && r.ReplenishmentId == Id(a)
                && r.WarehouseId == warehouse.Id && r.ItemId == line.ItemId, ct)) return false;
        }
        if (!beforePosting)
        {
            try { await HotelOps.Api.Services.Inventory.ReceiptPostingProof.RequireAsync(db, receipt.DocumentId, ct); }
            catch (System.ComponentModel.DataAnnotations.ValidationException) { return false; }
            if (phase is Dev2AuthorizationPhase.Admission or Dev2AuthorizationPhase.BeforeStockPosting or Dev2AuthorizationPhase.BeforeDisclosure)
            foreach (var mapping in input.Lines)
            {
                var line = lines.Single(l => l.LineId == mapping.StockDocumentLineId);
                if (line.BaseQty > 0 && !await db.MaterialReplenishmentReceiptLines.AnyAsync(c => c.StockDocumentLineId == line.LineId
                    && c.ReplenishmentLineId == mapping.ReplenishmentLineId && c.CreditedBaseQty == line.BaseQty, ct)) return false;
            }
        }
        if (!beforePosting)
        {
            var lineIds = lines.Select(l => l.LineId).ToArray();
            var detail = await db.StockReceiptLineDetails.AsNoTracking().Where(d => lineIds.Contains(d.StockDocumentLineId)).ToArrayAsync(ct);
            if (input.ReceiptLines.Any(l => !detail.Any(d => d.StockDocumentLineId == l.LineId && d.RejectedQty == l.RejectedQty
                && d.DamagedQty == l.DamagedQty && d.DiscrepancyReason == l.DiscrepancyReason))) return false;
        }
        if (!beforePosting && input.ReceiptLines.Any(l => lines.Single(s => s.LineId == l.LineId).ActualQty != l.AcceptedQty)) return false;
        return true;
    }
}
