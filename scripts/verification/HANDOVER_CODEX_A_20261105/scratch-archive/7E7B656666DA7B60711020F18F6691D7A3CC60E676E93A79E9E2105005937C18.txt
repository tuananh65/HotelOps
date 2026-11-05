using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Users;

public sealed class UserDirectoryQuery : IValidatableObject
{
    [StringLength(200)]
    public string? Search { get; init; }

    [Range(1, long.MaxValue)]
    public long? BranchId { get; init; }

    [Range(1, long.MaxValue)]
    public long? DepartmentId { get; init; }

    [Range(1, long.MaxValue)]
    public long? RoleId { get; init; }

    public string? AccountStatus { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(AccountStatus)
            && AccountStatus.Trim().ToUpperInvariant() is not ("ACTIVE" or "INACTIVE"))
            yield return new ValidationResult("AccountStatus must be Active or Inactive.", [nameof(AccountStatus)]);
        if (Page > 0 && PageSize > 0 && (long)(Page - 1) * PageSize > int.MaxValue)
            yield return new ValidationResult("The requested page offset is too large.", [nameof(Page)]);
    }
}

public sealed record UserDirectoryBranchDto(long Id, string Code, string Name);
public sealed record UserDirectoryDepartmentDto(long Id, string Code, string Name);
public sealed record UserDirectoryRoleDto(long Id, string Code, string Name, UserDirectoryBranchDto? Branch);

public sealed record UserDirectoryItemDto(long Id, string EmployeeCode, string Username, string FullName,
    string Email, UserDirectoryBranchDto? PrimaryBranch, UserDirectoryDepartmentDto? Department,
    IReadOnlyList<UserDirectoryRoleDto> Roles, string AccountStatus);

public sealed record UserDirectoryResponse(IReadOnlyList<UserDirectoryItemDto> Items, int Page,
    int PageSize, int TotalCount);
