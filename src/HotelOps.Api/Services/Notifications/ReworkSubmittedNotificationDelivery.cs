using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

// UC036's protected receipt is the durable intent. Delivery does not assign a reinspector.
internal sealed class ReworkSubmittedNotificationDelivery(HotelOpsDbContext db,TimeProvider clock)
{
    private const string Kind="REWORK_SUBMITTED";

    internal Task<InspectionNotificationHint[]> ReadPendingAsync(int limit,long cursor,CancellationToken ct)=>
        db.Database.SqlQuery<InspectionNotificationHint>($"""
            SELECT TOP({limit}) audit_id AS AuditId,CAST(-2 AS bigint) AS RecipientUserId FROM dbo.audit_logs
            CROSS APPLY(SELECT CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END AS payload) j
            WHERE audit_id>{cursor} AND entity_type='cleaning_reworks' AND action='UPDATE'
              AND JSON_VALUE(j.payload,'$.contract')='FE04_REWORK_SUBMISSION_V1'
              AND JSON_VALUE(j.payload,'$.notification.kind')={Kind}
              AND JSON_VALUE(j.payload,'$.notification.schemaVersion')='1'
              AND JSON_VALUE(j.payload,'$.notification.audience')='BRANCH_HK_SUPERVISORS' ORDER BY audit_id
            """).ToArrayAsync(ct);

    internal async Task<string> DeliverAsync(long auditId,CancellationToken ct)
    {
        if(!db.Database.IsSqlServer()||db.Database.CurrentTransaction!=null||db.ChangeTracker.Entries().Any()
            ||System.Transactions.Transaction.Current!=null||db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("NOTIFICATION_INDEPENDENT_CLEAN_CONTEXT_REQUIRED");
        var branch=await db.AuditLogs.AsNoTracking().Where(a=>a.AuditId==auditId).Select(a=>a.BranchId).SingleOrDefaultAsync(ct);
        if(branch is not >0)return "SOURCE_DENIED";
        // Discover before locking; newly eligible users are handled on the next durable scan.
        var candidates=await InspectionFailureRecipients.SupervisorsAsync(db,branch.Value,clock.GetUtcNow().UtcDateTime,ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync(candidates,ct);
        var audit=await db.AuditLogs.FromSqlInterpolated($"SELECT * FROM dbo.audit_logs WITH(UPDLOCK,HOLDLOCK) WHERE audit_id={auditId}").AsNoTracking().SingleOrDefaultAsync(ct);
        if(audit is not {EntityType:"cleaning_reworks",Action:"UPDATE",ActorUserId:>0}||audit.BranchId!=branch
            ||!long.TryParse(audit.EntityId,out var rework)||rework<=0||audit.NewValues==null)return "SOURCE_DENIED";
        if(await db.Database.SqlQueryRaw<int>("""
            SELECT CASE WHEN OBJECT_ID('dbo.fe04_commit_rework_submission','P') IS NOT NULL
              AND OBJECT_ID('dbo.fe04_rework_authority_issuers','U') IS NOT NULL
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.ReworkSubmission.V1'
                AND CONVERT(nvarchar(128),value)='FE04_REWORK_SUBMISSION_V1') THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)!=1)return "SOURCE_UNAVAILABLE";
        if(await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations o WITH(HOLDLOCK)
            CROSS APPLY(SELECT CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END AS receipt) j
            JOIN dbo.cleaning_reworks w WITH(UPDLOCK,HOLDLOCK) ON w.rework_id={rework} AND w.assignee_user_id=o.actor_user_id
            JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=w.cleaning_task_id
            JOIN dbo.rooms r ON r.room_id=t.room_id AND r.branch_id={branch} AND r.is_active=1
            JOIN dbo.room_cleaning_cycle_state c ON c.room_id=r.room_id AND c.cleaning_task_id=t.cleaning_task_id
            JOIN dbo.fe04_rework_authority_issuers x ON x.issuer='FE04.UC036' AND x.version=1 AND x.is_active=1
            JOIN sys.sql_modules m ON m.object_id=x.writer_object_id AND m.execute_as_principal_id=-2
            WHERE o.actor_user_id={audit.ActorUserId} AND o.completed=1 AND o.fingerprint_version=1
              AND x.writer_object_id=OBJECT_ID('dbo.fe04_commit_rework_submission') AND x.source_commit LIKE REPLICATE('[0-9a-f]',40)
              AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition))=x.module_digest
              AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),o.result_json))=HASHBYTES('SHA2_256',CONVERT(varbinary(max),{audit.NewValues}))
              AND w.workflow_status='PENDING_REINSPECTION' AND w.status='IN_PROGRESS' AND w.completed_at IS NULL
              AND JSON_VALUE(j.receipt,'$.contract')='FE04_REWORK_SUBMISSION_V1'
              AND JSON_VALUE(j.receipt,'$.issuer')='FE04.UC036' AND JSON_VALUE(j.receipt,'$.issuerVersion')='1'
              AND JSON_VALUE(j.receipt,'$.notification.kind')={Kind} AND JSON_VALUE(j.receipt,'$.notification.schemaVersion')='1'
              AND JSON_VALUE(j.receipt,'$.notification.audience')='BRANCH_HK_SUPERVISORS'
              AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,'$.actorUserId'))=o.actor_user_id
              AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,'$.branchId'))=r.branch_id
              AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,'$.reworkId'))=w.rework_id
              AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(j.receipt,'$.operationId'))=o.operation_key
              AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,'$.writerTransactionId'))>0
              AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,'$.writerTransactionId'))<>CURRENT_TRANSACTION_ID()
              AND JSON_VALUE(j.receipt,'$.entity.state')='PENDING_REINSPECTION'
              AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,'$.entity.inspectionId'))=w.inspection_id
              AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,'$.entity.cleaningTaskId'))=t.cleaning_task_id
            """).SingleAsync(ct)!=1)return "SOURCE_DENIED";
        var now=clock.GetUtcNow().UtcDateTime;
        var eligible=(await InspectionFailureRecipients.SupervisorsAsync(db,branch.Value,now,ct)).Intersect(candidates).ToArray();
        var existing=await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE notification_type={Kind} AND source_type='CLEANING_REWORK' AND source_id={rework}").AsNoTracking().ToArrayAsync(ct);
        if(existing.Length>1||existing.Any(n=>n.CreatedBy!=audit.ActorUserId))throw new InvalidOperationException("REWORK_NOTIFICATION_RECEIPT_CONFLICT");
        var notice=existing.SingleOrDefault();
        foreach(var user in eligible)
        {
            if(notice==null)
            {
                notice=new Notification{NotificationType=Kind,SourceType="CLEANING_REWORK",SourceId=rework,
                    Title="Cleaning rework submitted",Message="A cleaning rework task is waiting for reinspection.",
                    Priority="NORMAL",CreatedBy=audit.ActorUserId,CreatedAt=audit.OccurredAt};
                db.Notifications.Add(notice);await db.SaveChangesAsync(ct);
            }
            var rows=await db.NotificationRecipients.AsNoTracking().Where(r=>r.NotificationId==notice.NotificationId&&r.UserId==user).ToArrayAsync(ct);
            if(rows.Length>0)
            {
                if(rows.Length!=1||rows[0].DeliveryChannel!="IN_APP"||rows[0].DeliveryStatus!="DELIVERED"||rows[0].DeliveredAt==null)
                    throw new InvalidOperationException("REWORK_NOTIFICATION_RECEIPT_CONFLICT");
                continue;
            }
            db.NotificationRecipients.Add(new NotificationRecipient{NotificationId=notice.NotificationId,UserId=user,
                DeliveryChannel="IN_APP",DeliveryStatus="DELIVERED",DeliveredAt=now});await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);db.ChangeTracker.Clear();return eligible.Length>0?"DELIVERED":"RECIPIENT_READ_DENIED";
    }
}
