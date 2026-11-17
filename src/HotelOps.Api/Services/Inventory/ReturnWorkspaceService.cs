using System.Data;
using System.Globalization;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed class ReturnWorkspaceService(HotelOpsDbContext db, ReturnPostingAuthority authority,
    InsightAccess access, InventoryService inventory)
{
    public async Task<object> DetailAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await authority.RequireAsync(access.UserId, id, ct);
        var document = await inventory.GetDocument(id, ct) ?? throw new InventoryAccessException(403);
        var originals = await db.StockReturnLineDetails.AsNoTracking().Where(r => r.StockDocumentLine.DocumentId == id)
            .Select(r => new { ReturnLineId = r.StockDocumentLineId, r.OriginalIssueLineId,
                OriginalDocumentNo = r.OriginalIssueLine.Document.DocumentNo, OriginalDocumentId = r.OriginalIssueLine.DocumentId,
                r.StockDocumentLine.Item.ItemName, r.StockDocumentLine.Uom.UomName }).ToArrayAsync(ct);
        await authority.RequireAsync(access.UserId, id, ct);
        await tx.CommitAsync(ct);
        return new { document, originals,
            receivedQuantities = document.Lines.Select(l => new { lineId = l.LineId,
                accepted = l.ActualQty.ToString(CultureInfo.InvariantCulture),
                damaged = l.DamagedQty?.ToString(CultureInfo.InvariantCulture),
                reason = l.DiscrepancyReason }).ToArray(),
            expectedQuantities = document.Lines.Select(l => l.RequestedQty?.ToString(CultureInfo.InvariantCulture)).ToArray() };
    }
}
