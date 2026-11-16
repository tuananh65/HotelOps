using System.Data;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

// Called only by RecoveryAuthProbe after exact disposable identity and persisted HTTPS session checks.
internal static class RecoveryReplenishmentScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient http, long actor, string csrf,
        List<object> checks, CancellationToken ct)
    {
        var prefix = "D3R" + Guid.NewGuid().ToString("N")[..10];
        var branch = new Branch { BranchCode = prefix, BranchName = "Synthetic replenishment", IsActive = true };
        var role = new Role { RoleCode = prefix, RoleName = "Synthetic isolated grants", IsActive = true };
        var unit = new UnitsOfMeasure { UomCode = prefix, UomName = "Synthetic unit", IsActive = true };
        var category = new ItemCategory { CategoryCode = prefix, CategoryName = "Synthetic category", IsActive = true };
        var warehouse = new Warehouse { Branch = branch, WarehouseCode = prefix, WarehouseName = "Synthetic warehouse", IsActive = true };
        var item = new InventoryItem { ItemCode = prefix, ItemName = "Synthetic item", Category = category, BaseUom = unit, IsActive = true, IsConsumable = true };
        var threshold = new StockThreshold { Warehouse = warehouse, Item = item, MinimumQty = 5, AlertEnabled = true };
        long[] ownedRoles = [];
        try
        {
            await using (var setup = await db.Database.BeginTransactionAsync(ct))
            {
                var storekeeper = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "STOREKEEPER" && r.IsActive, ct);
                var codes = new[] { "REPLENISHMENT_VIEW", "REPLENISHMENT_CREATE", "REPLENISHMENT_CLOSE", "INVENTORY_VIEW" };
                var permissions = await db.Permissions.AsNoTracking().Where(p => codes.Contains(p.PermissionCode)).ToArrayAsync(ct);
                if (permissions.Length != codes.Length) throw new InvalidOperationException("Required approved catalog missing.");
                db.AddRange(branch, role, unit, category, warehouse, item, threshold); await db.SaveChangesAsync(ct);
                foreach (var p in permissions) db.RolePermissions.Add(new() { RoleId = role.RoleId, PermissionId = p.PermissionId, DataScope = "BRANCH", GrantedAt = DateTime.UtcNow });
                var assigned = new[] { new UserRole { UserId = actor, RoleId = role.RoleId, BranchId = branch.BranchId, IsActive = true, AssignedAt = DateTime.UtcNow },
                    new UserRole { UserId = actor, RoleId = storekeeper.RoleId, BranchId = branch.BranchId, IsActive = true, AssignedAt = DateTime.UtcNow } };
                db.UserRoles.AddRange(assigned);
                db.UserBranchAccesses.Add(new() { UserId = actor, BranchId = branch.BranchId, AccessLevel = "STANDARD", GrantedAt = DateTime.UtcNow });
                await db.SaveChangesAsync(ct); ownedRoles = assigned.Select(r => r.UserRoleId).ToArray();
                await setup.CommitAsync(ct); db.ChangeTracker.Clear();
            }
            async Task<JsonElement> Call(HttpMethod method, string path, int expected, object? body = null, bool token = true)
            {
                using var request = new HttpRequestMessage(method, path);
                if (body != null) request.Content = JsonContent.Create(body);
                if (token) request.Headers.Add("X-CSRF-TOKEN", csrf);
                using var response = await http.SendAsync(request, ct);
                checks.Add(new { replenishment = path, method = method.Method, expected, actual = (int)response.StatusCode });
                if ((int)response.StatusCode != expected) throw new InvalidOperationException($"Unexpected replenishment status {response.StatusCode} for {path}");
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); return json.RootElement.Clone();
            }
            var shortages = await Call(HttpMethod.Get, $"/api/replenishment-shortages/low-stock?branchId={branch.BranchId}&warehouseId={warehouse.WarehouseId}", 200);
            if (!shortages.GetProperty("items").EnumerateArray().Any(r => r.GetProperty("thresholdId").GetInt64() == threshold.ThresholdId))
                throw new InvalidOperationException("Synthetic shortage missing.");
            var shortage = shortages.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("thresholdId").GetInt64() == threshold.ThresholdId);
            if (shortage.GetProperty("itemName").GetString() != item.ItemName || shortage.GetProperty("unitName").GetString() != unit.UomName)
                throw new InvalidOperationException("Shortage item/base-unit labels incorrect.");
            var input = new { actorUserId = actor, warehouseId = warehouse.WarehouseId, supplierName = "Synthetic supplier", expectedOn = (string?)null,
                lines = new[] { new { lineNo = 1, itemId = item.ItemId, sourceType = "LOW_STOCK", sourceThresholdId = threshold.ThresholdId,
                    sourceMaterialRequestLineId = (long?)null, requestedBaseQty = 5m } } };
            await Call(HttpMethod.Post, "/api/replenishments", 400, input, token: false);
            var created = await Call(HttpMethod.Post, "/api/replenishments", 201, input);
            var id = created.GetProperty("replenishmentId").GetInt64();
            var version = created.GetProperty("rowVersion").GetString();
            await Call(HttpMethod.Post, "/api/replenishments", 400, input);
            var detail = await Call(HttpMethod.Get, $"/api/replenishments/{id}", 200);
            if (detail.GetProperty("rawStatus").GetString() != "REQUESTED" || detail.GetProperty("waitingRequestsVisible").GetBoolean())
                throw new InvalidOperationException("Created state or independent MR_VIEW disclosure incorrect.");
            var list = await Call(HttpMethod.Get, $"/api/replenishments?branchId={branch.BranchId}", 200);
            if (list.GetProperty("total").GetInt32() != 1) throw new InvalidOperationException("Scoped list count incorrect.");
            await RecoveryReplenishmentNotice.RunAsync(db, http, id, branch.BranchId, actor, checks, ct);
            await Call(HttpMethod.Post, $"/api/replenishments/{id}/close", 200, new { actorUserId = actor, expectedRowVersion = version, reason = "Synthetic delivery cancelled" });
            if (!await db.MaterialReplenishments.AnyAsync(r => r.ReplenishmentId == id && r.Status == "CLOSED" && r.ClosedBy == actor, ct)
                || await db.MaterialReplenishmentLines.AnyAsync(l => l.ReplenishmentId == id && l.IsOpenCoverage, ct))
                throw new InvalidOperationException("Close state/coverage not persisted.");
            if (await db.AuditLogs.CountAsync(a => a.ActorUserId == actor && a.BranchId == branch.BranchId && a.EntityType == "material_replenishments", ct) < 2)
                throw new InvalidOperationException("Create/close audit missing.");
            checks.Add(new { persistedCreateCloseAndAudit = true, independentMrView = true });
            await db.UserRoles.Where(r => ownedRoles.Contains(r.UserRoleId)).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
            await Call(HttpMethod.Get, $"/api/replenishments/{id}", 403);
        }
        finally
        {
            db.ChangeTracker.Clear();
            using var cleanupBudget = new CancellationTokenSource(TimeSpan.FromSeconds(30)); var clean = cleanupBudget.Token;
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, clean);
            if (await db.Branches.AnyAsync(b => b.BranchId == branch.BranchId && b.BranchCode == prefix, clean))
            {
                var roots = db.MaterialReplenishments.Where(r => r.WarehouseId == warehouse.WarehouseId && r.RequestedBy == actor);
                var notices = db.Notifications.Where(n => n.SourceType == "REPLENISHMENT_CREATED"
                    && roots.Any(r => r.ReplenishmentId == n.SourceId) && n.CreatedBy == actor);
                await db.NotificationRecipients.Where(r => notices.Any(n => n.NotificationId == r.NotificationId)).ExecuteDeleteAsync(clean);
                await notices.ExecuteDeleteAsync(clean);
                await db.MaterialReplenishmentLines.Where(l => roots.Any(r => r.ReplenishmentId == l.ReplenishmentId)).ExecuteDeleteAsync(clean);
                await roots.ExecuteDeleteAsync(clean);
                await db.AuditLogs.Where(a => a.ActorUserId == actor && a.BranchId == branch.BranchId && a.EntityType == "material_replenishments").ExecuteDeleteAsync(clean);
                await db.StockThresholds.Where(t => t.ThresholdId == threshold.ThresholdId).ExecuteDeleteAsync(clean);
                await db.InventoryItems.Where(i => i.ItemId == item.ItemId && i.ItemCode == prefix).ExecuteDeleteAsync(clean);
                await db.Warehouses.Where(w => w.WarehouseId == warehouse.WarehouseId && w.WarehouseCode == prefix).ExecuteDeleteAsync(clean);
                await db.ItemCategories.Where(c => c.ItemCategoryId == category.ItemCategoryId && c.CategoryCode == prefix).ExecuteDeleteAsync(clean);
                await db.UnitsOfMeasures.Where(u => u.UomId == unit.UomId && u.UomCode == prefix).ExecuteDeleteAsync(clean);
                await db.UserRoles.Where(r => r.UserId == actor && r.BranchId == branch.BranchId).ExecuteDeleteAsync(clean);
                await db.UserBranchAccesses.Where(a => a.UserId == actor && a.BranchId == branch.BranchId).ExecuteDeleteAsync(clean);
                await db.RolePermissions.Where(r => r.RoleId == role.RoleId).ExecuteDeleteAsync(clean);
                await db.Roles.Where(r => r.RoleId == role.RoleId && r.RoleCode == prefix).ExecuteDeleteAsync(clean);
                await db.Branches.Where(b => b.BranchId == branch.BranchId && b.BranchCode == prefix).ExecuteDeleteAsync(clean);
            }
            await tx.CommitAsync(clean);
            checks.Add(new { replenishmentFixtureCleanup = true });
        }
    }
}
