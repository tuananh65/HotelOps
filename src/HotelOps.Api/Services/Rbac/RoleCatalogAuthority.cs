using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.Rbac;
public static class RoleCatalogAuthority
{
 public static async Task<PermissionEvaluation> EvaluateAsync(HotelOpsDbContext db, ICurrentUserAccessor current,
     IPermissionService permissions, TimeProvider clock, CancellationToken ct)
 {
     if (!current.TryGetCurrentUser(out var actor)) return new(PermissionDecision.Unauthenticated);
     if (actor.IsRestricted) return new(PermissionDecision.Restricted);
     var now = clock.GetUtcNow().UtcDateTime;
     // The role catalog is system-wide: a branch-bound Administrator cannot administer global roles.
     if (!await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == actor.UserId && r.Role.RoleCode == "SYS_ADMIN"
         && r.BranchId == null && r.IsActive && r.Role.IsActive && r.AssignedAt <= now
         && (r.ExpiresAt == null || r.ExpiresAt > now), ct)) return new(PermissionDecision.Denied);
     return await EvaluateAsync(permissions, ct);
 }
 public static async Task<PermissionEvaluation> EvaluateAsync(IPermissionService permissions,CancellationToken ct){ct.ThrowIfCancellationRequested();var user=await permissions.EvaluateGlobalAsync("USER_MANAGE",ct);if(!user.IsGranted)return user;return await permissions.EvaluateGlobalAsync("ROLE_MANAGE",ct);}
}
