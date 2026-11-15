using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

internal sealed record InspectionNotificationHint(long AuditId, long RecipientUserId);

internal interface IInspectionNotificationDelivery
{
    Task<InspectionNotificationHint[]> ReadPendingAsync(int limit, long afterAuditId, CancellationToken ct);
    Task<string> DeliverAsync(long auditId, long recipientHint, CancellationToken ct);
}

// Audit is the durable event source; notification/inbox rows are the delivery receipt.
// Never purge these events while delivery is outstanding. No schema or permission seeds.
internal sealed class InspectionPassedNotificationDelivery(HotelOpsDbContext db, TimeProvider clock) : IInspectionNotificationDelivery
{
    private const string Kind = "INSPECTION_PASSED";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<InspectionNotificationHint[]> ReadPendingAsync(int limit, long afterAuditId, CancellationToken ct)
    {
        if (limit is < 1 or > 100 || afterAuditId < 0) throw new ArgumentOutOfRangeException(nameof(limit));
        if (db.Database.CurrentTransaction != null) throw new InvalidOperationException("NOTIFICATION_DISCOVERY_OUTSIDE_TRANSACTION_REQUIRED");
        // Advisory only. Unknown/legacy audit JSON cannot become an event or authorize a recipient.
        return await db.Database.SqlQuery<InspectionNotificationHint>($"""
            SELECT TOP({limit}) a.audit_id AS AuditId,
                TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.notification.recipientUserId')) AS RecipientUserId
            FROM dbo.audit_logs a
            CROSS APPLY(SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END AS payload) j
            WHERE a.audit_id>{afterAuditId} AND a.entity_type='INSPECTION' AND a.action='UPDATE'
              AND JSON_VALUE(j.payload,'$.notification.kind')={Kind}
              AND JSON_VALUE(j.payload,'$.notification.schemaVersion')='1'
              AND TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.notification.recipientUserId'))>0
              AND NOT EXISTS(SELECT 1 FROM dbo.notifications n JOIN dbo.notification_recipients r ON r.notification_id=n.notification_id
                WHERE n.notification_type={Kind} AND n.source_type='INSPECTION'
                  AND n.source_id=TRY_CONVERT(bigint,a.entity_id) AND r.delivery_channel='IN_APP' AND r.delivery_status='DELIVERED'
                  AND r.user_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.notification.recipientUserId')))
            ORDER BY a.audit_id
            """).ToArrayAsync(ct);
    }

    public async Task<string> DeliverAsync(long auditId, long recipientHint, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null) throw new InvalidOperationException("NOTIFICATION_INDEPENDENT_DELIVERY_REQUIRED");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await DeliverInCallerTransactionAsync(auditId, recipientHint, ct);
        await tx.CommitAsync(ct);
        return result;
        // Errors propagate. A later retry uses the same event key, including after lost commit
        // acknowledgement; it never retries the inspection business command.
    }

    private async Task<string> DeliverInCallerTransactionAsync(long auditId, long recipientHint, CancellationToken ct)
    {
        if (auditId <= 0 || recipientHint <= 0) throw new ArgumentOutOfRangeException(nameof(auditId));
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable
            || db.ChangeTracker.Entries().Any() || System.Transactions.Transaction.Current != null
            || db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("NOTIFICATION_CLEAN_SERIALIZABLE_TRANSACTION_REQUIRED");
        // Hint comes from discovery, not authority. Lock User before audit/source; verify identity again.
        await new UserEligibilityLock(db).LockAsync([recipientHint], ct);
        var audit = await db.AuditLogs.FromSqlInterpolated($"SELECT * FROM dbo.audit_logs WITH(UPDLOCK,HOLDLOCK) WHERE audit_id={auditId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (audit is not { EntityType: "INSPECTION", Action: "UPDATE", BranchId: > 0, ActorUserId: > 0 }
            || !long.TryParse(audit.EntityId, out var inspection) || inspection <= 0 || audit.NewValues == null)
            return "SOURCE_DENIED";
        Intent? intent;
        try
        {
            using var payload = JsonDocument.Parse(audit.NewValues);
            if (!payload.RootElement.TryGetProperty("notification", out var value)) return "SOURCE_DENIED";
            intent = value.Deserialize<Intent>(Json);
        }
        catch (JsonException) { return "SOURCE_DENIED"; }
        if (intent == null || intent.SchemaVersion != 1 || intent.Kind != Kind || intent.RecipientUserId != recipientHint
            || intent.CleaningTaskId <= 0 || intent.RoomId <= 0 || intent.AssignmentId <= 0 || intent.CompletionProgressId <= 0
            || intent.CompletionRevision is not { Length: 16 }
            || intent.CompletionRevision.Any(c => !Uri.IsHexDigit(c))) return "SOURCE_DENIED";
        if (await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN OBJECT_ID('dbo.fe04_cleaner_attributions','U') IS NOT NULL
              AND OBJECT_ID('dbo.fe04_capture_cleaner_attribution','P') IS NOT NULL
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_fe04_cleaner_immutable','TR')
                AND parent_id=OBJECT_ID('dbo.fe04_cleaner_attributions','U') AND is_disabled=0)
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
                AND name='HotelOps.FE04.CleanerAttribution' AND CONVERT(nvarchar(128),value)='R04_V1')
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) != 1)
            return "SOURCE_UNAVAILABLE";
        // Inspection is the immutable event identity. This retained lock serializes duplicate
        // audits/workers too. A later room cleaning cycle does not erase historical attribution.
        var valid = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.room_inspections i WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=i.cleaning_task_id AND t.room_id=i.room_id
            JOIN dbo.rooms r ON r.room_id=i.room_id AND r.branch_id={audit.BranchId}
            JOIN dbo.fe04_cleaner_attributions a ON a.cleaning_task_id=t.cleaning_task_id AND a.room_id=r.room_id AND a.branch_id=r.branch_id
            JOIN dbo.cleaning_assignments ca ON ca.assignment_id=a.assignment_id AND ca.cleaning_task_id=t.cleaning_task_id
              AND ca.assignee_user_id=a.cleaner_user_id AND ca.assigned_at<=a.completed_at AND (ca.ended_at IS NULL OR ca.ended_at>=a.completed_at)
            JOIN dbo.cleaning_progress_logs p ON p.progress_id=a.completion_progress_id AND p.cleaning_task_id=t.cleaning_task_id
              AND p.logged_by=a.cleaner_user_id AND p.status='COMPLETED' AND p.progress_percent=100 AND p.logged_at=a.completed_at
            WHERE i.inspection_id={inspection} AND i.inspector_user_id={audit.ActorUserId}
              AND i.workflow_status='PASSED' AND i.status='COMPLETED' AND i.result='PASS'
              AND t.workflow_status='COMPLETED' AND t.status='COMPLETED' AND t.actual_end_at=a.completed_at
              AND i.created_at>=a.completed_at AND a.cleaning_task_id={intent.CleaningTaskId} AND a.room_id={intent.RoomId}
              AND a.assignment_id={intent.AssignmentId} AND a.completion_progress_id={intent.CompletionProgressId}
              AND a.cleaner_user_id={recipientHint} AND CONVERT(varchar(16),a.completion_revision,2)={intent.CompletionRevision}
            """).SingleAsync(ct);
        if (valid != 1) return "SOURCE_DENIED";
        if (!await InspectionRecipientReadAccess.CanReadAsync(db, recipientHint, audit.BranchId.Value, clock.GetUtcNow().UtcDateTime, ct))
            return "RECIPIENT_READ_DENIED";

        var existing = await db.Notifications.FromSqlInterpolated($"""
            SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK)
            WHERE notification_type={Kind} AND source_type='INSPECTION' AND source_id={inspection}
            """).AsNoTracking().ToArrayAsync(ct);
        if (existing.Length != 0)
        {
            if (existing.Length != 1 || existing[0].CreatedBy != audit.ActorUserId)
                throw new InvalidOperationException("INSPECTION_NOTIFICATION_RECEIPT_CONFLICT");
            var rows = await db.NotificationRecipients.AsNoTracking().Where(r => r.NotificationId == existing[0].NotificationId).ToArrayAsync(ct);
            if (rows.Length != 1 || rows[0].UserId != recipientHint || rows[0].DeliveryChannel != "IN_APP"
                || rows[0].DeliveryStatus != "DELIVERED" || rows[0].DeliveredAt == null)
                throw new InvalidOperationException("INSPECTION_NOTIFICATION_RECEIPT_CONFLICT");
            return "ALREADY_DELIVERED";
        }
        // No navigation link until canonical inspection-history routing is accepted.
        // Content is deliberately minimal; source fields preserve the inspection reference.
        var notice = new Notification
        {
            NotificationType = Kind, Title = "Room inspection passed", Message = "The inspection of the room you cleaned has passed.",
            Priority = "NORMAL", SourceType = "INSPECTION", SourceId = inspection,
            CreatedBy = audit.ActorUserId, CreatedAt = audit.OccurredAt
        };
        db.Notifications.Add(notice);
        await db.SaveChangesAsync(ct);
        db.NotificationRecipients.Add(new NotificationRecipient
        {
            NotificationId = notice.NotificationId, UserId = recipientHint, DeliveryChannel = "IN_APP",
            DeliveryStatus = "DELIVERED", DeliveredAt = clock.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return "DELIVERED";
    }

    private sealed class Intent
    {
        public int SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public long RecipientUserId { get; set; }
        public long CleaningTaskId { get; set; }
        public long RoomId { get; set; }
        public long AssignmentId { get; set; }
        public long CompletionProgressId { get; set; }
        public string? CompletionRevision { get; set; }
    }
}
