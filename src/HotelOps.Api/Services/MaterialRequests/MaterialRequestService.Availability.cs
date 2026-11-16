using HotelOps.Api.DTOs.MaterialRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.MaterialRequests;

public sealed partial class MaterialRequestService
{
    // The production read contract supplies branch-scoped INVENTORY_VIEW and
    // a Serializable transaction through disclosure. No stock is reserved here.
    private async Task<IReadOnlyList<MaterialWarehouseAvailabilityDto>> WarehouseAvailability(MaterialRequestDto request, CancellationToken ct)
    {
        var lines = request.Lines.Where(l => l.IsActive).ToArray();
        var itemIds = lines.Select(l => l.ItemId).Distinct().ToArray();
        var warehouses = await db.Warehouses.AsNoTracking().Where(w => w.BranchId == request.BranchId && w.IsActive && w.Branch.IsActive)
            .OrderBy(w => w.WarehouseName).ThenBy(w => w.WarehouseId).Select(w => new { w.WarehouseId, w.WarehouseName }).ToArrayAsync(ct);
        var warehouseIds = warehouses.Select(w => w.WarehouseId).ToArray();
        var items = await db.InventoryItems.AsNoTracking().Where(i => itemIds.Contains(i.ItemId))
            .Select(i => new { i.ItemId, i.BaseUomId, BaseName = i.BaseUom.UomName,
                Active = i.IsActive && i.BaseUom.IsActive && i.Category.IsActive }).ToDictionaryAsync(i => i.ItemId, ct);
        var activeUnits = await db.UnitsOfMeasures.AsNoTracking().Where(u => u.IsActive).Select(u => u.UomId).ToArrayAsync(ct);
        var conversions = await db.UomConversions.AsNoTracking().Where(c => c.ItemId != null && itemIds.Contains(c.ItemId.Value)
            && c.IsActive && c.FromUom.IsActive && c.ToUom.IsActive).ToArrayAsync(ct);
        var balances = await db.StockMovements.AsNoTracking().Where(m => warehouseIds.Contains(m.WarehouseId) && itemIds.Contains(m.ItemId))
            .GroupBy(m => new { m.WarehouseId, m.ItemId })
            .Select(g => new { g.Key.WarehouseId, g.Key.ItemId, Qty = g.Sum(m => m.SignedBaseQty) }).ToArrayAsync(ct);
        // Aggregate only inbound quantities. Never disclose transfer documents or
        // the source warehouse/branch, which may be outside the viewer's scope.
        var transfers = await db.StockDocumentLines.AsNoTracking().Where(l => l.Document.DocumentType == "TRANSFER"
            && l.Document.DestinationWarehouseId != null && warehouseIds.Contains(l.Document.DestinationWarehouseId.Value)
            && itemIds.Contains(l.ItemId) && l.Document.Status != "DRAFT" && l.Document.Status != "CANCELLED")
            .Select(l => new { l.ItemId, WarehouseId = l.Document.DestinationWarehouseId!.Value, l.Document.Status,
                l.ActualQty, l.BaseQty, l.ConversionFactor,
                Reconciled = l.StockTransferReceiptLines.Sum(r => (decimal?)(r.ReceivedQty + r.DamagedQty + r.MissingQty)) ?? 0,
                InvalidReceipt = l.StockTransferReceiptLines.Any(r => r.TransferReceipt.TransferDocumentId != l.DocumentId
                    || r.ReceivedQty < 0 || r.DamagedQty < 0 || r.MissingQty < 0) }).ToArrayAsync(ct);
        var result = new List<MaterialWarehouseAvailabilityDto>();
        foreach (var warehouse in warehouses)
        {
            var previews = new List<MaterialStockPreviewDto>();
            foreach (var line in lines)
            {
                var stock = balances.SingleOrDefault(b => b.WarehouseId == warehouse.WarehouseId && b.ItemId == line.ItemId)?.Qty ?? 0;
                var inbound = transfers.Where(t => t.WarehouseId == warehouse.WarehouseId && t.ItemId == line.ItemId).ToArray();
                var transitValid = inbound.All(t => t.Status is "IN_TRANSIT" or "PARTIALLY_RECEIVED" or "RECEIVED"
                    && !t.InvalidReceipt && t.ConversionFactor > 0 && t.ActualQty >= 0 && t.Reconciled <= t.ActualQty
                    && t.BaseQty == t.ActualQty * t.ConversionFactor && (t.Status != "RECEIVED" || t.Reconciled == t.ActualQty));
                decimal? transit = transitValid ? inbound.Sum(t => (t.ActualQty - t.Reconciled) * t.ConversionFactor) : null;
                items.TryGetValue(line.ItemId, out var item);
                decimal? factor = null;
                if (item is { Active: true } && activeUnits.Contains(line.UomId))
                {
                    if (line.UomId == item.BaseUomId) factor = 1;
                    else
                    {
                        var matches = conversions.Where(c => c.ItemId == line.ItemId && c.FromUomId == line.UomId && c.ToUomId == item.BaseUomId).ToArray();
                        if (matches.Length == 1 && matches[0].ConversionFactor > 0) factor = matches[0].ConversionFactor;
                    }
                }
                string? reason = line.ApprovedQty == null ? "Awaiting approval; no approved quantity is available to issue."
                    : line.HistoricalRemainingQty < 0 ? "Issued quantity exceeds approved quantity."
                    : factor == null ? "Active item/unit and an unambiguous item-specific conversion are required."
                    : stock < 0 ? "Stock ledger balance is negative; reconciliation is required." : null;
                decimal? fulfillable = reason == null ? PreviewQuantity(stock, line.HistoricalRemainingQty!.Value, factor!.Value) : null;
                previews.Add(new(line.RequestLineId, line.ItemName ?? $"Item {line.ItemId}", line.UnitName ?? $"Unit {line.UomId}",
                    item?.BaseName ?? "Unknown", line.HistoricalRemainingQty, stock, transit, fulfillable, reason,
                    transitValid ? null : "Transfer history is legacy or inconsistent; in-transit quantity is unavailable.",
                    lines.Count(other => other.ItemId == line.ItemId) > 1));
            }
            result.Add(new(warehouse.WarehouseId, warehouse.WarehouseName, previews));
        }
        return result;
    }

    // Both issue quantity and converted base quantity must fit decimal(18,3).
    // With a decimal(18,6) factor, find the smallest representable issue step;
    // rounding stock/factor alone could advertise an unpostable quantity.
    private static decimal PreviewQuantity(decimal stock, decimal remaining, decimal factor)
    {
        var scaled = factor * 1_000_000m;
        var a = scaled; var b = 1_000_000m;
        while (b != 0) { var remainder = a % b; a = b; b = remainder; }
        var step = 1000m / a;
        var limit = Math.Min(remaining, Math.Min(stock, 999_999_999_999_999.999m) / factor);
        return decimal.Floor(limit / step) * step;
    }
}
