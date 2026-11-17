using HotelOps.Api.Security;
using HotelOps.Api.Services.Assets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController,InsightErrors,Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme),AutoValidateAntiforgeryToken]
[Route("api/asset-placement")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AssetPlacementController(AssetPlacementCatalog catalog,AssetPlacementWriter writer):ControllerBase
{
    [HttpGet("{id:long}/catalog")]
    public Task<object> Read(long id,CancellationToken ct)=>catalog.ReadAsync(id,ct);
    [HttpPost("{id:long}")]
    public Task<object> Save(long id,AssetPlacementInput input,CancellationToken ct)=>
        writer.SaveAsync(id,Request.Headers.IfMatch.ToString() is{Length:>0} value?value:null,input,ct);
}
