using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace HotelOps.Api.DTOs.Rbac;
public sealed record ConfiguredRoleGrant(long PermissionId, string DataScope);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RolePermissionConfigurationRequest(ConfiguredRoleGrant[] Grants)
{
    public ConfiguredRoleGrant[] Validate()
    {
        if (Grants == null || Grants.Length > 2000 || Grants.Any(g => g == null || g.PermissionId <= 0 || g.DataScope is not ("ALL" or "BRANCH" or "OWN"))
            || Grants.Select(g => g.PermissionId).Distinct().Count() != Grants.Length)
            throw new ValidationException("Invalid permission selection or scope.");
        return Grants.OrderBy(g => g.PermissionId).ToArray();
    }
}
public sealed record RolePermissionConfigurationDto(RoleCatalogDto Role, IReadOnlyList<ConfiguredRoleGrant> Grants,
    IReadOnlyList<PermissionCatalogDto> Catalog);
public sealed record RolePermissionConfigurationResult(int Status, string? Code = null,
    RolePermissionConfigurationDto? Value = null, byte[]? Version = null);
