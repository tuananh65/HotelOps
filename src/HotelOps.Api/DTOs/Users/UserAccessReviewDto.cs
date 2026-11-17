using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Users;

public sealed class UserAccessReviewQuery
{
    [StringLength(60)] public string? Feature { get; init; }
    [StringLength(100)] public string? PermissionCode { get; init; }
}

public sealed record AccessReviewRoleDto(long AssignmentId, long RoleId, string Code, string Name,
    long? BranchId, bool AssignmentActive, bool RoleActive, DateTime AssignedAtUtc, DateTime? ExpiresAtUtc);
public sealed record AccessReviewMembershipDto(long BranchId, string Code, string Name, string AccessLevel,
    bool BranchActive, DateTime GrantedAtUtc, DateTime? RevokedAtUtc);
public sealed record AccessReviewGrantDto(string PermissionCode, string PermissionName, string? Feature,
    string ConfiguredScope, long AssignmentId, string RoleCode, long? BranchId, bool IsEffective);
public sealed record AccessReviewPermissionSourceDto(long AssignmentId, string RoleCode,
    string Scope, long? BranchId, bool RequiresResourceOwnership);
public sealed record AccessReviewPermissionDto(string Code, string Name, string? Feature,
    IReadOnlyList<AccessReviewPermissionSourceDto> Sources);
public sealed record AccessReviewScopeDto(bool IsGlobal, IReadOnlyList<long> BranchIds);
public sealed record AccessReviewDepartmentDto(long BranchId, long DepartmentId, string Code, string Name,
    bool DepartmentActive, DateTime GrantedAtUtc, DateTime? RevokedAtUtc);
public sealed record UserAccessReviewDto(UserAccountDetailDto Account, DateTime EvaluatedAtUtc,
    AccessReviewScopeDto ReviewerScope, IReadOnlyList<AccessReviewRoleDto> AssignedRoles,
    IReadOnlyList<AccessReviewMembershipDto> BranchMemberships,
    IReadOnlyList<AccessReviewGrantDto> ConfiguredGrants,
    IReadOnlyList<AccessReviewPermissionDto> EffectivePermissions)
{
    public bool DepartmentMembershipsAvailable { get; init; }
    public IReadOnlyList<AccessReviewDepartmentDto> DepartmentMemberships { get; init; } = [];
}
