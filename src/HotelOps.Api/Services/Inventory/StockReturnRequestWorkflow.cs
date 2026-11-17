using System.Data;
using System.Globalization;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed class StockReturnRequestWorkflow(HotelOpsDbContext db, InsightAccess access,
    StockReturnRequestAuthority authority, InventoryService inventory)
{
    public async Task<object> IssueAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await inventory.AcquireCatalog(ct);
        var lines = await authority.IssuedAsync(access.UserId, id, ct);
        var document = await db.StockDocuments.AsNoTracking().SingleAsync(d => d.DocumentId == id, ct);
        var result = new List<object>();
        foreach (var line in lines)
        {
            var capacity = await inventory.Capacity(line.LineId, null, ct);
            var names = await db.StockDocumentLines.AsNoTracking().Where(l => l.LineId == line.LineId)
                .Select(l => new { l.Item.ItemName, l.Uom.UomName }).SingleAsync(ct);
            if (line.ConversionFactor <= 0) throw new InsightException(409, "ISSUE_UNIT_INVALID", "Historical issue conversion unavailable.");
            result.Add(new { line.LineId, line.ItemId, line.UomId, names.ItemName, names.UomName,
                Issued = Text(capacity.IssuedBaseQty / line.ConversionFactor), Returned = Text(capacity.ReturnedBaseQty / line.ConversionFactor),
                Consumed = Text(capacity.ConsumedBaseQty / line.ConversionFactor), Remaining = Text(capacity.RemainingBaseQty / line.ConversionFactor) });
        }
        await authority.IssuedAsync(access.UserId, id, ct);
        await tx.CommitAsync(ct);
        return new { document.DocumentId, document.DocumentNo, WarehouseId = document.SourceWarehouseId, Lines = result };
    }

    public async Task<object> ReadAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var source = await db.StockDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.DocumentId == id)
            ?? throw new InsightException(404, "RETURN_UNAVAILABLE", "Return request unavailable.");
        await authority.RequireOwnAsync(access.UserId, source, ct);
        var document = await inventory.GetDocument(id, ct);
        var approvalValid = source.ApprovedBy != null && await StockReturnApprovalStore.InstalledAsync(db, ct)
            && await StockReturnApprovalStore.MatchesAsync(db, id, source.Status == "DRAFT", ct);
        await authority.RequireOwnAsync(access.UserId, source, ct);
        await tx.CommitAsync(ct);
        return new { Document = document, source.ApprovedBy, source.ApprovedAt, ApprovalValid = approvalValid,
            CanWithdraw = source.Status == "DRAFT" };
    }

    public Task<InventoryDocumentDto?> SubmitAsync(InventoryDraft input, CancellationToken ct)
        => inventory.SaveDraft("RETURN", null, access.UserId, input, ct);

    public Task<InventoryDocumentDto?> WithdrawAsync(long id, InventoryCommand input, CancellationToken ct)
        => inventory.CancelDocument(id, access.UserId, input, ct);

    private static string Text(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
}
