using HotelOps.Api.Security;
using HotelOps.Api.Services.Assets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, InsightErrors, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme), AutoValidateAntiforgeryToken]
[Route("api/asset-condition/{id:long}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AssetConditionController(AssetConditionWorkflow workflow) : ControllerBase
{
    [HttpGet]
    public Task<object> Read(long id, CancellationToken ct) => workflow.ReadAsync(id, ct);
    [HttpPost]
    public Task<object> Save(long id, AssetConditionInput input, CancellationToken ct) => workflow.SaveAsync(id,
        Request.Headers.IfMatch.ToString() is { Length: > 0 } value ? value : null, input, ct);
}
