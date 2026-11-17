using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

// GBR-153 defines the actor relationship. IR_VIEW never substitutes for the
// independent reopen grant; granting IR_REOPEN does not grant access to a request.
internal static class InternalRequestReopenAccess
{
    internal static async Task<bool> AllowsAsync(HotelOpsDbContext db, long actor, InternalRequest request, DateTime now, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.UserId == actor && u.AccountStatus == "ACTIVE"
            && u.EmploymentStatus == "ACTIVE" && u.DeletedAt == null, ct)
            || !await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == actor && a.BranchId == request.BranchId
                && a.GrantedAt <= now && a.RevokedAt == null && a.Branch.IsActive, ct)) return false;
        var read = await new InternalRequestReadAccess(db).ReadAsync(actor, now, ct);
        if (!await read.Apply(db.InternalRequests.AsNoTracking(), now).AnyAsync(r => r.InternalRequestId == request.InternalRequestId, ct)) return false;
        var own = actor == request.RequesterId;
        var grants = await new EfPermissionDataSource(db).ReadPathsAsync(actor, "IR_REOPEN", now, ct);
        if (!grants.Any(p => p.PermissionCode == "IR_REOPEN" && PermissionGrantRules.IsEligible(p, now)
            && (PermissionGrantRules.EffectiveScope(p) == "GLOBAL"
                || p.BranchId == request.BranchId && (PermissionGrantRules.EffectiveScope(p) == "BRANCH"
                    || own && PermissionGrantRules.EffectiveScope(p) == "OWN")))) return false;
        if (own) return true;
        if (request.RequesterDepartmentId is not { } department || !await new InternalRequestProcessAuthority(db).InstalledAsync(ct)) return false;
        if (!await db.Departments.AsNoTracking().AnyAsync(d => d.DepartmentId == department && d.BranchId == request.BranchId && d.IsActive, ct)
            || !await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == actor && r.Role.RoleCode == "DEPT_MANAGER" && r.Role.IsActive && r.IsActive
                && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now) && (r.BranchId == request.BranchId || r.BranchId == null), ct)) return false;
        return await db.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM dbo.department_management_access WITH(HOLDLOCK)
            WHERE user_id={actor} AND branch_id={request.BranchId} AND department_id={department}
              AND granted_at<={now} AND revoked_at IS NULL
            """).AnyAsync(ct);
    }
}
