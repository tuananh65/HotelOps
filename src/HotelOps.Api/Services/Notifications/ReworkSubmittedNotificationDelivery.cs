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
        if(audit==null||audit.BranchId!=branch)return "SOURCE_DENIED";
        var verified=await ReworkSubmittedNotificationSource.VerifyAsync(db,audit,ct);
        if(verified!="VALID")return verified;
        var rework=long.Parse(audit.EntityId,System.Globalization.CultureInfo.InvariantCulture);
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
