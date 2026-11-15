using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed class InspectionFailedNotificationDelivery(HotelOpsDbContext db, TimeProvider clock)
{
    private const string Kind = "INSPECTION_FAILED";
    internal Task<InspectionNotificationHint[]> ReadPendingAsync(int limit, long cursor, CancellationToken ct) =>
        db.Database.SqlQuery<InspectionNotificationHint>($"""
            SELECT TOP({limit}) a.audit_id AS AuditId,CAST(0 AS bigint) AS RecipientUserId
            FROM dbo.audit_logs a
            CROSS APPLY(SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END AS payload) j
            WHERE a.audit_id>{cursor} AND a.entity_type='INSPECTION' AND a.action='UPDATE'
              AND JSON_VALUE(j.payload,'$.notification.kind')={Kind}
              AND JSON_VALUE(j.payload,'$.notification.schemaVersion')='1'
              AND EXISTS(SELECT 1 FROM OPENJSON(j.payload,'$.notification.recipients') v
                WHERE TRY_CONVERT(bigint,v.value)>0 AND NOT EXISTS(
                  SELECT 1 FROM dbo.notifications n JOIN dbo.notification_recipients r ON r.notification_id=n.notification_id
                  WHERE n.notification_type={Kind} AND n.source_type='INSPECTION' AND n.source_id=TRY_CONVERT(bigint,a.entity_id)
                    AND r.user_id=TRY_CONVERT(bigint,v.value) AND r.delivery_channel='IN_APP' AND r.delivery_status='DELIVERED'))
            ORDER BY a.audit_id
            """).ToArrayAsync(ct);

    internal async Task<string> DeliverAsync(long auditId, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()
            || System.Transactions.Transaction.Current != null || db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("NOTIFICATION_INDEPENDENT_CLEAN_CONTEXT_REQUIRED");
        // Discovery is advisory. Acquire all user locks in order before reloading the authoritative event.
        var hint = InspectionFailedNotificationSource.Read(await db.AuditLogs.AsNoTracking().Where(a => a.AuditId == auditId).Select(a => a.NewValues).SingleOrDefaultAsync(ct));
        if (hint == null) return "SOURCE_DENIED";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync(hint.Recipients, ct);
        var audit = await db.AuditLogs.FromSqlInterpolated($"SELECT * FROM dbo.audit_logs WITH(UPDLOCK,HOLDLOCK) WHERE audit_id={auditId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        var intent = InspectionFailedNotificationSource.Read(audit?.NewValues);
        if (intent == null || !intent.Recipients.SequenceEqual(hint.Recipients)
            || audit is not { EntityType: "INSPECTION", Action: "UPDATE", BranchId: > 0, ActorUserId: > 0 }
            || !long.TryParse(audit.EntityId, out var inspection)) return "SOURCE_DENIED";
        var (sourceStatus, source) = await InspectionFailedNotificationSource.VerifyAsync(db, audit, ct);
        if (source == null) return sourceStatus;
        var cleaner = source.Intent.OriginalCleaner;
        var now = clock.GetUtcNow().UtcDateTime;
        var supervisors = await InspectionFailureRecipients.SupervisorsAsync(db, audit.BranchId.Value, now, ct);
        var eligible = new List<long>();
        foreach (var user in intent.Recipients)
            if ((user == cleaner.CleanerUserId || supervisors.Contains(user))
                && await InspectionRecipientReadAccess.CanReadAsync(db, user, audit.BranchId.Value, now, ct)) eligible.Add(user);
        var notices = await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE notification_type={Kind} AND source_type='INSPECTION' AND source_id={inspection}")
            .AsNoTracking().ToArrayAsync(ct);
        if (notices.Length > 1 || notices.Any(n => n.CreatedBy != audit.ActorUserId)) throw new InvalidOperationException("INSPECTION_NOTIFICATION_RECEIPT_CONFLICT");
        var notice = notices.SingleOrDefault();
        foreach (var user in eligible)
        {
            if (notice == null)
            {
                notice = new Notification { NotificationType = Kind, SourceType = "INSPECTION", SourceId = inspection,
                    Title = "Room inspection failed", Message = "An inspection requires cleaning rework.", Priority = "NORMAL",
                    CreatedBy = audit.ActorUserId, CreatedAt = audit.OccurredAt };
                db.Notifications.Add(notice);
                await db.SaveChangesAsync(ct);
            }
            var receipts = await db.NotificationRecipients.AsNoTracking().Where(r => r.NotificationId == notice.NotificationId && r.UserId == user).ToArrayAsync(ct);
            if (receipts.Length > 0)
            {
                if (receipts.Length != 1 || receipts[0].DeliveryChannel != "IN_APP" || receipts[0].DeliveryStatus != "DELIVERED" || receipts[0].DeliveredAt == null)
                    throw new InvalidOperationException("INSPECTION_NOTIFICATION_RECEIPT_CONFLICT");
                continue;
            }
            db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notice.NotificationId, UserId = user,
                DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return eligible.Count == intent.Recipients.Length ? "DELIVERED" : "RECIPIENT_READ_DENIED";
    }
}
