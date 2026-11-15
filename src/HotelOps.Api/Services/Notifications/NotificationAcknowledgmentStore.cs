using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed class NotificationAcknowledgmentRow
{
    public long RecipientId { get; set; }
    public bool RequiresAcknowledgment { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}

internal static class NotificationAcknowledgmentStore
{
    internal static Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) =>
        db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.NotificationAcknowledgment.V1' AND CONVERT(nvarchar(20),value)='1')
              AND EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.notifications')
                AND name='requires_acknowledgment' AND system_type_id=104 AND is_nullable=0)
              AND EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.notification_recipients')
                AND name='acknowledged_at' AND system_type_id=42 AND is_nullable=1)
              AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.notification_recipients')
                AND name='CK_notification_ack_read' AND is_disabled=0 AND is_not_trusted=0)
              THEN 1 ELSE 0 END AS Value
            """).AnyAsync(value => value == 1, ct);

    internal static IQueryable<NotificationAcknowledgmentRow> Query(HotelOpsDbContext db) =>
        db.Database.SqlQuery<NotificationAcknowledgmentRow>($"""
            SELECT r.recipient_id AS RecipientId,n.requires_acknowledgment AS RequiresAcknowledgment,
              r.acknowledged_at AS AcknowledgedAt
            FROM dbo.notification_recipients r JOIN dbo.notifications n ON n.notification_id=r.notification_id
            """);
}
