using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed class AssetRegistrationAuthority(HotelOpsDbContext db, InsightAccess access, DashboardSessionGuard session, IPermissionService permissions)
{
    public async Task RequireAsync(long branch, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Asset authority requires its transaction.");
        await session.RequireAsync(ct);
        var actor = access.UserId;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == actor && a.BranchId == branch && a.Branch.IsActive
                && a.RevokedAt == null && a.GrantedAt <= now, ct)
            || !await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == actor && r.IsActive && r.Role.IsActive && r.Role.RoleCode == "OPERATIONS_MANAGER"
                && (r.BranchId == null || r.BranchId == branch) && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
                && r.User.DeletedAt == null && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE", ct))
            throw new InsightException(403, "ACCESS_DENIED", "Current Operations Manager role and branch access required.");
        InsightAccess.Require(await permissions.EvaluateBranchAsync("ASSET_MANAGE", branch, ct));
        InsightAccess.Require(await permissions.EvaluateBranchAsync("ASSET_VIEW", branch, ct));
    }
}
