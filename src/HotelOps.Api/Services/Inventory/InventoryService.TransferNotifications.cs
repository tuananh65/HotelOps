using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;

namespace HotelOps.Api.Services.Inventory;

public sealed partial class InventoryService
{
    private async Task TransferIntentAsync(StockDocument document, long targetBranch, string kind, DateTime at, long actor, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Transfer event transaction required.");
        var evidence = db.AuditLogs.Local.Single(a => a.EntityType == "stock_documents"
            && a.EntityId == document.DocumentId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            && a.BranchId == targetBranch && a.ActorUserId == actor && a.OccurredAt == at && a.Action == "UPDATE");
        db.Notifications.Add(new Notification
        {
            SourceType = TransferNotifications.Source, SourceId = evidence.AuditId, NotificationType = kind,
            Title = kind == TransferNotifications.Dispatched ? "Stock transfer dispatched" : "Stock transfer receipt recorded",
            Message = "Open the authorized transfer to review recorded quantities and remaining goods in transit.",
            Priority = "NORMAL", CreatedBy = actor, CreatedAt = at
        });
        await Save(ct);
    }
}
