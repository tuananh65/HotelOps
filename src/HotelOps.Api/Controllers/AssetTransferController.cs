using HotelOps.Api.Security;
using HotelOps.Api.Services.Assets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, InsightErrors, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme), AutoValidateAntiforgeryToken]
[Route("api/asset-transfer/{id:long}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AssetTransferController(AssetPlacementCatalog catalog, AssetPlacementWriter writer, AssetCrossBranchTransfer crossBranch) : ControllerBase
{
    [HttpGet("catalog")]
    public Task<object> Catalog(long id, CancellationToken ct) => catalog.ReadAsync(id, ct, transfer: true);
    [HttpPost]
    public Task<object> Save(long id, AssetPlacementInput input, CancellationToken ct) => writer.SaveAsync(id,
        Request.Headers.IfMatch.ToString() is { Length: > 0 } value ? value : null, input, ct, transfer: true);

    [HttpGet("cross-branch")]
    public Task<object> CrossBranch(long id, CancellationToken ct) => crossBranch.ReadAsync(id, ct);

    [HttpPost("cross-branch")]
    public Task<object> Submit(long id, AssetCrossBranchInput input, CancellationToken ct) =>
        crossBranch.SubmitAsync(id, Request.Headers.IfMatch.ToString(), input, ct);

    [HttpPost("cross-branch/{handoverId:long}/decision")]
    public Task<object> Decide(long id, long handoverId, AssetCrossBranchDecisionInput input, CancellationToken ct) =>
        crossBranch.DecideAsync(id, handoverId, Request.Headers.IfMatch.ToString(), input, ct);
}
