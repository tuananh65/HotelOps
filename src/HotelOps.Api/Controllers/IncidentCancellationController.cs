using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController,Authorize,InternalRequestReadErrors]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class IncidentCancellationController(IncidentCancellationService service):ControllerBase
{
    [HttpPost("api/incidents/{id:long}/cancel-triage")]
    public async Task<ActionResult<IncidentCancellationSaved>> Cancel(long id,IncidentCancellationInput input,CancellationToken ct)
    {
        if(!Request.IsHttps) throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid) || ticket==null) throw new InternalRequestReadException(401,"SESSION_INVALID");
        var tags=Request.Headers.IfMatch;
        if(tags.Count!=1 || tags[0] is not {Length:18} tag || tag[0]!='"' || tag[^1]!='"' || tag[1..^1].Any(c=>!char.IsAsciiHexDigitUpper(c)))
            throw new InternalRequestReadException(428,"INCIDENT_VERSION_REQUIRED");
        try{return await service.CancelAsync(ticket,sid,id,tag,input,ct);}
        catch(ValidationException error){return Problem(statusCode:400,title:"Invalid cancellation",detail:error.Message);}
    }
}
