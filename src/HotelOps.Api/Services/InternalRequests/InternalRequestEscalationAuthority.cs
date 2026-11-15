using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.InternalRequests;

// UC122 actor/resource authorization only. This does not certify overdue eligibility,
// validate an intervention, or replace the caller's session and final authority checks.
internal sealed class InternalRequestEscalationAuthority(HotelOpsDbContext db)
{
    internal static bool Eligible(string role, PermissionGrantPath path, long branch, DateTime now) =>
        role == "OPERATIONS_MANAGER" && path.PermissionCode == "IR_OVERDUE_HANDLE"
        && path.BranchId == branch && PermissionGrantRules.IsEligible(path, now)
        && PermissionGrantRules.EffectiveScope(path) == "BRANCH";

    internal async Task<bool> AllowsAsync(long user, long request, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Escalation authority requires the caller's serializable transaction.");
        if (user <= 0 || request <= 0) return false;
        var users = await new UserEligibilityLock(db).LockAsync([user], ct);
        if (!users.TryGetValue(user, out var actor) || actor is null || actor.DeletedAt != null
            || actor.AccountStatus != "ACTIVE" || actor.EmploymentStatus != "ACTIVE") return false;
        var target = await db.InternalRequests.FromSqlInterpolated(
            $"SELECT * FROM dbo.internal_requests WITH(UPDLOCK,HOLDLOCK) WHERE internal_request_id={request}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (target is null) return false;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var read = await new InternalRequestReadAccess(db).ReadAsync(user, now, ct);
        if (!await read.Apply(db.InternalRequests.AsNoTracking(), now).AnyAsync(r => r.InternalRequestId == request, ct)) return false;
        var grants = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == user && assignment.Role.RoleCode == "OPERATIONS_MANAGER"
                && grant.Permission.PermissionCode == "IR_OVERDUE_HANDLE" && assignment.BranchId == target.BranchId
            select new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, assignment.BranchId,
                assignment.IsActive, assignment.Role.IsActive, assignment.AssignedAt, assignment.ExpiresAt,
                assignment.Branch != null && assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(a => a.UserId == user && a.BranchId == target.BranchId
                    && a.GrantedAt <= now && a.RevokedAt == null))).ToArrayAsync(ct);
        return grants.Any(p => Eligible("OPERATIONS_MANAGER", p, target.BranchId, now));
    }
}
