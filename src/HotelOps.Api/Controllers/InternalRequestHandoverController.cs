using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController,Authorize,InternalRequestReadErrors]
[Route("api/internal-requests/{id:long}/handover")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class InternalRequestHandoverController(InternalRequestHandoverService service):ControllerBase
{
    private (SessionReference Ticket,long Sid) Session()
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid))throw new InternalRequestReadException(401,"SESSION_INVALID");
        return(ticket!,sid);
    }
    private byte[] Version()
    {
        var tags=Request.Headers.IfMatch;
        if(tags.Count!=1||tags[0] is not {Length:14} tag||tag[0]!='"'||tag[^1]!='"')throw new InternalRequestReadException(428,"IR_VERSION_REQUIRED");
        byte[] value;try{value=Convert.FromBase64String(tag[1..^1]);}catch(FormatException){throw new InternalRequestReadException(400,"IR_VERSION_INVALID");}
        if(value.Length!=8||Convert.ToBase64String(value)!=tag[1..^1])throw new InternalRequestReadException(400,"IR_VERSION_INVALID");return value;
    }
    [HttpGet]
    public async Task<InternalRequestHandoverView> Read(long id,CancellationToken ct)
    {
        var auth=Session();var result=await service.ReadAsync(auth.Ticket,auth.Sid,id,ct);Response.Headers.ETag='"'+result.Version+'"';return result;
    }
    [HttpPost]
    public async Task<InternalRequestHandoverResult> Submit(long id,[FromBody]InternalRequestHandoverInput input,CancellationToken ct)
    {
        var auth=Session();var result=await service.SubmitAsync(auth.Ticket,auth.Sid,id,Version(),input,ct);Response.Headers.ETag='"'+result.Version+'"';return result;
    }
    [HttpPost("decision")]
    public async Task<InternalRequestHandoverResult> Decide(long id,[FromBody]InternalRequestHandoverDecision input,CancellationToken ct)
    {
        var auth=Session();var result=await service.DecideAsync(auth.Ticket,auth.Sid,id,Version(),input,ct);Response.Headers.ETag='"'+result.Version+'"';return result;
    }
}
