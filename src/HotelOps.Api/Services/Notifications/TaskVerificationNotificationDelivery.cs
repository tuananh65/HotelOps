using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed class TaskVerificationNotificationDelivery(HotelOpsDbContext db,TimeProvider clock)
{
    internal async Task<long[]> ReadPendingAsync(long after,int limit,CancellationToken ct)
    {
        if(limit is <1 or >100)throw new ArgumentOutOfRangeException(nameof(limit));
        if(!await TaskVerificationStore.InstalledAsync(db,ct))return [];
        return await db.Database.SqlQuery<long>($"""
            SELECT TOP({limit}) v.submission_id AS Value FROM dbo.task_verification_events v
            JOIN dbo.task_submission_events e ON e.progress_id=v.submission_id
            WHERE v.submission_id>{after} AND NOT EXISTS(SELECT 1 FROM dbo.notifications n
              JOIN dbo.notification_recipients r ON r.notification_id=n.notification_id
              WHERE n.source_type='TASK_VERIFICATION' AND n.source_id=v.submission_id
                AND n.notification_type=CASE WHEN v.decision='ACCEPT' THEN 'TASK_VERIFIED' ELSE 'TASK_RETURNED' END
                AND r.user_id=e.submitted_by AND r.delivery_channel='IN_APP' AND r.delivery_status='DELIVERED')
            ORDER BY v.submission_id
            """).ToArrayAsync(ct);
    }
    internal async Task<string> DeliverAsync(long id,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction!=null||db.ChangeTracker.Entries().Any()||System.Transactions.Transaction.Current!=null||db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("Independent clean delivery context required");
        if(!await TaskVerificationStore.InstalledAsync(db,ct))return "SCHEMA_REQUIRED";
        var hint=await TaskVerificationStore.Query(db).SingleOrDefaultAsync(v=>v.SubmissionId==id,ct);
        if(hint==null)return "SOURCE_DENIED";
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([hint.SubmittedBy,hint.VerifiedBy],ct);
        var row=await TaskVerificationStore.Query(db).SingleOrDefaultAsync(v=>v.SubmissionId==id,ct);
        if(row==null||row.AuditId!=hint.AuditId||!await TaskSubmissionStore.Query(db).AnyAsync(s=>s.ProgressId==id&&s.SubmittedBy==row.SubmittedBy,ct))return "SOURCE_DENIED";
        var now=clock.GetUtcNow().UtcDateTime;
        if(!await TaskVerificationStore.ExecutorReadAsync(db,row.SubmittedBy,row.BranchId,row.TaskId,now,ct))return "RECIPIENT_READ_DENIED";
        var kind=row.Decision=="ACCEPT"?"TASK_VERIFIED":"TASK_RETURNED";
        var notices=await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE source_type='TASK_VERIFICATION' AND source_id={id}").AsNoTracking().ToArrayAsync(ct);
        if(notices.Length>1||notices.Any(n=>n.NotificationType!=kind||n.CreatedBy!=row.VerifiedBy||n.CreatedAt!=row.DecidedAt))throw new InvalidOperationException("Verification notice conflict");
        var notice=notices.SingleOrDefault();
        if(notice==null)
        {
            notice=new Notification{NotificationType=kind,SourceType="TASK_VERIFICATION",SourceId=id,Title=row.Decision=="ACCEPT"?"Task verified":"Task returned",
                Message="A task verification decision is available.",Priority="NORMAL",CreatedBy=row.VerifiedBy,CreatedAt=row.DecidedAt};
            db.Notifications.Add(notice);await db.SaveChangesAsync(ct);
        }
        var recipients=await db.NotificationRecipients.AsNoTracking().Where(r=>r.NotificationId==notice.NotificationId).ToArrayAsync(ct);
        if(recipients.Length==0)
        {
            db.NotificationRecipients.Add(new(){NotificationId=notice.NotificationId,UserId=row.SubmittedBy,DeliveryChannel="IN_APP",DeliveryStatus="DELIVERED",DeliveredAt=now});
            await db.SaveChangesAsync(ct);
        }
        else if(recipients.Length!=1||recipients[0].UserId!=row.SubmittedBy||recipients[0].DeliveryChannel!="IN_APP"||recipients[0].DeliveryStatus!="DELIVERED"||recipients[0].DeliveredAt==null)
            throw new InvalidOperationException("Verification delivery receipt conflict");
        await tx.CommitAsync(ct);db.ChangeTracker.Clear();return "DELIVERED";
    }
}
