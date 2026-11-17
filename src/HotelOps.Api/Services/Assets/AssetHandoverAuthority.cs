using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Assets;

// UC069 custody authority is independent of asset location. A user's home
// department or a designation alone never grants handover authority.
internal static class AssetHandoverAuthority
{
    internal static async Task<bool> AllowsAsync(HotelOpsDbContext db,long user,long branch,long department,bool receiver,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel!=IsolationLevel.Serializable)
            throw new InvalidOperationException("Asset custody authority requires a serializable transaction.");
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var permissions=new EfPermissionDataSource(db);
        if(!await permissions.IsActiveUserAsync(user,ct)
            ||!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a=>a.UserId==user&&a.BranchId==branch&&a.Branch.IsActive
                &&a.RevokedAt==null&&a.GrantedAt<=now,ct)
            ||!await db.Departments.AsNoTracking().AnyAsync(d=>d.DepartmentId==department&&d.BranchId==branch&&d.IsActive,ct))return false;
        var read=await permissions.ReadPathsAsync(user,"ASSET_VIEW",now,ct);
        if(!read.Any(p=>PermissionGrantRules.IsEligible(p,now)
            &&(PermissionGrantRules.EffectiveScope(p)=="GLOBAL"||p.BranchId==branch&&PermissionGrantRules.EffectiveScope(p)=="BRANCH")))return false;
        var paths=await(from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId==user&&grant.Permission.PermissionCode=="ASSET_TRANSFER"
                &&(assignment.Role.RoleCode=="DEPT_MANAGER"||!receiver&&assignment.Role.RoleCode=="OPERATIONS_MANAGER")
            select new{Role=assignment.Role.RoleCode,Path=new PermissionGrantPath(grant.Permission.PermissionCode,grant.DataScope,assignment.BranchId,
                assignment.IsActive,assignment.Role.IsActive,assignment.AssignedAt,assignment.ExpiresAt,
                assignment.Branch!=null&&assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(a=>a.UserId==user&&a.BranchId==branch&&a.RevokedAt==null&&a.GrantedAt<=now))}).ToArrayAsync(ct);
        var eligible=paths.Where(p=>PermissionGrantRules.IsEligible(p.Path,now)
            &&(PermissionGrantRules.EffectiveScope(p.Path)=="GLOBAL"||p.Path.BranchId==branch&&PermissionGrantRules.EffectiveScope(p.Path)=="BRANCH")).ToArray();
        if(!receiver&&eligible.Any(p=>p.Role=="OPERATIONS_MANAGER"))return true;
        if(!eligible.Any(p=>p.Role=="DEPT_MANAGER")||!await new InternalRequestProcessAuthority(db).InstalledAsync(ct))return false;
        return await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.department_management_access WITH(HOLDLOCK)
            WHERE user_id={user} AND branch_id={branch} AND department_id={department}
              AND granted_at<={now} AND revoked_at IS NULL
            """).SingleAsync(ct)==1;
    }
}
