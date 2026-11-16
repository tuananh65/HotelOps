using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Dev2Authorization;
using HotelOps.Api.Services.MaterialRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

internal static class MaintenanceMaterialAccess
{
    internal static async Task<bool> AllowsUsageAsync(HotelOpsDbContext db, long user, long branch, long work,
        IReadOnlyList<HotelOps.Api.DTOs.Maintenance.MaintenanceMaterialUsageWrite> input, CancellationToken ct)
    {
        if (input.Count == 0) return true;
        if (input.Any(x => x == null || x.StockDocumentLineId <= 0)
            || input.Select(x => x.StockDocumentLineId).Distinct().Count() != input.Count) return false;
        var scope = await ScopeAsync(db, user, branch, ct);
        var requests = scope.Apply(db.MaterialRequests.AsNoTracking());
        var ids = input.Select(x => x.StockDocumentLineId).ToArray();
        var visible = await db.MaterialFulfillmentLines.AsNoTracking().Where(f => f.StockDocumentLineId != null
            && ids.Contains(f.StockDocumentLineId.Value) && requests.Any(r => r.RequestId == f.Fulfillment.RequestId
                && db.MaintenanceWorks.Any(w => w.MaintenanceWorkId == work && w.IncidentId == r.IncidentId)))
            .Select(f => f.StockDocumentLineId!.Value).Distinct().CountAsync(ct);
        return visible == ids.Length;
    }

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
