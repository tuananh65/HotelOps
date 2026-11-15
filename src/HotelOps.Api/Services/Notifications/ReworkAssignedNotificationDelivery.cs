using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed record ReworkAssignmentIntent(int SchemaVersion,string Kind,long RecipientUserId);

internal sealed class ReworkAssignedNotificationDelivery(HotelOpsDbContext db,TimeProvider clock)
{
    private const string Kind="REWORK_ASSIGNED";
    private static ReworkAssignmentIntent? Read(string? json)
    {
        if(json==null)return null;
        try
        {
            using var doc=JsonDocument.Parse(json);
            if(!doc.RootElement.TryGetProperty("notification",out var value))return null;
            var intent=value.Deserialize<ReworkAssignmentIntent>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return intent is {SchemaVersion:1,Kind:Kind,RecipientUserId:>0}?intent:null;
        }
        catch(JsonException){return null;}
    }

    internal Task<InspectionNotificationHint[]> ReadPendingAsync(int limit,long cursor,CancellationToken ct)=>
        db.Database.SqlQuery<InspectionNotificationHint>($"""
            SELECT TOP({limit}) a.audit_id AS AuditId,CAST(-1 AS bigint) AS RecipientUserId
            FROM dbo.audit_logs a CROSS APPLY(SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END AS payload) j
            WHERE a.audit_id>{cursor} AND a.entity_type='cleaning_reworks' AND a.action='CREATE'
              AND JSON_VALUE(j.payload,'$.notification.kind')={Kind} AND JSON_VALUE(j.payload,'$.notification.schemaVersion')='1'
              AND TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.notification.recipientUserId'))>0
              AND NOT EXISTS(SELECT 1 FROM dbo.notifications n JOIN dbo.notification_recipients r ON r.notification_id=n.notification_id
                WHERE n.notification_type={Kind} AND n.source_type='CLEANING_REWORK' AND n.source_id=TRY_CONVERT(bigint,a.entity_id)
                  AND r.user_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.notification.recipientUserId'))
                  AND r.delivery_channel='IN_APP' AND r.delivery_status='DELIVERED') ORDER BY a.audit_id
            """).ToArrayAsync(ct);

    internal async Task<string> DeliverAsync(long auditId,CancellationToken ct)
    {
        if(!db.Database.IsSqlServer()||db.Database.CurrentTransaction!=null||db.ChangeTracker.Entries().Any()
            ||System.Transactions.Transaction.Current!=null||db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("NOTIFICATION_INDEPENDENT_CLEAN_CONTEXT_REQUIRED");
        var hint=Read(await db.AuditLogs.AsNoTracking().Where(a=>a.AuditId==auditId).Select(a=>a.NewValues).SingleOrDefaultAsync(ct));
        if(hint==null)return "SOURCE_DENIED";
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([hint.RecipientUserId],ct);
        var audit=await db.AuditLogs.FromSqlInterpolated($"SELECT * FROM dbo.audit_logs WITH(UPDLOCK,HOLDLOCK) WHERE audit_id={auditId}").AsNoTracking().SingleOrDefaultAsync(ct);
        var intent=Read(audit?.NewValues);
        if(intent!=hint||audit is not {EntityType:"cleaning_reworks",Action:"CREATE",BranchId:>0,ActorUserId:>0}
            ||!long.TryParse(audit.EntityId,out var rework))return "SOURCE_DENIED";
        if(await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN OBJECT_ID('dbo.fs016_rework_source_proofs','U') IS NOT NULL
              AND OBJECT_ID('dbo.fe04_rework_authority_issuers','U') IS NOT NULL
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE04.ReworkAuthority.V1'
                AND CONVERT(nvarchar(128),value)='FE05_COMMITTED_V1') THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)!=1)return "SOURCE_UNAVAILABLE";
        // Validate the committed producer occurrence, not just the advisory JSON. This is a
        // notification source check, never a substitute for the executor's authority reader.
        if(await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.cleaning_reworks w WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.room_inspections i ON i.inspection_id=w.inspection_id AND i.cleaning_task_id=w.cleaning_task_id
            JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=w.cleaning_task_id AND t.room_id=i.room_id
            JOIN dbo.rooms r ON r.room_id=i.room_id AND r.branch_id={audit.BranchId} AND r.is_active=1
            JOIN dbo.room_cleaning_cycle_state c ON c.room_id=r.room_id AND c.cleaning_task_id=t.cleaning_task_id
            JOIN dbo.fs016_rework_source_proofs p ON p.rework_id=w.rework_id AND p.inspection_id=i.inspection_id
              AND p.cleaning_task_id=t.cleaning_task_id AND p.room_id=r.room_id AND p.branch_id=r.branch_id
              AND p.version_id=i.version_id AND p.inspection_revision=i.row_version
            JOIN dbo.fe04_rework_authority_issuers x ON x.issuer=p.issuer AND x.version=p.issuer_version
            JOIN sys.sql_modules m ON m.object_id=x.writer_object_id
            JOIN dbo.notification_operation_reservations o ON o.actor_user_id=p.actor_user_id AND o.operation_key=p.operation_key
            JOIN dbo.audit_logs a ON a.audit_id={auditId}
            CROSS APPLY(SELECT CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END AS receipt) j
            WHERE w.rework_id={rework} AND w.assignee_user_id={intent.RecipientUserId} AND w.status NOT IN('COMPLETED','CANCELLED')
              AND i.workflow_status='FAILED' AND i.result='FAIL' AND i.status='COMPLETED' AND t.workflow_status='REWORK_REQUIRED'
              AND p.origin='FE05_COMMITTED_V1' AND p.contract_version=1 AND p.issuer='FE05.UC045.UC046' AND p.issuer_version=1
              AND p.actor_user_id={audit.ActorUserId} AND p.writer_transaction_id>0 AND p.writer_transaction_id<>CURRENT_TRANSACTION_ID()
              AND x.is_active=1 AND x.source_commit LIKE REPLICATE('[0-9a-f]',40) AND m.execute_as_principal_id=-2
              AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition))=x.module_digest AND o.completed=1
              AND JSON_VALUE(j.receipt,'$.contract')='FE05_REWORK_AUTHORITY_V1'
              AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,'$.reworkId'))=w.rework_id
              AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(j.receipt,'$.operationId'))=p.operation_key
              AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,'$.writerTransactionId'))=p.writer_transaction_id
              AND TRY_CONVERT(varbinary(32),JSON_VALUE(j.receipt,'$.sourceDigest'),2)=p.source_digest
              AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(a.new_values,'$.operationId'))=p.operation_key
              AND JSON_VALUE(a.new_values,'$.authorityContract')='FE05_REWORK_AUTHORITY_V1'
              AND TRY_CONVERT(varbinary(32),JSON_VALUE(a.new_values,'$.sourceDigest'),2)=p.source_digest
              AND NOT EXISTS(SELECT 1 FROM dbo.room_inspections newer WHERE newer.cleaning_task_id=t.cleaning_task_id AND newer.inspection_id>i.inspection_id)
            """).SingleAsync(ct)!=1)return "SOURCE_DENIED";
        var now=clock.GetUtcNow().UtcDateTime;
        if(!(await ReworkRecipientReadAccess.EvaluateAsync(db,intent.RecipientUserId,audit.BranchId.Value,rework,"CLEANING_VIEW",now,ct)).IsGranted)
            return "RECIPIENT_READ_DENIED";
        var notices=await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE notification_type={Kind} AND source_type='CLEANING_REWORK' AND source_id={rework}").AsNoTracking().ToArrayAsync(ct);
        if(notices.Length>1||notices.Any(n=>n.CreatedBy!=audit.ActorUserId))throw new InvalidOperationException("REWORK_NOTIFICATION_RECEIPT_CONFLICT");
        var notice=notices.SingleOrDefault();
        if(notice==null)
        {
            notice=new Notification{NotificationType=Kind,SourceType="CLEANING_REWORK",SourceId=rework,Title="Cleaning rework assigned",
                Message="A cleaning rework task is waiting for your acceptance.",Priority="NORMAL",CreatedBy=audit.ActorUserId,CreatedAt=audit.OccurredAt};
            db.Notifications.Add(notice);await db.SaveChangesAsync(ct);
        }
        var receipts=await db.NotificationRecipients.AsNoTracking().Where(r=>r.NotificationId==notice.NotificationId).ToArrayAsync(ct);
        if(receipts.Length>0)
        {
            if(receipts.Length!=1||receipts[0].UserId!=intent.RecipientUserId||receipts[0].DeliveryChannel!="IN_APP"
                ||receipts[0].DeliveryStatus!="DELIVERED"||receipts[0].DeliveredAt==null)throw new InvalidOperationException("REWORK_NOTIFICATION_RECEIPT_CONFLICT");
        }
        else
        {
            db.NotificationRecipients.Add(new NotificationRecipient{NotificationId=notice.NotificationId,UserId=intent.RecipientUserId,
                DeliveryChannel="IN_APP",DeliveryStatus="DELIVERED",DeliveredAt=now});
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);db.ChangeTracker.Clear();return "DELIVERED";
    }
}
