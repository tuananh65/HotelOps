using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed class ReinspectionPassedNotificationDelivery(HotelOpsDbContext db,TimeProvider clock)
{
    private const string Kind=ReinspectionPassedNotificationSource.Kind;
    internal Task<InspectionNotificationHint[]> ReadPendingAsync(int limit,long cursor,CancellationToken ct)=>
        db.Database.SqlQuery<InspectionNotificationHint>($"""
            SELECT TOP({limit}) a.audit_id AS AuditId,CAST(-3 AS bigint) AS RecipientUserId
            FROM dbo.audit_logs a CROSS APPLY(SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END AS payload) j
            WHERE a.audit_id>{cursor} AND a.entity_type='REINSPECTION' AND a.action='UPDATE'
              AND JSON_VALUE(j.payload,'$.notification.kind')={Kind} AND JSON_VALUE(j.payload,'$.notification.schemaVersion')='1'
              AND TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.notification.recipientUserId'))>0
              AND NOT EXISTS(SELECT 1 FROM dbo.notifications n JOIN dbo.notification_recipients r ON r.notification_id=n.notification_id
                WHERE n.notification_type={Kind} AND n.source_type='CLEANING_REWORK'
                  AND n.source_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.reworkId'))
                  AND r.user_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.notification.recipientUserId'))
                  AND r.delivery_channel='IN_APP' AND r.delivery_status='DELIVERED') ORDER BY a.audit_id
            """).ToArrayAsync(ct);

    internal async Task<string> DeliverAsync(long auditId,CancellationToken ct)
    {
        if(!db.Database.IsSqlServer()||db.Database.CurrentTransaction!=null||db.ChangeTracker.Entries().Any()
            ||System.Transactions.Transaction.Current!=null||db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("NOTIFICATION_INDEPENDENT_CLEAN_CONTEXT_REQUIRED");
        var hint=ReinspectionPassedNotificationSource.Parse(await db.AuditLogs.AsNoTracking().Where(a=>a.AuditId==auditId)
            .Select(a=>a.NewValues).SingleOrDefaultAsync(ct));
        if(hint==null)return "SOURCE_DENIED";
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([hint.Notification.RecipientUserId],ct);
        var audit=await db.AuditLogs.FromSqlInterpolated($"SELECT * FROM dbo.audit_logs WITH(UPDLOCK,HOLDLOCK) WHERE audit_id={auditId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        var receipt=audit==null?null:await ReinspectionPassedNotificationSource.VerifyAsync(db,audit,ct);
        if(receipt==null||receipt!=hint)return "SOURCE_DENIED";
        var user=receipt.Notification.RecipientUserId;var branch=receipt.BranchId;var rework=receipt.ReworkId;
        var now=clock.GetUtcNow().UtcDateTime;
        if(!await HotelOps.Api.Services.WorkTime.WorkdayMembershipStore.InstalledAsync(db,ct)
            ||!await ReworkRecipientReadAccess.EligibleAssignees(db,branch,now).AnyAsync(u=>u.UserId==user,ct)
            ||!(await ReworkRecipientReadAccess.EvaluateAsync(db,user,branch,rework,"CLEANING_VIEW",now,ct)).IsGranted)
            return "RECIPIENT_READ_DENIED";
        var notices=await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE notification_type={Kind} AND source_type='CLEANING_REWORK' AND source_id={rework}")
            .AsNoTracking().ToArrayAsync(ct);
        if(notices.Length>1||notices.Any(n=>n.CreatedBy!=receipt.ActorUserId||n.CreatedAt!=audit!.OccurredAt))
            throw new InvalidOperationException("REINSPECTION_NOTIFICATION_RECEIPT_CONFLICT");
        var notice=notices.SingleOrDefault();
        if(notice==null)
        {
            notice=new Notification{NotificationType=Kind,SourceType="CLEANING_REWORK",SourceId=rework,Title="Reinspection passed",
                Message="Your cleaning rework passed reinspection. Open the task to view its result.",Priority="NORMAL",
                CreatedBy=receipt.ActorUserId,CreatedAt=audit!.OccurredAt};
            db.Notifications.Add(notice);await db.SaveChangesAsync(ct);
        }
        var recipients=await db.NotificationRecipients.AsNoTracking().Where(r=>r.NotificationId==notice.NotificationId).ToArrayAsync(ct);
        if(recipients.Length>0)
        {
            if(recipients.Length!=1||recipients[0].UserId!=user||recipients[0].DeliveryChannel!="IN_APP"
                ||recipients[0].DeliveryStatus!="DELIVERED"||recipients[0].DeliveredAt==null)
                throw new InvalidOperationException("REINSPECTION_NOTIFICATION_RECEIPT_CONFLICT");
        }
        else
        {
            db.NotificationRecipients.Add(new NotificationRecipient{NotificationId=notice.NotificationId,UserId=user,
                DeliveryChannel="IN_APP",DeliveryStatus="DELIVERED",DeliveredAt=now});
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);db.ChangeTracker.Clear();return "DELIVERED";
    }
}
