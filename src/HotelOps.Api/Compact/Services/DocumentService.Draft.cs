using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class DocumentService
{
    public Task<object> UpdateReceiptDraft(long id, ReceiptDraftRequest input, CancellationToken ct) => atomic.Run(async () => {
        var document = await Load(id, "create", ct);
        Output.Version(document.RowVersion, input.RowVersion);
        Output.Require(document.Kind == "RECEIPT" && document.Status == "DRAFT" && document.PostedAt == null && document.ApprovedBy == null, "DOCUMENT_NOT_EDITABLE");
        Output.Require((input.SupplierName?.Length ?? 0) <= 200 && (input.Reason?.Length ?? 0) <= 2000, "INVALID_DOCUMENT_HEADER", 400);
        document.SupplierName = input.SupplierName?.Trim();
        document.Reason = input.Reason?.Trim();
        await db.SaveChangesAsync(ct);
        await Event(document, "DRAFT_UPDATED", Guid.NewGuid(), document.Reason, ct);
        return (object)Output.Record(document);
    }, ct);

    public Task<object> AddStocktakeItems(long id, StocktakeItemsRequest input, CancellationToken ct) => atomic.Run(async () => {
        var document = await Load(id, "create", ct);
        Output.Version(document.RowVersion, input.RowVersion);
        Output.Require(document.Kind == "STOCKTAKE" && document.Status == "IN_PROGRESS" && document.SourceWarehouseId != null, "DOCUMENT_NOT_EDITABLE");
        Output.Require(input.ItemIds is { Count: > 0 and <= 100 } && input.ItemIds.Distinct().Count() == input.ItemIds.Count, "DOCUMENT_LINES_REQUIRED", 400);
        var existing = await db.InventoryDocumentLines.Where(l => l.DocumentId == id).Select(l => l.ItemId).ToArrayAsync(ct);
        Output.Require(!input.ItemIds.Any(existing.Contains), "DUPLICATE_STOCKTAKE_ITEM", 409);
        foreach (var itemId in input.ItemIds) {
            var unit = await db.InventoryItems.Where(i => i.ItemId == itemId && i.IsActive).Select(i => (long?)i.BaseUnitId).SingleOrDefaultAsync(ct);
            Output.Require(unit != null, "ITEM_UNAVAILABLE");
            await Line(id, null, new DocumentLineRequest(Convert.ToBase64String(document.RowVersion), itemId, unit!.Value, 1, null, null, 0), ct);
        }
        return (object)Output.Record(document);
    }, ct);
}
