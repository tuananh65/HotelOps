using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.InternalRequests;

internal sealed class InternalRequestProgressNotice
{
    public long EventId {get;set;}
    public long RequestId {get;set;}
    public long RequesterId {get;set;}
    public long ActorId {get;set;}
    public string Decision {get;set;}="";
    public DateTime At {get;set;}
    public long? NotificationId {get;set;}
}

public sealed class InternalRequestProgressDelivery(HotelOpsDbContext db,TimeProvider clock,IOptions<NotificationCenterOptions> options)
{
    internal static IQueryable<InternalRequestProgressNotice> Query(HotelOpsDbContext db) => db.Database.SqlQuery<InternalRequestProgressNotice>($"""
        SELECT e.event_id AS EventId,e.internal_request_id AS RequestId,o.requester_id AS RequesterId,
          e.actor_id AS ActorId,e.event_type AS Decision,e.occurred_at AS At,o.notification_id AS NotificationId
        FROM dbo.internal_request_progress_outbox o JOIN dbo.internal_request_events e ON e.event_id=o.event_id
        JOIN dbo.internal_requests r ON r.internal_request_id=e.internal_request_id
        WHERE e.old_status='IN_PROGRESS' AND e.event_type='PROGRESS' AND e.new_status='IN_PROGRESS'
          AND o.created_at=e.occurred_at AND o.requester_id=r.requester_id
        """);

    public async Task<long[]> PendingAsync(long after,int limit,CancellationToken ct)
    {
        if(limit is <1 or >100)throw new ArgumentOutOfRangeException(nameof(limit));
        if(!await InternalRequestProgressService.InstalledAsync(db,ct))return [];
        var window=NotificationRetention.At(clock.GetUtcNow().UtcDateTime,options.Value);
        return await Query(db).Where(r=>r.NotificationId==null&&r.EventId>after&&r.At>=window.Cutoff&&r.At<=window.Now)
            .OrderBy(r=>r.EventId).Take(limit).Select(r=>r.EventId).ToArrayAsync(ct);
    }

    public async Task<string> DeliverAsync(long eventId,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction!=null||db.ChangeTracker.Entries().Any())throw new InvalidOperationException("Independent clean delivery context required.");
        if(!await InternalRequestProgressService.InstalledAsync(db,ct))return "SCHEMA_REQUIRED";
        var hint=await Query(db).SingleOrDefaultAsync(r=>r.EventId==eventId,ct);
        if(hint==null)return "SOURCE_DENIED";
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            await new UserEligibilityLock(db).LockAsync([hint.RequesterId],ct);
            await db.Database.SqlQuery<long>($"SELECT event_id AS Value FROM dbo.internal_request_progress_outbox WITH(UPDLOCK,HOLDLOCK) WHERE event_id={eventId}").SingleAsync(ct);
            var row=await Query(db).SingleOrDefaultAsync(r=>r.EventId==eventId,ct);
            if(row==null||row.RequesterId!=hint.RequesterId||row.RequestId!=hint.RequestId)return "SOURCE_DENIED";
            var now=clock.GetUtcNow().UtcDateTime;
            if(!NotificationRetention.At(now,options.Value).Contains(row.At))return "EVENT_OUTSIDE_RETENTION";
            var scope=await new InternalRequestReadAccess(db).ReadAsync(row.RequesterId,now,ct);
            if(!await scope.Apply(db.InternalRequests.AsNoTracking(),now).AnyAsync(r=>r.InternalRequestId==row.RequestId,ct))return "RECIPIENT_READ_DENIED";
            if(row.NotificationId is {} existing)
            {
                if(!await db.Notifications.AnyAsync(n=>n.NotificationId==existing&&n.SourceType=="INTERNAL_REQUEST_PROGRESS"&&n.SourceId==eventId
                    &&n.CreatedBy==row.ActorId&&n.CreatedAt==row.At&&n.NotificationType=="IR_READY",ct)
                    ||!await db.NotificationRecipients.AnyAsync(r=>r.NotificationId==existing&&r.UserId==row.RequesterId&&r.DeliveryChannel=="IN_APP"&&r.DeliveryStatus=="DELIVERED",ct))
                    throw new InvalidOperationException("Progress delivery receipt conflict.");
                await tx.CommitAsync(ct);return "ALREADY_DELIVERED";
            }
            var notice=new Notification{NotificationType="IR_READY",SourceType="INTERNAL_REQUEST_PROGRESS",SourceId=eventId,
                Title="Request progress reached 100%",Message="Progress reached 100%. Open the request to review the result; it is not closed.",
                Priority="NORMAL",CreatedBy=row.ActorId,CreatedAt=row.At};
            db.Notifications.Add(notice);await db.SaveChangesAsync(ct);
            db.NotificationRecipients.Add(new(){NotificationId=notice.NotificationId,UserId=row.RequesterId,DeliveryChannel="IN_APP",DeliveryStatus="DELIVERED",DeliveredAt=now});
            await db.SaveChangesAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_request_progress_outbox SET notification_id={notice.NotificationId} WHERE event_id={eventId} AND notification_id IS NULL",ct);
            var finalNow=clock.GetUtcNow().UtcDateTime;
            var finalScope=await new InternalRequestReadAccess(db).ReadAsync(row.RequesterId,finalNow,ct);
            if(!await finalScope.Apply(db.InternalRequests.AsNoTracking(),finalNow).AnyAsync(r=>r.InternalRequestId==row.RequestId,ct))
                throw new InvalidOperationException("Recipient authority changed before delivery commit.");
            await tx.CommitAsync(ct);return "DELIVERED";
        }
        finally{db.ChangeTracker.Clear();}
    }
}
