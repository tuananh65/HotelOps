using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.PreventiveMaintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController,Authorize,InternalRequestReadErrors]
[Route("api/branches/{branch:long}/preventive-occurrences")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PreventiveOccurrencesController(PreventiveOccurrenceReadService service,PreventiveOccurrenceManagementService management):ControllerBase
{
    public sealed record Assignment(long AssigneeId);
    public sealed record Cancellation(string Reason);
    private string Version()
    {
        var tags=Request.Headers.IfMatch;
        if(tags.Count!=1||tags[0] is not {Length:18} tag||tag[0]!='"'||tag[^1]!='"'||tag[1..^1].Any(c=>!char.IsAsciiHexDigitUpper(c)))
            throw new InternalRequestReadException(428,"OCCURRENCE_VERSION_REQUIRED");
        return tag;
    }
    [HttpPost("{id:long}/assign"),RequestSizeLimit(16384)]
    public async Task<PreventiveOccurrenceView> Assign(long branch,long id,Assignment input,CancellationToken ct)
    {
        var(ticket,sid)=Identity();return await management.ChangeAsync(ticket,sid,branch,id,Version(),input.AssigneeId,null,false,ct);
    }
    [HttpPost("{id:long}/cancel"),RequestSizeLimit(16384)]
    public async Task<PreventiveOccurrenceView> Cancel(long branch,long id,Cancellation input,CancellationToken ct)
    {
        var(ticket,sid)=Identity();return await management.ChangeAsync(ticket,sid,branch,id,Version(),null,input.Reason,true,ct);
    }
    private (SessionReference Ticket,long Sid) Identity()
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid)||ticket==null)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        return(ticket,sid);
    }
    [HttpGet]
    public async Task<IReadOnlyList<PreventiveOccurrenceView>> List(long branch,CancellationToken ct,[FromQuery]long afterId=0)
    {
        var(ticket,sid)=Identity();return await service.ReadAsync(ticket,sid,branch,null,afterId,ct);
    }
    [HttpGet("{id:long}")]
    public async Task<PreventiveOccurrenceView> Get(long branch,long id,CancellationToken ct)
    {
        var(ticket,sid)=Identity();return (await service.ReadAsync(ticket,sid,branch,id,0,ct)).Single();
    }
}
