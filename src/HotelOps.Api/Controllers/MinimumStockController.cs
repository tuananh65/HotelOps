using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme), InsightErrors]
[Route("api/inventory-minimums")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MinimumStockController(MinimumStockService service) : ControllerBase
{
    [HttpGet]
    public Task<MinimumStockPage> Read(long warehouseId, int page = 1, int pageSize = 25, CancellationToken ct = default)
        => service.ReadAsync(warehouseId, page, pageSize, ct);
    [HttpPut("{warehouseId:long}"), ValidateAntiForgeryToken, RequestSizeLimit(65536)]
    public Task<InventoryThresholdDto[]> Save(long warehouseId, [FromBody] MinimumStockChange[] changes, CancellationToken ct)
        => service.SaveAsync(warehouseId, changes, ct);
}
