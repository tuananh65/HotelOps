using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Notifications;

internal sealed class LowStockEpisode
{
    public long Id { get; set; }
    public long WarehouseId { get; set; }
    public long ItemId { get; set; }
    public long BranchId { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

internal sealed class LowStockAlerts(HotelOpsDbContext db, IOptions<NotificationCenterOptions> retention)
{
    internal const string Kind = "LOW_STOCK", Source = "LOW_STOCK_EPISODE";
    internal static Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) => db.Database.SqlQuery<int>($"""
        SELECT 1 AS Value WHERE EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.LowStockEpisodes.V1' AND CONVERT(int,value)=1)
        AND COL_LENGTH('dbo.low_stock_episodes','open_audit_id')=8
        AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.low_stock_episodes') AND name='UX_low_stock_open' AND is_unique=1 AND is_disabled=0)
        AND EXISTS(SELECT 1 FROM sys.triggers WHERE parent_id=OBJECT_ID('dbo.low_stock_episodes') AND name='TR_low_stock_immutable' AND is_disabled=0)
        AND NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.low_stock_episodes') AND (is_disabled=1 OR is_not_trusted=1 OR delete_referential_action<>0))
        """).AnyAsync(ct);

    internal static IQueryable<LowStockEpisode> Query(HotelOpsDbContext db) => db.Database.SqlQuery<LowStockEpisode>($"""
        SELECT e.episode_id AS Id,e.warehouse_id AS WarehouseId,e.item_id AS ItemId,e.branch_id AS BranchId,e.opened_at AS OpenedAt,e.closed_at AS ClosedAt
        FROM dbo.low_stock_episodes e JOIN dbo.audit_logs a ON a.audit_id=e.open_audit_id
        WHERE a.actor_user_id IS NULL AND a.branch_id=e.branch_id AND a.entity_type='low_stock_episodes'
          AND a.action='CREATE' AND a.occurred_at=e.opened_at AND ISJSON(a.new_values)=1
          AND TRY_CONVERT(bigint,JSON_VALUE(a.new_values,'$.warehouseId'))=e.warehouse_id
          AND TRY_CONVERT(bigint,JSON_VALUE(a.new_values,'$.itemId'))=e.item_id
        """);

    private static async Task LockAsync(HotelOpsDbContext db, long warehouse, long item, CancellationToken ct)
    {
        var resource = $"HotelOps:LowStock:{warehouse}:{item}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
            IF @r<0 THROW 51980,'Low stock observation lock unavailable.',1;
            """, ct);
    }
    private async Task<(long Branch, decimal Minimum, decimal Quantity, bool Enabled)?> Condition(long warehouse, long item, DateTime now, CancellationToken ct)
    {
        var t = await db.StockThresholds.AsNoTracking().Where(t => t.WarehouseId == warehouse && t.ItemId == item
            && (t.Warehouse.DepartmentId == null || t.Warehouse.Department!.BranchId == t.Warehouse.BranchId))
            .Select(t => new { t.Warehouse.BranchId, t.MinimumQty,
                Enabled = t.AlertEnabled && t.Warehouse.IsActive && t.Warehouse.Branch.IsActive && t.Item.IsActive }).SingleOrDefaultAsync(ct);
        if (t == null) return null;
        if (t.MinimumQty < 0) throw new InvalidOperationException("Invalid minimum stock configuration.");
        var quantity = await Inventory.StockBalanceReadService.Posted(db).Where(m => m.WarehouseId == warehouse && m.ItemId == item && m.OccurredAt <= now)
            .SumAsync(m => (decimal?)m.SignedBaseQty, ct) ?? 0;
        return quantity < t.MinimumQty ? (t.BranchId, t.MinimumQty, quantity, t.Enabled) : null;
    }
    internal async Task<long?> ObserveAsync(long warehouse, long item, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockAsync(db, warehouse, item, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var condition = await Condition(warehouse, item, now, ct);
        var open = await Query(db).SingleOrDefaultAsync(e => e.WarehouseId == warehouse && e.ItemId == item && e.ClosedAt == null, ct);
        if (open != null && (condition == null || condition.Value.Branch != open.BranchId))
        {
            var audit = new AuditLog { BranchId = open.BranchId, EntityType = "low_stock_episodes", EntityId = open.Id.ToString(), Action = "UPDATE", OccurredAt = now,
                NewValues = JsonSerializer.Serialize(new { operation = "CLOSE", warehouseId = warehouse, itemId = item, closedAt = now }) };
            db.AuditLogs.Add(audit); await db.SaveChangesAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.low_stock_episodes SET closed_at={now},last_observed_at={now},close_audit_id={audit.AuditId} WHERE episode_id={open.Id} AND closed_at IS NULL", ct);
            open = null;
        }
        long? id = open?.Id;
        if (condition is { Enabled: true } low && open == null)
        {
            var audit = new AuditLog { BranchId = low.Branch, EntityType = "low_stock_episodes", EntityId = $"{warehouse}:{item}", Action = "CREATE", OccurredAt = now,
                NewValues = JsonSerializer.Serialize(new { operation = "OPEN", warehouseId = warehouse, itemId = item, quantity = low.Quantity, minimum = low.Minimum }) };
            db.AuditLogs.Add(audit); await db.SaveChangesAsync(ct);
            // INSERT OUTPUT is intentionally non-composed; SqlQuery is only the materializer.
            var inserted = await db.Database.SqlQuery<long>($"""
                INSERT dbo.low_stock_episodes(warehouse_id,item_id,branch_id,opened_at,last_observed_at,opening_quantity,minimum_quantity,open_audit_id)
                OUTPUT INSERTED.episode_id AS Value VALUES({warehouse},{item},{low.Branch},{now},{now},{low.Quantity},{low.Minimum},{audit.AuditId})
                """).ToArrayAsync(ct);
            id = inserted.Single();
            db.Notifications.Add(new Notification { NotificationType = Kind, SourceType = Source, SourceId = id,
                Title = "Stock below minimum", Message = "A warehouse item fell below its configured minimum. Open current authorized stock to review replenishment.",
                Priority = "HIGH", CreatedAt = now });
            await db.SaveChangesAsync(ct);
        }
        else if (open != null)
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.low_stock_episodes SET last_observed_at={now} WHERE episode_id={open.Id} AND closed_at IS NULL", ct);
        await tx.CommitAsync(ct); db.ChangeTracker.Clear();
        return id;
    }

    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db, long user, LowStockEpisode episode, DateTime now, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.UserId == user && u.DeletedAt == null && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE", ct)
            || !await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == user && a.BranchId == episode.BranchId && a.Branch.IsActive && a.RevokedAt == null && a.GrantedAt <= now, ct)
            || !await db.Warehouses.AsNoTracking().AnyAsync(w => w.WarehouseId == episode.WarehouseId && w.BranchId == episode.BranchId
                && (w.DepartmentId == null || w.Department!.BranchId == episode.BranchId), ct)
            || !await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == user && r.IsActive && r.Role.IsActive && r.AssignedAt <= now
                && (r.ExpiresAt == null || r.ExpiresAt > now) && (r.BranchId == null || r.BranchId == episode.BranchId)
                && (r.Role.RoleCode == "STOREKEEPER" || r.Role.RoleCode == "OPERATIONS_MANAGER"), ct)) return false;
        var paths = await new EfPermissionDataSource(db).ReadPathsAsync(user, "INVENTORY_VIEW", now, ct);
        return paths.Any(p => PermissionGrantRules.IsEligible(p, now) && (PermissionGrantRules.EffectiveScope(p) == "GLOBAL"
            || p.BranchId == episode.BranchId && PermissionGrantRules.EffectiveScope(p) == "BRANCH"));
    }

    internal async Task<int> DeliverAsync(long id, CancellationToken ct)
    {
        var hint = await Query(db).SingleOrDefaultAsync(e => e.Id == id && e.ClosedAt == null, ct);
        if (hint == null) return 0;
        var candidates = await db.UserBranchAccesses.AsNoTracking().Where(a => a.BranchId == hint.BranchId && a.RevokedAt == null)
            .Select(a => a.UserId).Distinct().Order().ToArrayAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync(candidates, ct);
        await LockAsync(db, hint.WarehouseId, hint.ItemId, ct);
        var episode = await Query(db).SingleOrDefaultAsync(e => e.Id == id && e.ClosedAt == null, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (episode == null || !NotificationRetention.At(now, retention.Value).Contains(episode.OpenedAt)
            || await Condition(episode.WarehouseId, episode.ItemId, now, ct) is not { Enabled: true } condition || condition.Branch != episode.BranchId) return 0;
        var notice = await db.Notifications.SingleOrDefaultAsync(n => n.NotificationType == Kind && n.SourceType == Source && n.SourceId == id, ct);
        if (notice == null || notice.CreatedBy != null || notice.CreatedAt != episode.OpenedAt) throw new InvalidOperationException("Low stock intent provenance mismatch.");
        var delivered = new List<long>();
        foreach (var user in candidates)
            if (await CanReadAsync(db, user, episode, now, ct)
                && !await db.NotificationRecipients.AnyAsync(r => r.NotificationId == notice.NotificationId && r.UserId == user, ct))
            {
                db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notice.NotificationId, UserId = user,
                    DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now }); delivered.Add(user);
            }
        if (delivered.Count > 0) db.AuditLogs.Add(new AuditLog { BranchId = episode.BranchId, EntityType = "notifications",
            EntityId = notice.NotificationId.ToString(), Action = "UPDATE", OccurredAt = now,
            NewValues = JsonSerializer.Serialize(new { operation = "LOW_STOCK_DELIVERY", episodeId = id, recipientCount = delivered.Count }) });
        await db.SaveChangesAsync(ct);
        now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (await Condition(episode.WarehouseId, episode.ItemId, now, ct) is not { Enabled: true } current || current.Branch != episode.BranchId)
            throw new InvalidOperationException("Low stock condition changed before delivery.");
        foreach (var user in delivered) if (!await CanReadAsync(db, user, episode, now, ct)) throw new InvalidOperationException("Recipient authority changed.");
        await tx.CommitAsync(ct); db.ChangeTracker.Clear();
        return delivered.Count;
    }
}
