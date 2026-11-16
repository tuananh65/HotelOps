using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.MaterialRequests;

// Independent MR_VIEW only. Request ownership is RequesterId, never an approval,
// inventory or Internal Request grant. The caller owns session/transaction checks.
internal static class MaterialRequestReadAccess
{
    internal sealed record Scope(long UserId, long BranchId, bool Branch, bool Own)
    {
        internal IQueryable<MaterialRequest> Apply(IQueryable<MaterialRequest> requests) => requests.Where(r =>
            r.BranchId == BranchId && r.Branch.IsActive && r.Department.BranchId == r.BranchId
            && (r.SourceWarehouse == null || r.SourceWarehouse.BranchId == r.BranchId)
            && (Branch || Own && r.RequesterId == UserId));
    }

    internal static async Task<Scope> ReadAsync(HotelOpsDbContext db, long user, long branch, DateTime now, CancellationToken ct)
    {
        var grants = await new EfPermissionDataSource(db).ReadPathsAsync(user, "MR_VIEW", now, ct);
        var eligible = grants.Where(g => g.PermissionCode == "MR_VIEW" && PermissionGrantRules.IsEligible(g, now)).ToArray();
        return new(user, branch,
            eligible.Any(g => PermissionGrantRules.EffectiveScope(g) == "GLOBAL"
                || g.BranchId == branch && PermissionGrantRules.EffectiveScope(g) == "BRANCH"),
            eligible.Any(g => g.BranchId == branch && PermissionGrantRules.EffectiveScope(g) == "OWN"));
    }
}
