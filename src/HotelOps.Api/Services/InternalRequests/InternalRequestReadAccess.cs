using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

// UC123 only. This scope must never be used to authorize intake or other mutations.
public sealed class InternalRequestReadAccess(HotelOpsDbContext db)
{
    internal sealed record Grant(string RoleCode, PermissionGrantPath Path);
    internal sealed record Scope(long UserId, long[] Branches, long[] RelatedBranches, bool Global)
    {
        internal IQueryable<InternalRequest> Apply(IQueryable<InternalRequest> requests, DateTime now) =>
            requests.Where(r => r.Branch.IsActive && (Global || Branches.Contains(r.BranchId)
                || RelatedBranches.Contains(r.BranchId) && (r.RequesterId == UserId
                    || r.InternalRequestAssignments.Count(a => a.Status == "ACTIVE" && a.EndedAt == null && a.AssignedAt <= now) == 1
                    && r.InternalRequestAssignments.Any(a => a.AssigneeId == UserId && a.Status == "ACTIVE" && a.EndedAt == null && a.AssignedAt <= now))));
    }

    internal static Scope Resolve(long userId, IEnumerable<Grant> grants, DateTime now)
    {
        var valid = grants.Where(g => g.Path.PermissionCode == "IR_VIEW"
            && PermissionGrantRules.IsEligible(g.Path, now)).ToArray();
        var global = valid.Any(g => g.RoleCode == "SYS_ADMIN" && PermissionGrantRules.EffectiveScope(g.Path) == "GLOBAL");
        var branches = valid.Where(g => g.RoleCode is "OPERATIONS_MANAGER" or "DEPT_MANAGER" or "SYS_ADMIN"
            && PermissionGrantRules.EffectiveScope(g.Path) == "BRANCH").Select(g => g.Path.BranchId!.Value).Distinct().Order().ToArray();
        var related = valid.Where(g => PermissionGrantRules.EffectiveScope(g.Path) is "BRANCH" or "OWN")
            .Select(g => g.Path.BranchId!.Value).Distinct().Order().ToArray();
        return new(userId, branches, related, global);
    }

    internal async Task<Scope> ReadAsync(long user, DateTime now, CancellationToken ct)
    {
        var grants = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == user && grant.Permission.PermissionCode == "IR_VIEW"
                && assignment.User.AccountStatus == "ACTIVE" && assignment.User.DeletedAt == null
            select new Grant(assignment.Role.RoleCode, new PermissionGrantPath(grant.Permission.PermissionCode,
                grant.DataScope, assignment.BranchId, assignment.IsActive, assignment.Role.IsActive,
                assignment.AssignedAt, assignment.ExpiresAt, assignment.Branch != null && assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(a => a.UserId == user && a.BranchId == assignment.BranchId
                    && a.RevokedAt == null && a.GrantedAt <= now)))).ToArrayAsync(ct);
        return Resolve(user, grants, now);
    }
}
