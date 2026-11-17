using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.WorkTime;

internal sealed class ShiftHandoverNotice
{
    public long EventId{get;set;} public long HandoverId{get;set;} public long RecipientId{get;set;} public long ActorId{get;set;}
    public long BranchId{get;set;} public long DepartmentId{get;set;} public string Kind{get;set;}="";
    public DateTime At{get;set;} public long? NotificationId{get;set;}
}
public sealed class ShiftHandoverDelivery(HotelOpsDbContext db,TimeProvider clock,IOptions<NotificationCenterOptions> options)
{
    // A missing/ineligible official Head does not roll back acknowledgment. Keep
    // the committed decision as the durable retry source until a Head can read it.
    public async Task<long[]> PendingHeadsAsync(long after,CancellationToken ct)
    {
        if(!await ShiftHandoverStore.InstalledAsync(db,ct))return [];
        var window=NotificationRetention.At(clock.GetUtcNow().UtcDateTime,options.Value);
        return await db.Database.SqlQuery<long>($"""
            SELECT TOP(100) h.handover_id AS Value FROM dbo.operational_shift_handovers h
            WHERE h.state='ACKNOWLEDGED' AND h.handover_id>{after} AND h.decision_at>={window.Cutoff} AND h.decision_at<={window.Now}
              AND NOT EXISTS(SELECT 1 FROM dbo.operational_shift_handover_outbox o
                WHERE o.event_audit_id=h.decision_audit_id AND o.recipient_kind='HEAD')
              AND NOT EXISTS(SELECT 1 FROM dbo.departments d JOIN dbo.operational_shift_handover_outbox o
                ON o.recipient_id=d.manager_user_id AND o.event_audit_id=h.decision_audit_id
                WHERE d.department_id=h.department_id AND d.branch_id=h.branch_id)
            ORDER BY h.handover_id
            """).ToArrayAsync(ct);
    }
    public async Task ExpandHeadAsync(long id,CancellationToken ct)
    {
        var hint=await ShiftHandoverStore.Rows(db).SingleOrDefaultAsync(h=>h.Id==id,ct);
        if(hint==null)return;
        var head=await db.Departments.AsNoTracking().Where(d=>d.DepartmentId==hint.DepartmentId&&d.BranchId==hint.BranchId).Select(d=>d.ManagerUserId).SingleOrDefaultAsync(ct);
        if(head==null)return;
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([head.Value],ct);
        var row=await ShiftHandoverStore.Rows(db).SingleOrDefaultAsync(h=>h.Id==id,ct);var now=clock.GetUtcNow().UtcDateTime;
        if(row?.State!="ACKNOWLEDGED"||row.DecisionAuditId==null||row.DecisionAt==null||!NotificationRetention.At(now,options.Value).Contains(row.DecisionAt.Value)
            ||!await db.Departments.AsNoTracking().AnyAsync(d=>d.DepartmentId==row.DepartmentId&&d.BranchId==row.BranchId&&d.ManagerUserId==head&&d.IsActive,ct)
            ||!await ShiftHandoverAccess.HeadAsync(db,head.Value,row.BranchId,row.DepartmentId,now,ct))return;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.operational_shift_handover_outbox(event_audit_id,handover_id,recipient_id,recipient_kind,created_at)
            SELECT {row.DecisionAuditId},{id},{head.Value},'HEAD',{row.DecisionAt}
            WHERE NOT EXISTS(SELECT 1 FROM dbo.operational_shift_handover_outbox WITH(UPDLOCK,HOLDLOCK)
              WHERE event_audit_id={row.DecisionAuditId} AND (recipient_kind='HEAD' OR recipient_id={head.Value}))
            """,ct);
        if(!await ShiftHandoverAccess.HeadAsync(db,head.Value,row.BranchId,row.DepartmentId,clock.GetUtcNow().UtcDateTime,ct))return;
        await tx.CommitAsync(ct);
    }
    internal static IQueryable<ShiftHandoverNotice> Query(HotelOpsDbContext db)=>db.Database.SqlQuery<ShiftHandoverNotice>($"""
        SELECT o.event_audit_id AS EventId,o.handover_id AS HandoverId,o.recipient_id AS RecipientId,o.recipient_kind AS Kind,
          o.created_at AS At,o.notification_id AS NotificationId,a.actor_user_id AS ActorId,h.branch_id AS BranchId,h.department_id AS DepartmentId
        FROM dbo.operational_shift_handover_outbox o JOIN dbo.operational_shift_handovers h ON h.handover_id=o.handover_id
          JOIN dbo.audit_logs a ON a.audit_id=o.event_audit_id
        WHERE a.entity_type='operational_shift_handovers' AND a.branch_id=h.branch_id AND a.occurred_at=o.created_at
          AND ((o.event_audit_id=h.submission_audit_id AND o.created_at=h.submitted_at AND o.recipient_kind='RECEIVER'
            AND o.recipient_id=h.receiver_id AND a.actor_user_id=h.sender_id AND a.action='CREATE'
            AND a.entity_id=CONVERT(nvarchar(50),h.request_key))
          OR (o.event_audit_id=h.decision_audit_id AND o.created_at=h.decision_at AND a.actor_user_id=h.receiver_id AND a.action='UPDATE'
            AND a.entity_id=CONVERT(nvarchar(50),h.handover_id)
            AND ((o.recipient_kind='SENDER' AND o.recipient_id=h.sender_id AND h.state IN('ACKNOWLEDGED','REJECTED'))
              OR (o.recipient_kind='HEAD' AND h.state='ACKNOWLEDGED'))))
        """);
    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db,ShiftHandoverNotice notice,DateTime now,CancellationToken ct)
    {
        var row=await ShiftHandoverStore.Rows(db).SingleOrDefaultAsync(h=>h.Id==notice.HandoverId,ct);
        if(row==null||!await ShiftHandoverAccess.ReadAsync(db,notice.RecipientId,row,now,ct))return false;
        return notice.Kind!="HEAD"||await db.Departments.AsNoTracking().AnyAsync(d=>d.DepartmentId==row.DepartmentId&&d.BranchId==row.BranchId
            &&d.ManagerUserId==notice.RecipientId&&d.IsActive,ct)&&await ShiftHandoverAccess.HeadAsync(db,notice.RecipientId,row.BranchId,row.DepartmentId,now,ct);
    }
    public async Task<long[]> PendingAsync(long after,CancellationToken ct)
    {
        if(!await ShiftHandoverStore.InstalledAsync(db,ct))return [];
        var window=NotificationRetention.At(clock.GetUtcNow().UtcDateTime,options.Value);
        return await Query(db).Where(n=>n.NotificationId==null&&n.EventId>after&&n.At>=window.Cutoff&&n.At<=window.Now)
            .Select(n=>n.EventId).Distinct().Order().Take(100).ToArrayAsync(ct);
    }
    public async Task DeliverAsync(long eventId,CancellationToken ct)
    {
        if(!await ShiftHandoverStore.InstalledAsync(db,ct))return;
        var recipients=await Query(db).Where(n=>n.EventId==eventId&&n.NotificationId==null).Select(n=>n.RecipientId).Distinct().Order().ToArrayAsync(ct);
        foreach(var recipient in recipients)
        {
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            try
            {
                await new UserEligibilityLock(db).LockAsync([recipient],ct);
                await db.Database.SqlQuery<long>($"SELECT recipient_id AS Value FROM dbo.operational_shift_handover_outbox WITH(UPDLOCK,HOLDLOCK) WHERE event_audit_id={eventId} AND recipient_id={recipient}").ToArrayAsync(ct);
                var row=await Query(db).SingleOrDefaultAsync(n=>n.EventId==eventId&&n.RecipientId==recipient,ct);
                var now=clock.GetUtcNow().UtcDateTime;
                if(row==null||row.NotificationId!=null||!NotificationRetention.At(now,options.Value).Contains(row.At)||!await CanReadAsync(db,row,now,ct))continue;
                var notice=new Notification{NotificationType="SHIFT_HANDOVER",SourceType="SHIFT_HANDOVER",SourceId=eventId,
                    Title="Shift handover",Message="A shift handover was submitted or decided. Open to review the receipt.",
                    Priority="NORMAL",CreatedBy=row.ActorId,CreatedAt=row.At};
                db.Notifications.Add(notice);await db.SaveChangesAsync(ct);
                db.NotificationRecipients.Add(new(){NotificationId=notice.NotificationId,UserId=recipient,DeliveryChannel="IN_APP",DeliveryStatus="DELIVERED",DeliveredAt=now});
                await db.SaveChangesAsync(ct);
                if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_shift_handover_outbox SET notification_id={notice.NotificationId} WHERE event_audit_id={eventId} AND recipient_id={recipient} AND notification_id IS NULL",ct)!=1)
                    throw new InvalidOperationException("Handover delivery changed.");
                if(!await CanReadAsync(db,row,clock.GetUtcNow().UtcDateTime,ct))throw new InvalidOperationException("Handover recipient authority changed.");
                await tx.CommitAsync(ct);
            }
            finally{db.ChangeTracker.Clear();}
        }
    }
}
