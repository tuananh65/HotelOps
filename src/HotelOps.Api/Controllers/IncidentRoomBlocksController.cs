using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;
[ApiController,Authorize,InternalRequestReadErrors]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class IncidentRoomBlocksController(IncidentRoomBlockService service):ControllerBase
{
    private (SessionReference Ticket,long Sid) Session()
    {
        if(!Request.IsHttps) throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid) || ticket==null) throw new InternalRequestReadException(401,"SESSION_INVALID");
        return(ticket,sid);
    }
    [HttpGet("api/incidents/{id:long}/room-blocks")]
    public Task<IncidentRoomBlockContext> Read(long id,int page=1,CancellationToken ct=default)
    {var (ticket,sid)=Session();return service.ReadAsync(ticket,sid,id,page,ct);}
    [HttpPost("api/incidents/{id:long}/room-blocks")]
    public async Task<ActionResult<IncidentRoomBlockSaved>> Change(long id,IncidentRoomBlockInput input,CancellationToken ct)
    {
        var (ticket,sid)=Session();var tags=Request.Headers.IfMatch;
        if(tags.Count!=1 || tags[0] is not {Length:18} tag || tag[0]!='"' || tag[^1]!='"' || tag[1..^1].Any(c=>!char.IsAsciiHexDigitUpper(c))) throw new InternalRequestReadException(428,"INCIDENT_VERSION_REQUIRED");
        try{return await service.ChangeAsync(ticket,sid,id,tag,input,ct);}
        catch(ValidationException error){return Problem(statusCode:400,title:"Invalid room block change",detail:error.Message);}
    }
}
