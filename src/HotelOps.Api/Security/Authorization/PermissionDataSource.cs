using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Security.Authorization;

// Read model keeps every grant attached to its own assignment. No role-name bypasses.
public sealed record PermissionGrantPath(string PermissionCode, string DataScope, long? BranchId,
    bool AssignmentActive, bool RoleActive, DateTime AssignedAt, DateTime? ExpiresAt,
    bool BranchActive, bool BranchAccessActive);

public interface IPermissionDataSource
{
    Task<bool> IsActiveUserAsync(long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long userId, string permissionCode,
        DateTime utcNow, CancellationToken cancellationToken);
    Task<bool> IsActiveBranchAsync(long branchId, CancellationToken cancellationToken);
}

public sealed class EfPermissionDataSource(HotelOpsDbContext db) : IPermissionDataSource
{
    public Task<bool> IsActiveUserAsync(long userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().AnyAsync(u => u.UserId == userId && u.AccountStatus == "ACTIVE"
            && u.DeletedAt == null, cancellationToken);

    public Task<bool> IsActiveBranchAsync(long branchId, CancellationToken cancellationToken) =>
        db.Branches.AsNoTracking().AnyAsync(b => b.BranchId == branchId && b.IsActive, cancellationToken);

    public async Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long userId, string permissionCode,
        DateTime utcNow, CancellationToken cancellationToken) =>
        await (from assignment in db.UserRoles.AsNoTracking()
               join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
               where assignment.UserId == userId && grant.Permission.PermissionCode == permissionCode
                   && assignment.User.AccountStatus == "ACTIVE" && assignment.User.DeletedAt == null
               select new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope,
                   assignment.BranchId, assignment.IsActive, assignment.Role.IsActive,
                   assignment.AssignedAt, assignment.ExpiresAt,
                   assignment.Branch != null && assignment.Branch.IsActive,
                   db.UserBranchAccesses.Any(a => a.UserId == userId && a.BranchId == assignment.BranchId
                       && a.RevokedAt == null && a.GrantedAt <= utcNow)))
            .ToListAsync(cancellationToken);
}
