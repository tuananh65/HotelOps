using HotelOps.Api.Security;
using HotelOps.Api.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme), InsightErrors]
[Route("api/warehouse-management")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WarehouseManagementController(WarehouseManagementService service) : ControllerBase
{
    [HttpGet]
    public Task<WarehouseManagementSnapshot> Read(int page = 1, int pageSize = 25, CancellationToken ct = default)
        => service.ReadAsync(page, pageSize, ct);

    [HttpGet("{id:long}/deactivation")]
    public Task<WarehouseDeactivation> Preview(long id, CancellationToken ct) => service.PreviewAsync(id, ct);
}
