using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed class StocktakeAuthority(HotelOpsDbContext db, InsightAccess access,
    IPermissionService permissions, DashboardSessionGuard session)
{
    public async Task RequireAsync(long actor, long warehouse, string action, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Stocktake transaction required.");
        await session.RequireAsync(ct);
        if (actor != access.UserId || action is not ("read" or "perform" or "submit" or "cancel" or "reconcile" or "return" or "approve-adjustment"))
            throw new InventoryAccessException(403);
        var branch = await db.Warehouses.AsNoTracking().Where(w => w.WarehouseId == warehouse && w.Branch.IsActive)
            .Select(w => (long?)w.BranchId).SingleOrDefaultAsync(ct) ?? throw new InventoryAccessException(403);
        InsightAccess.Require(await permissions.EvaluateBranchAsync("INVENTORY_VIEW", branch, ct));
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == actor && a.BranchId == branch && a.RevokedAt == null && a.GrantedAt <= now, ct))
            throw new InventoryAccessException(403);
        if (action == "read") return;
        var decides = action is "return" or "approve-adjustment";
        InsightAccess.Require(await permissions.EvaluateBranchAsync(decides ? "STOCK_ADJUST_POST" : "STOCKTAKE_PERFORM", branch, ct));
        var role = decides ? "OPERATIONS_MANAGER" : "STOREKEEPER";
        if (!await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == actor && r.IsActive && r.Role.IsActive && r.Role.RoleCode == role
            && (r.BranchId == null || r.BranchId == branch) && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && r.User.DeletedAt == null && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE", ct))
            throw new InventoryAccessException(403);
    }
}
