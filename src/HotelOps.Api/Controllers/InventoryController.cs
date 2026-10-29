using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Services.Inventory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;

namespace HotelOps.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[Route("api/inventory")]
public sealed class InventoryController(InventoryService service, InventoryAccess access, ICurrentUserAccessor current) : ControllerBase
{
    private async Task<IActionResult> Execute<T>(Func<long, Task<T>> operation, Func<T, string>? location = null, object? input = null)
    {
        try
        {
            if (!current.TryGetCurrentUser(out var caller)) return Unauthorized();
            if (caller.IsRestricted) return Forbid(HotelOpsCookieTicket.Scheme);
            if (!await access.Authorize(ControllerContext.ActionDescriptor.ActionName, RouteData.Values, input, HttpContext.RequestAborted)) return NotFound();
            if (HttpMethods.IsGet(Request.Method)) service.RestrictReads(await access.ReadWarehouses(HttpContext.RequestAborted));
            var result = await operation(caller.UserId);
            if (result is null) return NotFound();
            return location is null ? Ok(result) : Created(location(result), result);
        }
        catch (InventoryAccessException e) { return e.Status == 401 ? Unauthorized() : Forbid(HotelOpsCookieTicket.Scheme); }
        catch (ValidationException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Inventory validation failed", detail: exception.Message);
        }
    }
    [HttpGet("items")]
    public Task<IActionResult> Items([FromQuery] InventoryPage page, CancellationToken ct) => Execute(actor => service.GetItems(page, ct));
    [HttpGet("items/{id:long}")]
    public Task<IActionResult> Item(long id, CancellationToken ct) => Execute(actor => service.GetItem(id, ct));
    [HttpPost("items")]
    public Task<IActionResult> CreateItem(InventoryItemWrite request, CancellationToken ct) => Execute(actor => service.SaveItem(null, actor, request, ct), x => $"/api/inventory/items/{x!.ItemId}", input: request);
    [HttpPut("items/{id:long}")]
    public Task<IActionResult> UpdateItem(long id, InventoryItemWrite request, CancellationToken ct) => Execute(actor => service.SaveItem(id, actor, request, ct), input: request);
    [HttpGet("item-categories")]
    public Task<IActionResult> Categories(CancellationToken ct) => Execute(actor => service.GetCategories(ct));
    [HttpGet("units")]
    public Task<IActionResult> Units([FromQuery] InventoryPage page, CancellationToken ct) => Execute(actor => service.GetUnits(page, ct));
    [HttpGet("units/{id:long}")]
    public Task<IActionResult> Unit(long id, CancellationToken ct) => Execute(actor => service.GetUnit(id, ct));
    [HttpPost("units")]
    public Task<IActionResult> CreateUnit(InventoryUnitWrite request, CancellationToken ct) => Execute(actor => service.SaveUnit(null, actor, request, ct), x => $"/api/inventory/units/{x!.UomId}", input: request);
    [HttpPut("units/{id:long}")]
    public Task<IActionResult> UpdateUnit(long id, InventoryUnitWrite request, CancellationToken ct) => Execute(actor => service.SaveUnit(id, actor, request, ct), input: request);
    [HttpGet("items/{id:long}/conversions")]
    public Task<IActionResult> Conversions(long id, CancellationToken ct) => Execute(actor => service.GetConversions(id, ct));
    [HttpGet("items/{id:long}/conversions/{conversionId:long}")]
    public Task<IActionResult> Conversion(long id, long conversionId, CancellationToken ct) => Execute(actor => service.GetConversion(id, conversionId, ct));
    [HttpPost("items/{id:long}/conversions")]
    public Task<IActionResult> CreateConversion(long id, InventoryConversionWrite request, CancellationToken ct) => Execute(actor => service.SaveConversion(id, null, actor, request, ct), x => $"/api/inventory/items/{id}/conversions/{x!.ConversionId}", input: request);
    [HttpPut("items/{id:long}/conversions/{conversionId:long}")]
    public Task<IActionResult> UpdateConversion(long id, long conversionId, InventoryConversionWrite request, CancellationToken ct) => Execute(actor => service.SaveConversion(id, conversionId, actor, request, ct), input: request);
    [HttpGet("warehouses")]
    public Task<IActionResult> Warehouses([FromQuery] InventoryPage page, CancellationToken ct) => Execute(actor => service.GetWarehouses(page, ct));
    [HttpGet("warehouses/{id:long}")]
    public Task<IActionResult> Warehouse(long id, CancellationToken ct) => Execute(actor => service.GetWarehouse(id, ct));
    [HttpPost("warehouses")]
    public Task<IActionResult> CreateWarehouse(InventoryWarehouseWrite request, CancellationToken ct) => Execute(actor => service.SaveWarehouse(null, actor, request, ct), x => $"/api/inventory/warehouses/{x!.WarehouseId}", input: request);
    [HttpPut("warehouses/{id:long}")]
    public Task<IActionResult> UpdateWarehouse(long id, InventoryWarehouseWrite request, CancellationToken ct) => Execute(actor => service.SaveWarehouse(id, actor, request, ct), input: request);
    [HttpGet("warehouses/{warehouseId:long}/thresholds")]
    public Task<IActionResult> Thresholds(long warehouseId, CancellationToken ct) => Execute(actor => service.GetThresholds(warehouseId, ct));
    [HttpPut("warehouses/{warehouseId:long}/items/{itemId:long}/threshold")]
    public Task<IActionResult> Threshold(long warehouseId, long itemId, InventoryThresholdWrite request, CancellationToken ct) => Execute(actor => service.SaveThreshold(warehouseId, itemId, actor, request, ct), input: request);
    [HttpGet("stock")]
    public Task<IActionResult> Stock([FromQuery] InventoryPage page, CancellationToken ct) => Execute(actor => service.GetStock(page, false, ct));
    [HttpGet("low-stock")]
    public Task<IActionResult> LowStock([FromQuery] InventoryPage page, CancellationToken ct) => Execute(actor => service.GetStock(page, true, ct));
    [HttpGet("warehouses/{warehouseId:long}/items/{itemId:long}/stock")]
    public Task<IActionResult> StockPair(long warehouseId, long itemId, CancellationToken ct) => Execute(actor => service.GetStock(warehouseId, itemId, ct));
    [HttpGet("movements")]
    public Task<IActionResult> Movements([FromQuery] InventoryPage page, CancellationToken ct) => Execute(actor => service.GetMovements(page, ct));
    [HttpGet("warehouses/{warehouseId:long}/items/{itemId:long}/ledger")]
    public Task<IActionResult> Ledger(long warehouseId, long itemId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct) => Execute(actor => service.GetLedger(warehouseId, itemId, from, to, ct));
    [HttpGet("documents")]
    public Task<IActionResult> Documents([FromQuery] InventoryPage page, CancellationToken ct) => Execute(actor => service.GetDocuments(page, ct));
    [HttpGet("documents/{id:long}")]
    public Task<IActionResult> Document(long id, CancellationToken ct) => Execute(actor => service.GetDocument(id, ct));
    [HttpGet("documents/{id:long}/history")]
    public Task<IActionResult> History(long id, CancellationToken ct) => Execute(actor => service.GetHistory(id, ct));
    [HttpPost("documents/{id:long}/cancel")]
    public Task<IActionResult> Cancel(long id, InventoryCommand request, CancellationToken ct) => Execute(actor => service.CancelDocument(id, actor, request, ct), input: request);
    [HttpPost("documents/{id:long}/reverse")]
    public Task<IActionResult> Reverse(long id, InventoryCommand request, CancellationToken ct) => Execute(actor => service.Reverse(id, actor, request, ct), input: request);
    [HttpPost("receipts")]
    public Task<IActionResult> CreateReceipt(InventoryDraft request, CancellationToken ct) => Execute(actor => service.SaveDraft("RECEIPT", null, actor, request, ct), x => $"/api/inventory/documents/{x!.DocumentId}", input: request);
    [HttpPut("receipts/{id:long}")]
    public Task<IActionResult> EditReceipt(long id, InventoryDraft request, CancellationToken ct) => Execute(actor => service.SaveDraft("RECEIPT", id, actor, request, ct), input: request);
    [HttpPost("receipts/{id:long}/post")]
    public Task<IActionResult> PostReceipt(long id, InventoryPost request, CancellationToken ct) => Execute(actor => service.Post("RECEIPT", id, actor, request, ct), input: request);
    [HttpPost("returns")]
    public Task<IActionResult> CreateReturn(InventoryDraft request, CancellationToken ct) => Execute(actor => service.SaveDraft("RETURN", null, actor, request, ct), x => $"/api/inventory/documents/{x!.DocumentId}", input: request);
    [HttpPut("returns/{id:long}")]
    public Task<IActionResult> EditReturn(long id, InventoryDraft request, CancellationToken ct) => Execute(actor => service.SaveDraft("RETURN", id, actor, request, ct), input: request);
    [HttpPost("returns/{id:long}/post")]
    public Task<IActionResult> PostReturn(long id, InventoryPost request, CancellationToken ct) => Execute(actor => service.Post("RETURN", id, actor, request, ct), input: request);
    [HttpPost("transfers")]
    public Task<IActionResult> CreateTransfer(InventoryDraft request, CancellationToken ct) => Execute(actor => service.SaveDraft("TRANSFER", null, actor, request, ct), x => $"/api/inventory/documents/{x!.DocumentId}", input: request);
    [HttpPut("transfers/{id:long}")]
    public Task<IActionResult> EditTransfer(long id, InventoryDraft request, CancellationToken ct) => Execute(actor => service.SaveDraft("TRANSFER", id, actor, request, ct), input: request);
    [HttpPost("transfers/{id:long}/dispatch")]
    public Task<IActionResult> Dispatch(long id, InventoryCommand request, CancellationToken ct) => Execute(actor => service.Dispatch(id, actor, request, ct), input: request);
    [HttpPost("transfers/{id:long}/receipts")]
    public Task<IActionResult> Receive(long id, InventoryReceive request, CancellationToken ct) => Execute(actor => service.Receive(id, actor, request, ct), input: request);
    [HttpPost("adjustments")]
    public Task<IActionResult> Adjust(InventoryAdjustment request, CancellationToken ct) => Execute(actor => service.CreateAdjustment(actor, request, ct), x => $"/api/inventory/documents/{x.DocumentId}", input: request);
    [HttpPost("adjustments/{id:long}/post")]
    public Task<IActionResult> PostAdjustment(long id, InventoryCommand request, CancellationToken ct) => Execute(actor => service.PostAdjustment(id, actor, request, ct), input: request);
    [HttpPost("reversals/{id:long}/post")]
    public Task<IActionResult> PostReversal(long id, InventoryCommand request, CancellationToken ct) => Execute(actor => service.PostReversal(id, actor, request, ct), input: request);
    [HttpGet("stocktakes")]
    public Task<IActionResult> Stocktakes([FromQuery] InventoryPage page, CancellationToken ct) => Execute(actor => service.GetStocktakes(page, ct));
    [HttpGet("stocktakes/{id:long}")]
    public Task<IActionResult> Stocktake(long id, CancellationToken ct) => Execute(actor => service.GetStocktake(id, ct));
    [HttpPost("stocktakes")]
    public Task<IActionResult> CreateStocktake(InventoryStocktakeCreate request, CancellationToken ct) => Execute(actor => service.CreateStocktake(actor, request, ct), x => $"/api/inventory/stocktakes/{x.StocktakeId}", input: request);
    [HttpPut("stocktakes/{id:long}/counts")]
    public Task<IActionResult> Counts(long id, InventoryCounts request, CancellationToken ct) => Execute(actor => service.SaveCounts(id, actor, request, ct), input: request);
    [HttpPost("stocktakes/{id:long}/submit")]
    public Task<IActionResult> Submit(long id, InventoryCommand request, CancellationToken ct) => Execute(actor => service.StocktakeAction(id, "submit", actor, request, ct), input: request);
    [HttpPost("stocktakes/{id:long}/reconcile")]
    public Task<IActionResult> Reconcile(long id, InventoryCommand request, CancellationToken ct) => Execute(actor => service.StocktakeAction(id, "reconcile", actor, request, ct), input: request);
    [HttpPost("stocktakes/{id:long}/approve-adjustment")]
    public Task<IActionResult> ApproveAdjustment(long id, InventoryCommand request, CancellationToken ct) => Execute(actor => service.StocktakeAction(id, "approve-adjustment", actor, request, ct), input: request);
    [HttpPost("stocktakes/{id:long}/return")]
    public Task<IActionResult> ReturnStocktake(long id, InventoryCommand request, CancellationToken ct) => Execute(actor => service.StocktakeAction(id, "return", actor, request, ct), input: request);
    [HttpPost("stocktakes/{id:long}/cancel")]
    public Task<IActionResult> CancelStocktake(long id, InventoryCommand request, CancellationToken ct) => Execute(actor => service.StocktakeAction(id, "cancel", actor, request, ct), input: request);
}
