using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed class TransferAuthority(HotelOpsDbContext db, InsightAccess access,
    IPermissionService permissions, DashboardSessionGuard session)
{
    public Task RequireAsync(long actor, StockDocument document, string side, CancellationToken ct)
    {
        if (document.DocumentType != "TRANSFER") throw new InventoryAccessException(403);
        return RequireAsync(actor, document.SourceWarehouseId, document.DestinationWarehouseId, side, ct);
    }

    public async Task RequireAsync(long actor, long? source, long? destination, string side, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Transfer authority requires transaction.");
        await session.RequireAsync(ct);
        if (actor != access.UserId || source is not > 0 || destination is not > 0 || source == destination
            || side is not ("source" or "destination" or "both" or "read")) throw new InventoryAccessException(403);
        foreach (var warehouseId in new[] { source.Value, destination.Value }.Order())
            await RequireWarehouseAsync(actor, warehouseId, side == "both" || side == "source" && warehouseId == source
                || side == "destination" && warehouseId == destination, ct);
    }

    public async Task RequireWarehouseAsync(long actor, long warehouseId, bool writes, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Transfer authority requires transaction.");
        await session.RequireAsync(ct);
        if (actor != access.UserId) throw new InventoryAccessException(403);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var branch = await db.Warehouses.AsNoTracking().Where(w => w.WarehouseId == warehouseId && w.Branch.IsActive)
            .Select(w => (long?)w.BranchId).SingleOrDefaultAsync(ct) ?? throw new InventoryAccessException(403);
        InsightAccess.Require(await permissions.EvaluateBranchAsync("INVENTORY_VIEW", branch, ct));
        if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == actor && a.BranchId == branch
            && a.GrantedAt <= now && a.RevokedAt == null, ct)) throw new InventoryAccessException(403);
        if (!writes) return;
        InsightAccess.Require(await permissions.EvaluateBranchAsync("STOCK_TRANSFER_POST", branch, ct));
        if (!await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == actor && r.IsActive && r.Role.IsActive
            && r.Role.RoleCode == "STOREKEEPER" && (r.BranchId == null || r.BranchId == branch)
            && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE" && r.User.DeletedAt == null, ct))
            throw new InventoryAccessException(403);
    }
}
