using HotelOps.Api.Entities;
using HotelOps.Api.DTOs.Inventory;

namespace HotelOps.Api.Services.Inventory;

public sealed partial class InventoryService
{
    private long[]? readableWarehouses;
    // Set only by the HTTP controller after production permission evaluation.
    // Trusted in-process workflow queries retain their existing behaviour.
    public void RestrictReads(long[] warehouseIds) => readableWarehouses = warehouseIds;
    private IQueryable<Warehouse> ReadWarehouses() => readableWarehouses is { } ids
        ? db.Warehouses.Where(x => ids.Contains(x.WarehouseId)) : db.Warehouses;
    private IQueryable<StockDocument> ReadDocuments() => readableWarehouses is { } ids
        ? db.StockDocuments.Where(x => (x.SourceWarehouseId == null || ids.Contains(x.SourceWarehouseId.Value))
            && (x.DestinationWarehouseId == null || ids.Contains(x.DestinationWarehouseId.Value))
            && (x.SourceWarehouseId != null || x.DestinationWarehouseId != null)) : db.StockDocuments;
    private IQueryable<Stocktake> ReadStocktakes() => readableWarehouses is { } ids
        ? db.Stocktakes.Where(x => ids.Contains(x.WarehouseId)) : db.Stocktakes;
    private IQueryable<StockMovement> ReadMovements() => readableWarehouses is { } ids
        ? db.StockMovements.Where(x => ids.Contains(x.WarehouseId)) : db.StockMovements;
    private IQueryable<InventoryStockDto> ReadStock() => readableWarehouses is { } ids
        ? StockQuery().Where(x => ids.Contains(x.WarehouseId)) : StockQuery();
}
