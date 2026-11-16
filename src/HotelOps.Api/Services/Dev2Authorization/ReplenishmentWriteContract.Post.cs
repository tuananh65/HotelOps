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
        var beforePosting = phase is Dev2AuthorizationPhase.Admission or Dev2AuthorizationPhase.BeforeStockPosting;
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
        if (lines.Length != input.ReceiptLines.Count || input.ReceiptLines.Any(l => !lines.Any(s => s.LineId == l.LineId))) return false;
        foreach (var mapping in input.Lines)
        {
            var line = lines.SingleOrDefault(l => l.LineId == mapping.StockDocumentLineId);
            if (line == null || !await db.MaterialReplenishmentLines.AsNoTracking().AnyAsync(r =>
                r.ReplenishmentLineId == mapping.ReplenishmentLineId && r.ReplenishmentId == Id(a)
                && r.WarehouseId == warehouse.Id && r.ItemId == line.ItemId, ct)) return false;
        }
        if (!beforePosting && input.ReceiptLines.Any(l => lines.Single(s => s.LineId == l.LineId).ActualQty != l.AcceptedQty)) return false;
        return true;
    }
}
