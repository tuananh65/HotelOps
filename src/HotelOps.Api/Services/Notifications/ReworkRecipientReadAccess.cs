using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal static class ReworkRecipientReadAccess
{
    internal static IQueryable<HotelOps.Api.Entities.User> EligibleAssignees(HotelOpsDbContext db,long branch,DateTime now)
        => db.Users.AsNoTracking().Where(u=>u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null
            &&u.Department!=null&&u.Department.IsActive&&u.Department.BranchId==branch&&u.Department.DepartmentCode=="HK"
            &&u.UserBranchAccessUsers.Any(a=>a.BranchId==branch&&a.RevokedAt==null&&a.GrantedAt<=now)
            &&db.UserRoles.Any(r=>r.UserId==u.UserId&&r.BranchId==branch&&r.IsActive&&r.Role.IsActive&&r.Role.RoleCode=="HK_STAFF"
                &&r.AssignedAt<=now&&(r.ExpiresAt==null||r.ExpiresAt>now)));
    // Same principal paths and ownership used by the executor; this grants no inspection access.
    internal static async Task<PermissionEvaluation> EvaluateAsync(HotelOpsDbContext db,long actor,long branch,long id,string permission,DateTime now,CancellationToken ct)
    {
        var data=new EfPermissionDataSource(db);
        if(!await data.IsActiveUserAsync(actor,ct)||!await data.IsActiveBranchAsync(branch,ct))return new(PermissionDecision.Denied);
        if(!await db.CleaningReworks.AsNoTracking().AnyAsync(w=>w.ReworkId==id&&w.AssigneeUserId==actor
            &&w.CleaningTask.Room.BranchId==branch&&w.Inspection.RoomId==w.CleaningTask.RoomId
            &&w.Inspection.CleaningTaskId==w.CleaningTaskId,ct))return new(PermissionDecision.Denied);
        foreach(var grant in await data.ReadPathsAsync(actor,permission,now,ct))
            if(grant.PermissionCode==permission&&PermissionGrantRules.IsEligible(grant,now)
                &&(PermissionGrantRules.EffectiveScope(grant)=="GLOBAL"||grant.BranchId==branch
                    &&PermissionGrantRules.EffectiveScope(grant) is "BRANCH" or "OWN"))return new(PermissionDecision.Granted);
        return new(PermissionDecision.Denied);
    }
}
