using HotelOps.Api.Security;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController,Authorize,InternalRequestReadErrors]
[Route("api/internal-requests/intake")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[RequestSizeLimit(72 * 1024 * 1024)]
public sealed class InternalRequestIntakeController(InternalRequestIntakeService service,IWebHostEnvironment environment,IConfiguration configuration):ControllerBase
{
    [HttpGet]
    public async Task<InternalRequestIntakeOptions> Options(CancellationToken ct,[FromQuery]long? branchId=null)
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid))throw new InternalRequestReadException(401,"SESSION_INVALID");
        return await service.OptionsAsync(ticket!,sid,branchId,ct);
    }

    [HttpPost]
    public async Task<ActionResult<InternalRequestIntakeResult>> Create([FromBody]InternalRequestIntakeInput input,CancellationToken ct)
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid))throw new InternalRequestReadException(401,"SESSION_INVALID");
        var root=configuration["Evidence:PrivateRoot"]??Path.Combine(environment.ContentRootPath,"App_Data","evidence");
        var result=await service.CreateAsync(ticket!,sid,input,root,ct);
        Response.Headers.ETag='"'+result.Version+'"';
        return StatusCode(StatusCodes.Status201Created,result);
    }
}
