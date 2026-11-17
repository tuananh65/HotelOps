using HotelOps.Api.Security;
using HotelOps.Api.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[InsightErrors]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[Route("api/return-workspace")]
public sealed class ReturnWorkspaceController(ReturnWorkspaceService service) : ControllerBase
{
    [HttpGet("{id:long}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
    {
        try { return Ok(await service.DetailAsync(id, ct)); }
        catch (InventoryAccessException e) { return e.Status == 401 ? Unauthorized() : Forbid(HotelOpsCookieTicket.Scheme); }
    }
}
