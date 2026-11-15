using HotelOps.Api.Security;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController,Authorize,InternalRequestReadErrors]
[Route("api/internal-requests/{id:long}/progress")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[RequestSizeLimit(72 * 1024 * 1024)]
public sealed class InternalRequestProgressController(InternalRequestProgressService service,IWebHostEnvironment environment,IConfiguration configuration):ControllerBase
{
    private string Root => configuration["Evidence:PrivateRoot"] ?? Path.Combine(environment.ContentRootPath,"App_Data","evidence");
    [HttpGet("evidence/{attachment:long}")]
    public async Task<IActionResult> Evidence(long id,long attachment,CancellationToken ct)
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid))throw new InternalRequestReadException(401,"SESSION_INVALID");
        var file=await service.ReadEvidenceAsync(ticket!,sid,id,attachment,Root,ct);
        Response.Headers["X-Content-Type-Options"]="nosniff";
        return File(file.Bytes,file.MimeType,file.Name);
    }
    [HttpGet]
    public async Task<InternalRequestProgressView> Read(long id,CancellationToken ct,[FromQuery]long? evidenceBefore=null)
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid))throw new InternalRequestReadException(401,"SESSION_INVALID");
        var result=await service.ReadAsync(ticket!,sid,id,ct,evidenceBefore);
        Response.Headers.ETag='"'+result.Version+'"';
        return result;
    }

    [HttpPost]
    public async Task<InternalRequestProgressResult> Update(long id,[FromBody]InternalRequestProgressInput input,CancellationToken ct)
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid))throw new InternalRequestReadException(401,"SESSION_INVALID");
        var values=Request.Headers.IfMatch;
        if(values.Count!=1||values[0] is not {Length:14} tag||tag[0]!='"'||tag[^1]!='"')
            throw new InternalRequestReadException(428,"IR_VERSION_REQUIRED");
        byte[] version;
        try{version=Convert.FromBase64String(tag[1..^1]);}
        catch(FormatException){throw new InternalRequestReadException(400,"IR_VERSION_INVALID");}
        if(version.Length!=8||Convert.ToBase64String(version)!=tag[1..^1])throw new InternalRequestReadException(400,"IR_VERSION_INVALID");
        var result=await service.UpdateAsync(ticket!,sid,id,version,input,ct,Root);
        Response.Headers.ETag='"'+result.Version+'"';
        return result;
    }
}
