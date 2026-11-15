using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

// UC135 only. Keep the role and REPORT_VIEW grant on the same assignment path.
public sealed class ReportAccess(HotelOpsDbContext db, InsightAccess access, TimeProvider clock)
{
    public long UserId => access.UserId;

    internal static bool Allows(string roleCode, PermissionGrantPath path, long? branch, DateTime now)
        => roleCode is "OPERATIONS_MANAGER" or "OWNER"
        && path.PermissionCode == "REPORT_VIEW" && PermissionGrantRules.IsEligible(path, now)
        && (path.DataScope == "ALL" && path.BranchId == null
            || branch != null && path.BranchId == branch && path.DataScope is "ALL" or "BRANCH");

    public async Task<InsightFilters> FiltersAsync(InsightQuery query, DateTimeOffset now, CancellationToken ct)
    {
        var filters = await access.FiltersAsync("REPORT_VIEW", query, now, ct);
        var allowed = new List<long>();
        foreach (var branch in filters.BranchIds)
            if (await AllowedAsync(branch, ct)) allowed.Add(branch);
        if (query.BranchId != null && allowed.Count != 1 || allowed.Count == 0 && !await AllowedAsync(null, ct))
            throw new InsightException(403, "REPORT_ACTOR_DENIED", "Reports require an authorized Operations Manager or Owner role.");
        if (query.DepartmentId is {} department && !await db.Departments.AsNoTracking()
            .AnyAsync(d => d.DepartmentId == department && allowed.Contains(d.BranchId), ct))
            throw new InsightException(403, "DEPARTMENT_SCOPE", "Department outside report scope.");
        return filters with { BranchIds = allowed.ToArray() };
    }

    private async Task<bool> AllowedAsync(long? branch, CancellationToken ct)
    {
        var user = UserId;
        var now = clock.GetUtcNow().UtcDateTime;
        var paths = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == user && grant.Permission.PermissionCode == "REPORT_VIEW"
                && assignment.User.AccountStatus == "ACTIVE" && assignment.User.DeletedAt == null
                && (assignment.Role.RoleCode == "OPERATIONS_MANAGER" || assignment.Role.RoleCode == "OWNER")
            select new { assignment.Role.RoleCode, Path = new PermissionGrantPath(grant.Permission.PermissionCode,
                grant.DataScope, assignment.BranchId, assignment.IsActive, assignment.Role.IsActive,
                assignment.AssignedAt, assignment.ExpiresAt, assignment.Branch != null && assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(a => a.UserId == user && a.BranchId == assignment.BranchId
                    && a.RevokedAt == null && a.GrantedAt <= now)) }).ToArrayAsync(ct);
        return paths.Any(p => Allows(p.RoleCode, p.Path, branch, now));
    }
}
