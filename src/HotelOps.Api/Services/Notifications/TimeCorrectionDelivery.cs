using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.WorkTime;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Notifications;

// The transaction's audit event is the durable intent. Discovery retries missing recipients;
// delivery and opening independently validate current authority. No stored URL grants access.
internal sealed class TimeCorrectionDelivery(HotelOpsDbContext db, TimeProvider clock, IOptions<NotificationCenterOptions> retention)
{
    internal sealed record Source(long EventId, long CorrectionId, long BranchId, long DepartmentId, long EmployeeId,
        long ActorId, DateTime At, string Kind);

    internal static async Task<Source?> SourceAsync(HotelOpsDbContext db, long auditId, CancellationToken ct)
    {
        var audit = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a => a.AuditId == auditId && a.EntityType == "time_correction_requests", ct);
        if (audit == null || !long.TryParse(audit.EntityId, out var id) || audit.ActorUserId is not { } actor) return null;
        var record = await (from r in db.TimeCorrectionRequests.AsNoTracking()
            join i in db.WorkIntervals.AsNoTracking() on r.IntervalId equals i.IntervalId
            join d in db.Workdays.AsNoTracking() on i.WorkdayId equals d.WorkdayId
            where r.CorrectionId == id && r.RequestedBy == i.UserId && d.UserId == i.UserId && i.BranchId == d.BranchId
            select new { Request = r, d.BranchId, d.DepartmentId }).SingleOrDefaultAsync(ct);
        if (record == null || record.BranchId != audit.BranchId) return null;
        var row = record.Request;
        var requested = audit.Action == "CREATE" && audit.OccurredAt == row.RequestedAt && actor == row.RequestedBy;
        var decided = audit.Action == "UPDATE" && row.Status is "APPLIED" or "REJECTED" && row.DecidedAt == audit.OccurredAt
            && row.DecidedBy == actor && actor != row.RequestedBy;
        if (!requested && !decided) return null;
        return new(auditId, id, record.BranchId, record.DepartmentId, row.RequestedBy, actor, audit.OccurredAt,
            requested ? "TIME_CORRECTION_REQUESTED" : "TIME_CORRECTION_DECIDED");
    }

    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db, Source source, long user, DateTime now, CancellationToken ct)
        => source.Kind == "TIME_CORRECTION_DECIDED"
            ? user == source.EmployeeId && await TimeCorrectionAccess.OwnAsync(db, user, source.BranchId, now, ct)
            : user != source.EmployeeId && await TimeCorrectionAccess.DecideAsync(db, user, source.BranchId, source.DepartmentId, now, ct);

    internal async Task<long[]> PendingAsync(long after, CancellationToken ct)
    {
        var window = NotificationRetention.At(clock.GetUtcNow().UtcDateTime, retention.Value);
        return await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "time_correction_requests" && (a.Action == "CREATE" || a.Action == "UPDATE")
            && a.AuditId > after && a.OccurredAt >= window.Cutoff && a.OccurredAt <= window.Now
            && !db.Notifications.Any(n => n.SourceType == "TIME_CORRECTION" && n.SourceId == a.AuditId
                && db.NotificationRecipients.Any(r => r.NotificationId == n.NotificationId && r.DeliveryChannel == "IN_APP" && r.DeliveryStatus == "DELIVERED")))
            .OrderBy(a => a.AuditId).Select(a => a.AuditId).Take(100).ToArrayAsync(ct);
    }

    internal async Task DeliverAsync(long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Independent delivery context required.");
        var hint = await SourceAsync(db, id, ct);
        if (hint == null) return;
        if (hint.Kind == "TIME_CORRECTION_REQUESTED" && !await db.TimeCorrectionRequests.AsNoTracking().AnyAsync(r => r.CorrectionId == hint.CorrectionId && r.Status == "PENDING", ct)) return;
        var recipient = hint.Kind == "TIME_CORRECTION_DECIDED" ? hint.EmployeeId
            : await db.Departments.AsNoTracking().Where(d => d.DepartmentId == hint.DepartmentId && d.BranchId == hint.BranchId && d.IsActive)
                .Select(d => d.ManagerUserId).SingleOrDefaultAsync(ct);
        if (recipient is not > 0) return;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([recipient.Value, hint.ActorId], ct);
        var source = await SourceAsync(db, id, ct);
        if (source != hint) return;
        if (source.Kind == "TIME_CORRECTION_REQUESTED" && !await db.TimeCorrectionRequests.AsNoTracking().AnyAsync(r => r.CorrectionId == source.CorrectionId && r.Status == "PENDING", ct)) return;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!NotificationRetention.At(now, retention.Value).Contains(source.At) || !await CanReadAsync(db, source, recipient.Value, now, ct)) return;
        if (source.Kind == "TIME_CORRECTION_REQUESTED" && !await db.Departments.AsNoTracking().AnyAsync(d => d.DepartmentId == source.DepartmentId
            && d.BranchId == source.BranchId && d.ManagerUserId == recipient && d.IsActive, ct)) return;
        var notice = await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE source_type='TIME_CORRECTION' AND source_id={id}").SingleOrDefaultAsync(ct);
        if (notice == null)
        {
            notice = new Notification { SourceType = "TIME_CORRECTION", SourceId = id, NotificationType = source.Kind,
                Title = source.Kind == "TIME_CORRECTION_REQUESTED" ? "Time correction requested" : "Time correction decided",
                Message = "A time correction is available for authorized review.", Priority = "NORMAL", CreatedBy = source.ActorId, CreatedAt = source.At };
            db.Notifications.Add(notice); await db.SaveChangesAsync(ct);
        }
        else if (notice.NotificationType != source.Kind || notice.CreatedBy != source.ActorId || notice.CreatedAt != source.At)
            throw new InvalidOperationException("Correction notification source conflict.");
        if (!await db.NotificationRecipients.AsNoTracking().AnyAsync(r => r.NotificationId == notice.NotificationId && r.UserId == recipient
            && r.DeliveryChannel == "IN_APP", ct))
        {
            db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notice.NotificationId, UserId = recipient.Value,
                DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
            await db.SaveChangesAsync(ct);
        }
        now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await CanReadAsync(db, source, recipient.Value, now, ct)) return;
        await tx.CommitAsync(ct);
    }
}
