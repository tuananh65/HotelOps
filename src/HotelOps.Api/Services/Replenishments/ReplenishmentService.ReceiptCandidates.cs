using Microsoft.EntityFrameworkCore;
using static HotelOps.Api.Services.Replenishments.ReplenishmentRules;

namespace HotelOps.Api.Services.Replenishments;

public sealed partial class ReplenishmentService
{
    public sealed record ReceiptCandidate(long StockDocumentLineId, long ReplenishmentLineId, string DocumentNo,
        string ItemName, decimal BaseQty, string UnitName, DateTime PostedAt);
    public sealed record ReceiptCandidatePage(long ReplenishmentId, string RowVersion, ReceiptCandidate[] Items, long? NextAfter);

    internal async Task<ReceiptCandidatePage> ReceiptCandidates(long id, long after, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Scoped transaction required.");
        if (after < 0) throw Invalid("Receipt cursor must be nonnegative.");
        var root = await Get(id, ct) ?? throw Invalid("Replenishment unavailable.");
        if (root.RawStatus is not ("REQUESTED" or "PARTIALLY_RECEIVED")) return new(id, root.RowVersion, [], null);
        var candidates = await (from r in db.MaterialReplenishmentLines.AsNoTracking()
            join s in db.StockDocumentLines.AsNoTracking() on r.ItemId equals s.ItemId
            where r.ReplenishmentId == id && r.WarehouseId == root.WarehouseId && r.IsOpenCoverage
                && r.BaseUomId == s.Item.BaseUomId && s.LineId > after
                && s.Document.DocumentType == "RECEIPT" && s.Document.Status == "POSTED"
                && s.Document.DestinationWarehouseId == root.WarehouseId && s.Document.PostedAt != null && s.Document.PostedBy != null
                && s.BaseQty > 0 && s.BaseQty <= r.RequestedBaseQty - r.ReceivedBaseQty
                && s.ActualQty > 0 && s.ConversionFactor > 0 && s.ActualQty * s.ConversionFactor == s.BaseQty
                && !db.MaterialReplenishmentReceiptLines.Any(l => l.StockDocumentLineId == s.LineId)
                && !db.StockDocuments.Any(d => d.ReversalOfDocumentId == s.DocumentId && d.Status == "POSTED")
                && s.StockMovements.Count() == 1 && s.StockMovements.Any(m => m.WarehouseId == root.WarehouseId
                    && m.ItemId == r.ItemId && m.SignedBaseQty == s.BaseQty)
            orderby s.LineId, r.ReplenishmentLineId
            select new ReceiptCandidate(s.LineId, r.ReplenishmentLineId, s.Document.DocumentNo, s.Item.ItemName,
                s.BaseQty, s.Item.BaseUom.UomName, s.Document.PostedAt!.Value)).Take(101).ToArrayAsync(ct);
        var page = candidates.Take(100).Select(c => c with { PostedAt = Utc(c.PostedAt) }).ToArray();
        return new(id, root.RowVersion, page, candidates.Length > 100 ? page[^1].StockDocumentLineId : null);
    }
}
