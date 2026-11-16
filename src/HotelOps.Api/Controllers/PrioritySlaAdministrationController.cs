using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;
[ApiController,Authorize,InternalRequestReadErrors]
[Route("api/reference-data/priority-sla")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[RequestSizeLimit(16384)]
public sealed class PrioritySlaAdministrationController(PrioritySlaAdministrationService service):ControllerBase
{
    private (SessionReference Ticket,long Sid) Identity()
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid)||ticket==null)throw new InternalRequestReadException(401,"SESSION_INVALID");
        return(ticket,sid);
    }
    [HttpGet] public Task<IReadOnlyList<PrioritySlaView>> List(CancellationToken ct)
    {var(t,s)=Identity();return service.ListAsync(t,s,ct);}
    [HttpPut("{id:long}")] public Task<PrioritySlaView> Save(long id,PrioritySlaInput input,CancellationToken ct)
    {
        var(t,s)=Identity();var tags=Request.Headers.IfMatch;
        if(tags.Count!=1)throw new InternalRequestReadException(428,"IF_MATCH_REQUIRED");
        return service.SaveAsync(t,s,id,tags[0],input,ct);
    }
}
