using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.MaterialRequests;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.Inventory;
public sealed partial class InventoryService
{
    private sealed record WaitingReceiptRequest(long RequestId, long RequesterId, long BranchId, object[] Shortages);
    private async Task<WaitingReceiptRequest[]> ReceiptWaitingAsync(StockDocument document, long[] incomingItems, CancellationToken ct)
    {
        if (incomingItems.Length == 0) return [];
        var warehouse = document.DestinationWarehouseId!.Value;
        var branch = await db.Warehouses.Where(w => w.WarehouseId == warehouse).Select(w => w.BranchId).SingleAsync(ct);
        var lines = await db.MaterialRequestLines.AsNoTracking().Where(l => l.IsActive && incomingItems.Contains(l.ItemId)
            && l.Request.SourceWarehouseId == warehouse && l.Request.BranchId == branch
            && (l.Request.Status == "APPROVED" || l.Request.Status == "PARTIALLY_ISSUED") && l.ApprovedQty > l.FulfilledQty
            && db.MaterialRequestApprovalCycles.Any(c => c.RequestId == l.RequestId && c.Status == "APPROVED"
                && !db.MaterialRequestApprovalCycles.Any(later => later.RequestId == c.RequestId && later.RevisionNo > c.RevisionNo)))
            .OrderBy(l => l.RequestId).ThenBy(l => l.LineId)
            .Select(l => new { l.RequestId, l.Request.RequesterId, l.LineId, l.ItemId, l.UomId, Remaining = l.ApprovedQty!.Value - l.FulfilledQty }).ToArrayAsync(ct);
        var result = new List<WaitingReceiptRequest>();
        foreach (var group in lines.GroupBy(l => new { l.RequestId, l.RequesterId }))
        {
            if (!await MaterialReadProvenance.ValidAsync(db, group.Key.RequestId, branch, ct)) throw Invalid("Material shortage provenance requires reconciliation.");
            var shortages = new List<object>();
            foreach (var itemLines in group.GroupBy(l => l.ItemId))
            {
                var item = await Item(itemLines.Key, ct);
                decimal remainingBase = 0;
                foreach (var line in itemLines) remainingBase += Base(line.Remaining, await Factor(item, line.UomId, ct));
                var availableBefore = await Balance(warehouse, itemLines.Key, ct);
                if (remainingBase > availableBefore) shortages.Add(new { LineIds = itemLines.Select(l => l.LineId).ToArray(), ItemId = itemLines.Key, remainingBase, availableBefore });
            }
            if (shortages.Count > 0) result.Add(new(group.Key.RequestId, group.Key.RequesterId, branch, shortages.ToArray()));
        }
        return result.ToArray();
    }
    private async Task ReceiptWaitingIntentsAsync(StockDocument document, WaitingReceiptRequest[] waiting, CancellationToken ct)
    {
        // Append-only audit is durable intent, atomic with receipt/ledger and linked replenishment.
        // No delivery occurs until the transaction commits.
        foreach (var recipient in waiting.GroupBy(r => r.RequesterId))
        {
            var request = recipient.OrderBy(r => r.RequestId).First();
            db.AuditLogs.Add(new AuditLog { EntityType = "stock_receipt_waiting_requests", EntityId = request.RequestId.ToString(CultureInfo.InvariantCulture),
                BranchId = request.BranchId, ActorUserId = document.PostedBy, Action = "CREATE", OccurredAt = document.PostedAt!.Value,
                NewValues = JsonSerializer.Serialize(new { documentId = document.DocumentId, requestId = request.RequestId,
                    requesterId = request.RequesterId, warehouseId = document.DestinationWarehouseId, relatedRequestIds = recipient.Select(r => r.RequestId).ToArray(), shortages = request.Shortages }),
                ChangedFields = "[\"receipt_notification_intent\"]" });
        }
        if (waiting.Length > 0) await Save(ct);
    }
}
