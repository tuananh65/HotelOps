using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

internal sealed class InternalRequestIntakeAccess(HotelOpsDbContext db)
{
    internal static bool Eligible(string role, PermissionGrantPath path,long branch,DateTime now) =>
        role is "SYS_ADMIN" or "OPERATIONS_MANAGER" or "HK_SUPERVISOR" or "HK_STAFF" or "MAINTENANCE_LEAD" or "MAINTENANCE_STAFF" or "STOREKEEPER" or "DEPT_MANAGER" or "RECEPTION"
        && path.PermissionCode=="IR_CREATE" && PermissionGrantRules.IsEligible(path,now)
        && (role=="SYS_ADMIN"&&PermissionGrantRules.EffectiveScope(path)=="GLOBAL"
            || path.BranchId==branch&&PermissionGrantRules.EffectiveScope(path) is "OWN" or "BRANCH");

    internal async Task<bool> AllowsAsync(long user,long branch,CancellationToken ct)
    {
        var now=DateTime.UtcNow;
        if(!await db.Users.AsNoTracking().AnyAsync(u=>u.UserId==user&&u.DeletedAt==null&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE",ct)
            || !await db.Branches.AsNoTracking().AnyAsync(b=>b.BranchId==branch&&b.IsActive,ct)) return false;
        var grants=await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId==user&&grant.Permission.PermissionCode=="IR_CREATE"
            select new {Role=assignment.Role.RoleCode,Path=new PermissionGrantPath(grant.Permission.PermissionCode,grant.DataScope,assignment.BranchId,
                assignment.IsActive,assignment.Role.IsActive,assignment.AssignedAt,assignment.ExpiresAt,assignment.Branch!=null&&assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(a=>a.UserId==user&&a.BranchId==assignment.BranchId&&a.RevokedAt==null&&a.GrantedAt<=now))}).ToArrayAsync(ct);
        return grants.Any(g=>Eligible(g.Role,g.Path,branch,now));
    }
}
