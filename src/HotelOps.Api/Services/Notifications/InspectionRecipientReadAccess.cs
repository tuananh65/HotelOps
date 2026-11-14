using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;

namespace HotelOps.Api.Services.Notifications;

internal static class InspectionRecipientReadAccess
{
    // Same branch-level INSPECTION_VIEW policy as the canonical inspection history endpoint.
    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db, long user, long branch, DateTime now, CancellationToken ct)
    {
        var data = new EfPermissionDataSource(db);
        if (!await data.IsActiveUserAsync(user, ct) || !await data.IsActiveBranchAsync(branch, ct)) return false;
        foreach (var grant in await data.ReadPathsAsync(user, "INSPECTION_VIEW", now, ct))
        {
            if (grant.PermissionCode != "INSPECTION_VIEW" || !PermissionGrantRules.IsEligible(grant, now)) continue;
            var scope = PermissionGrantRules.EffectiveScope(grant);
            if (scope == "GLOBAL" || scope == "BRANCH" && grant.BranchId == branch) return true;
        }
        return false;
    }
}
