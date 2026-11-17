using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Notifications;

internal sealed record StocktakeNoticeEvent(long Id, long StocktakeId, long BranchId, long Actor, DateTime At, string Event, long? Counter);

internal sealed class StocktakeNotifications(HotelOpsDbContext db, IOptions<NotificationCenterOptions> retention)
{
    internal const string Source = "STOCKTAKE_RECONCILIATION", Kind = "STOCKTAKE_ADJUSTMENT_REQUESTED", ReturnKind = "STOCKTAKE_RECOUNT_REQUESTED";
    internal static IQueryable<StocktakeNoticeEvent> Events(HotelOpsDbContext db) => db.Database.SqlQueryRaw<StocktakeNoticeEvent>("""
        SELECT a.audit_id AS Id,s.stocktake_id AS StocktakeId,w.branch_id AS BranchId,a.actor_user_id AS Actor,a.occurred_at AS At, JSON_VALUE(j.payload,'$.businessEvent') AS Event, TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.values.counted_by')) AS Counter
        FROM dbo.audit_logs a
        CROSS APPLY(SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'{}' END AS payload) j
        JOIN dbo.stocktakes s ON s.stocktake_id=TRY_CONVERT(bigint,a.entity_id)
        JOIN dbo.warehouses w ON w.warehouse_id=s.warehouse_id
        WHERE a.entity_type='stocktakes' AND a.action='UPDATE' AND a.actor_user_id IS NOT NULL AND a.branch_id=w.branch_id
          AND ((JSON_VALUE(j.payload,'$.businessEvent')='reconcile' AND JSON_VALUE(j.payload,'$.values.status')='PENDING_ADJUSTMENT')
            OR (JSON_VALUE(j.payload,'$.businessEvent')='return' AND JSON_VALUE(j.payload,'$.values.status')='COUNTED' AND LEN(LTRIM(RTRIM(JSON_VALUE(j.payload,'$.values.note'))))>=10))
          AND TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.values.stocktake_id'))=s.stocktake_id
          AND TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.values.warehouse_id'))=s.warehouse_id
          AND EXISTS(SELECT 1 FROM OPENJSON(j.payload,'$.lines') WITH(variance decimal(38,3) '$.variance_qty',reason nvarchar(max) '$.discrepancy_reason') l
            WHERE l.variance<>0 AND LEN(LTRIM(RTRIM(l.reason)))>=3)
        """);

    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db, long user, StocktakeNoticeEvent e, DateTime now, CancellationToken ct)
    {
        var returned = e.Event == "return";
        if (returned && e.Counter != user) return false;
        if (!returned && user == e.Actor || !await db.Users.AsNoTracking().AnyAsync(u => u.UserId == user && u.DeletedAt == null && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE", ct)
            || !await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == user && a.BranchId == e.BranchId && a.Branch.IsActive && a.GrantedAt <= now && a.RevokedAt == null, ct)
            || !await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == user && r.IsActive && r.Role.IsActive && r.Role.RoleCode == (returned ? "STOREKEEPER" : "OPERATIONS_MANAGER")
                && (r.BranchId == null || r.BranchId == e.BranchId) && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now), ct)
            || !returned && await HotelOps.Api.Services.Inventory.StocktakeSeparation.PerformedAsync(db, e.StocktakeId, user, ct)) return false;
        foreach (var code in new[] { "INVENTORY_VIEW", returned ? "STOCKTAKE_PERFORM" : "STOCK_ADJUST_POST" })
        {
            var paths = await new EfPermissionDataSource(db).ReadPathsAsync(user, code, now, ct);
            if (!paths.Any(p => PermissionGrantRules.IsEligible(p, now) && (PermissionGrantRules.EffectiveScope(p) == "GLOBAL"
                || PermissionGrantRules.EffectiveScope(p) == "BRANCH" && p.BranchId == e.BranchId))) return false;
        }
        return true;
    }

    internal async Task<long[]> PendingAsync(long after, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var cutoff = NotificationRetention.At(now, retention.Value).Cutoff;
        return await db.Notifications.AsNoTracking().Where(n => n.NotificationId > after && n.SourceType == Source && (n.NotificationType == Kind || n.NotificationType == ReturnKind)
            && n.CreatedAt >= cutoff && n.CreatedAt <= now).OrderBy(n => n.NotificationId).Select(n => n.NotificationId).Take(100).ToArrayAsync(ct);
    }

    internal async Task DeliverAsync(long id, CancellationToken ct)
    {
        var hint = await db.Notifications.AsNoTracking().SingleOrDefaultAsync(n => n.NotificationId == id && n.SourceType == Source && (n.NotificationType == Kind || n.NotificationType == ReturnKind), ct);
        if (hint?.SourceId is not {} eventId) return;
        var known = await Events(db).SingleOrDefaultAsync(e => e.Id == eventId, ct);
        if (known == null || known.Actor != hint.CreatedBy || known.At != hint.CreatedAt) return;
        var candidates = await db.UserBranchAccesses.AsNoTracking().Where(a => a.BranchId == known.BranchId && a.RevokedAt == null).Select(a => a.UserId).Distinct().Order().ToArrayAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync(candidates, ct);
        var resource = $"HotelOps:StocktakeNotification:{id}";
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51982,'Stocktake notification lock unavailable.',1;", ct);
        var notice = await db.Notifications.SingleAsync(n => n.NotificationId == id, ct);
        var current = await Events(db).SingleOrDefaultAsync(e => e.Id == eventId, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (current == null || current != known || notice.SourceId != eventId || notice.SourceType != Source || notice.NotificationType != (current.Event == "return" ? ReturnKind : Kind)
            || notice.CreatedAt != current.At || notice.CreatedBy != current.Actor || !NotificationRetention.At(now, retention.Value).Contains(current.At)
            || !await db.Stocktakes.AnyAsync(s => s.StocktakeId == current.StocktakeId && s.Status == (current.Event == "return" ? "COUNTED" : "PENDING_ADJUSTMENT"), ct)
            || await db.AuditLogs.AnyAsync(a => a.EntityType == "stocktakes" && a.EntityId == current.StocktakeId.ToString() && a.AuditId > eventId, ct)) return;
        var delivered = new List<long>();
        foreach (var user in candidates)
            if (await CanReadAsync(db, user, current, now, ct) && !await db.NotificationRecipients.AnyAsync(r => r.NotificationId == id && r.UserId == user, ct))
            {
                db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = id, UserId = user, DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
                delivered.Add(user);
            }
        if (delivered.Count > 0) db.AuditLogs.Add(new AuditLog { EntityType = "notifications", EntityId = id.ToString(), BranchId = current.BranchId,
            Action = "UPDATE", OccurredAt = now, NewValues = JsonSerializer.Serialize(new { operation = "STOCKTAKE_REVIEW_DELIVERY", eventId, recipientCount = delivered.Count }) });
        await db.SaveChangesAsync(ct);
        now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        foreach (var user in delivered) if (!await CanReadAsync(db, user, current, now, ct)) throw new InvalidOperationException("Stocktake recipient authority changed.");
        await tx.CommitAsync(ct);
    }
}
