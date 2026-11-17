using HotelOps.Api.Security;
using HotelOps.Api.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[InsightErrors]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[Route("api/stocktake-workspace")]
public sealed class StocktakeWorkspaceController(StocktakeWorkspaceService service) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> List(int page = 1, CancellationToken ct = default)
    {
        try { return Ok(await service.ListAsync(page, ct)); }
        catch (InventoryAccessException e) { return e.Status == 401 ? Unauthorized() : Forbid(HotelOpsCookieTicket.Scheme); }
    }

    [HttpGet("adjustments")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Adjustments(int page = 1, CancellationToken ct = default)
    {
        try { return Ok(await service.AdjustmentsAsync(page, ct)); }
        catch (InventoryAccessException e) { return e.Status == 401 ? Unauthorized() : Forbid(HotelOpsCookieTicket.Scheme); }
    }

    [HttpGet("{id:long}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
    {
        try { return Ok(await service.DetailAsync(id, ct)); }
        catch (InventoryAccessException e) { return e.Status == 401 ? Unauthorized() : Forbid(HotelOpsCookieTicket.Scheme); }
    }
}
