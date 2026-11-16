using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

internal static class MaintenanceReadAccess
{
    internal sealed record Grant(string Role, PermissionGrantPath Path);
    internal sealed record Scope(long User, long Branch, bool Lead, bool Staff)
    {
        internal IQueryable<MaintenanceWork> Apply(IQueryable<MaintenanceWork> query) => query.Where(w =>
            (Lead || Staff && w.AssigneeId == User)
            && (w.Department == null || w.Department.BranchId == Branch)
            && (w.Incident != null && w.Incident.BranchId == Branch && w.Incident.Branch.IsActive
                && (w.Incident.Room == null || w.Incident.Room.BranchId == Branch)
                && (w.Incident.Area == null || w.Incident.Area.BranchId == Branch)
                && (w.Incident.Asset == null || w.Incident.Asset.BranchId == Branch)
                && !w.PreventiveMaintenanceRuns.Any()
              || w.Incident == null && w.PreventiveMaintenanceRuns.Count() == 1
                && w.PreventiveMaintenanceRuns.Any(r =>
                    (r.Schedule.AssetId != null ? 1 : 0) + (r.Schedule.RoomId != null ? 1 : 0) + (r.Schedule.AreaId != null ? 1 : 0) == 1
                    && (r.Schedule.AssignedDepartment == null || r.Schedule.AssignedDepartment.BranchId == Branch)
                    && (r.Schedule.Asset != null && r.Schedule.Asset.BranchId == Branch && r.Schedule.Asset.Branch.IsActive
                        || r.Schedule.Room != null && r.Schedule.Room.BranchId == Branch && r.Schedule.Room.Branch.IsActive
                        || r.Schedule.Area != null && r.Schedule.Area.BranchId == Branch && r.Schedule.Area.Branch.IsActive))));
    }

    internal static Scope Resolve(long user, long branch, IEnumerable<Grant> grants, DateTime now)
    {
        var valid = grants.Where(g => g.Path.PermissionCode == "MAINTENANCE_VIEW" && g.Path.BranchId == branch
            && PermissionGrantRules.IsEligible(g.Path, now) && PermissionGrantRules.EffectiveScope(g.Path) is "BRANCH" or "OWN" or "GLOBAL").ToArray();
        return new(user, branch, valid.Any(g => g.Role == "MAINTENANCE_LEAD" && PermissionGrantRules.EffectiveScope(g.Path) is "BRANCH" or "GLOBAL"), valid.Any(g => g.Role is "MAINTENANCE_STAFF" or "MAINTENANCE_LEAD"));
    }
    internal static async Task<Scope> ReadAsync(HotelOpsDbContext db, long user, long branch, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var grants = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == user && assignment.BranchId == branch && grant.Permission.PermissionCode == "MAINTENANCE_VIEW"
                && assignment.User.AccountStatus == "ACTIVE" && assignment.User.EmploymentStatus == "ACTIVE" && assignment.User.DeletedAt == null
            select new Grant(assignment.Role.RoleCode, new PermissionGrantPath(grant.Permission.PermissionCode,
                grant.DataScope, assignment.BranchId, assignment.IsActive, assignment.Role.IsActive, assignment.AssignedAt, assignment.ExpiresAt,
                assignment.Branch != null && assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(a => a.UserId == user && a.BranchId == branch && a.RevokedAt == null && a.GrantedAt <= now))))
            .ToArrayAsync(ct);
        return Resolve(user, branch, grants, now);
    }
}
