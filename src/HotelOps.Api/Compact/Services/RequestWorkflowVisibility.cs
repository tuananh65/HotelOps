using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

internal static class RequestWorkflowVisibility
{
    // Shared by request deeplinks/notifications without a DI cycle through NotificationService.
    // Null means legacy work: keep its existing authorization path.
    internal static async Task<bool?> CanRead(HotelOpsCompactDbContext db,Access access,long user,long workId,DateTime now,CancellationToken ct)
    {
        var flow=await db.RequestWorkflows.AsNoTracking().SingleOrDefaultAsync(r=>r.WorkId==workId,ct);
        if(flow==null)return null;
        if(!await access.Allows("IR_VIEW",flow.BranchId,true,ct:ct))return false;
        if(await access.HasRole("OPERATION_MANAGER",flow.BranchId,ct)||await access.Allows("IR_VIEW",null,ct:ct))return true;
        if(await db.WorkItems.AnyAsync(w=>w.WorkId==workId&&w.CreatedBy==user,ct)||flow.HandlerUserId==user)return true;
        if(await db.RequestWorkflowEvents.AnyAsync(e=>e.WorkId==workId&&(e.ActorUserId==user||e.NewHandlerUserId==user),ct))return true;
        return flow.QueueRoleId!=null&&await db.UserRoles.AnyAsync(ur=>ur.UserId==user&&ur.BranchId==flow.BranchId&&ur.RoleId==flow.QueueRoleId&&ur.AssignedAt<=now&&ur.RevokedAt==null&&(ur.ExpiresAt==null||ur.ExpiresAt>now)&&db.Roles.Any(r=>r.RoleId==ur.RoleId&&r.IsActive),ct);
    }
}
