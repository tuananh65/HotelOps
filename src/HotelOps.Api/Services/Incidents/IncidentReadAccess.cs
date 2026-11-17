using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

internal static class IncidentReadAccess
{
    internal sealed record Grant(string Role, PermissionGrantPath Path);
    internal sealed record Scope(long User, long Branch, bool Lead, bool Staff, bool CurrentAssignmentsOnly = false)
    {
        internal IQueryable<Incident> Apply(IQueryable<Incident> query)
        {
            var scoped=query.Where(i=>i.BranchId==Branch && i.Branch.IsActive
                && (i.Room==null || i.Room.BranchId==Branch)
                && (i.Area==null || i.Area.BranchId==Branch)
                && (i.Asset==null || i.Asset.BranchId==Branch));
            if(Lead) return scoped;
            if(!Staff) return scoped.Where(i=>false);
            var assigned=IncidentCurrentAssignment.AssignedTo(scoped,User,CurrentAssignmentsOnly).Select(i=>i.IncidentId);
            return scoped.Where(i=>i.ReportedBy==User || assigned.Contains(i.IncidentId));
        }
    }
    internal static Scope Resolve(long user, long branch, IEnumerable<Grant> grants, DateTime now)
    {
        var valid = grants.Where(g => g.Path.PermissionCode == "INCIDENT_VIEW" && g.Path.BranchId == branch
            && PermissionGrantRules.IsEligible(g.Path, now) && PermissionGrantRules.EffectiveScope(g.Path) == "BRANCH").ToArray();
        return new(user, branch, valid.Any(g => g.Role == "MAINTENANCE_LEAD"), valid.Any(g => g.Role == "MAINTENANCE_STAFF"));
    }
    internal static Scope ResolveSource(long user, long branch, IEnumerable<Grant> grants, DateTime now)
    {
        var valid = grants.Where(g => g.Path.PermissionCode == "INCIDENT_VIEW" && g.Path.BranchId == branch
            && PermissionGrantRules.IsEligible(g.Path, now)).ToArray();
        return new(user, branch,
            valid.Any(g => g.Role is "MAINTENANCE_LEAD" or "OPERATIONS_MANAGER" && PermissionGrantRules.EffectiveScope(g.Path) == "BRANCH"),
            valid.Any(g => g.Role is "SYS_ADMIN" or "OPERATIONS_MANAGER" or "HK_SUPERVISOR" or "HK_STAFF"
                or "MAINTENANCE_LEAD" or "MAINTENANCE_STAFF" or "STOREKEEPER" or "DEPT_MANAGER"
                && PermissionGrantRules.EffectiveScope(g.Path) is "OWN" or "BRANCH"));
    }
    internal static async Task<Scope> ReadAsync(HotelOpsDbContext db, long user, long branch, CancellationToken ct, bool sourceRecord = false)
    {
        var now = DateTime.UtcNow;
        var grants = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == user && assignment.BranchId == branch && grant.Permission.PermissionCode == "INCIDENT_VIEW"
                && assignment.User.AccountStatus == "ACTIVE" && assignment.User.EmploymentStatus == "ACTIVE" && assignment.User.DeletedAt == null
            select new Grant(assignment.Role.RoleCode, new PermissionGrantPath(grant.Permission.PermissionCode,
                grant.DataScope, assignment.BranchId, assignment.IsActive, assignment.Role.IsActive, assignment.AssignedAt, assignment.ExpiresAt,
                assignment.Branch != null && assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(a => a.UserId == user && a.BranchId == branch && a.RevokedAt == null && a.GrantedAt <= now))))
            .ToArrayAsync(ct);
        return sourceRecord ? ResolveSource(user, branch, grants, now) : Resolve(user, branch, grants, now);
    }
}
