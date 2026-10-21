using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.DTOs.Replenishments;
using Microsoft.EntityFrameworkCore;

sealed partial class ReplenishmentSuite
{
    InventoryItemDto second = null!;
    InventoryThresholdDto secondThreshold = null!;
    async Task Additional()
    {
        await ReleaseAll(); await SetStock(0);
        second = await Send<InventoryItemDto>("POST", "inventory/items", new InventoryItemWrite(actor, prefix + "I2", prefix, category, unit.UomId, true), 201); items.Add(second.ItemId);
        secondThreshold = await Send<InventoryThresholdDto>("PUT", $"inventory/warehouses/{warehouse.WarehouseId}/items/{second.ItemId}/threshold", new InventoryThresholdWrite(actor, 100));
        var extraWarehouse = await Send<InventoryWarehouseDto>("POST", "inventory/warehouses", new InventoryWarehouseWrite(actor, branch, department, prefix + "W2", prefix), 201); warehouses.Add(extraWarehouse.WarehouseId);
        var noThreshold = await Send<ReplenishmentPaged<LowStockShortage>>("GET", $"replenishment-shortages/low-stock?warehouseId={extraWarehouse.WarehouseId}"); Check(noThreshold.Total == 0, "No threshold excluded");
        await Bad("POST", "replenishments", Input() with { WarehouseId = extraWarehouse.WarehouseId });
        var extraThreshold = await Send<InventoryThresholdDto>("PUT", $"inventory/warehouses/{extraWarehouse.WarehouseId}/items/{second.ItemId}/threshold", new InventoryThresholdWrite(actor, 1));
        var secondRoot = await Send<ReplenishmentDto>("POST", "replenishments", new ReplenishmentCreate(actor, extraWarehouse.WarehouseId, prefix, null, [new(1, second.ItemId, "LOW_STOCK", extraThreshold.ThresholdId, null, 1)]), 201);
        await Bad("PUT", $"inventory/items/{second.ItemId}", new InventoryItemWrite(actor, second.ItemCode, prefix, category, box.UomId, true, ExpectedRowVersion: second.RowVersion));
        await Bad("PUT", $"inventory/warehouses/{extraWarehouse.WarehouseId}", new InventoryWarehouseWrite(actor, branch, department, extraWarehouse.WarehouseCode, prefix, false, ExpectedRowVersion: extraWarehouse.RowVersion));
        await Close(secondRoot);
        var r = await Create(2); var wrong = await Post(await Draft(1, warehouseId: extraWarehouse.WarehouseId));
        await Bad("POST", $"replenishments/{r.ReplenishmentId}/receipts/link", new ReplenishmentLink(actor, r.RowVersion, [new(wrong.Lines[0].LineId, r.Lines[0].ReplenishmentLineId)]));
        wrong = await Post(await Draft(1, itemId: second.ItemId));
        await Bad("POST", $"replenishments/{r.ReplenishmentId}/receipts/link", new ReplenishmentLink(actor, r.RowVersion, [new(wrong.Lines[0].LineId, r.Lines[0].ReplenishmentLineId)]));
        var safeLegacy = await Post(await Draft(1));
        await using (var db = Db()) await db.StockReceiptLineDetails.Where(x => x.StockDocumentLineId == safeLegacy.Lines[0].LineId).ExecuteDeleteAsync();
        r = await Link(r, safeLegacy); Check(r.Lines[0].NetReceivedBaseQty == 1, "Deterministic missing-detail adoption");
        var unsafeLegacy = await Post(await Draft(1));
        await using (var db = Db()) await db.StockMovements.Where(x => x.DocumentLineId == unsafeLegacy.Lines[0].LineId).ExecuteDeleteAsync();
        await Bad("POST", $"replenishments/{r.ReplenishmentId}/receipts/link", new ReplenishmentLink(actor, r.RowVersion, [new(unsafeLegacy.Lines[0].LineId, r.Lines[0].ReplenishmentLineId)]));
        await Close(r);
        r = await Create(2); var reversed = await Post(await Draft(1)); await Reverse(reversed.DocumentId);
        await Bad("POST", $"replenishments/{r.ReplenishmentId}/receipts/link", new ReplenishmentLink(actor, r.RowVersion, [new(reversed.Lines[0].LineId, r.Lines[0].ReplenishmentLineId)])); await Close(r);
        // Same receipt document, distinct item lines, one replenishment with two sources.
        var multi = await Send<ReplenishmentDto>("POST", "replenishments", Input(2) with { Lines = [Input(2).Lines[0], new(2, second.ItemId, "LOW_STOCK", secondThreshold.ThresholdId, null, 2)] }, 201);
        var doc = await Send<InventoryDocumentDto>("POST", "inventory/receipts", new InventoryDraft(actor, null, warehouse.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), [new(item.ItemId, unit.UomId, 2), new(second.ItemId, unit.UomId, 2)], Note: prefix), 201);
        multi = await Send<ReplenishmentDto>("POST", $"replenishments/{multi.ReplenishmentId}/receipts/post", new ReplenishmentPost(actor, multi.RowVersion, doc.DocumentId, doc.RowVersion, doc.Lines.Select(x => new InventoryAcceptedLine(x.LineId, 2)).ToArray(), doc.Lines.Select(x => new ReplenishmentReceiptMapping(x.LineId, multi.Lines.Single(l => l.ItemId == x.ItemId).ReplenishmentLineId)).ToArray()));
        Check(multi.RawStatus == "FULLY_RECEIVED" && multi.Receipts.Count == 2, "Multi-line receipt correlation");
        await SetStock(0);
        // AlertEnabled is not low-stock eligibility.
        await using (var db = Db()) await db.StockThresholds.Where(x => x.ThresholdId == threshold.ThresholdId).ExecuteUpdateAsync(s => s.SetProperty(x => x.AlertEnabled, false));
        var low = await Send<ReplenishmentPaged<LowStockShortage>>("GET", $"replenishment-shortages/low-stock?warehouseId={warehouse.WarehouseId}&itemId={item.ItemId}"); Check(low.Total == 1, "Alert disabled still eligible");
        var seed = await Create(); await Close(seed); var date = DateTime.UtcNow.ToString("yyyyMMdd"); var head = "RPL-" + date + "-";
        await using (var db = Db())
        {
            var all = await db.MaterialReplenishments.Where(x => x.ReplenishmentNo.StartsWith(head)).Select(x => x.ReplenishmentNo).ToArrayAsync(); var maximum = all.Select(x => long.TryParse(x[head.Length..], out var n) ? n : 0).Max(); var suffix = Math.Max(999, maximum + 10);
            await db.MaterialReplenishments.Where(x => x.ReplenishmentId == seed.ReplenishmentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReplenishmentNo, head + suffix));
            r = await Create(); Check(r.ReplenishmentNo == head + (suffix + 1).ToString("D3"), "999 to1000 / UTC numbering"); await Close(r);
            await db.MaterialReplenishments.Where(x => x.ReplenishmentId == seed.ReplenishmentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReplenishmentNo, head + "bad"));
            r = await Create(); Check(r.ReplenishmentNo == head + (suffix + 2).ToString("D3"), "Malformed suffix ignored"); await Close(r);
        }
    }
    async Task Cleanup()
    {
        await using var db = Db(); await using var tx = await db.Database.BeginTransactionAsync();
        var rootIds = await db.MaterialReplenishments.Where(x => warehouses.Contains(x.WarehouseId)).Select(x => x.ReplenishmentId).ToArrayAsync();
        var mr = await db.MaterialRequests.Where(x => x.Purpose.StartsWith(prefix)).Select(x => x.RequestId).ToArrayAsync();
        var fs = await db.MaterialFulfillments.Where(x => mr.Contains(x.RequestId)).Select(x => x.FulfillmentId).ToArrayAsync();
        var docs = await db.StockDocuments.Where(x => x.StockDocumentLines.Any(l => items.Contains(l.ItemId))).Select(x => x.DocumentId).ToArrayAsync();
        await db.MaterialReplenishmentReceiptLines.Where(x => rootIds.Contains(x.ReplenishmentLine.ReplenishmentId)).ExecuteDeleteAsync();
        await db.MaterialReplenishmentLines.Where(x => rootIds.Contains(x.ReplenishmentId)).ExecuteDeleteAsync(); await db.MaterialReplenishments.Where(x => rootIds.Contains(x.ReplenishmentId)).ExecuteDeleteAsync();
        await db.MaterialRequestApprovals.Where(x => mr.Contains(x.RequestId)).ExecuteDeleteAsync(); await db.MaterialRequestApprovalLineSnapshots.Where(x => mr.Contains(x.ApprovalCycle.RequestId)).ExecuteDeleteAsync(); await db.MaterialRequestApprovalCycles.Where(x => mr.Contains(x.RequestId)).ExecuteDeleteAsync();
        await db.MaterialFulfillmentLines.Where(x => fs.Contains(x.FulfillmentId)).ExecuteDeleteAsync(); await db.MaterialFulfillments.Where(x => fs.Contains(x.FulfillmentId)).ExecuteDeleteAsync(); await db.MaterialRequestLines.Where(x => mr.Contains(x.RequestId)).ExecuteDeleteAsync(); await db.MaterialRequests.Where(x => mr.Contains(x.RequestId)).ExecuteDeleteAsync();
        await db.StockMovements.Where(x => docs.Contains(x.DocumentLine.DocumentId)).ExecuteDeleteAsync(); await db.StockReceiptLineDetails.Where(x => docs.Contains(x.StockDocumentLine.DocumentId)).ExecuteDeleteAsync(); await db.StockReturnLineDetails.Where(x => docs.Contains(x.StockDocumentLine.DocumentId)).ExecuteDeleteAsync();
        await db.StockDocumentLines.Where(x => docs.Contains(x.DocumentId)).ExecuteDeleteAsync(); await db.StockDocuments.Where(x => docs.Contains(x.DocumentId)).ExecuteDeleteAsync();
        await db.StockThresholds.Where(x => warehouses.Contains(x.WarehouseId)).ExecuteDeleteAsync(); await db.UomConversions.Where(x => x.ItemId != null && items.Contains(x.ItemId.Value)).ExecuteDeleteAsync(); await db.InventoryItems.Where(x => items.Contains(x.ItemId)).ExecuteDeleteAsync(); await db.Warehouses.Where(x => warehouses.Contains(x.WarehouseId)).ExecuteDeleteAsync(); await db.UnitsOfMeasures.Where(x => units.Contains(x.UomId)).ExecuteDeleteAsync();
        var rs = rootIds.Select(x => x.ToString()).ToArray(); var ds = docs.Select(x => x.ToString()).ToArray(); var ms = mr.Select(x => x.ToString()).ToArray(); var fss = fs.Select(x => x.ToString()).ToArray();
        await db.AuditLogs.Where(x => x.NewValues != null && x.NewValues.Contains(prefix) || x.EntityType == "material_replenishments" && rs.Contains(x.EntityId) || x.EntityType == "stock_documents" && ds.Contains(x.EntityId) || x.EntityType == "material_requests" && ms.Contains(x.EntityId) || x.EntityType == "material_fulfillments" && fss.Contains(x.EntityId)).ExecuteDeleteAsync();
        await tx.CommitAsync(); Console.WriteLine("Owned fixture cleanup committed.");
    }
}
