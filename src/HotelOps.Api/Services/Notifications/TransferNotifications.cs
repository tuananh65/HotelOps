using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Notifications;

internal sealed record TransferNoticeEvent(long Id, long DocumentId, long SourceBranch, long DestinationBranch,
    long TargetBranch, long Actor, DateTime At, string Event);

internal sealed class TransferNotifications(HotelOpsDbContext db, IOptions<NotificationCenterOptions> retention)
{
    internal const string Source = "STOCK_TRANSFER_EVENT", Dispatched = "STOCK_TRANSFER_DISPATCHED", Received = "STOCK_TRANSFER_RECEIVED";
    internal static IQueryable<TransferNoticeEvent> Events(HotelOpsDbContext db) => db.Database.SqlQueryRaw<TransferNoticeEvent>("""
        SELECT a.audit_id AS Id,d.document_id AS DocumentId,s.branch_id AS SourceBranch,t.branch_id AS DestinationBranch,
          a.branch_id AS TargetBranch,a.actor_user_id AS Actor,a.occurred_at AS At,JSON_VALUE(j.payload,'$.businessEvent') AS Event
        FROM dbo.audit_logs a
        CROSS APPLY(SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'{}' END AS payload) j
        JOIN dbo.stock_documents d ON d.document_id=TRY_CONVERT(bigint,a.entity_id)
        JOIN dbo.warehouses s ON s.warehouse_id=d.source_warehouse_id
        JOIN dbo.warehouses t ON t.warehouse_id=d.destination_warehouse_id
        WHERE a.entity_type='stock_documents' AND a.action='UPDATE' AND a.actor_user_id IS NOT NULL
          AND d.document_type='TRANSFER' AND d.source_warehouse_id<>d.destination_warehouse_id
          AND TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.sourceBranchId'))=s.branch_id
          AND TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.destinationBranchId'))=t.branch_id
          AND ((JSON_VALUE(j.payload,'$.businessEvent')='DISPATCHED' AND a.branch_id=t.branch_id
            AND EXISTS(SELECT 1 FROM dbo.stock_document_lines l JOIN dbo.stock_movements m ON m.document_line_id=l.line_id
              WHERE l.document_id=d.document_id AND m.warehouse_id=s.warehouse_id AND m.signed_base_qty<0
                AND m.created_by=a.actor_user_id AND m.occurred_at=a.occurred_at))
            OR (JSON_VALUE(j.payload,'$.businessEvent')='BATCH_RECEIVED' AND a.branch_id=s.branch_id
              AND EXISTS(SELECT 1 FROM dbo.stock_transfer_receipts r WHERE r.transfer_document_id=d.document_id
                AND r.received_by=a.actor_user_id AND r.received_at=a.occurred_at)))
        """);

    internal static bool Matches(TransferNoticeEvent e, string kind, long? actor, DateTime at) =>
        e.Actor == actor && e.At == at && (kind == Dispatched && e.Event == "DISPATCHED" || kind == Received && e.Event == "BATCH_RECEIVED");

    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db, long user, TransferNoticeEvent e, DateTime now, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.UserId == user && u.DeletedAt == null && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE", ct)
            || !await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == user && r.IsActive && r.Role.IsActive && r.Role.RoleCode == "STOREKEEPER"
                && (r.BranchId == null || r.BranchId == e.TargetBranch) && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now), ct)) return false;
        var paths = await new EfPermissionDataSource(db).ReadPathsAsync(user, "INVENTORY_VIEW", now, ct);
        foreach (var branch in new[] { e.SourceBranch, e.DestinationBranch }.Distinct())
        {
            if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == user && a.BranchId == branch && a.Branch.IsActive && a.GrantedAt <= now && a.RevokedAt == null, ct)) return false;
            if (!paths.Any(p => PermissionGrantRules.IsEligible(p, now) && (PermissionGrantRules.EffectiveScope(p) == "GLOBAL"
                || PermissionGrantRules.EffectiveScope(p) == "BRANCH" && p.BranchId == branch))) return false;
        }
        return true;
    }

    internal async Task<long[]> PendingAsync(long after, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var cutoff = NotificationRetention.At(now, retention.Value).Cutoff;
        return await db.Notifications.AsNoTracking().Where(n => n.NotificationId > after && n.SourceType == Source
            && (n.NotificationType == Dispatched || n.NotificationType == Received) && n.CreatedAt >= cutoff && n.CreatedAt <= now)
            .OrderBy(n => n.NotificationId).Select(n => n.NotificationId).Take(100).ToArrayAsync(ct);
    }

    internal async Task DeliverAsync(long id, CancellationToken ct)
    {
        var hint = await db.Notifications.AsNoTracking().SingleOrDefaultAsync(n => n.NotificationId == id && n.SourceType == Source, ct);
        if (hint?.SourceId is not {} eventId) return;
        var known = await Events(db).SingleOrDefaultAsync(e => e.Id == eventId, ct);
        if (known == null || !Matches(known, hint.NotificationType, hint.CreatedBy, hint.CreatedAt)) return;
        var candidates = await db.UserBranchAccesses.AsNoTracking().Where(a => a.BranchId == known.TargetBranch && a.RevokedAt == null)
            .Select(a => a.UserId).Distinct().Order().ToArrayAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync(candidates, ct);
        var resource = $"HotelOps:TransferNotification:{id}";
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51981,'Transfer notification lock unavailable.',1;", ct);
        var notice = await db.Notifications.SingleAsync(n => n.NotificationId == id, ct);
        var current = await Events(db).SingleOrDefaultAsync(e => e.Id == eventId, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (current == null || current != known || notice.SourceType != Source || notice.SourceId != eventId
            || !Matches(current, notice.NotificationType, notice.CreatedBy, notice.CreatedAt)
            || !NotificationRetention.At(now, retention.Value).Contains(current.At)) return;
        var delivered = new List<long>();
        foreach (var user in candidates)
            if (await CanReadAsync(db, user, current, now, ct)
                && !await db.NotificationRecipients.AnyAsync(r => r.NotificationId == id && r.UserId == user, ct))
            {
                db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = id, UserId = user,
                    DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
                delivered.Add(user);
            }
        if (delivered.Count > 0) db.AuditLogs.Add(new AuditLog { EntityType = "notifications", EntityId = id.ToString(), BranchId = current.TargetBranch,
            Action = "UPDATE", OccurredAt = now, NewValues = JsonSerializer.Serialize(new { operation = "TRANSFER_DELIVERY", eventId, recipientCount = delivered.Count }) });
        await db.SaveChangesAsync(ct);
        now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        foreach (var user in delivered) if (!await CanReadAsync(db, user, current, now, ct)) throw new InvalidOperationException("Transfer recipient authority changed.");
        await tx.CommitAsync(ct);
    }
}
