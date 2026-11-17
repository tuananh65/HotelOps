using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

public sealed class UserDirectoryService(HotelOpsDbContext db, TimeProvider clock, UserDirectoryScopeState? scopeState = null) : IUserDirectoryService
{
    public async Task<UserAccountDetailDto?> GetAsync(long userId, CancellationToken cancellationToken)
    {
        if (userId <= 0) throw new ValidationException("UserId must be a positive Int64.");
        cancellationToken.ThrowIfCancellationRequested();
        var scope = RequireScope();
        var global = scope.IsGlobal;
        var branches = scope.BranchIds;
        var now = clock.GetUtcNow().UtcDateTime;
        return await db.Users.AsNoTracking().Where(user => user.UserId == userId && user.DeletedAt == null
                && (global || user.UserBranchAccessUsers.Any(b => branches.Contains(b.BranchId)
                    && b.RevokedAt == null && b.GrantedAt <= now && b.Branch.IsActive)))
            .Select(user => new UserAccountDetailDto(user.UserId, user.EmployeeCode, user.Username,
                user.FullName, user.Email, user.Phone, user.AccountStatus,
                user.PrimaryBranch == null || !global && !branches.Contains(user.PrimaryBranch.BranchId) ? null : new UserDirectoryBranchDto(user.PrimaryBranch.BranchId,
                    user.PrimaryBranch.BranchCode, user.PrimaryBranch.BranchName),
                user.Department == null || !global && !branches.Contains(user.Department.BranchId) ? null : new UserDirectoryDepartmentDto(user.Department.DepartmentId,
                    user.Department.DepartmentCode, user.Department.DepartmentName)))
            .TagWith("UserAccountDetail:Get").SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<UserDirectoryResponse> ListAsync(UserDirectoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var scope = RequireScope();
        Validator.ValidateObject(query, new ValidationContext(query), validateAllProperties: true);
        if (query.BranchId is { } branchId && query.DepartmentId is { } departmentId
            && !await db.Departments.AsNoTracking().AnyAsync(department => department.DepartmentId == departmentId
                && department.BranchId == branchId, cancellationToken))
            throw new ValidationException("Department must belong to the selected branch.");

        // The read filter establishes actor/scope and revalidates it before publishing.
        // One instant gives consistent assignment time predicates in count and page queries.
        var now = clock.GetUtcNow().UtcDateTime;
        var users = UserDirectoryQueries.Filter(db.Users, query, now, scope);
        var total = await users.TagWith("UC141:Count").CountAsync(cancellationToken);
        var items = await UserDirectoryQueries.Page(users, query, now, scope)
            .TagWith("UC141:Page").ToListAsync(cancellationToken);
        return new UserDirectoryResponse(items, query.Page, query.PageSize, total);
    }
    private AuthorizedDirectoryScope RequireScope() => scopeState?.Current
        ?? throw new InvalidOperationException("User directory requires the authenticated scoped read boundary.");
}
