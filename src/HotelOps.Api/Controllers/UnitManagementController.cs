using HotelOps.Api.Security;
using HotelOps.Api.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme), InsightErrors]
[Route("api/unit-management")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UnitManagementController(UnitManagementService service) : ControllerBase
{
    [HttpGet]
    public Task<UnitManagementSnapshot> Read(int page = 1, int pageSize = 25, long? itemId = null, int conversionPage = 1, CancellationToken ct = default)
        => service.ReadAsync(page, pageSize, itemId, conversionPage, ct);
}
