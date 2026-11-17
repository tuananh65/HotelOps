using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

internal static class UserRoleAdministrationAccess
{
    // Actor eligibility is independent of permissions. Call under retained authority locks.
    internal static Task<bool> IsAdministratorAsync(HotelOpsDbContext db, long user, DateTime now, CancellationToken ct) =>
        db.UserRoles.AsNoTracking().AnyAsync(a => a.UserId == user && a.IsActive && a.Role.IsActive
            && a.Role.RoleCode == "SYS_ADMIN" && a.BranchId == null && a.AssignedAt <= now
            && (a.ExpiresAt == null || a.ExpiresAt > now), ct);
}
