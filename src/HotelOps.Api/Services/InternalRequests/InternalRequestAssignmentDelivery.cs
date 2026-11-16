using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.InternalRequests;

internal sealed class InternalRequestAssignmentNotice
{
    public long EventId { get; set; }
    public long RequestId { get; set; }
    public long RecipientId { get; set; }
    public long ActorId { get; set; }
    public DateTime At { get; set; }
    public long? NotificationId { get; set; }
}

public sealed class InternalRequestAssignmentDelivery(HotelOpsDbContext db, TimeProvider clock, IOptions<NotificationCenterOptions> options)
{
    internal static IQueryable<InternalRequestAssignmentNotice> Query(HotelOpsDbContext db) => db.Database.SqlQuery<InternalRequestAssignmentNotice>($"""
        SELECT e.event_id AS EventId,e.internal_request_id AS RequestId,o.recipient_id AS RecipientId,
          e.actor_id AS ActorId,e.occurred_at AS At,o.notification_id AS NotificationId
        FROM dbo.internal_request_assignment_outbox o JOIN dbo.internal_request_events e ON e.event_id=o.event_id
        JOIN dbo.internal_request_assignments a ON a.assignment_id=o.assignment_id AND a.internal_request_id=e.internal_request_id
        JOIN dbo.internal_requests r ON r.internal_request_id=e.internal_request_id
        WHERE e.event_type IN('ASSIGN','REASSIGN') AND e.new_status='IN_PROGRESS' AND e.old_status IN('ACCEPTED','IN_PROGRESS')
          AND e.actor_id=a.assigned_by AND e.occurred_at=a.assigned_at AND o.created_at=e.occurred_at
          AND (o.recipient_id=r.requester_id OR o.recipient_id=a.assignee_id)
        """);

    public async Task<long[]> PendingAsync(long after, CancellationToken ct)
    {
        if (!await InternalRequestAssignmentService.InstalledAsync(db, ct)) return [];
        var window = NotificationRetention.At(clock.GetUtcNow().UtcDateTime, options.Value);
        return await Query(db).Where(r => r.NotificationId == null && r.EventId > after && r.At >= window.Cutoff && r.At <= window.Now)
            .Select(r => r.EventId).Distinct().OrderBy(id => id).Take(100).ToArrayAsync(ct);
    }

    internal async Task<long[]> PendingRecipientsAsync(long eventId, CancellationToken ct)
    {
        if (!await InternalRequestAssignmentService.InstalledAsync(db, ct)) return [];
        return await Query(db).Where(r => r.EventId == eventId && r.NotificationId == null)
            .Select(r => r.RecipientId).Distinct().Order().ToArrayAsync(ct);
    }

    public async Task DeliverEventAsync(long eventId, CancellationToken ct)
    {
        if (!await InternalRequestAssignmentService.InstalledAsync(db, ct)) return;
        var recipients = await Query(db).Where(r => r.EventId == eventId).Select(r => r.RecipientId).Order().ToArrayAsync(ct);
        foreach (var recipient in recipients) await DeliverAsync(eventId, recipient, ct);
    }

    public async Task<string> DeliverAsync(long eventId, long recipient, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Clean delivery context required.");
        if (!await InternalRequestAssignmentService.InstalledAsync(db, ct)) return "SCHEMA_REQUIRED";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await new UserEligibilityLock(db).LockAsync([recipient], ct);
            var exists = await db.Database.SqlQuery<long>($"SELECT event_id AS Value FROM dbo.internal_request_assignment_outbox WITH(UPDLOCK,HOLDLOCK) WHERE event_id={eventId} AND recipient_id={recipient}").ToArrayAsync(ct);
            if (exists.Length != 1) return "SOURCE_DENIED";
            var row = await Query(db).SingleOrDefaultAsync(r => r.EventId == eventId && r.RecipientId == recipient, ct);
            if (row == null) return "SOURCE_DENIED";
            var now = clock.GetUtcNow().UtcDateTime;
            if (!NotificationRetention.At(now, options.Value).Contains(row.At)) return "EVENT_OUTSIDE_RETENTION";
            async Task<bool> CanRead()
            {
                var current = clock.GetUtcNow().UtcDateTime;
                var scope = await new InternalRequestReadAccess(db).ReadAsync(recipient, current, ct);
                return await scope.Apply(db.InternalRequests.AsNoTracking(), current).AnyAsync(r => r.InternalRequestId == row.RequestId, ct);
            }
            if (!await CanRead()) return "RECIPIENT_READ_DENIED";
            if (row.NotificationId is {} existing)
            {
                if (!await db.Notifications.AnyAsync(n => n.NotificationId == existing && n.NotificationType == "IR_ASSIGNED" && n.SourceType == "INTERNAL_REQUEST_ASSIGNMENT"
                    && n.SourceId == eventId && n.CreatedAt == row.At && n.CreatedBy == row.ActorId, ct)
                    || !await db.NotificationRecipients.AnyAsync(r => r.NotificationId == existing && r.UserId == recipient && r.DeliveryStatus == "DELIVERED" && r.DeliveryChannel == "IN_APP", ct))
                    throw new InvalidOperationException("Assignment delivery receipt conflict.");
                await tx.CommitAsync(ct); return "ALREADY_DELIVERED";
            }
            var notice = new Notification { NotificationType = "IR_ASSIGNED", SourceType = "INTERNAL_REQUEST_ASSIGNMENT", SourceId = eventId,
                Title = "Request handler assigned", Message = "An internal request handler changed. Open the request to view the details.",
                Priority = "NORMAL", CreatedBy = row.ActorId, CreatedAt = row.At };
            db.Notifications.Add(notice); await db.SaveChangesAsync(ct);
            db.NotificationRecipients.Add(new() { NotificationId = notice.NotificationId, UserId = recipient, DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
            await db.SaveChangesAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_request_assignment_outbox SET notification_id={notice.NotificationId} WHERE event_id={eventId} AND recipient_id={recipient} AND notification_id IS NULL", ct);
            if (!await CanRead()) throw new InvalidOperationException("Recipient authority changed before commit.");
            await tx.CommitAsync(ct); return "DELIVERED";
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
