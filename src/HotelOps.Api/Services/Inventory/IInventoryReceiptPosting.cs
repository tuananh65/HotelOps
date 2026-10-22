using HotelOps.Api.DTOs.Inventory;

namespace HotelOps.Api.Services.Inventory;

public sealed record InventoryBaseQuantity(long BaseUomId, decimal BaseQty);
public sealed record InventoryReceiptPostedLine(long StockDocumentLineId, long ItemId, long UomId, long BaseUomId, decimal ActualQty, decimal BaseQty);
public sealed record InventoryReceiptPosted(long DocumentId, long WarehouseId, IReadOnlyList<InventoryReceiptPostedLine> Lines);
/// <summary>Same scoped context; caller owns transaction and rollback. Never commits.</summary>
public interface IInventoryReceiptPosting
{
    Task<InventoryReceiptPosted> PostReceipt(long id, InventoryPost input, CancellationToken ct);
}
public interface IInventoryQuantityContext
{
    Task<InventoryBaseQuantity> ConvertToBase(long item, long uom, decimal quantity, CancellationToken ct);
    Task<decimal> ObserveStock(long warehouse, long item, CancellationToken ct);
}
