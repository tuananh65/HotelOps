using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

/// <summary>
/// Validates the stored vocabulary and proven invariants around the approved DEV2
/// responsibility predicates. No workflow mutation or writer-coordination certification.
/// Uses ordinal classification after SQL reads, including case variants allowed by CI SQL.
/// </summary>
internal sealed class Dev2ResponsibilityStateQueries(HotelOpsDbContext db)
{
    internal async Task<IReadOnlyList<string>> ReadUnresolvedAsync(long targetId, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetId);
        var unresolved = new HashSet<string>(StringComparer.Ordinal);
        void Invalid(string code) { unresolved.Add("DEV2_UNKNOWN_STATE_COVERAGE"); unresolved.Add(code); }
        var works = await (from work in db.MaintenanceWorks.AsNoTracking()
            where work.AssigneeId == targetId
            join incident in db.Incidents.AsNoTracking() on work.IncidentId equals (long?)incident.IncidentId into incidents
            from incident in incidents.DefaultIfEmpty()
            select new { work.Status, work.IncidentId, work.ResolvedAt, work.ReviewedAt,
                IncidentStatus = incident == null ? null : incident.Status }).ToArrayAsync(ct);
        foreach (var row in works)
        {
            var incident = row.Status switch
            {
                "PENDING" => "OPEN", "IN_PROGRESS" or "ON_HOLD" => "IN_PROGRESS",
                "COMPLETED" => "RESOLVED", "CLOSED" => "CLOSED", "CANCELLED" => "CANCELLED", _ => null
            };
            if (incident is null) { Invalid("DEV2_MAINTENANCE_STATUS"); continue; }
            if (row.IncidentId != null && row.IncidentStatus != incident) Invalid("DEV2_MAINTENANCE_INCIDENT_STATE");
            if (row.Status is "PENDING" or "IN_PROGRESS" or "ON_HOLD")
            { if (row.ResolvedAt != null) Invalid("DEV2_MAINTENANCE_COMPLETION_MARKER"); }
            else if (row.Status is "COMPLETED" or "CLOSED")
            {
                if (row.ResolvedAt == null || row.Status == "CLOSED" && row.ReviewedAt == null)
                    Invalid("DEV2_MAINTENANCE_COMPLETION_MARKER");
            }
        }
        // Warehouse.IsActive is a nonnullable SQL bit: no unclassified status vocabulary.
        var custody = await db.Assets.AsNoTracking().Where(x => x.CustodianUserId == targetId)
            .Select(x => x.LifecycleStatus).ToArrayAsync(ct);
        if (custody.Any(x => x is not ("IN_USE" or "UNDER_REPAIR" or "RETIRED"))) Invalid("DEV2_ASSET_LIFECYCLE");
        var handovers = await db.AssetHandovers.AsNoTracking().Where(x => x.ToUserId == targetId)
            .Select(x => new { x.HandoverStatus, x.AcceptedAt }).ToArrayAsync(ct);
        foreach (var row in handovers)
        {
            if (row.HandoverStatus is not ("PENDING" or "ACCEPTED" or "REJECTED")) Invalid("DEV2_HANDOVER_STATUS");
            else if (row.HandoverStatus is "PENDING" or "REJECTED" && row.AcceptedAt != null
                || row.HandoverStatus == "ACCEPTED" && row.AcceptedAt == null) Invalid("DEV2_HANDOVER_ACK_MARKER");
        }
        // The patch retains legacy values for read-only compatibility. Their next actor
        // is not specified by the current workflow, so they cannot produce a clear result.
        static bool CurrentRequestState(string? state) => state is "DRAFT" or "PENDING_APPROVAL" or "NEEDS_INFORMATION"
            or "APPROVED" or "PARTIALLY_ISSUED" or "FULLY_ISSUED" or "REMAINDER_CLOSED" or "REJECTED" or "CANCELLED";
        void ReviewCycle(string? parent, string status, int revision, DateTime submitted, DateTime? resolved)
        {
            if (!CurrentRequestState(parent)) Invalid("DEV2_MATERIAL_APPROVAL_PARENT");
            if (status is not ("PENDING" or "NEEDS_INFORMATION" or "APPROVED" or "REJECTED" or "SUPERSEDED"))
            { Invalid("DEV2_MATERIAL_APPROVAL_STATUS"); return; }
            if (revision <= 0 || resolved < submitted || status == "PENDING" && resolved != null || status != "PENDING" && resolved == null)
                Invalid("DEV2_MATERIAL_APPROVAL_MARKER");
            var matches = parent switch
            {
                "PENDING_APPROVAL" => status == "PENDING", "NEEDS_INFORMATION" => status == "NEEDS_INFORMATION",
                "APPROVED" or "PARTIALLY_ISSUED" or "FULLY_ISSUED" or "REMAINDER_CLOSED" => status == "APPROVED",
                "REJECTED" => status == "REJECTED", "CANCELLED" => status != "PENDING", _ => false
            };
            if (!matches) Invalid("DEV2_MATERIAL_REQUEST_CURRENT_CYCLE");
        }
        var requests = await db.MaterialRequests.AsNoTracking().Where(x => x.RequesterId == targetId)
            .Select(x => new { x.Status, Latest = db.MaterialRequestApprovalCycles.Where(c => c.RequestId == x.RequestId)
                .OrderByDescending(c => c.RevisionNo).Select(c => new { c.Status, c.ResolvedAt, c.SubmittedAt, c.RevisionNo }).FirstOrDefault() }).ToArrayAsync(ct);
        foreach (var row in requests)
        {
            if (!CurrentRequestState(row.Status)) Invalid("DEV2_MATERIAL_REQUEST_STATUS_OR_LEGACY");
            else if (row.Latest != null) ReviewCycle(row.Status, row.Latest.Status, row.Latest.RevisionNo, row.Latest.SubmittedAt, row.Latest.ResolvedAt);
            else if (row.Status is not ("DRAFT" or "CANCELLED")) Invalid("DEV2_MATERIAL_REQUEST_CURRENT_CYCLE");
        }
        var cycles = await (from cycle in db.MaterialRequestApprovalCycles.AsNoTracking()
            where cycle.AssignedApproverId == targetId
            join parent in db.MaterialRequests.AsNoTracking() on cycle.RequestId equals parent.RequestId into parents
            from parent in parents.DefaultIfEmpty()
            select new { cycle.Status, cycle.RevisionNo, cycle.SubmittedAt, cycle.ResolvedAt,
                Parent = parent == null ? null : parent.Status,
                IsLatest = !db.MaterialRequestApprovalCycles.Any(newer => newer.RequestId == cycle.RequestId && newer.RevisionNo > cycle.RevisionNo) }).ToArrayAsync(ct);
        foreach (var row in cycles)
        {
            // Greatest revision is the approved effective-cycle contract. Older rows,
            // even older PENDING rows, cannot recreate approver responsibility.
            if (!row.IsLatest) continue;
            ReviewCycle(row.Parent, row.Status, row.RevisionNo, row.SubmittedAt, row.ResolvedAt);
        }
        return unresolved.Order(StringComparer.Ordinal).ToArray();
    }
}
