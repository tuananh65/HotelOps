using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Housekeeping;

// Reception may read room readiness/turnover, but its CLEANING_VIEW grant is not
// admission to a cleaner's inbox, execution or rework. Keep grants from other roles.
internal static class HousekeepingFeatureAccess
{
    internal static Task<PermissionGrantPath[]> PathsAsync(HotelOpsDbContext db, long user,
        string permission, DateTime now, CancellationToken ct) =>
        (from r in db.UserRoles.AsNoTracking()
         join p in db.RolePermissions.AsNoTracking() on r.RoleId equals p.RoleId
         where r.UserId == user && r.User.AccountStatus == "ACTIVE" && r.User.DeletedAt == null
             && r.Role.RoleCode != "RECEPTION" && p.Permission.PermissionCode == permission
         select new PermissionGrantPath(p.Permission.PermissionCode, p.DataScope, r.BranchId,
             r.IsActive, r.Role.IsActive, r.AssignedAt, r.ExpiresAt,
             r.Branch != null && r.Branch.IsActive,
             db.UserBranchAccesses.Any(a => a.UserId == user && a.BranchId == r.BranchId
                 && a.RevokedAt == null && a.GrantedAt <= now))).ToArrayAsync(ct);
}
