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
        internal IQueryable<Incident> Apply(IQueryable<Incident> query) => query.Where(i => i.BranchId == Branch
            && i.Branch.IsActive && (Lead || Staff && (i.ReportedBy == User || i.MaintenanceWorks.Any(w => w.AssigneeId == User
                && (w.Department == null || w.Department.BranchId == i.BranchId)
                && (!CurrentAssignmentsOnly || w.Status == "PENDING" || w.Status == "IN_PROGRESS" || w.Status == "ON_HOLD"))))
            && (i.Room == null || i.Room.BranchId == i.BranchId)
            && (i.Area == null || i.Area.BranchId == i.BranchId)
            && (i.Asset == null || i.Asset.BranchId == i.BranchId));
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
                && PermissionGrantRules.EffectiveScope(g.Path) is "OWN" or "BRANCH"), true);
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
