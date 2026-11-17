using HotelOps.Api.Security;
using HotelOps.Api.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme), InsightErrors]
[Route("api/receipt-workspace"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReceiptWorkspaceController(ReceiptWorkspaceService service) : ControllerBase
{
    [HttpGet] public Task<ReceiptWorkspace> Read(int page = 1, CancellationToken ct = default) => service.ReadAsync(page, ct);
    [HttpGet("{id:long}")] public Task<ReceiptDetail> Detail(long id, CancellationToken ct) => service.DetailAsync(id, ct);
}
