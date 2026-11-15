using HotelOps.Api.Security;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController,Authorize,InternalRequestReadErrors]
[Route("api/internal-requests/{id:long}/assignment")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class InternalRequestAssignmentController(InternalRequestAssignmentService service):ControllerBase
{
    [HttpGet]
    public async Task<InternalRequestAssignmentView> Read(long id,CancellationToken ct)
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid))throw new InternalRequestReadException(401,"SESSION_INVALID");
        var result=await service.ReadAsync(ticket!,sid,id,ct);
        Response.Headers.ETag='"'+result.Version+'"';
        return result;
    }

    [HttpPost]
    public async Task<InternalRequestAssignmentResult> Assign(long id,[FromBody]InternalRequestAssignmentInput input,CancellationToken ct)
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
        var result=await service.AssignAsync(ticket!,sid,id,version,input,ct);
        Response.Headers.ETag='"'+result.Version+'"';
        return result;
    }
}
