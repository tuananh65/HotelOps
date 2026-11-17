using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed class AssetProfileAccess(HotelOpsDbContext db, InsightAccess access, DashboardSessionGuard session)
{
    private static readonly string[] Actors = ["OPERATIONS_MANAGER", "HK_SUPERVISOR", "HK_STAFF", "MAINTENANCE_LEAD", "MAINTENANCE_STAFF", "STOREKEEPER", "DEPT_MANAGER", "OWNER"];
    public async Task<bool> AllowedAsync(long branch, CancellationToken ct)
    {
        var user = access.UserId;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == user && r.IsActive && r.Role.IsActive && Actors.Contains(r.Role.RoleCode)
            && (r.BranchId == null || r.BranchId == branch) && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE" && r.User.DeletedAt == null, ct)
            || !await db.UserBranchAccesses.AsNoTracking().AnyAsync(b => b.UserId == user && b.BranchId == branch
                && b.Branch.IsActive && b.RevokedAt == null && b.GrantedAt <= now, ct)) return false;
        try { await access.BranchesAsync("ASSET_VIEW", branch, ct); return true; }
        catch (InsightException e) when (e.Status == 403) { return false; }
    }

    public async Task RequireAsync(long branch, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Asset profile requires a snapshot transaction.");
        await session.RequireAsync(ct);
        if (!await AllowedAsync(branch, ct)) throw new InsightException(403, "ACCESS_DENIED", "Current asset-view role, permission and branch scope required.");
    }

    internal IQueryable<Asset> Query(long branch) => db.Assets.AsNoTracking().Where(a => a.BranchId == branch && a.Branch.IsActive
        && !(a.CurrentRoomId != null && a.CurrentAreaId != null)
        && (a.CurrentRoom == null || a.CurrentRoom.BranchId == branch && a.CurrentRoom.Floor.BranchId == branch)
        && (a.CurrentArea == null || a.CurrentArea.BranchId == branch && (a.CurrentArea.Floor == null || a.CurrentArea.Floor.BranchId == branch))
        && (a.CurrentDepartment == null || a.CurrentDepartment.BranchId == branch));

    internal async Task<int> PageSizeAsync(CancellationToken ct)
    {
        var values = await db.SystemSettings.AsNoTracking().Where(s => s.BranchId == null && s.SettingKey == "SYS-PAR-11")
            .Select(s => s.SettingValue).ToArrayAsync(ct);
        if (values.Length == 0) return 20;
        if (values.Length != 1 || !int.TryParse(values[0], out var size) || size is < 1 or > 100)
            throw new InsightException(503, "PAGE_SIZE_CONFIGURATION", "Invalid SYS-PAR-11.");
        return size;
    }
}
