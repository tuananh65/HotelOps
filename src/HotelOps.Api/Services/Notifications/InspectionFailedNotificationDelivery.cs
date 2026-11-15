using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed class InspectionFailedNotificationDelivery(HotelOpsDbContext db, TimeProvider clock)
{
    private const string Kind = "INSPECTION_FAILED";
    private static InspectionFailureIntent? Read(string? json)
    {
        if (json == null) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("notification", out var value)) return null;
            var intent = value.Deserialize<InspectionFailureIntent>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return intent is { SchemaVersion: 1, Kind: Kind, SupervisorIds: not null, OriginalCleaner: not null }
                && intent.SupervisorIds.All(id => id > 0) && intent.OriginalCleaner.CleanerUserId > 0
                && intent.OriginalCleaner.CompletionRevision is { Length: 8 } ? intent : null;
        }
        catch (JsonException) { return null; }
    }

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
        var hint = Read(await db.AuditLogs.AsNoTracking().Where(a => a.AuditId == auditId).Select(a => a.NewValues).SingleOrDefaultAsync(ct));
        if (hint == null) return "SOURCE_DENIED";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync(hint.Recipients, ct);
        var audit = await db.AuditLogs.FromSqlInterpolated($"SELECT * FROM dbo.audit_logs WITH(UPDLOCK,HOLDLOCK) WHERE audit_id={auditId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        var intent = Read(audit?.NewValues);
        if (intent == null || !intent.Recipients.SequenceEqual(hint.Recipients)
            || audit is not { EntityType: "INSPECTION", Action: "UPDATE", BranchId: > 0, ActorUserId: > 0 }
            || !long.TryParse(audit.EntityId, out var inspection)) return "SOURCE_DENIED";
        var cleaner = intent.OriginalCleaner;
        if (cleaner.BranchId != audit.BranchId) return "SOURCE_DENIED";
        if (await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN OBJECT_ID('dbo.fe04_cleaner_attributions','U') IS NOT NULL
              AND OBJECT_ID('dbo.fe04_capture_cleaner_attribution','P') IS NOT NULL
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_fe04_cleaner_immutable','TR')
                AND parent_id=OBJECT_ID('dbo.fe04_cleaner_attributions','U') AND is_disabled=0)
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
                AND name='HotelOps.FE04.CleanerAttribution' AND CONVERT(nvarchar(128),value)='R04_V1')
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) != 1) return "SOURCE_UNAVAILABLE";
        var valid = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.room_inspections i WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.rooms r ON r.room_id=i.room_id AND r.branch_id={audit.BranchId}
            JOIN dbo.fe04_cleaner_attributions a ON a.cleaning_task_id=i.cleaning_task_id AND a.room_id=i.room_id AND a.branch_id=r.branch_id
            JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=a.cleaning_task_id AND t.room_id=a.room_id AND t.actual_end_at=a.completed_at
            JOIN dbo.cleaning_assignments ca ON ca.assignment_id=a.assignment_id AND ca.cleaning_task_id=a.cleaning_task_id
              AND ca.assignee_user_id=a.cleaner_user_id AND ca.assigned_at<=a.completed_at AND (ca.ended_at IS NULL OR ca.ended_at>=a.completed_at)
            JOIN dbo.cleaning_progress_logs p ON p.progress_id=a.completion_progress_id AND p.cleaning_task_id=a.cleaning_task_id
              AND p.logged_by=a.cleaner_user_id AND p.status='COMPLETED' AND p.progress_percent=100 AND p.logged_at=a.completed_at
            WHERE i.inspection_id={inspection} AND i.inspector_user_id={audit.ActorUserId}
              AND i.workflow_status='FAILED' AND i.status='COMPLETED' AND i.result='FAIL' AND i.created_at>=a.completed_at
              AND a.cleaning_task_id={cleaner.CleaningTaskId} AND a.room_id={cleaner.RoomId}
              AND a.cleaner_user_id={cleaner.CleanerUserId} AND a.assignment_id={cleaner.AssignmentId}
              AND a.completion_progress_id={cleaner.CompletionProgressId} AND a.completion_revision={cleaner.CompletionRevision}
              AND a.completed_at={cleaner.CompletedAtUtc} AND a.writer_transaction_id={cleaner.WriterTransactionId}
            """).SingleAsync(ct);
        if (valid != 1) return "SOURCE_DENIED";
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
