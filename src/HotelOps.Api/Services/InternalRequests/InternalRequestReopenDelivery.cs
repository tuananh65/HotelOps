using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.InternalRequests;

internal sealed class InternalRequestReopenDelivery(HotelOpsDbContext db, TimeProvider clock, IOptions<NotificationCenterOptions> retention)
{
    internal sealed record Source
    {
        public long EventId { get; init; }
        public long RequestId { get; init; }
        public long BranchId { get; init; }
        public long DepartmentId { get; init; }
        public long HandlerId { get; init; }
        public long ActorId { get; init; }
        public DateTime At { get; init; }
    }
    internal static IQueryable<Source> Query(HotelOpsDbContext db) => db.Database.SqlQuery<Source>($"""
        SELECT e.event_id AS EventId,e.internal_request_id AS RequestId,audit.branch_id AS BranchId,
          r.receiving_department_id AS DepartmentId,a.assignee_id AS HandlerId,e.actor_id AS ActorId,e.occurred_at AS At
        FROM dbo.internal_request_reopens r
        JOIN dbo.internal_request_events e ON e.event_id=r.event_id AND e.event_type='REOPEN' AND e.old_status='CLOSED' AND e.new_status='IN_PROGRESS'
        JOIN dbo.internal_requests source ON source.internal_request_id=e.internal_request_id
        JOIN dbo.internal_request_closures c ON c.event_id=r.closure_event_id AND c.internal_request_id=e.internal_request_id
        JOIN dbo.internal_request_assignments previous ON previous.assignment_id=c.assignment_id AND previous.internal_request_id=e.internal_request_id
        JOIN dbo.internal_request_assignments a ON a.assignment_id=r.assignment_id AND a.internal_request_id=e.internal_request_id
        JOIN dbo.audit_logs audit ON audit.audit_id=r.audit_id AND audit.entity_type='internal_requests'
          AND audit.entity_id=CONVERT(nvarchar(30),e.internal_request_id) AND audit.action='UPDATE'
          AND audit.actor_user_id=e.actor_id AND audit.occurred_at=e.occurred_at AND audit.branch_id=source.branch_id
        JOIN dbo.departments d ON d.department_id=r.receiving_department_id AND d.branch_id=source.branch_id
        WHERE previous.status='COMPLETED' AND previous.ended_at=c.closed_at AND previous.assignee_id=a.assignee_id
          AND a.assigned_at=e.occurred_at AND a.assigned_by=e.actor_id AND a.assignment_note=e.event_note
          AND LEN(LTRIM(RTRIM(e.event_note)))>=10 AND c.closed_at<=e.occurred_at
          AND DATEDIFF_BIG(second,c.closed_at,e.occurred_at)<=CAST(r.window_days AS bigint)*86400
        """);

    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db, Source source, long user, DateTime now, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.UserId == user && u.DeletedAt == null && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE", ct)
            || !await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == user && a.BranchId == source.BranchId
                && a.RevokedAt == null && a.GrantedAt <= now && a.Branch.IsActive, ct)) return false;
        var read = await new InternalRequestReadAccess(db).ReadAsync(user, now, ct);
        if (!await read.Apply(db.InternalRequests.AsNoTracking(), now).AnyAsync(r => r.InternalRequestId == source.RequestId, ct)) return false;
        if (user == source.HandlerId) return true;
        return await db.Departments.AsNoTracking().AnyAsync(d => d.DepartmentId == source.DepartmentId && d.BranchId == source.BranchId
            && d.ManagerUserId == user && d.IsActive, ct)
            && await new InternalRequestProcessAuthority(db).AllowsDepartmentAsync(user, source.BranchId, source.DepartmentId, ct);
    }
    internal async Task<long[]> PendingAsync(long after, CancellationToken ct)
    {
        if (!await InternalRequestReopenService.InstalledAsync(db, ct)) return [];
        var window = NotificationRetention.At(clock.GetUtcNow().UtcDateTime, retention.Value);
        return await Query(db).Where(r => r.EventId > after && r.At >= window.Cutoff && r.At <= window.Now)
            .OrderBy(r => r.EventId).Select(r => r.EventId).Take(100).ToArrayAsync(ct);
    }
    internal async Task DeliverAsync(long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Independent notification context required.");
        var hint = await Query(db).SingleOrDefaultAsync(r => r.EventId == id, ct); if (hint == null) return;
        var head = await db.Departments.AsNoTracking().Where(d => d.DepartmentId == hint.DepartmentId && d.BranchId == hint.BranchId && d.IsActive)
            .Select(d => d.ManagerUserId).SingleOrDefaultAsync(ct);
        var candidates = new[] { hint.HandlerId, head ?? hint.HandlerId }.Distinct().Order().ToArray();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync(candidates.Append(hint.ActorId), ct);
        var source = await Query(db).SingleOrDefaultAsync(r => r.EventId == id, ct); if (source != hint) return;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!NotificationRetention.At(now, retention.Value).Contains(source.At)) return;
        var eligible = new List<long>();
        foreach (var user in candidates) if (await CanReadAsync(db, source, user, now, ct)) eligible.Add(user);
        if (eligible.Count == 0) return; // Receipt remains discoverable when no current recipient is eligible.
        var notice = await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE source_type='INTERNAL_REQUEST_REOPEN' AND source_id={id}").SingleOrDefaultAsync(ct);
        if (notice == null)
        {
            notice = new Notification { NotificationType = "IR_REOPENED", SourceType = "INTERNAL_REQUEST_REOPEN", SourceId = id,
                Title = "Internal request reopened", Message = "An internal request was reopened with its last handler. Open it to review the reason and retained results.",
                Priority = "NORMAL", CreatedAt = source.At, CreatedBy = source.ActorId };
            db.Notifications.Add(notice); await db.SaveChangesAsync(ct);
        }
        else if (notice.NotificationType != "IR_REOPENED" || notice.CreatedBy != source.ActorId || notice.CreatedAt != source.At)
            throw new InvalidOperationException("Reopen notification provenance conflict.");
        foreach (var user in eligible)
        {
            var recipient = await db.NotificationRecipients.SingleOrDefaultAsync(r => r.NotificationId == notice.NotificationId && r.UserId == user && r.DeliveryChannel == "IN_APP", ct);
            if (recipient == null) db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notice.NotificationId, UserId = user,
                DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
            else if (recipient.DeliveryStatus != "DELIVERED" || recipient.DeliveredAt == null) throw new InvalidOperationException("Reopen delivery receipt conflict.");
        }
        await db.SaveChangesAsync(ct); now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        foreach (var user in eligible) if (!await CanReadAsync(db, source, user, now, ct)) return;
        await tx.CommitAsync(ct);
    }
}
