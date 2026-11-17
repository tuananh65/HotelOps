using HotelOps.Api.Security;
using HotelOps.Api.Services.Assets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, InsightErrors]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[Route("api/asset-location-review")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AssetLocationReviewController(AssetLocationReview reader) : ControllerBase
{
    [HttpGet("options")]
    public Task<object> Options(long? branch, CancellationToken ct) => reader.OptionsAsync(branch, ct);
    [HttpGet]
    public Task<object> Read([FromQuery] AssetLocationFilter filter, CancellationToken ct) => reader.ReadAsync(filter, ct);
}
