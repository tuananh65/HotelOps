using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

// Transaction-local source selection, not a delivery receipt or permission grant.
// Delivery must revalidate the recipient and source in its own transaction.
internal sealed record PassedInspectionRecipient(string Status, CleanerAttribution? Cleaner);

public sealed partial class RoomTypeCreationService
{
    private async Task<PassedInspectionRecipient> ReadPassedInspectionRecipientAsync(
        long inspection, long branch, CancellationToken ct)
    {
        var transaction = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("INSPECTION_NOTIFICATION_CALLER_TRANSACTION_REQUIRED");
        RequireAuthorityTransaction(db.Database.GetDbConnection(), transaction.GetDbTransaction());
        // Called only after the authorized approval domain write. Never infer a cleaner from
        // the inspector, current assignee, most recent progress actor or notification producer.
        if (await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.room_inspections i
            JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=i.cleaning_task_id AND t.room_id=i.room_id
            JOIN dbo.rooms r ON r.room_id=i.room_id AND r.branch_id={branch}
            WHERE i.inspection_id={inspection} AND i.workflow_status='PASSED'
              AND i.status='COMPLETED' AND i.result='PASS'
              AND t.workflow_status='COMPLETED' AND t.status='COMPLETED'
            """).SingleAsync(ct) != 1)
            throw new InvalidOperationException("INSPECTION_PASSED_SOURCE_REQUIRED");
        var cleaner = await ReadInspectionCleanerAttributionAsync(db.Database.GetDbConnection(),
            transaction.GetDbTransaction(), inspection, branch, ct);
        if (cleaner == null) return new("ATTRIBUTION_UNAVAILABLE", null);

        // Match the canonical inspection-history branch policy for this explicit recipient.
        // Historical cleaner identity does not grant current read access (GBR-010).
        return new(await InspectionRecipientReadAccess.CanReadAsync(db, cleaner.CleanerUserId, branch,
            clock.GetUtcNow().UtcDateTime, ct) ? "ELIGIBLE" : "RECIPIENT_READ_DENIED", cleaner);
    }
}
