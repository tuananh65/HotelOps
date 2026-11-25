using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace HotelOps.Api.Compact.Controllers;
[ApiController,Authorize,Route("api/v2/advisory"),EnableRateLimiting("advisory")]
public sealed class AdvisoryController(AdvisoryService service):ControllerBase
{
    [HttpPost]public Task<AdvisoryResult> Analyze(AdvisoryRequest request,CancellationToken ct)=>service.Analyze(request,ct);
}
