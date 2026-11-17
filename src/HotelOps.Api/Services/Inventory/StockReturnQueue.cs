using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed class StockReturnQueue(HotelOpsDbContext db, InsightAccess access, DashboardSessionGuard session,
    StockReturnRequestAuthority requester, StockReturnApprovalAuthority approver)
{
    public async Task<object> ReadAsync(string mode, long after, CancellationToken ct)
    {
        if (mode is not ("mine" or "post" or "approve") || after < 0) throw new InsightException(400, "INVALID_QUEUE", "Select a valid return queue.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await session.RequireAsync(ct);
        var branches = new List<long>();
        foreach (var branch in await db.Branches.AsNoTracking().Where(b => b.IsActive).Select(b => b.BranchId).ToArrayAsync(ct))
            if (await StockReturnReadAccess.QueueBranchAsync(db, access.UserId, branch, mode, ct)) branches.Add(branch);
        if (branches.Count == 0) throw new InsightException(403, "ACCESS_DENIED", "No current grant for this return queue.");
        // A bounded scan cursor is not a count of authorized or hidden documents.
        var candidates = await db.StockDocuments.AsNoTracking().Where(d => d.DocumentType == "RETURN" && d.DocumentId > after
            && d.DestinationWarehouse != null && d.DestinationWarehouse.IsActive && branches.Contains(d.DestinationWarehouse.BranchId)
            && (mode == "mine" ? d.CreatedBy == access.UserId : d.Status == "DRAFT")
            && (mode != "approve" || d.CreatedBy != access.UserId && d.ApprovedBy == null && d.ApprovedAt == null))
            .OrderBy(d => d.DocumentId).Take(100).ToArrayAsync(ct);
        var rows = new List<object>();
        var included = new List<long>();
        foreach (var d in candidates)
        {
            try
            {
                if (mode == "mine") await requester.RequireOwnAsync(access.UserId, d, ct);
                else if (mode == "approve") await approver.RequireAsync(d.DocumentId, ct);
                else if (!await StockReturnReadAccess.StorekeeperAsync(db, access.UserId, d.DocumentId, ct)) continue;
                rows.Add(new { d.DocumentId, d.DocumentNo, d.Status, d.CreatedAt, d.ApprovedAt });
                included.Add(d.DocumentId);
            }
            catch (InsightException e) when (e.Status is 403 or 404) { }
        }
        foreach (var branch in branches)
            if (!await StockReturnReadAccess.QueueBranchAsync(db, access.UserId, branch, mode, ct))
                throw new InsightException(403, "ACCESS_CHANGED", "Return queue access changed; reload.");
        foreach (var id in included)
        {
            if (mode == "approve") await approver.RequireAsync(id, ct);
            else if (mode == "post" && !await StockReturnReadAccess.StorekeeperAsync(db, access.UserId, id, ct))
                throw new InsightException(403, "ACCESS_CHANGED", "Return source access changed; reload.");
        }
        await session.RequireAsync(ct);
        await tx.CommitAsync(ct);
        return new { Items = rows, NextAfter = candidates.Length == 100 ? (long?)candidates[^1].DocumentId : null };
    }
}
