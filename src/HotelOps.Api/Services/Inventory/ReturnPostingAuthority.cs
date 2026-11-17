using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed class ReturnPostingAuthority(HotelOpsDbContext db, InsightAccess access,
    IPermissionService permissions, DashboardSessionGuard session)
{
    public async Task RequireAsync(long actor, long documentId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Return posting transaction required.");
        await session.RequireAsync(ct);
        if (actor != access.UserId) throw new InventoryAccessException(403);
        var document = await db.StockDocuments.AsNoTracking().Where(d => d.DocumentId == documentId && d.DocumentType == "RETURN")
            .Select(d => new { d.DestinationWarehouseId, d.SourceWarehouseId }).SingleOrDefaultAsync(ct);
        if (document?.DestinationWarehouseId is not {} warehouse || document.SourceWarehouseId != null)
            throw new InventoryAccessException(403);
        var branch = await db.Warehouses.AsNoTracking().Where(w => w.WarehouseId == warehouse && w.IsActive && w.Branch.IsActive)
            .Select(w => (long?)w.BranchId).SingleOrDefaultAsync(ct) ?? throw new InventoryAccessException(403);
        InsightAccess.Require(await permissions.EvaluateBranchAsync("STOCK_RETURN_POST", branch, ct));
        InsightAccess.Require(await permissions.EvaluateBranchAsync("INVENTORY_VIEW", branch, ct));
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == actor && a.BranchId == branch && a.GrantedAt <= now && a.RevokedAt == null, ct)
            || !await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == actor && r.IsActive && r.Role.IsActive && r.Role.RoleCode == "STOREKEEPER"
                && (r.BranchId == null || r.BranchId == branch) && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
                && r.User.DeletedAt == null && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE", ct))
            throw new InventoryAccessException(403);
        // A destination grant does not disclose an original issue in another branch.
        var origins = await db.StockDocumentLines.AsNoTracking().Where(l => l.DocumentId == documentId)
            .Select(l => new { l.StockReturnLineDetailStockDocumentLine!.OriginalIssueLine.Document.SourceWarehouseId,
                BranchId = (long?)l.StockReturnLineDetailStockDocumentLine!.OriginalIssueLine.Document.SourceWarehouse!.BranchId })
            .ToListAsync(ct);
        if (origins.Count == 0 || origins.Any(x => x.SourceWarehouseId == null || x.BranchId == null))
            throw new InventoryAccessException(403);
        foreach (var origin in origins.Select(x => x.BranchId!.Value).Distinct())
        {
            InsightAccess.Require(await permissions.EvaluateBranchAsync("INVENTORY_VIEW", origin, ct));
            if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == actor && a.BranchId == origin
                && a.GrantedAt <= now && a.RevokedAt == null && a.Branch.IsActive, ct)) throw new InventoryAccessException(403);
        }
    }
}
