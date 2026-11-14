using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Dev2Authorization;

// Facts are not an OWN rule. A signed action contract must choose the actual ownership/SoD predicate.
public sealed record Dev2ResourceFacts(string Kind, long Id, long BranchId, long? ParentId,
    long? OwnerUserId, long? TargetUserId, string State, byte[]? RowVersion);

public sealed class Dev2ResourceFactReader(HotelOpsDbContext db)
{
    public IQueryable<Dev2ResourceFacts> Query(string kind, long id) => kind switch
    {
        "branch" => db.Branches.AsNoTracking().Where(x => x.BranchId == id && x.IsActive)
            .Select(x => new Dev2ResourceFacts("branch", x.BranchId, x.BranchId, null, null, null, "ACTIVE", null)),
        "asset" => db.Assets.AsNoTracking().Where(x => x.AssetId == id)
            .Select(x => new Dev2ResourceFacts("asset", x.AssetId, x.BranchId, null,
                x.CustodianUserId, null, x.LifecycleStatus, null)),
        "asset-handover" => db.AssetHandovers.AsNoTracking().Where(x => x.HandoverId == id)
            .Select(x => new Dev2ResourceFacts("asset-handover", x.HandoverId, x.Asset.BranchId,
                x.AssetId, x.FromUserId, x.ToUserId, x.HandoverStatus, null)),
        "incident" => db.Incidents.AsNoTracking().Where(x => x.IncidentId == id)
            .Select(x => new Dev2ResourceFacts("incident", x.IncidentId, x.BranchId,
                x.AssetId, x.ReportedBy, null, x.Status, null)),
        // Null-Incident work is deliberately excluded: no accepted preventive-work resolver exists.
        "maintenance-work" => db.MaintenanceWorks.AsNoTracking().Where(x => x.MaintenanceWorkId == id && x.IncidentId != null)
            .Select(x => new Dev2ResourceFacts("maintenance-work", x.MaintenanceWorkId, x.Incident!.BranchId,
                x.IncidentId, x.AssigneeId, x.ReviewedBy, x.Status, null)),
        "material-request" => db.MaterialRequests.AsNoTracking().Where(x => x.RequestId == id)
            .Select(x => new Dev2ResourceFacts("material-request", x.RequestId, x.BranchId,
                x.SourceWarehouseId, x.RequesterId,
                x.MaterialRequestApprovalCycles.OrderByDescending(c => c.RevisionNo)
                    .Select(c => (long?)c.AssignedApproverId).FirstOrDefault(), x.Status, x.RowVersion)),
        "material-fulfillment" => db.MaterialFulfillments.AsNoTracking().Where(x => x.FulfillmentId == id)
            .Select(x => new Dev2ResourceFacts("material-fulfillment", x.FulfillmentId, x.Request.BranchId,
                x.RequestId, x.FulfilledBy, null, x.Status, null)),
        "replenishment" => db.MaterialReplenishments.AsNoTracking().Where(x => x.ReplenishmentId == id)
            .Select(x => new Dev2ResourceFacts("replenishment", x.ReplenishmentId, x.Warehouse.BranchId,
                x.WarehouseId, x.RequestedBy, x.ClosedBy, x.Status, x.RowVersion)),
        "warehouse" => db.Warehouses.AsNoTracking().Where(x => x.WarehouseId == id && x.IsActive)
            .Select(x => new Dev2ResourceFacts("warehouse", x.WarehouseId, x.BranchId, null, null, null, "ACTIVE", null)),
        _ => throw new Dev2AccessDenied(403)
    };

    public async Task<Dev2ResourceFacts?> ReadAsync(string kind, long id, long? expectedParentId, CancellationToken ct)
    {
        if (id <= 0) throw new Dev2AccessDenied(403);
        var facts = await Query(kind, id).SingleOrDefaultAsync(ct);
        return Matches(facts, kind, id, expectedParentId) ? facts : null;
    }

    public static bool Matches(Dev2ResourceFacts? facts, string kind, long id, long? expectedParentId) =>
        facts is { BranchId: > 0 } && facts.Kind == kind && facts.Id == id && id > 0
        && (expectedParentId is null || facts.ParentId == expectedParentId);
}
