using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

// UC119 receipt access only. Never add former/pending handlers to UC123 request scope.
internal static class InternalRequestHandoverReadAccess
{
    internal static async Task<bool> HasScopeAsync(HotelOpsDbContext db,long user,long branch,DateTime now,CancellationToken ct)
    {
        var scope=await new InternalRequestReadAccess(db).ReadAsync(user,now,ct);
        return (scope.Global||scope.Branches.Contains(branch)||scope.RelatedBranches.Contains(branch))
            &&await db.UserBranchAccesses.AsNoTracking().AnyAsync(a=>a.UserId==user&&a.BranchId==branch
                &&a.GrantedAt<=now&&a.RevokedAt==null&&a.Branch.IsActive&&a.User.AccountStatus=="ACTIVE"
                &&a.User.EmploymentStatus=="ACTIVE"&&a.User.DeletedAt==null,ct);
    }

    internal static async Task<bool> AllowsAsync(HotelOpsDbContext db,long user,InternalRequestHandoverRow receipt,DateTime now,CancellationToken ct)
        =>receipt.CreatedAt<=now&&await HasScopeAsync(db,user,receipt.BranchId,now,ct)
            &&await db.InternalRequestAssignments.AsNoTracking().AnyAsync(a=>a.AssignmentId==receipt.SenderAssignmentId
                &&a.InternalRequestId==receipt.RequestId&&a.InternalRequest.BranchId==receipt.BranchId
                &&a.AssignedAt<=receipt.CreatedAt&&(a.EndedAt==null||a.EndedAt>=receipt.CreatedAt)
                &&(a.AssigneeId==user||receipt.ReceiverId==user),ct);
}
