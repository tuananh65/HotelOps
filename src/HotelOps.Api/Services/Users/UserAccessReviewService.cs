using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

// Only the authenticated read filter creates and consumes this request-local capability.
internal sealed record AuthorizedAccessReviewScope(long ReviewerId, long TargetId, bool IsGlobal, long[] BranchIds)
{
    public bool Covers(AuthorizedAccessReviewScope original) => ReviewerId == original.ReviewerId
        && TargetId == original.TargetId && (IsGlobal || (!original.IsGlobal && original.BranchIds.All(BranchIds.Contains)));
}

public sealed class UserAccessReviewService(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionDataSource data, IUserDirectoryService users, TimeProvider clock)
{
    internal async Task<AuthorizedAccessReviewScope?> ResolveScopeAsync(long targetId, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.IsRestricted || targetId <= 0) return null;
        var now = clock.GetUtcNow().UtcDateTime;
        var view = await Scopes("USER_VIEW");
        var manage = await Scopes("ROLE_MANAGE");
        if (view.Global && manage.Global) return new(actor.UserId, targetId, true, []);
        var permitted = view.Global ? manage.Branches : manage.Global ? view.Branches
            : view.Branches.Intersect(manage.Branches).ToArray();
        if (permitted.Length == 0) return null;
        var visible = await db.UserBranchAccesses.AsNoTracking()
            .Where(x => x.UserId == targetId && x.User.DeletedAt == null && permitted.Contains(x.BranchId)
                && x.RevokedAt == null && x.GrantedAt <= now && x.Branch.IsActive)
            .Select(x => x.BranchId).OrderBy(x => x).ToArrayAsync(ct);
        return visible.Length == 0 ? null : new(actor.UserId, targetId, false, visible);

        async Task<(bool Global, long[] Branches)> Scopes(string code)
        {
            var paths = (await data.ReadPathsAsync(actor.UserId, code, now, ct))
                .Where(p => string.Equals(p.PermissionCode, code, StringComparison.Ordinal)
                    && PermissionGrantRules.IsEligible(p, now)).ToArray();
            return (paths.Any(p => PermissionGrantRules.EffectiveScope(p) == "GLOBAL"),
                paths.Where(p => PermissionGrantRules.EffectiveScope(p) == "BRANCH")
                    .Select(p => p.BranchId!.Value).Distinct().Order().ToArray());
        }
    }

    internal async Task<UserAccessReviewDto?> ReadAsync(AuthorizedAccessReviewScope scope,
        UserAccessReviewQuery query, CancellationToken ct)
    {
        Validator.ValidateObject(query, new ValidationContext(query), true);
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Access review requires the authenticated SQL read boundary.");
        var account = await users.GetAsync(scope.TargetId, ct);
        if (account is null) return null;
        var now = clock.GetUtcNow().UtcDateTime;
        var branches = scope.BranchIds;
        var assignments = db.UserRoles.AsNoTracking().Where(x => x.UserId == scope.TargetId);
        if (!scope.IsGlobal) assignments = assignments.Where(x => x.BranchId != null && branches.Contains(x.BranchId.Value));
        var roles = await assignments.OrderBy(x => x.Role.RoleCode).ThenBy(x => x.BranchId).ThenBy(x => x.UserRoleId)
            .Select(x => new AccessReviewRoleDto(x.UserRoleId, x.RoleId, x.Role.RoleCode, x.Role.RoleName,
                x.BranchId, x.IsActive, x.Role.IsActive, x.AssignedAt, x.ExpiresAt))
            .TagWith("UC011:AssignedRoles").ToListAsync(ct);
        roles = roles.Select(x => x with { AssignedAtUtc = Utc(x.AssignedAtUtc),
            ExpiresAtUtc = x.ExpiresAtUtc is { } expires ? Utc(expires) : null }).ToList();
        var feature = query.Feature?.Trim();
        var code = query.PermissionCode?.Trim();
        var grants = from assignment in assignments
                     join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
                     where (feature == null || feature == "" || grant.Permission.ModuleCode == feature)
                        && (code == null || code == "" || grant.Permission.PermissionCode == code)
                     select new
                     {
                         grant.Permission.PermissionCode, grant.Permission.PermissionName, grant.Permission.ModuleCode,
                         grant.DataScope, assignment.UserRoleId, assignment.Role.RoleCode,
                         Path = new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, assignment.BranchId,
                             assignment.IsActive, assignment.Role.IsActive, assignment.AssignedAt, assignment.ExpiresAt,
                             assignment.Branch != null && assignment.Branch.IsActive,
                             db.UserBranchAccesses.Any(a => a.UserId == scope.TargetId && a.BranchId == assignment.BranchId
                                 && a.RevokedAt == null && a.GrantedAt <= now))
                     };
        var raw = await grants.OrderBy(x => x.PermissionCode).ThenBy(x => x.UserRoleId)
            .TagWith("UC011:PermissionPaths").ToListAsync(ct);
        var configured = raw.Select(x => new AccessReviewGrantDto(x.PermissionCode, x.PermissionName, x.ModuleCode,
            x.DataScope, x.UserRoleId, x.RoleCode, x.Path.BranchId, account.AccountStatus == "ACTIVE"
                && PermissionGrantRules.IsEligible(x.Path, now) && PermissionGrantRules.EffectiveScope(x.Path) != null)).ToList();
        var effective = raw.Where(x => account.AccountStatus == "ACTIVE"
                && PermissionGrantRules.IsEligible(x.Path, now) && PermissionGrantRules.EffectiveScope(x.Path) != null)
            .GroupBy(x => x.PermissionCode, StringComparer.Ordinal).Select(g => new AccessReviewPermissionDto(g.Key,
                g.First().PermissionName, g.First().ModuleCode, g.Select(x => new AccessReviewPermissionSourceDto(
                    x.UserRoleId, x.RoleCode, PermissionGrantRules.EffectiveScope(x.Path)!, x.Path.BranchId,
                    x.Path.DataScope == "OWN")).ToArray())).ToArray();
        var membershipQuery = db.UserBranchAccesses.AsNoTracking().Where(x => x.UserId == scope.TargetId);
        if (!scope.IsGlobal) membershipQuery = membershipQuery.Where(x => branches.Contains(x.BranchId));
        var memberships = await membershipQuery.OrderBy(x => x.BranchId)
            .Select(x => new AccessReviewMembershipDto(x.BranchId, x.Branch.BranchCode, x.Branch.BranchName,
                x.AccessLevel, x.Branch.IsActive, x.GrantedAt, x.RevokedAt))
            .TagWith("UC011:BranchMemberships").ToListAsync(ct);
        memberships = memberships.Select(x => x with { GrantedAtUtc = Utc(x.GrantedAtUtc),
            RevokedAtUtc = x.RevokedAtUtc is { } revoked ? Utc(revoked) : null }).ToList();
        if (!scope.IsGlobal)
        {
            var departmentVisible = account.Department != null && await db.Departments.AsNoTracking()
                .AnyAsync(x => x.DepartmentId == account.Department.Id && branches.Contains(x.BranchId), ct);
            account = account with
            {
                PrimaryBranch = account.PrimaryBranch != null && branches.Contains(account.PrimaryBranch.Id) ? account.PrimaryBranch : null,
                Department = departmentVisible ? account.Department : null
            };
        }
        return new(account, now, new(scope.IsGlobal, branches), roles, memberships, configured, effective);
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
