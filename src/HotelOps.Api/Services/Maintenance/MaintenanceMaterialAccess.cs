using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Dev2Authorization;
using HotelOps.Api.Services.MaterialRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

internal static class MaintenanceMaterialAccess
{
    internal static bool AllowsInventory(PermissionGrantPath path, long branch, DateTime now)
        => path.PermissionCode == "INVENTORY_VIEW" && PermissionGrantRules.IsEligible(path, now)
            && (PermissionGrantRules.EffectiveScope(path) == "GLOBAL"
                || path.BranchId == branch && PermissionGrantRules.EffectiveScope(path) == "BRANCH");
    // Independent inventory and request grants. Maintenance ownership cannot supply either.
    internal static async Task<MaterialRequestReadAccess.Scope> ScopeAsync(HotelOpsDbContext db, long user, long branch, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var paths = await new EfPermissionDataSource(db).ReadPathsAsync(user, "INVENTORY_VIEW", now, ct);
        if (!paths.Any(p => AllowsInventory(p, branch, now))) throw new Dev2AccessDenied(403);
        var scope = await MaterialRequestReadAccess.ReadAsync(db, user, branch, now, ct);
        if (!scope.Branch && !scope.Own) throw new Dev2AccessDenied(403);
        return scope;
    }
}
