using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Entities;

namespace HotelOps.Api.Services.Inventory;

public sealed partial class InventoryService
{
    private async Task ReturnSubmittedIntentAsync(StockDocument document, long branch, CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog
        {
            EntityType = "stock_return_request_intents", EntityId = document.DocumentId.ToString(CultureInfo.InvariantCulture),
            Action = "CREATE", ActorUserId = document.CreatedBy, BranchId = branch, OccurredAt = document.CreatedAt,
            NewValues = await StockReturnApprovalStore.SnapshotAsync(db, document.DocumentId, ct),
            ChangedFields = "[\"return_request_notification_intent\"]"
        });
        await Save(ct);
    }

    private async Task ReturnPostedIntentAsync(StockDocument document, long branch, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null || document.DocumentType != "RETURN" || document.Status != "POSTED"
            || document.PostedAt == null || document.PostedBy == null)
            throw new InvalidOperationException("Committed return event must be prepared inside the posting transaction.");
        // Delivery is separate from posting. CreatedBy identifies the return requester;
        // do not substitute the material-request owner or the original issue's issuer.
        // A future delivery attempt must prove current source read authorization.
        db.AuditLogs.Add(new AuditLog
        {
            EntityType = "stock_return_notification_intents",
            EntityId = document.DocumentId.ToString(CultureInfo.InvariantCulture),
            Action = "CREATE", ActorUserId = document.PostedBy, BranchId = branch,
            OccurredAt = document.PostedAt.Value,
            NewValues = JsonSerializer.Serialize(new
            {
                businessEvent = "RETURN_POSTED", documentId = document.DocumentId,
                requesterId = document.CreatedBy, warehouseId = document.DestinationWarehouseId,
                lines = document.StockDocumentLines.OrderBy(l => l.LineNo).Select(l => new
                {
                    lineId = l.LineId, originalIssueLineId = l.StockReturnLineDetailStockDocumentLine!.OriginalIssueLineId,
                    l.ItemId, l.UomId, declared = l.RequestedQty, accepted = l.ActualQty,
                    damaged = l.StockReturnLineDetailStockDocumentLine.DamagedQty,
                    reason = l.StockReturnLineDetailStockDocumentLine.Note,
                    l.ConversionFactor, acceptedBase = l.BaseQty
                }).ToArray()
            }),
            ChangedFields = "[\"return_notification_intent\"]"
        });
        await Save(ct);
    }
}
