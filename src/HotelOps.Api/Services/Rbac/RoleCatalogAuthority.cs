using HotelOps.Api.Security.Authorization;
namespace HotelOps.Api.Services.Rbac;
public static class RoleCatalogAuthority
{
 public static async Task<PermissionEvaluation> EvaluateAsync(IPermissionService permissions,CancellationToken ct){ct.ThrowIfCancellationRequested();var user=await permissions.EvaluateGlobalAsync("USER_MANAGE",ct);if(!user.IsGranted)return user;return await permissions.EvaluateGlobalAsync("ROLE_MANAGE",ct);}
}
