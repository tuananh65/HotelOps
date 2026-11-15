using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.InternalRequests;

// Processing authority only. Call again after routing; the caller owns session,
// request version/state validation, audit, and commit of the business operation.
public sealed class InternalRequestProcessAuthority(HotelOpsDbContext db)
{
    internal static bool Eligible(string role, PermissionGrantPath path, long branch, DateTime now) =>
        role == "DEPT_MANAGER" && path.PermissionCode == "IR_PROCESS"
        && path.BranchId == branch && PermissionGrantRules.IsEligible(path, now)
        && PermissionGrantRules.EffectiveScope(path) == "BRANCH";

    public async Task<bool> InstalledAsync(CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.DepartmentAuthority.V1' AND CONVERT(nvarchar(20),value)='1')
              AND COL_LENGTH('dbo.department_management_access','user_id')=8
              AND COL_LENGTH('dbo.department_management_access','branch_id')=8
              AND COL_LENGTH('dbo.department_management_access','department_id')=8
              AND COL_LENGTH('dbo.department_management_access','revision')=8
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.department_management_access')
                AND name IN ('FK_department_management_user','FK_department_management_department')
                AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=2
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.department_management_access')
                AND name='PK_department_management_access' AND is_primary_key=1 AND is_disabled=0)
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    public async Task<bool> AllowsCurrentRequestAsync(long user, long request, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Internal request processing requires the caller's serializable transaction.");
        if (user <= 0 || request <= 0 || !await InstalledAsync(ct)) return false;
        var users = await new UserEligibilityLock(db).LockAsync([user], ct);
        if (!users.TryGetValue(user, out var actor) || actor is null || actor.DeletedAt != null
            || actor.AccountStatus != "ACTIVE" || actor.EmploymentStatus != "ACTIVE") return false;
        // Request lock prevents a concurrent route from changing the department between authorization and write.
        var row = await db.InternalRequests.FromSqlInterpolated($"SELECT * FROM dbo.internal_requests WITH(UPDLOCK,HOLDLOCK) WHERE internal_request_id={request}")
            .AsNoTracking().Select(r => new { r.BranchId, r.ReceivingDepartmentId }).SingleOrDefaultAsync(ct);
        if (row?.ReceivingDepartmentId is not { } department) return false;
        return await AllowsDepartmentAsync(user, row.BranchId, department, ct);
    }

    // Origin authority can be revalidated after Route changes the request's department.
    // This never grants authority over the new receiving department.
    internal async Task<bool> AllowsDepartmentAsync(long user, long branch, long department, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Department authority requires a serializable transaction.");
        if (user <= 0 || branch <= 0 || department <= 0 || !await InstalledAsync(ct)) return false;
        var users = await new UserEligibilityLock(db).LockAsync([user], ct);
        if (!users.TryGetValue(user, out var actor) || actor is null || actor.DeletedAt != null
            || actor.AccountStatus != "ACTIVE" || actor.EmploymentStatus != "ACTIVE") return false;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await db.Departments.AsNoTracking().AnyAsync(d => d.DepartmentId == department && d.BranchId == branch
            && d.IsActive && d.Branch.IsActive, ct)) return false;
        var grants = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == user && assignment.Role.RoleCode == "DEPT_MANAGER"
                && grant.Permission.PermissionCode == "IR_PROCESS" && assignment.BranchId == branch
            select new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, assignment.BranchId,
                assignment.IsActive, assignment.Role.IsActive, assignment.AssignedAt, assignment.ExpiresAt,
                assignment.Branch != null && assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(a => a.UserId == user && a.BranchId == branch
                    && a.RevokedAt == null && a.GrantedAt <= now))).ToArrayAsync(ct);
        if (!grants.Any(p => Eligible("DEPT_MANAGER", p, branch, now))) return false;
        // HOLDLOCK retains the exact key/range even when mapping is absent. Revocation
        // and insertion cannot race this decision; never infer from a user's home department.
        return await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.department_management_access WITH(UPDLOCK,HOLDLOCK)
            WHERE user_id={user} AND branch_id={branch} AND department_id={department}
              AND granted_at<={now} AND revoked_at IS NULL
            """).SingleAsync(ct) == 1;
    }
}
