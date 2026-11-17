using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

internal static class UserDirectoryQueries
{
    internal static IQueryable<User> Filter(IQueryable<User> users, UserDirectoryQuery query, DateTime utcNow, AuthorizedDirectoryScope? scope = null)
    {
        var global = scope == null || scope.IsGlobal;
        var branches = scope?.BranchIds ?? [];
        users = users.AsNoTracking().Where(user => user.DeletedAt == null
            && (global || user.UserBranchAccessUsers.Any(b => branches.Contains(b.BranchId)
                && b.RevokedAt == null && b.GrantedAt <= utcNow && b.Branch.IsActive)));
        if (query.Search?.Trim() is { Length: > 0 } search)
            users = users.Where(user => user.FullName.Contains(search)
                || user.Username.Contains(search) || user.Email.Contains(search));
        if (query.BranchId is { } branchId)
            users = users.Where(user => user.PrimaryBranchId == branchId && (global || branches.Contains(branchId)));
        if (query.DepartmentId is { } departmentId)
            users = users.Where(user => user.DepartmentId == departmentId && (global || user.Department != null && branches.Contains(user.Department.BranchId)));
        if (query.RoleId is { } roleId)
            users = users.Where(user => user.UserRoleUsers.Any(assignment => assignment.RoleId == roleId
                && (global || assignment.BranchId != null && branches.Contains(assignment.BranchId.Value))
                && assignment.IsActive && assignment.Role.IsActive && assignment.AssignedAt <= utcNow
                && (assignment.ExpiresAt == null || assignment.ExpiresAt > utcNow)));
        if (query.AccountStatus?.Trim().ToUpperInvariant() is { Length: > 0 } status)
            users = users.Where(user => user.AccountStatus == status);
        return users;
    }

    internal static IQueryable<UserDirectoryItemDto> Page(IQueryable<User> users,
        UserDirectoryQuery query, DateTime utcNow, AuthorizedDirectoryScope? scope = null)
    {
        var global = scope == null || scope.IsGlobal;
        var branches = scope?.BranchIds ?? [];
        return users
        .OrderBy(user => user.FullName).ThenBy(user => user.UserId)
        .Skip(checked((query.Page - 1) * query.PageSize)).Take(query.PageSize)
        .Select(user => new UserDirectoryItemDto(user.UserId, user.EmployeeCode, user.Username,
            user.FullName, user.Email,
            user.PrimaryBranch == null || !global && !branches.Contains(user.PrimaryBranch.BranchId) ? null : new UserDirectoryBranchDto(user.PrimaryBranch.BranchId,
                user.PrimaryBranch.BranchCode, user.PrimaryBranch.BranchName),
            user.Department == null || !global && !branches.Contains(user.Department.BranchId) ? null : new UserDirectoryDepartmentDto(user.Department.DepartmentId,
                user.Department.DepartmentCode, user.Department.DepartmentName),
            user.UserRoleUsers.Where(assignment => (global || assignment.BranchId != null && branches.Contains(assignment.BranchId.Value))
                && assignment.IsActive && assignment.Role.IsActive
                && assignment.AssignedAt <= utcNow && (assignment.ExpiresAt == null || assignment.ExpiresAt > utcNow))
                .OrderBy(assignment => assignment.Role.RoleCode).ThenBy(assignment => assignment.UserRoleId)
                .Select(assignment => new UserDirectoryRoleDto(assignment.RoleId, assignment.Role.RoleCode,
                    assignment.Role.RoleName, assignment.Branch == null ? null
                        : new UserDirectoryBranchDto(assignment.Branch.BranchId,
                            assignment.Branch.BranchCode, assignment.Branch.BranchName))).ToList(),
            user.AccountStatus));
    }
}
