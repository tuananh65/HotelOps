using HotelOps.Api.Security;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme), InsightErrors]
[Route("api/stock-balance")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StockBalanceController(StockBalanceReadService service) : ControllerBase
{
    [HttpGet("documents/{id:long}")]
    public Task<HotelOps.Api.DTOs.Inventory.InventoryDocumentDto> Document(long id, CancellationToken ct) => service.DocumentAsync(id, ct);

    [HttpGet]
    public Task<StockBalanceResult> Read([FromQuery] StockBalanceQuery query, CancellationToken ct) => service.ReadAsync(query, ct);
}
