namespace HotelOps.Api.Compact.Contracts;

public sealed record UserEmploymentDto(long UserId, long BranchId, string BranchName,
    long? DepartmentId, string? DepartmentName);
public sealed record UserDirectoryRoleDto(long UserId, long RoleId, string Name, long? BranchId);
public sealed record UserDirectoryBranchDto(long UserId, long BranchId, string BranchName);
