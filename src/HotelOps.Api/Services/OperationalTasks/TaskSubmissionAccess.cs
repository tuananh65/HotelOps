using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.OperationalTasks;

internal static class TaskSubmissionAccess
{
    // Department Head manages departments in a branch; no invented single-department restriction.
    internal static async Task<long[]> ManagersAsync(HotelOpsDbContext db,long branch,DateTime now,CancellationToken ct)
    {
        var candidates=await db.UserRoles.AsNoTracking().Where(r=>r.Role.RoleCode=="DEPT_MANAGER" && r.IsActive && r.Role.IsActive
            && (r.BranchId==branch || r.BranchId==null) && r.AssignedAt<=now && (r.ExpiresAt==null || r.ExpiresAt>now)
            && r.User.AccountStatus=="ACTIVE" && r.User.EmploymentStatus=="ACTIVE" && r.User.DeletedAt==null
            && db.UserBranchAccesses.Any(a=>a.UserId==r.UserId && a.BranchId==branch && a.GrantedAt<=now && a.RevokedAt==null))
            .Select(r=>r.UserId).Distinct().Order().ToArrayAsync(ct);
        var eligible=new List<long>();
        foreach(var user in candidates)
            if(await BranchGrant(db,user,branch,"TASK_MANAGE",now,ct) && await BranchGrant(db,user,branch,"TASK_VIEW",now,ct)) eligible.Add(user);
        return eligible.ToArray();
    }
    private static async Task<bool> BranchGrant(HotelOpsDbContext db,long user,long branch,string code,DateTime now,CancellationToken ct)
    {
        var data=new EfPermissionDataSource(db);
        if(!await data.IsActiveUserAsync(user,ct)||!await data.IsActiveBranchAsync(branch,ct))return false;
        return (await data.ReadPathsAsync(user,code,now,ct)).Any(p=>p.PermissionCode==code && PermissionGrantRules.IsEligible(p,now)
            && (PermissionGrantRules.EffectiveScope(p)=="GLOBAL" || PermissionGrantRules.EffectiveScope(p)=="BRANCH" && p.BranchId==branch));
    }
}
