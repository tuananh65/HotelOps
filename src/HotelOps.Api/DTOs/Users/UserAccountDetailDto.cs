namespace HotelOps.Api.DTOs.Users;

public sealed record UserAccountDetailDto(long Id, string EmployeeCode, string Username, string FullName,
    string Email, string? Phone, string AccountStatus, UserDirectoryBranchDto? PrimaryBranch,
    UserDirectoryDepartmentDto? Department);
