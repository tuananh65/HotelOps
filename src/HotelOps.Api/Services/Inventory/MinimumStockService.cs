using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed record MinimumStockChange(long ItemId, [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal MinimumQty, string? ExpectedRowVersion);
public sealed record MinimumStockRow(long ItemId, string Item, string Unit,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal Available,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal? MinimumQty, string? RowVersion);
public sealed record MinimumStockPage(long WarehouseId, string Warehouse, int Page, int PageSize, int TotalCount, MinimumStockRow[] Items);

public sealed class MinimumStockService(HotelOpsDbContext db, InsightAccess access, DashboardSessionGuard session, IPermissionService permissions)
{
    private async Task<Warehouse> Admit(long warehouse, CancellationToken ct)
    {
        await session.RequireAsync(ct);
        var user = access.UserId;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var row = await db.Warehouses.AsNoTracking().SingleOrDefaultAsync(w => w.WarehouseId == warehouse && w.IsActive && w.Branch.IsActive
            && (w.DepartmentId == null || w.Department!.BranchId == w.BranchId), ct)
            ?? throw new InsightException(404, "WAREHOUSE_UNAVAILABLE", "Active warehouse unavailable.");
        InsightAccess.Require(await permissions.EvaluateBranchAsync("ITEM_MASTER_MANAGE", row.BranchId, ct));
        InsightAccess.Require(await permissions.EvaluateBranchAsync("INVENTORY_VIEW", row.BranchId, ct));
        if (!await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == user && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE"
            && r.User.DeletedAt == null && r.IsActive && r.Role.IsActive && r.Role.RoleCode == "STOREKEEPER"
            && (r.BranchId == null || r.BranchId == row.BranchId) && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && db.UserBranchAccesses.Any(b => b.UserId == user && b.BranchId == row.BranchId && b.GrantedAt <= now && b.RevokedAt == null), ct))
            throw new InsightException(403, "ACCESS_DENIED", "Current Storekeeper role, branch access and minimum-stock management/read grants required.");
        return row;
    }
    public async Task<MinimumStockPage> ReadAsync(long warehouse, int page, int size, CancellationToken ct)
    {
        if (warehouse <= 0 || page is < 1 or > 1000000 || size is < 1 or > 100) throw new InsightException(400, "INVALID_FILTER", "Invalid warehouse/page.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var w = await Admit(warehouse, ct);
        var items = db.InventoryItems.AsNoTracking().Where(i => i.IsActive && i.Category.IsActive && i.BaseUom.IsActive);
        var count = await items.CountAsync(ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var selected = await items.OrderBy(i => i.ItemId).Skip((page - 1) * size).Take(size)
            .Select(i => new { i.ItemId, i.ItemName, Unit = i.BaseUom.UomCode }).ToArrayAsync(ct);
        var ids = selected.Select(i => i.ItemId).ToArray();
        var thresholds = await db.StockThresholds.AsNoTracking().Where(t => t.WarehouseId == warehouse && ids.Contains(t.ItemId)).ToDictionaryAsync(t => t.ItemId, ct);
        var balances = await StockBalanceReadService.Posted(db).Where(m => m.WarehouseId == warehouse && ids.Contains(m.ItemId) && m.OccurredAt <= now)
            .GroupBy(m => m.ItemId).Select(g => new { ItemId = g.Key, Quantity = g.Sum(m => m.SignedBaseQty) }).ToDictionaryAsync(g => g.ItemId, g => g.Quantity, ct);
        var result = selected.Select(i => new MinimumStockRow(i.ItemId, i.ItemName, i.Unit, balances.GetValueOrDefault(i.ItemId),
            thresholds.GetValueOrDefault(i.ItemId)?.MinimumQty, thresholds.TryGetValue(i.ItemId, out var t) ? Convert.ToBase64String(t.RowVersion) : null)).ToArray();
        await Admit(warehouse, ct); await tx.CommitAsync(ct);
        return new(warehouse, w.WarehouseName, page, size, count, result);
    }
    public async Task<InventoryThresholdDto[]> SaveAsync(long warehouse, IReadOnlyList<MinimumStockChange>? changes, CancellationToken ct)
    {
        if (warehouse <= 0 || changes == null || changes.Count is < 1 or > 100 || changes.Any(c => c == null || c.ItemId <= 0)
            || changes.Select(c => c.ItemId).Distinct().Count() != changes.Count)
            throw new InsightException(400, "INVALID_ITEMS", "Select 1 to 100 distinct items.");
        foreach (var c in changes)
            if (c.MinimumQty < 0 || c.MinimumQty > 999999999999999.999m || decimal.Round(c.MinimumQty, 3) != c.MinimumQty)
                throw new InsightException(400, "INVALID_MINIMUM", $"Item {c.ItemId}: minimum must be nonnegative with at most three decimal places.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await session.RequireAsync(ct);
        // Cooperate with existing inventory writers and warehouse deactivation.
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:Inventory:Catalog',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51981,'Inventory catalog lock unavailable.',1;", ct);
        var w = await Admit(warehouse, ct);
        var rows = new List<StockThreshold>();
        foreach (var c in changes.OrderBy(c => c.ItemId))
        {
            if (!await db.InventoryItems.AsNoTracking().AnyAsync(i => i.ItemId == c.ItemId && i.IsActive && i.Category.IsActive && i.BaseUom.IsActive, ct))
                throw new InsightException(400, "ITEM_UNAVAILABLE", $"Item {c.ItemId} must be active with an active category/base unit.");
            var row = await db.StockThresholds.FromSqlInterpolated($"SELECT * FROM dbo.stock_thresholds WITH(UPDLOCK,HOLDLOCK) WHERE warehouse_id={warehouse} AND item_id={c.ItemId}").SingleOrDefaultAsync(ct);
            var created = row == null;
            byte[]? expected = null;
            if (c.ExpectedRowVersion != null)
            {
                try { expected = Convert.FromBase64String(c.ExpectedRowVersion); }
                catch (FormatException) { throw new InsightException(400, "INVALID_VERSION", "Invalid threshold version."); }
                if (expected.Length != 8) throw new InsightException(400, "INVALID_VERSION", "Invalid threshold version.");
            }
            if (created ? expected != null : expected == null || !row!.RowVersion.SequenceEqual(expected))
                throw new InsightException(412, "STALE_VERSION", $"Item {c.ItemId}: minimum changed. Reload before saving; no items were saved.");
            if (row == null) { row = new StockThreshold { WarehouseId = warehouse, ItemId = c.ItemId, AlertEnabled = true }; db.StockThresholds.Add(row); }
            if (created || row.MinimumQty != c.MinimumQty)
            {
                var before = created ? null : JsonSerializer.Serialize(new { minimumQty = row.MinimumQty, row.AlertEnabled, version = Convert.ToBase64String(row.RowVersion) });
                var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
                row.MinimumQty = c.MinimumQty; row.UpdatedAt = now;
                await db.SaveChangesAsync(ct);
                db.AuditLogs.Add(new AuditLog { ActorUserId = access.UserId, BranchId = w.BranchId, EntityType = "stock_thresholds", EntityId = row.ThresholdId.ToString(),
                    Action = created ? "CREATE" : "UPDATE", OccurredAt = now, OldValues = before,
                    NewValues = JsonSerializer.Serialize(new { warehouseId = warehouse, itemId = c.ItemId, minimumQty = row.MinimumQty, row.AlertEnabled, version = Convert.ToBase64String(row.RowVersion) }),
                    ChangedFields = "[\"minimumQty\"]" });
            }
            rows.Add(row);
        }
        await db.SaveChangesAsync(ct); await Admit(warehouse, ct); await tx.CommitAsync(ct);
        return rows.Select(r => new InventoryThresholdDto(r.ThresholdId, r.WarehouseId, r.ItemId, r.MinimumQty, r.MaximumQty, r.ReorderQty, r.AlertEnabled, Convert.ToBase64String(r.RowVersion))).ToArray();
    }
}
