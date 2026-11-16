using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;
[ApiController,Authorize,InternalRequestReadErrors]
[Route("api/reference-data/catalog/{group}")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[RequestSizeLimit(16384)]
public sealed class ReferenceAdministrationController(ReferenceAdministrationService service):ControllerBase
{
    private (SessionReference Ticket,long Sid) Identity()
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid)||ticket==null)throw new InternalRequestReadException(401,"SESSION_INVALID");
        return(ticket,sid);
    }
    [HttpGet] public Task<IReadOnlyList<ReferenceView>> List(string group,[FromQuery]long afterId,CancellationToken ct)
    {var(t,s)=Identity();return service.ListAsync(t,s,group,afterId,ct);}
    [HttpPost] public Task<ReferenceView> Create(string group,ReferenceInput input,CancellationToken ct)
    {var(t,s)=Identity();return service.SaveAsync(t,s,group,null,null,input,ct);}
    [HttpPut("{id:long}")] public Task<ReferenceView> Save(string group,long id,ReferenceInput input,CancellationToken ct)
    {var(t,s)=Identity();var tags=Request.Headers.IfMatch;if(tags.Count!=1)throw new InternalRequestReadException(428,"IF_MATCH_REQUIRED");return service.SaveAsync(t,s,group,id,tags[0],input,ct);}
}
