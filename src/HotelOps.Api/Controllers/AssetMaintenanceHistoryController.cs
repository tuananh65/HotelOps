using HotelOps.Api.Security;
using HotelOps.Api.Services.Assets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, InsightErrors]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[Route("api/asset-maintenance-history")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AssetMaintenanceHistoryController(AssetMaintenanceHistoryReader reader) : ControllerBase
{
    [HttpGet("options")]
    public Task<object> Options(CancellationToken ct) => reader.OptionsAsync(ct);
    [HttpGet("assets")]
    public Task<object> Assets(long branch, string? search, CancellationToken ct, long after = 0) => reader.AssetsAsync(branch, search, after, ct);
    [HttpGet("{id:long}")]
    public Task<object> Read(long id, DateOnly? from, DateOnly? to, string? type, CancellationToken ct, int page = 1)
        => reader.ReadAsync(id, from, to, type, page, ct);
}
