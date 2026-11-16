using Microsoft.EntityFrameworkCore;
using static HotelOps.Api.Services.Replenishments.ReplenishmentRules;

namespace HotelOps.Api.Services.Replenishments;

public sealed partial class ReplenishmentService
{
    public sealed record DraftReceiptLine(long LineId, string ItemName, string UnitName, decimal ExpectedQty, long? ReplenishmentLineId);
    public sealed record DraftReceipt(long DocumentId, string DocumentNo, string RowVersion, DraftReceiptLine[] Lines);
    public sealed record DraftReceiptPage(long ReplenishmentId, string RowVersion, DraftReceipt[] Items, long? NextAfter);

    internal async Task<DraftReceiptPage> DraftReceipts(long id, long after, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Scoped transaction required.");
        if (after < 0) throw Invalid("Receipt cursor must be nonnegative.");
        var root = await Get(id, ct) ?? throw Invalid("Replenishment unavailable.");
        if (root.RawStatus is not ("REQUESTED" or "PARTIALLY_RECEIVED")) return new(id, root.RowVersion, [], null);
        var itemIds = root.Lines.Where(l => l.IsOpenCoverage && l.RemainingBaseQty > 0).Select(l => l.ItemId).ToArray();
        var headers = await db.StockDocuments.AsNoTracking().Where(d => d.DocumentId > after && d.DocumentType == "RECEIPT"
            && d.Status == "DRAFT" && d.DestinationWarehouseId == root.WarehouseId && d.SourceWarehouseId == null
            && d.ReversalOfDocumentId == null && d.CancelledAt == null && d.PostedAt == null && d.PostedBy == null
            && d.StockDocumentLines.Any(l => itemIds.Contains(l.ItemId))
            && !d.StockDocumentLines.Any(l => l.RequestedQty == null || l.RequestedQty < 0 || !l.Item.IsActive
                || !l.Item.Category.IsActive || !l.Uom.IsActive || !l.Item.BaseUom.IsActive || l.StockMovements.Any()
                || db.MaterialReplenishmentReceiptLines.Any(r => r.StockDocumentLineId == l.LineId)))
            .OrderBy(d => d.DocumentId).Select(d => new { d.DocumentId, d.DocumentNo, d.RowVersion }).Take(21).ToArrayAsync(ct);
        var ids = headers.Take(20).Select(d => d.DocumentId).ToArray();
        var lines = await db.StockDocumentLines.AsNoTracking().Where(l => ids.Contains(l.DocumentId)).OrderBy(l => l.LineNo)
            .Select(l => new { l.DocumentId, l.LineId, l.ItemId, l.Item.ItemName, l.Uom.UomName, l.RequestedQty }).ToArrayAsync(ct);
        var items = headers.Take(20).Select(d => new DraftReceipt(d.DocumentId, d.DocumentNo, Convert.ToBase64String(d.RowVersion),
            lines.Where(l => l.DocumentId == d.DocumentId).Select(l => new DraftReceiptLine(l.LineId, l.ItemName, l.UomName, l.RequestedQty!.Value,
                root.Lines.SingleOrDefault(r => r.ItemId == l.ItemId && r.IsOpenCoverage && r.RemainingBaseQty > 0)?.ReplenishmentLineId)).ToArray())).ToArray();
        return new(id, root.RowVersion, items, headers.Length > 20 ? ids[^1] : null);
    }
}
