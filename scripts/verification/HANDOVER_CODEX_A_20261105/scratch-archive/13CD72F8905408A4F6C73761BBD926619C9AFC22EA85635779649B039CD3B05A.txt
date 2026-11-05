using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

/// <summary>
/// Approved DEV2 predicates only. These diagnostic reads do not certify that a user
/// can be deactivated: DEV1 predicates and coordinated writers are still required.
/// Not registered as a service and never used to authorize an account mutation.
/// </summary>
internal sealed class DeactivationResponsibilityQueries(HotelOpsDbContext db)
{
    internal async Task<IReadOnlyList<string>> ReadApprovedDev2BlockersAsync(long targetId, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetId);
        var blockers = new List<string>();
        if (await db.MaintenanceWorks.AsNoTracking().AnyAsync(x => x.AssigneeId == targetId
                && (x.Status == "PENDING" || x.Status == "IN_PROGRESS" || x.Status == "ON_HOLD"), ct))
            blockers.Add("MAINTENANCE_ASSIGNEE");
        if (await db.Warehouses.AsNoTracking().AnyAsync(x => x.ManagerUserId == targetId && x.IsActive, ct))
            blockers.Add("WAREHOUSE_MANAGER");
        if (await db.Assets.AsNoTracking().AnyAsync(x => x.CustodianUserId == targetId
                && (x.LifecycleStatus == "IN_USE" || x.LifecycleStatus == "UNDER_REPAIR"), ct))
            blockers.Add("ASSET_CUSTODIAN");
        if (await db.AssetHandovers.AsNoTracking().AnyAsync(x => x.ToUserId == targetId && x.HandoverStatus == "PENDING", ct))
            blockers.Add("ASSET_RECEIVER");
        if (await db.MaterialRequests.AsNoTracking().AnyAsync(x => x.RequesterId == targetId
                && (x.Status == "DRAFT" || x.Status == "NEEDS_INFORMATION"), ct))
            blockers.Add("MATERIAL_REQUEST_REQUESTER");
        // MaterialRequestService.Latest/Cycle use greatest RevisionNo; the mapping
        // enforces unique (RequestId, RevisionNo). Do not select latest PENDING only:
        // an older pending row must not override a newer resolved/superseding cycle.
        if (await db.MaterialRequestApprovalCycles.AsNoTracking().AnyAsync(x => x.AssignedApproverId == targetId
                && x.Status == "PENDING" && x.Request.Status == "PENDING_APPROVAL"
                && !db.MaterialRequestApprovalCycles.Any(newer => newer.RequestId == x.RequestId
                    && newer.RevisionNo > x.RevisionNo), ct))
            blockers.Add("MATERIAL_REQUEST_APPROVER");
        return blockers.AsReadOnly();
    }
}
