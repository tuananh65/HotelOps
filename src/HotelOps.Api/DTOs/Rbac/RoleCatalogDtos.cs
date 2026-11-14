namespace HotelOps.Api.DTOs.Rbac;
public sealed record RoleCatalogDto(long RoleId,string RoleCode,string RoleName,string? Description,bool IsSystemRole,bool IsActive,int AssignedUserCount,int PermissionGrantCount);
public sealed record PermissionCatalogDto(long PermissionId,string PermissionCode,string PermissionName,string ModuleCode,string ResourceCode,string ActionCode,string? Description);
public sealed record RoleGrantDto(long RolePermissionId,PermissionCatalogDto Permission,string DataScope);
public sealed record RoleDetailDto(RoleCatalogDto Role,IReadOnlyList<RoleGrantDto> Grants);
public sealed record RoleGrantsDto(long RoleId,IReadOnlyList<RoleGrantDto> Grants);
public sealed record CatalogPage<T>(IReadOnlyList<T> Items,int Page,int PageSize,int Total);
