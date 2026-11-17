using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.MaterialRequests;

internal static class MaterialReversalEvidence
{
    internal static async Task<List<MaterialFulfillment>> EffectiveAsync(HotelOpsDbContext db, List<MaterialFulfillment> batches, CancellationToken ct)
    {
        foreach (var batch in batches.Where(f => f.StockDocument?.Status == "REVERSED"))
        {
            var original = batch.StockDocument!;
            var reversals = await db.StockDocuments.AsNoTracking().Include(d => d.StockDocumentLines).ThenInclude(l => l.StockMovements)
                .Where(d => d.ReversalOfDocumentId == original.DocumentId).ToArrayAsync(ct);
            var effects = await db.StockMovements.AsNoTracking().Where(m => m.DocumentLine.DocumentId == original.DocumentId).ToArrayAsync(ct);
            if (batch.Status != "ISSUED" || original.DocumentType != "ISSUE" || original.ReferenceType != "MATERIAL_FULFILLMENT"
                || original.ReferenceId != batch.FulfillmentId || original.SourceWarehouseId != batch.WarehouseId
                || reversals.Length != 1 || reversals[0] is not { Status: "POSTED", DocumentType: "REVERSAL", PostedAt: not null, PostedBy: not null } reversal
                || reversal.SourceWarehouseId != original.SourceWarehouseId || reversal.DestinationWarehouseId != original.DestinationWarehouseId
                || effects.Length != original.StockDocumentLines.Count || reversal.StockDocumentLines.Count != original.StockDocumentLines.Count
                || batch.MaterialFulfillmentLines.Count != original.StockDocumentLines.Count)
                throw new ValidationException("Reversed fulfillment lacks an authoritative offsetting document.");
            foreach (var line in original.StockDocumentLines)
            {
                var linked = batch.MaterialFulfillmentLines.SingleOrDefault(l => l.StockDocumentLineId == line.LineId);
                var effect = effects.SingleOrDefault(m => m.DocumentLineId == line.LineId);
                var offset = reversal.StockDocumentLines.SingleOrDefault(l => l.LineNo == line.LineNo);
                if (linked == null || linked.ItemId != line.ItemId || linked.UomId != line.UomId || linked.IssuedQty != line.ActualQty
                    || effect == null || effect.SignedBaseQty != -line.BaseQty || effect.WarehouseId != batch.WarehouseId || effect.ItemId != line.ItemId
                    || offset == null || offset.ItemId != line.ItemId || offset.UomId != line.UomId || offset.BaseQty != line.BaseQty
                    || offset.ActualQty != line.ActualQty || offset.ConversionFactor != line.ConversionFactor || offset.StockMovements.Count != 1
                    || offset.StockMovements.Any(m => m.SignedBaseQty != -effect.SignedBaseQty || m.WarehouseId != effect.WarehouseId
                        || m.ItemId != line.ItemId || m.OccurredAt != reversal.PostedAt || m.CreatedBy != reversal.PostedBy))
                    throw new ValidationException("Reversed fulfillment ledger cannot be reconciled safely.");
            }
        }
        return batches.Where(f => f.StockDocument?.Status != "REVERSED").ToList();
    }
}
