using System.Data;
using System.Globalization;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed record WarehouseDepartment(long Id, long BranchId, string Name, bool IsActive);
public sealed record WarehouseManagementSnapshot(InventoryPaged<InventoryWarehouseDto> Warehouses,
    InventoryLookup[] Branches, WarehouseDepartment[] Departments);
public sealed record WarehouseBlocker(string Kind, long Id, string Detail);
public sealed record WarehouseDeactivation(string RowVersion, WarehouseBlocker[] Blockers);

public sealed class WarehouseManagementService(HotelOpsDbContext db, InsightAccess access,
    DashboardSessionGuard session, IPermissionService permissions)
{
    public async Task RequireAsync(long branch, CancellationToken ct)
    {
        await session.RequireAsync(ct);
        InsightAccess.Require(await permissions.EvaluateBranchAsync("ITEM_MASTER_MANAGE", branch, ct));
        var user = access.UserId;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == user && a.BranchId == branch
            && a.Branch.IsActive && a.GrantedAt <= now && a.RevokedAt == null, ct)
            || !await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == user && r.IsActive && r.Role.IsActive
                && r.Role.RoleCode == "SYS_ADMIN" && (r.BranchId == null || r.BranchId == branch)
                && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
                && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE" && r.User.DeletedAt == null, ct))
            throw new InsightException(403, "ACCESS_DENIED", "Current Administrator role, branch access and ITEM_MASTER_MANAGE are required.");
    }

    public async Task<WarehouseManagementSnapshot> ReadAsync(int page, int size, CancellationToken ct)
    {
        if (page is < 1 or > 1000000 || size is < 1 or > 100) throw new InsightException(400, "INVALID_PAGE", "Invalid page.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await session.RequireAsync(ct);
        var candidates = await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId).Select(b => b.BranchId).ToArrayAsync(ct);
        var allowed = new List<long>();
        foreach (var branch in candidates)
        {
            try { await RequireAsync(branch, ct); allowed.Add(branch); }
            catch (InsightException e) when (e.Status == 403) { }
        }
        if (allowed.Count == 0) throw new InsightException(403, "ACCESS_DENIED", "No authorized warehouse management branch.");
        var query = db.Warehouses.AsNoTracking().Where(w => allowed.Contains(w.BranchId));
        var count = await query.CountAsync(ct);
        var rows = await query.OrderBy(w => w.BranchId).ThenBy(w => w.WarehouseCode).ThenBy(w => w.WarehouseId)
            .Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        var items = rows.Select(w => new InventoryWarehouseDto(w.WarehouseId, w.BranchId, w.DepartmentId,
            w.WarehouseCode, w.WarehouseName, w.ManagerUserId, w.LocationDescription, w.IsActive, Convert.ToBase64String(w.RowVersion))).ToArray();
        var branches = await db.Branches.AsNoTracking().Where(b => allowed.Contains(b.BranchId))
            .OrderBy(b => b.BranchId).Select(b => new InventoryLookup(b.BranchId, b.BranchCode, b.BranchName, b.IsActive)).ToArrayAsync(ct);
        var departments = await db.Departments.AsNoTracking().Where(d => allowed.Contains(d.BranchId))
            .OrderBy(d => d.DepartmentId).Select(d => new WarehouseDepartment(d.DepartmentId, d.BranchId, d.DepartmentName, d.IsActive)).ToArrayAsync(ct);
        foreach (var branch in allowed) await RequireAsync(branch, ct);
        await tx.CommitAsync(ct);
        return new(new(items, count, page, size), branches, departments);
    }

    public async Task<WarehouseDeactivation> PreviewAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await session.RequireAsync(ct);
        var warehouse = await db.Warehouses.AsNoTracking().SingleOrDefaultAsync(w => w.WarehouseId == id, ct)
            ?? throw new InsightException(404, "NOT_FOUND", "Warehouse not found.");
        await RequireAsync(warehouse.BranchId, ct);
        // Management authority does not grant access to stock/document data.
        InsightAccess.Require(await permissions.EvaluateBranchAsync("INVENTORY_VIEW", warehouse.BranchId, ct));
        var blockers = await BlockersAsync(db, id, ct);
        await RequireAsync(warehouse.BranchId, ct);
        InsightAccess.Require(await permissions.EvaluateBranchAsync("INVENTORY_VIEW", warehouse.BranchId, ct));
        await tx.CommitAsync(ct);
        return new(Convert.ToBase64String(warehouse.RowVersion), blockers);
    }

    internal static async Task<WarehouseBlocker[]> BlockersAsync(HotelOpsDbContext db, long id, CancellationToken ct)
    {
        // Use all persisted stock effects: inconsistent legacy effects must also block closure.
        var balances = await db.StockMovements.AsNoTracking().Where(m => m.WarehouseId == id).GroupBy(m => m.ItemId)
            .Select(g => new { Id = g.Key, Quantity = g.Sum(m => m.SignedBaseQty) }).Where(x => x.Quantity != 0).OrderBy(x => x.Id).ToArrayAsync(ct);
        var result = balances.Select(b => new WarehouseBlocker("ITEM_BALANCE", b.Id, b.Quantity.ToString(CultureInfo.InvariantCulture))).ToList();
        var documents = await db.StockDocuments.AsNoTracking().Where(d => (d.SourceWarehouseId == id || d.DestinationWarehouseId == id)
            && d.Status != "POSTED" && d.Status != "REVERSED" && d.Status != "CANCELLED" && d.Status != "RECEIVED")
            .OrderBy(d => d.DocumentId).Select(d => new WarehouseBlocker("DOCUMENT", d.DocumentId, d.Status)).ToArrayAsync(ct);
        // Return only the local blocker identity/status, never the other branch's transaction details.
        result.AddRange(documents);
        result.AddRange(await db.Stocktakes.AsNoTracking().Where(s => s.WarehouseId == id && s.Status != "RECONCILED" && s.Status != "CANCELLED" && s.Status != "COMPLETED")
            .OrderBy(s => s.StocktakeId).Select(s => new WarehouseBlocker("STOCKTAKE", s.StocktakeId, s.Status)).ToArrayAsync(ct));
        result.AddRange(await db.MaterialReplenishments.AsNoTracking().Where(r => r.WarehouseId == id && (r.Status == "REQUESTED" || r.Status == "PARTIALLY_RECEIVED"))
            .OrderBy(r => r.ReplenishmentId).Select(r => new WarehouseBlocker("REPLENISHMENT", r.ReplenishmentId, r.Status)).ToArrayAsync(ct));
        return result.ToArray();
    }
}
