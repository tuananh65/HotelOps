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
public sealed class PreventiveOccurrencesController(PreventiveOccurrenceReadService service):ControllerBase
{
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
