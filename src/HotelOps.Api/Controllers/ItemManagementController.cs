using HotelOps.Api.Security;
using HotelOps.Api.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme), InsightErrors]
[Route("api/item-management")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ItemManagementController(UnitManagementService catalogue) : ControllerBase
{
    [HttpGet]
    public Task<ItemManagementSnapshot> Read(int page = 1, int pageSize = 25, CancellationToken ct = default)
        => catalogue.ReadItemsAsync(page, pageSize, ct);
}
