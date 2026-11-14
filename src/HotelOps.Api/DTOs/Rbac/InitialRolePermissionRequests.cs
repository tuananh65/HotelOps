using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace HotelOps.Api.DTOs.Rbac;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InitialPermissionGrant(long PermissionId, string DataScope);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InitialRolePermissionsRequest(int ContractVersion, InitialPermissionGrant[] Grants)
{
    public InitialPermissionGrant[] Validate()
    {
        if (ContractVersion != 1 || Grants is null || Grants.Length is < 1 or > 32 || Grants.Any(g => g is null || g.PermissionId <= 0 || g.DataScope is not ("OWN" or "BRANCH")) || Grants.Select(g => g.PermissionId).Distinct().Count() != Grants.Length)
            throw new ValidationException("Invalid initial configuration.");
        return Grants.OrderBy(g => g.PermissionId).ToArray();
    }
}
public sealed record InitialRolePermissionsDto(int ContractVersion, long RoleId, IReadOnlyList<InitialPermissionGrant> Grants);