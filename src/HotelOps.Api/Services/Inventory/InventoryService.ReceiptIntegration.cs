using HotelOps.Api.DTOs.Inventory;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed partial class InventoryService : IInventoryReceiptPosting, IInventoryQuantityContext
{
    public async Task<InventoryBaseQuantity> ConvertToBase(long item, long uom, decimal quantity, CancellationToken ct)
    {
        var catalog = await Item(item, ct);
        return new(catalog.BaseUomId, Base(Qty(quantity), await Factor(catalog, uom, ct)));
    }
    public Task<decimal> ObserveStock(long warehouse, long item, CancellationToken ct) => Balance(warehouse, item, ct);
    public async Task<InventoryReceiptPosted> PostReceipt(long id, InventoryReceiptPostRequest input, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Receipt primitive requires caller transaction.");
        await Gate("HotelOps:Inventory:Catalog", ct); await Actor(input.ActorUserId, ct);
        var document = await PostCore("RECEIPT", id, input.ActorUserId, new InventoryPost(input.ExpectedRowVersion, input.Lines), ct) ?? throw Invalid("Receipt document does not exist.");
        var lines = new List<InventoryReceiptPostedLine>();
        foreach (var line in document.StockDocumentLines)
            lines.Add(new(line.LineId, line.ItemId, line.UomId, (await Item(line.ItemId, ct)).BaseUomId, line.ActualQty, line.BaseQty));
        return new(document.DocumentId, document.DestinationWarehouseId!.Value, lines);
    }
}
