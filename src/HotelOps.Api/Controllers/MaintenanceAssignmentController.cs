using HotelOps.Api.Security;
using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController,Authorize,InternalRequestReadErrors]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class MaintenanceAssignmentController(MaintenanceAssignmentService service):ControllerBase
{
    private (SessionReference Ticket,long Sid) Session()
    {
        if(!Request.IsHttps) throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid)) throw new InternalRequestReadException(401,"SESSION_INVALID");
        return(ticket!,sid);
    }
    [HttpGet("api/incidents/{id:long}/assignment")]
    public Task<MaintenanceAssignmentContext> Context(long id,CancellationToken ct)
    {var (ticket,sid)=Session();return service.ContextAsync(ticket,sid,id,ct);}

    [HttpPost("api/incidents/{id:long}/assignment")]
    public async Task<ActionResult<MaintenanceAssignmentSaved>> Assign(long id,MaintenanceAssignmentInput input,CancellationToken ct)
    {
        var (ticket,sid)=Session();var tags=Request.Headers.IfMatch;
        if(tags.Count!=1 || tags[0] is not {Length:18} tag || tag[0]!='"' || tag[^1]!='"' || tag[1..^1].Any(c=>!char.IsAsciiHexDigitUpper(c)))
            throw new InternalRequestReadException(428,"INCIDENT_VERSION_REQUIRED");
        try { return await service.AssignAsync(ticket,sid,id,tag,input,ct); }
        catch(ValidationException error) { return Problem(statusCode:400,title:"Invalid assignment",detail:error.Message); }
    }
    [HttpGet("api/maintenance-assignment-receipts/{id:long}")]
    public Task<MaintenanceAssignmentReceipt> Receipt(long id,CancellationToken ct)
    {var (ticket,sid)=Session();return service.ReceiptAsync(ticket,sid,id,ct);}
}
