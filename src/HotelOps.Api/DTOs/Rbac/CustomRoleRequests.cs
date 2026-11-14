using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace HotelOps.Api.DTOs.Rbac;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CustomRoleCreateRequest(string RoleCode,string RoleName,string? Description=null)
{
 public CustomRoleCreateRequest Normalize(){var code=RoleCode?.Trim().ToUpperInvariant();var name=CustomRoleMetadata.Name(RoleName);var description=CustomRoleMetadata.Description(Description);if(string.IsNullOrEmpty(code)||code.Length>60||code.Any(char.IsControl))throw new ValidationException("Invalid role code.");return new(code,name,description);}
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CustomRoleUpdateRequest(string RoleName,string? Description=null)
{public CustomRoleUpdateRequest Normalize()=>new(CustomRoleMetadata.Name(RoleName),CustomRoleMetadata.Description(Description));}
public static class CustomRoleMetadata
{
 public static string Name(string? value){var name=value?.Trim();if(string.IsNullOrEmpty(name)||name.Length>150||name.Any(char.IsControl))throw new ValidationException("Invalid role name.");return name;}
 // Technical request bound; schema description itself is nvarchar(max).
 public static string? Description(string? value){var text=value?.Trim();if(text?.Length>4000||text?.Any(c=>char.IsControl(c)&&c is not '\r' and not '\n' and not '\t')==true)throw new ValidationException("Invalid description.");return string.IsNullOrEmpty(text)?null:text;}
}
public sealed record CustomRoleCreatedDto(long RoleId,string RoleCode,string RoleName,string? Description,bool IsSystemRole,bool IsActive,int AssignedUserCount,int PermissionGrantCount);
