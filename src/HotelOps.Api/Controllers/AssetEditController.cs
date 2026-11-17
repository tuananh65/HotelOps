using HotelOps.Api.Security;
using HotelOps.Api.Services.Assets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, InsightErrors, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[Route("api/asset-editor")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AssetEditController(AssetEditService service) : ControllerBase
{
    [HttpGet("{id:long}")]
    public Task<object> Read(long id, CancellationToken ct) => service.ReadAsync(id, ct);
    [HttpPut("{id:long}")]
    public Task<object> Save(long id, AssetEditInput input, CancellationToken ct)
        => service.SaveAsync(id, Request.Headers.IfMatch.ToString() is { Length: > 0 } tag ? tag : null, input, ct);
}
