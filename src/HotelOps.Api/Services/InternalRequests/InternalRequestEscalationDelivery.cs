using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.InternalRequests;

internal sealed class InternalRequestEscalationNotice
{
    public long EventId {get;set;}
    public long RequestId {get;set;}
    public long RecipientId {get;set;}
    public long ActorId {get;set;}
    public long BranchId {get;set;}
    public long? DepartmentId {get;set;}
    public long? HandlerId {get;set;}
    public bool IsRequester {get;set;}
    public bool IsHandler {get;set;}
    public bool IsManager {get;set;}
    public DateTime At {get;set;}
    public long? NotificationId {get;set;}
}

public sealed class InternalRequestEscalationDelivery(HotelOpsDbContext db,TimeProvider clock,IOptions<NotificationCenterOptions> options)
{
    internal static IQueryable<InternalRequestEscalationNotice> Query(HotelOpsDbContext db)=>db.Database.SqlQuery<InternalRequestEscalationNotice>($"""
        SELECT e.event_id AS EventId,e.internal_request_id AS RequestId,o.recipient_id AS RecipientId,
         e.actor_id AS ActorId,r.branch_id AS BranchId,a.department_id AS DepartmentId,a.handler_id AS HandlerId,
         o.is_requester AS IsRequester,o.is_handler AS IsHandler,o.is_manager AS IsManager,
         e.occurred_at AS At,o.notification_id AS NotificationId
        FROM dbo.internal_request_escalation_outbox o
        JOIN dbo.internal_request_escalation_actions a ON a.event_id=o.event_id
        JOIN dbo.internal_request_events e ON e.event_id=a.event_id
        JOIN dbo.internal_request_escalations x ON x.opened_event_id=e.event_id AND x.internal_request_id=e.internal_request_id AND x.opened_at=e.occurred_at
        JOIN dbo.internal_requests r ON r.internal_request_id=e.internal_request_id
        JOIN dbo.audit_logs l ON l.audit_id=a.audit_id AND l.actor_user_id=e.actor_id AND l.branch_id=r.branch_id
         AND l.entity_type='internal_requests' AND l.entity_id=CONVERT(nvarchar(30),r.internal_request_id) AND l.action='UPDATE' AND l.occurred_at=e.occurred_at
        WHERE e.event_type='ESCALATE' AND e.event_note=a.reason AND o.created_at=e.occurred_at
          AND (e.old_status=e.new_status OR (e.old_status='ACCEPTED' AND e.new_status='PENDING_ACCEPTANCE'
            AND JSON_VALUE(l.old_values,'$.status')='ACCEPTED' AND JSON_VALUE(l.new_values,'$.status')='PENDING_ACCEPTANCE'
            AND TRY_CONVERT(bigint,JSON_VALUE(l.new_values,'$.receivingDepartmentId'))=a.department_id
            AND TRY_CONVERT(bigint,JSON_VALUE(l.old_values,'$.receivingDepartmentId'))<>a.department_id
            AND a.handler_id IS NULL))
        """);

    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db,InternalRequestEscalationNotice row,DateTime now,CancellationToken ct)
    {
        var scope=await new InternalRequestReadAccess(db).ReadAsync(row.RecipientId,now,ct);
        var request=await scope.Apply(db.InternalRequests.AsNoTracking(),now).SingleOrDefaultAsync(r=>r.InternalRequestId==row.RequestId&&r.BranchId==row.BranchId,ct);
        if(request==null)return false;
        if(row.IsRequester&&request.RequesterId==row.RecipientId)return true;
        if(row.IsHandler&&row.HandlerId==row.RecipientId
            &&await db.InternalRequestAssignments.CountAsync(a=>a.InternalRequestId==row.RequestId&&a.Status=="ACTIVE"&&a.EndedAt==null&&a.AssignedAt<=now,ct)==1
            &&await db.InternalRequestAssignments.AnyAsync(a=>a.InternalRequestId==row.RequestId&&a.AssigneeId==row.RecipientId&&a.Status=="ACTIVE"&&a.EndedAt==null&&a.AssignedAt<=now,ct))return true;
        return row.IsManager&&row.DepartmentId is {} department&&request.ReceivingDepartmentId==department
            &&await new InternalRequestProcessAuthority(db).AllowsDepartmentAsync(row.RecipientId,row.BranchId,department,ct);
    }

    public async Task<long[]> PendingAsync(long after,CancellationToken ct)
    {
        if(!await InternalRequestEscalationService.InstalledAsync(db,ct)||!await NotificationAcknowledgmentStore.InstalledAsync(db,ct))return [];
        var window=NotificationRetention.At(clock.GetUtcNow().UtcDateTime,options.Value);
        return await Query(db).Where(r=>r.NotificationId==null&&r.EventId>after&&r.At>=window.Cutoff&&r.At<=window.Now)
            .Select(r=>r.EventId).Distinct().Order().Take(100).ToArrayAsync(ct);
    }

    internal async Task<long[]> PendingRecipientsAsync(long eventId, CancellationToken ct)
    {
        if (!await InternalRequestEscalationService.InstalledAsync(db, ct) || !await NotificationAcknowledgmentStore.InstalledAsync(db, ct)) return [];
        return await Query(db).Where(r => r.EventId == eventId && r.NotificationId == null)
            .Select(r => r.RecipientId).Distinct().Order().ToArrayAsync(ct);
    }

    public async Task DeliverEventAsync(long id,CancellationToken ct)
    {
        if(!await InternalRequestEscalationService.InstalledAsync(db,ct)||!await NotificationAcknowledgmentStore.InstalledAsync(db,ct))return;
        var users=await Query(db).Where(r=>r.EventId==id).Select(r=>r.RecipientId).Order().ToArrayAsync(ct);
        foreach(var user in users)await DeliverAsync(id,user,ct);
    }

    public async Task<string> DeliverAsync(long id,long recipient,CancellationToken ct)
    {
        if(db.ChangeTracker.Entries().Any()||db.Database.CurrentTransaction!=null)throw new InvalidOperationException("Clean delivery context required.");
        if(!await InternalRequestEscalationService.InstalledAsync(db,ct)||!await NotificationAcknowledgmentStore.InstalledAsync(db,ct))return "SCHEMA_REQUIRED";
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            await new UserEligibilityLock(db).LockAsync([recipient],ct);
            var keys=await db.Database.SqlQuery<long>($"SELECT event_id AS Value FROM dbo.internal_request_escalation_outbox WITH(UPDLOCK,HOLDLOCK) WHERE event_id={id} AND recipient_id={recipient}").ToArrayAsync(ct);
            if(keys.Length!=1)return "SOURCE_DENIED";
            var row=await Query(db).SingleOrDefaultAsync(r=>r.EventId==id&&r.RecipientId==recipient,ct);
            if(row==null)return "SOURCE_DENIED";
            var now=clock.GetUtcNow().UtcDateTime;
            if(!NotificationRetention.At(now,options.Value).Contains(row.At))return "EVENT_OUTSIDE_RETENTION";
            if(!await CanReadAsync(db,row,now,ct))return "RECIPIENT_READ_DENIED";
            if(row.NotificationId is {} existing)
            {
                if(!await db.Notifications.AnyAsync(n=>n.NotificationId==existing&&n.NotificationType=="IR_ESCALATED"&&n.SourceType=="INTERNAL_REQUEST_ESCALATION"&&n.SourceId==id&&n.CreatedBy==row.ActorId&&n.CreatedAt==row.At,ct)
                    ||!await db.Database.SqlQuery<int>($"SELECT 1 AS Value FROM dbo.notifications WHERE notification_id={existing} AND requires_acknowledgment=1").AnyAsync(ct)
                    ||!await db.NotificationRecipients.AnyAsync(r=>r.NotificationId==existing&&r.UserId==recipient&&r.DeliveryChannel=="IN_APP"&&r.DeliveryStatus=="DELIVERED",ct))
                    throw new InvalidOperationException("Escalation delivery receipt conflict.");
                await tx.CommitAsync(ct);return "ALREADY_DELIVERED";
            }
            var notice=new Notification{NotificationType="IR_ESCALATED",SourceType="INTERNAL_REQUEST_ESCALATION",SourceId=id,
                Title="Internal request escalated",Message="An intervention was recorded. Review the request and acknowledge this alert.",Priority="NORMAL",CreatedBy=row.ActorId,CreatedAt=row.At};
            db.Notifications.Add(notice);await db.SaveChangesAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notifications SET requires_acknowledgment=1 WHERE notification_id={notice.NotificationId}",ct);
            db.NotificationRecipients.Add(new(){NotificationId=notice.NotificationId,UserId=recipient,DeliveryChannel="IN_APP",DeliveryStatus="DELIVERED",DeliveredAt=now});
            await db.SaveChangesAsync(ct);
            if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_request_escalation_outbox SET notification_id={notice.NotificationId} WHERE event_id={id} AND recipient_id={recipient} AND notification_id IS NULL",ct)!=1)
                throw new InvalidOperationException("Escalation delivery changed.");
            if(!await CanReadAsync(db,row,clock.GetUtcNow().UtcDateTime,ct))throw new InvalidOperationException("Recipient authority changed.");
            await tx.CommitAsync(ct);return "DELIVERED";
        }
        finally{db.ChangeTracker.Clear();}
    }
}
