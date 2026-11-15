using Microsoft.Extensions.Options;
using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed class TaskAssignmentNotificationDelivery(HotelOpsDbContext db,TimeProvider clock,IOptions<NotificationCenterOptions> retention)
{
    public TaskAssignmentNotificationDelivery(HotelOpsDbContext db, TimeProvider clock) : this(db, clock, Options.Create(new NotificationCenterOptions())) { }

    internal async Task<long[]> ReadPendingAsync(long after,int limit,CancellationToken ct)
    {
        if(limit is <1 or >100)throw new ArgumentOutOfRangeException(nameof(limit));
        if(!await TaskDeclineStore.InstalledAsync(db,ct))return [];
        var window=NotificationRetention.At(clock.GetUtcNow().UtcDateTime,retention.Value);
        return await db.Database.SqlQuery<long>($"""
            SELECT TOP({limit}) e.audit_id AS Value FROM dbo.audit_logs e
            CROSS APPLY(SELECT CASE WHEN ISJSON(e.new_values)=1 THEN e.new_values ELSE N'[]' END AS payload) j
            WHERE e.occurred_at>={window.Cutoff} AND e.occurred_at<={window.Now} AND e.audit_id>{after} AND e.entity_type='operational_tasks' AND e.action='UPDATE'
              AND JSON_VALUE(j.payload,'$.operation')='ASSIGN'
              AND EXISTS(SELECT 1 FROM OPENJSON(j.payload,'$.recipients') r
              WHERE TRY_CONVERT(bigint,r.value)>0 AND NOT EXISTS(SELECT 1 FROM dbo.notifications n
                JOIN dbo.notification_recipients x ON x.notification_id=n.notification_id
                WHERE n.notification_type='TASK_ASSIGNED' AND n.source_type='TASK_ASSIGNMENT' AND n.source_id=e.audit_id
                  AND x.user_id=TRY_CONVERT(bigint,r.value) AND x.delivery_channel='IN_APP' AND x.delivery_status='DELIVERED'))
            ORDER BY e.audit_id
            """).ToArrayAsync(ct);
    }
    internal async Task<string> DeliverAsync(long progress,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction!=null||db.ChangeTracker.Entries().Any()||System.Transactions.Transaction.Current!=null||db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("Independent clean delivery context required");
        if(!await TaskDeclineStore.InstalledAsync(db,ct))return "SCHEMA_REQUIRED";
        var hint=await TaskAssignmentStore.Query(db).SingleOrDefaultAsync(s=>s.AuditId==progress,ct);
        if(hint==null)return "SOURCE_DENIED";
        var recipients=hint.PreviousAssigneeId is {} old ? new[] { old, hint.AssigneeId } : new[] { hint.AssigneeId };
        if(recipients.Any(u=>u<=0)||recipients.Distinct().Count()!=recipients.Length)return "SOURCE_DENIED";
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync(recipients.Append(hint.AssignedBy),ct);
        var row=await TaskAssignmentStore.Query(db).SingleOrDefaultAsync(s=>s.AuditId==progress,ct);
        if(row==null||row.AuditId!=hint.AuditId||row.AssigneeId!=hint.AssigneeId||row.PreviousAssigneeId!=hint.PreviousAssigneeId)return "SOURCE_DENIED";
        var now=clock.GetUtcNow().UtcDateTime;
        if(!NotificationRetention.At(now,retention.Value).Contains(row.AssignedAt))return "EVENT_OUTSIDE_RETENTION";
        var eligible=new List<long>();
        foreach(var user in recipients)
        {
            if(!await TaskAssignmentEligibility.Candidates(db,row.BranchId,row.DepartmentId,now).AnyAsync(u=>u.UserId==user,ct))continue;
            // A removed owner receives a generic notice, never an access grant.
            if(user==row.PreviousAssigneeId || await TaskVerificationStore.ExecutorReadAsync(db,user,row.BranchId,row.TaskId,now,ct))eligible.Add(user);
        }
        var notices=await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE notification_type='TASK_ASSIGNED' AND source_type='TASK_ASSIGNMENT' AND source_id={progress}").AsNoTracking().ToArrayAsync(ct);
        if(notices.Length>1||notices.Any(n=>n.CreatedBy!=row.AssignedBy||n.CreatedAt!=row.AssignedAt))throw new InvalidOperationException("Task notification receipt conflict");
        var notice=notices.SingleOrDefault();
        foreach(var recipient in eligible)
        {
            if(notice==null)
            {
                notice=new Notification {NotificationType=TaskAssignmentStore.NotificationKind,SourceType="TASK_ASSIGNMENT",SourceId=progress,
                    Title="Task assignment changed",Message="A task assignment involving you has changed. Open the source only if you still have access.",Priority="NORMAL",CreatedBy=row.AssignedBy,CreatedAt=row.AssignedAt};
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
        return eligible.Count==recipients.Length?"DELIVERED":"RECIPIENT_READ_DENIED";
    }
}
