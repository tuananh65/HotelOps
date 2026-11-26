using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    private Task<bool> IsReadOnlyWorkActor(string kind,long branchId,CancellationToken ct) =>
        (from assignment in db.UserRoles.AsNoTracking()
         join role in db.Roles on assignment.RoleId equals role.RoleId
         where assignment.UserId==actor.Id&&assignment.AssignedAt<=Now&&assignment.RevokedAt==null
             &&(assignment.ExpiresAt==null||assignment.ExpiresAt>Now)&&role.IsActive
             &&(assignment.BranchId==null||assignment.BranchId==branchId)
             &&(role.Code=="OWNER"||((kind=="INCIDENT"||kind=="INTERNAL_REQUEST"||kind=="OPERATIONAL")&&role.Code=="OPERATION_MANAGER"))
         select assignment.UserRoleId).AnyAsync(ct);

    private async Task RequireWorkMutation(string kind,long branchId,CancellationToken ct)
    {
        // Enforce workflow restrictions even if an obsolete or additional grant survives migration.
        Output.Require(!await IsReadOnlyWorkActor(kind,branchId,ct),"WORK_ROLE_READ_ONLY",403);
        if(kind=="MAINTENANCE")
            Output.Require(await access.HasRole("MAINTENANCE_SUPERVISOR",branchId,ct)||await access.HasRole("MAINTENANCE_STAFF",branchId,ct),"MAINTENANCE_ROLE_REQUIRED",403);
    }
}
