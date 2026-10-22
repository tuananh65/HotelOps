using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Services.Inventory;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/inventory")]
public sealed class InventoryController(InventoryService service) : ControllerBase
{
    private async Task<IActionResult> Execute<T>(Func<Task<T>> operation, Func<T, string>? location = null)
    {
        try
        {
            var result = await operation();
            if (result is null) return NotFound();
            return location is null ? Ok(result) : Created(location(result), result);
        }
        catch (ValidationException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Inventory validation failed", detail: exception.Message);
        }
    }
    [HttpGet("items")]
    public Task<IActionResult> Items([FromQuery] InventoryPage page, CancellationToken ct) => Execute(() => service.GetItems(page, ct));
    [HttpGet("items/{id:long}")]
    public Task<IActionResult> Item(long id, CancellationToken ct) => Execute(() => service.GetItem(id, ct));
    [HttpPost("items")]
    public Task<IActionResult> CreateItem(InventoryItemWrite request, CancellationToken ct) => Execute(() => service.SaveItem(null, request, ct), x => $"/api/inventory/items/{x!.ItemId}");
    [HttpPut("items/{id:long}")]
    public Task<IActionResult> UpdateItem(long id, InventoryItemWrite request, CancellationToken ct) => Execute(() => service.SaveItem(id, request, ct));
    [HttpGet("item-categories")]
    public Task<IActionResult> Categories(CancellationToken ct) => Execute(() => service.GetCategories(ct));
    [HttpGet("units")]
    public Task<IActionResult> Units([FromQuery] InventoryPage page, CancellationToken ct) => Execute(() => service.GetUnits(page, ct));
    [HttpGet("units/{id:long}")]
    public Task<IActionResult> Unit(long id, CancellationToken ct) => Execute(() => service.GetUnit(id, ct));
    [HttpPost("units")]
    public Task<IActionResult> CreateUnit(InventoryUnitWrite request, CancellationToken ct) => Execute(() => service.SaveUnit(null, request, ct), x => $"/api/inventory/units/{x!.UomId}");
    [HttpPut("units/{id:long}")]
    public Task<IActionResult> UpdateUnit(long id, InventoryUnitWrite request, CancellationToken ct) => Execute(() => service.SaveUnit(id, request, ct));
    [HttpGet("items/{id:long}/conversions")]
    public Task<IActionResult> Conversions(long id, CancellationToken ct) => Execute(() => service.GetConversions(id, ct));
    [HttpGet("items/{id:long}/conversions/{conversionId:long}")]
    public Task<IActionResult> Conversion(long id, long conversionId, CancellationToken ct) => Execute(() => service.GetConversion(id, conversionId, ct));
    [HttpPost("items/{id:long}/conversions")]
    public Task<IActionResult> CreateConversion(long id, InventoryConversionWrite request, CancellationToken ct) => Execute(() => service.SaveConversion(id, null, request, ct), x => $"/api/inventory/items/{id}/conversions/{x!.ConversionId}");
    [HttpPut("items/{id:long}/conversions/{conversionId:long}")]
    public Task<IActionResult> UpdateConversion(long id, long conversionId, InventoryConversionWrite request, CancellationToken ct) => Execute(() => service.SaveConversion(id, conversionId, request, ct));
    [HttpGet("warehouses")]
    public Task<IActionResult> Warehouses([FromQuery] InventoryPage page, CancellationToken ct) => Execute(() => service.GetWarehouses(page, ct));
    [HttpGet("warehouses/{id:long}")]
    public Task<IActionResult> Warehouse(long id, CancellationToken ct) => Execute(() => service.GetWarehouse(id, ct));
    [HttpPost("warehouses")]
    public Task<IActionResult> CreateWarehouse(InventoryWarehouseWrite request, CancellationToken ct) => Execute(() => service.SaveWarehouse(null, request, ct), x => $"/api/inventory/warehouses/{x!.WarehouseId}");
    [HttpPut("warehouses/{id:long}")]
    public Task<IActionResult> UpdateWarehouse(long id, InventoryWarehouseWrite request, CancellationToken ct) => Execute(() => service.SaveWarehouse(id, request, ct));
    [HttpGet("warehouses/{warehouseId:long}/thresholds")]
    public Task<IActionResult> Thresholds(long warehouseId, CancellationToken ct) => Execute(() => service.GetThresholds(warehouseId, ct));
    [HttpPut("warehouses/{warehouseId:long}/items/{itemId:long}/threshold")]
    public Task<IActionResult> Threshold(long warehouseId, long itemId, InventoryThresholdWrite request, CancellationToken ct) => Execute(() => service.SaveThreshold(warehouseId, itemId, request, ct));
    [HttpGet("stock")]
    public Task<IActionResult> Stock([FromQuery] InventoryPage page, CancellationToken ct) => Execute(() => service.GetStock(page, false, ct));
    [HttpGet("low-stock")]
    public Task<IActionResult> LowStock([FromQuery] InventoryPage page, CancellationToken ct) => Execute(() => service.GetStock(page, true, ct));
    [HttpGet("warehouses/{warehouseId:long}/items/{itemId:long}/stock")]
    public Task<IActionResult> StockPair(long warehouseId, long itemId, CancellationToken ct) => Execute(() => service.GetStock(warehouseId, itemId, ct));
    [HttpGet("movements")]
    public Task<IActionResult> Movements([FromQuery] InventoryPage page, CancellationToken ct) => Execute(() => service.GetMovements(page, ct));
    [HttpGet("warehouses/{warehouseId:long}/items/{itemId:long}/ledger")]
    public Task<IActionResult> Ledger(long warehouseId, long itemId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct) => Execute(() => service.GetLedger(warehouseId, itemId, from, to, ct));
    [HttpGet("documents")]
    public Task<IActionResult> Documents([FromQuery] InventoryPage page, CancellationToken ct) => Execute(() => service.GetDocuments(page, ct));
    [HttpGet("documents/{id:long}")]
    public Task<IActionResult> Document(long id, CancellationToken ct) => Execute(() => service.GetDocument(id, ct));
    [HttpGet("documents/{id:long}/history")]
    public Task<IActionResult> History(long id, CancellationToken ct) => Execute(() => service.GetHistory(id, ct));
    [HttpPost("documents/{id:long}/cancel")]
    public Task<IActionResult> Cancel(long id, InventoryCommand request, CancellationToken ct) => Execute(() => service.CancelDocument(id, request, ct));
    [HttpPost("documents/{id:long}/reverse")]
    public Task<IActionResult> Reverse(long id, InventoryCommand request, CancellationToken ct) => Execute(() => service.Reverse(id, request, ct));
    [HttpPost("receipts")]
    public Task<IActionResult> CreateReceipt(InventoryDraft request, CancellationToken ct) => Execute(() => service.SaveDraft("RECEIPT", null, request, ct), x => $"/api/inventory/documents/{x!.DocumentId}");
    [HttpPut("receipts/{id:long}")]
    public Task<IActionResult> EditReceipt(long id, InventoryDraft request, CancellationToken ct) => Execute(() => service.SaveDraft("RECEIPT", id, request, ct));
    [HttpPost("receipts/{id:long}/post")]
    public Task<IActionResult> PostReceipt(long id, InventoryPost request, CancellationToken ct) => Execute(() => service.Post("RECEIPT", id, request, ct));
    [HttpPost("returns")]
    public Task<IActionResult> CreateReturn(InventoryDraft request, CancellationToken ct) => Execute(() => service.SaveDraft("RETURN", null, request, ct), x => $"/api/inventory/documents/{x!.DocumentId}");
    [HttpPut("returns/{id:long}")]
    public Task<IActionResult> EditReturn(long id, InventoryDraft request, CancellationToken ct) => Execute(() => service.SaveDraft("RETURN", id, request, ct));
    [HttpPost("returns/{id:long}/post")]
    public Task<IActionResult> PostReturn(long id, InventoryPost request, CancellationToken ct) => Execute(() => service.Post("RETURN", id, request, ct));
    [HttpPost("transfers")]
    public Task<IActionResult> CreateTransfer(InventoryDraft request, CancellationToken ct) => Execute(() => service.SaveDraft("TRANSFER", null, request, ct), x => $"/api/inventory/documents/{x!.DocumentId}");
    [HttpPut("transfers/{id:long}")]
    public Task<IActionResult> EditTransfer(long id, InventoryDraft request, CancellationToken ct) => Execute(() => service.SaveDraft("TRANSFER", id, request, ct));
    [HttpPost("transfers/{id:long}/dispatch")]
    public Task<IActionResult> Dispatch(long id, InventoryCommand request, CancellationToken ct) => Execute(() => service.Dispatch(id, request, ct));
    [HttpPost("transfers/{id:long}/receipts")]
    public Task<IActionResult> Receive(long id, InventoryReceive request, CancellationToken ct) => Execute(() => service.Receive(id, request, ct));
    [HttpPost("adjustments")]
    public Task<IActionResult> Adjust(InventoryAdjustment request, CancellationToken ct) => Execute(() => service.CreateAdjustment(request, ct), x => $"/api/inventory/documents/{x.DocumentId}");
    [HttpGet("stocktakes")]
    public Task<IActionResult> Stocktakes([FromQuery] InventoryPage page, CancellationToken ct) => Execute(() => service.GetStocktakes(page, ct));
    [HttpGet("stocktakes/{id:long}")]
    public Task<IActionResult> Stocktake(long id, CancellationToken ct) => Execute(() => service.GetStocktake(id, ct));
    [HttpPost("stocktakes")]
    public Task<IActionResult> CreateStocktake(InventoryStocktakeCreate request, CancellationToken ct) => Execute(() => service.CreateStocktake(request, ct), x => $"/api/inventory/stocktakes/{x.StocktakeId}");
    [HttpPut("stocktakes/{id:long}/counts")]
    public Task<IActionResult> Counts(long id, InventoryCounts request, CancellationToken ct) => Execute(() => service.SaveCounts(id, request, ct));
    [HttpPost("stocktakes/{id:long}/submit")]
    public Task<IActionResult> Submit(long id, InventoryCommand request, CancellationToken ct) => Execute(() => service.StocktakeAction(id, "submit", request, ct));
    [HttpPost("stocktakes/{id:long}/reconcile")]
    public Task<IActionResult> Reconcile(long id, InventoryCommand request, CancellationToken ct) => Execute(() => service.StocktakeAction(id, "reconcile", request, ct));
    [HttpPost("stocktakes/{id:long}/approve-adjustment")]
    public Task<IActionResult> ApproveAdjustment(long id, InventoryCommand request, CancellationToken ct) => Execute(() => service.StocktakeAction(id, "approve-adjustment", request, ct));
    [HttpPost("stocktakes/{id:long}/return")]
    public Task<IActionResult> ReturnStocktake(long id, InventoryCommand request, CancellationToken ct) => Execute(() => service.StocktakeAction(id, "return", request, ct));
    [HttpPost("stocktakes/{id:long}/cancel")]
    public Task<IActionResult> CancelStocktake(long id, InventoryCommand request, CancellationToken ct) => Execute(() => service.StocktakeAction(id, "cancel", request, ct));
}
