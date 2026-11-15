using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed class TaskSubmissionNotificationDelivery(HotelOpsDbContext db,TimeProvider clock)
{
    internal async Task<long[]> ReadPendingAsync(long after,int limit,CancellationToken ct)
    {
        if(limit is <1 or >100)throw new ArgumentOutOfRangeException(nameof(limit));
        if(!await TaskSubmissionStore.InstalledAsync(db,ct))return [];
        return await db.Database.SqlQuery<long>($"""
            SELECT TOP({limit}) e.progress_id AS Value FROM dbo.task_submission_events e
            WHERE e.progress_id>{after} AND EXISTS(SELECT 1 FROM OPENJSON(e.recipients_json) r
              WHERE TRY_CONVERT(bigint,r.value)>0 AND NOT EXISTS(SELECT 1 FROM dbo.notifications n
                JOIN dbo.notification_recipients x ON x.notification_id=n.notification_id
                WHERE n.notification_type='TASK_SUBMITTED' AND n.source_type='TASK_SUBMISSION' AND n.source_id=e.progress_id
                  AND x.user_id=TRY_CONVERT(bigint,r.value) AND x.delivery_channel='IN_APP' AND x.delivery_status='DELIVERED'))
            ORDER BY e.progress_id
            """).ToArrayAsync(ct);
    }
    internal async Task<string> DeliverAsync(long progress,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction!=null||db.ChangeTracker.Entries().Any()||System.Transactions.Transaction.Current!=null||db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("Independent clean delivery context required");
        if(!await TaskSubmissionStore.InstalledAsync(db,ct))return "SCHEMA_REQUIRED";
        var hint=await TaskSubmissionStore.Query(db).SingleOrDefaultAsync(s=>s.ProgressId==progress,ct);
        if(hint==null)return "SOURCE_DENIED";
        var recipients=JsonSerializer.Deserialize<long[]>(hint.RecipientsJson,TaskSubmissionStore.Json)??[];
        if(recipients.Any(u=>u<=0)||recipients.Distinct().Count()!=recipients.Length)return "SOURCE_DENIED";
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync(recipients.Append(hint.SubmittedBy),ct);
        var row=await TaskSubmissionStore.Query(db).SingleOrDefaultAsync(s=>s.ProgressId==progress,ct);
        if(row==null||row.AuditId!=hint.AuditId||row.RecipientsJson!=hint.RecipientsJson)return "SOURCE_DENIED";
        var now=clock.GetUtcNow().UtcDateTime;
        var managers=await TaskSubmissionAccess.ManagersAsync(db,row.BranchId,now,ct);
        var eligible=recipients.Where(u=>u!=row.SubmittedBy&&managers.Contains(u)).ToArray();
        var notices=await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE notification_type='TASK_SUBMITTED' AND source_type='TASK_SUBMISSION' AND source_id={progress}").AsNoTracking().ToArrayAsync(ct);
        if(notices.Length>1||notices.Any(n=>n.CreatedBy!=row.SubmittedBy||n.CreatedAt!=row.SubmittedAt))throw new InvalidOperationException("Task notification receipt conflict");
        var notice=notices.SingleOrDefault();
        foreach(var recipient in eligible)
        {
            if(notice==null)
            {
                notice=new Notification {NotificationType=TaskSubmissionStore.NotificationKind,SourceType="TASK_SUBMISSION",SourceId=progress,
                    Title="Task awaiting verification",Message="A submitted task is ready for review.",Priority="NORMAL",CreatedBy=row.SubmittedBy,CreatedAt=row.SubmittedAt};
                db.Notifications.Add(notice);await db.SaveChangesAsync(ct);
            }
            var receipts=await db.NotificationRecipients.AsNoTracking().Where(r=>r.NotificationId==notice.NotificationId&&r.UserId==recipient).ToArrayAsync(ct);
            if(receipts.Length>0)
            {
                if(receipts.Length!=1||receipts[0].DeliveryChannel!="IN_APP"||receipts[0].DeliveryStatus!="DELIVERED"||receipts[0].DeliveredAt==null)throw new InvalidOperationException("Task delivery receipt conflict");
                continue;
            }
            db.NotificationRecipients.Add(new NotificationRecipient{NotificationId=notice.NotificationId,UserId=recipient,DeliveryChannel="IN_APP",DeliveryStatus="DELIVERED",DeliveredAt=now});
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);db.ChangeTracker.Clear();
        return eligible.Length==recipients.Length?"DELIVERED":"RECIPIENT_READ_DENIED";
    }
}
