using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.MaterialRequests;

// UC096 PRE-02. MR_VIEW/INVENTORY_VIEW remain separate requirements enforced
// by the endpoint contract, including immediately before disclosure.
internal static class MaterialAvailabilityAccess
{
    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db, long requestId, long user,
        DateTime now, CancellationToken ct)
    {
        var request = await db.MaterialRequests.AsNoTracking().SingleOrDefaultAsync(r => r.RequestId == requestId, ct);
        if (request == null || !await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == user
            && (r.Role.RoleCode == "STOREKEEPER" || r.Role.RoleCode == "DEPT_MANAGER" || r.Role.RoleCode == "OPERATIONS_MANAGER")
            && r.IsActive && r.Role.IsActive && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && (r.BranchId == null || r.BranchId == request.BranchId)
            && db.UserBranchAccesses.Any(b => b.UserId == user && b.BranchId == request.BranchId
                && b.Branch.IsActive && b.GrantedAt <= now && b.RevokedAt == null), ct)) return false;
        if (request.Status is "APPROVED" or "PARTIALLY_ISSUED") return true;
        if (request.Status != "PENDING_APPROVAL") return false;
        var cycle = await db.MaterialRequestApprovalCycles.AsNoTracking().Where(c => c.RequestId == requestId)
            .OrderByDescending(c => c.RevisionNo).FirstOrDefaultAsync(ct);
        if (cycle == null || cycle.Status != "PENDING" || cycle.ResolvedAt != null || cycle.AssignedApproverId != user) return false;
        try { await MaterialApprovalAuthority.ValidateAsync(db, request, user, ct); }
        catch (ValidationException) { return false; }
        return true;
    }
}
