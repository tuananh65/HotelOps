using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

// UC136/137 have their own Administrator actor, independent of UC135 report actors.
public sealed class AuditAccess(HotelOpsDbContext db, InsightAccess access, TimeProvider clock)
{
    public long UserId => access.UserId;

    internal static bool Allows(string roleCode, PermissionGrantPath path, long? branch, DateTime now)
        => roleCode == "SYS_ADMIN" && path.PermissionCode == "AUDIT_VIEW" && PermissionGrantRules.IsEligible(path, now)
            && (path.DataScope == "ALL" && path.BranchId == null
                || branch != null && path.BranchId == branch && path.DataScope is "ALL" or "BRANCH");

    public async Task<bool> HasGlobalAsync(CancellationToken ct)
        => await access.HasGlobalAsync("AUDIT_VIEW", ct) && await AllowedAsync(null, ct);

    public async Task<long[]> BranchesAsync(long? branch, CancellationToken ct)
    {
        var candidates = await access.BranchesAsync("AUDIT_VIEW", branch, ct);
        var allowed = new List<long>();
        foreach (var candidate in candidates)
            if (await AllowedAsync(candidate, ct)) allowed.Add(candidate);
        if (branch != null && allowed.Count != 1 || allowed.Count == 0 && !await AllowedAsync(null, ct))
            throw new InsightException(403, "AUDIT_ACTOR_DENIED", "Audit history requires an authorized Administrator role.");
        return allowed.ToArray();
    }

    private async Task<bool> AllowedAsync(long? branch, CancellationToken ct)
    {
        var user = UserId;
        var now = clock.GetUtcNow().UtcDateTime;
        var paths = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == user && grant.Permission.PermissionCode == "AUDIT_VIEW"
                && assignment.User.AccountStatus == "ACTIVE" && assignment.User.DeletedAt == null
                && assignment.Role.RoleCode == "SYS_ADMIN"
            select new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, assignment.BranchId,
                assignment.IsActive, assignment.Role.IsActive, assignment.AssignedAt, assignment.ExpiresAt,
                assignment.Branch != null && assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(a => a.UserId == user && a.BranchId == assignment.BranchId
                    && a.RevokedAt == null && a.GrantedAt <= now))).ToArrayAsync(ct);
        return paths.Any(path => Allows("SYS_ADMIN", path, branch, now));
    }
}
