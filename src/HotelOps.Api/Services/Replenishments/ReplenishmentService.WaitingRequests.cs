using HotelOps.Api.DTOs.Replenishments;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.MaterialRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Replenishments;

public sealed partial class ReplenishmentService
{
    // Called only inside the authorized detail read transaction, with independent MR_VIEW scope.
    internal async Task<ReplenishmentDto> WithWaitingRequests(ReplenishmentDto detail,
        IQueryable<MaterialRequest> readableRequests, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Waiting requests require the authorized read transaction.");
        var items = detail.Lines.Select(l => l.ItemId).Distinct().ToArray();
        var rows = await db.MaterialRequestLines.AsNoTracking()
            .Where(l => l.IsActive && items.Contains(l.ItemId) && l.ApprovedQty > l.FulfilledQty
                && l.Request.SourceWarehouseId == detail.WarehouseId && l.Request.BranchId == detail.BranchId
                && (l.Request.Status == "APPROVED" || l.Request.Status == "PARTIALLY_ISSUED")
                && readableRequests.Any(r => r.RequestId == l.RequestId)
                && db.MaterialRequestApprovalCycles.Any(c => c.RequestId == l.RequestId && c.Status == "APPROVED"
                    && !db.MaterialRequestApprovalCycles.Any(later => later.RequestId == c.RequestId && later.RevisionNo > c.RevisionNo)))
            .OrderBy(l => l.RequestId).ThenBy(l => l.LineNo)
            .Select(l => new ReplenishmentWaitingRequest(l.RequestId, l.Request.RequestNo, l.Request.Status,
                l.LineId, l.ItemId, l.Item.ItemName, l.Uom.UomName, l.ApprovedQty!.Value,
                l.FulfilledQty, l.ApprovedQty.Value - l.FulfilledQty)).ToListAsync(ct);
        foreach (var requestId in rows.Select(r => r.RequestId).Distinct())
            if (!await MaterialReadProvenance.ValidAsync(db, requestId, detail.BranchId, ct))
                throw new System.ComponentModel.DataAnnotations.ValidationException("Material request provenance requires reconciliation.");
        return detail with { WaitingRequestsVisible = true, WaitingRequests = rows };
    }
}
