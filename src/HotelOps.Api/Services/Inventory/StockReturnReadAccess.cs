using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

internal static class StockReturnReadAccess
{
    internal static async Task<bool> QueueBranchAsync(HotelOpsDbContext db, long user, long branch, string mode, CancellationToken ct)
    {
        if (mode == "mine") return await RequesterAsync(db, user, branch, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await ActiveAsync(db, user, branch, now, ct)
            || !await GrantAsync(db, user, branch, "INVENTORY_VIEW", false, now, ct)
            || !await GrantAsync(db, user, branch, mode == "approve" ? "STOCK_RETURN_APPROVE" : "STOCK_RETURN_POST", false, now, ct)) return false;
        return mode == "approve" || await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == user && r.IsActive && r.Role.IsActive && r.Role.RoleCode == "STOREKEEPER"
            && (r.BranchId == null || r.BranchId == branch) && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now), ct);
    }

    internal static async Task<bool> RequesterAsync(HotelOpsDbContext db, long user, long branch, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await ActiveAsync(db, user, branch, now, ct)) return false;
        if (!await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == user && r.IsActive && r.Role.IsActive
            && new[] { "OPERATIONS_MANAGER", "HK_SUPERVISOR", "HK_STAFF", "MAINTENANCE_LEAD", "MAINTENANCE_STAFF", "STOREKEEPER", "DEPT_MANAGER" }.Contains(r.Role.RoleCode)
            && (r.BranchId == null || r.BranchId == branch) && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now), ct)) return false;
        return await GrantAsync(db, user, branch, "STOCK_RETURN_REQUEST", true, now, ct);
    }

    internal static async Task<bool> StorekeeperAsync(HotelOpsDbContext db, long user, long document, CancellationToken ct)
    {
        var source = await db.StockDocuments.AsNoTracking().Where(d => d.DocumentId == document && d.DocumentType == "RETURN" && d.SourceWarehouseId == null
            && d.DestinationWarehouse != null && d.DestinationWarehouse.IsActive && d.DestinationWarehouse.Branch.IsActive)
            .Select(d => new { d.DestinationWarehouse!.BranchId }).SingleOrDefaultAsync(ct);
        if (source == null) return false;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await ActiveAsync(db, user, source.BranchId, now, ct)
            || !await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == user && r.IsActive && r.Role.IsActive && r.Role.RoleCode == "STOREKEEPER"
                && (r.BranchId == null || r.BranchId == source.BranchId) && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now), ct)
            || !await GrantAsync(db, user, source.BranchId, "STOCK_RETURN_POST", false, now, ct)) return false;
        var origins = await db.StockDocumentLines.AsNoTracking().Where(l => l.DocumentId == document)
            .Select(l => (long?)l.StockReturnLineDetailStockDocumentLine!.OriginalIssueLine.Document.SourceWarehouse!.BranchId).ToArrayAsync(ct);
        if (origins.Length == 0 || origins.Any(b => b == null)) return false;
        foreach (var branch in origins.Select(b => b!.Value).Append(source.BranchId).Distinct())
            if (!await ActiveAsync(db, user, branch, now, ct) || !await GrantAsync(db, user, branch, "INVENTORY_VIEW", false, now, ct)) return false;
        return true;
    }

    private static async Task<bool> ActiveAsync(HotelOpsDbContext db, long user, long branch, DateTime now, CancellationToken ct)
        => await new EfPermissionDataSource(db).IsActiveUserAsync(user, ct)
            && await db.Users.AsNoTracking().AnyAsync(u => u.UserId == user && u.EmploymentStatus == "ACTIVE", ct)
            && await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == user && a.BranchId == branch
                && a.Branch.IsActive && a.GrantedAt <= now && a.RevokedAt == null, ct);

    private static async Task<bool> GrantAsync(HotelOpsDbContext db, long user, long branch, string permission, bool own, DateTime now, CancellationToken ct)
        => (await new EfPermissionDataSource(db).ReadPathsAsync(user, permission, now, ct)).Any(p => p.PermissionCode == permission
            && PermissionGrantRules.IsEligible(p, now) && (PermissionGrantRules.EffectiveScope(p) == "GLOBAL"
                || p.BranchId == branch && (PermissionGrantRules.EffectiveScope(p) == "BRANCH" || own && PermissionGrantRules.EffectiveScope(p) == "OWN")));
}
