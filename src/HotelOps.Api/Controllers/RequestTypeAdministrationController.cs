using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;
[ApiController,Authorize,InternalRequestReadErrors]
[Route("api/reference-data/request-types")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[RequestSizeLimit(16384)]
public sealed class RequestTypeAdministrationController(RequestTypeAdministrationService service):ControllerBase
{
    private (SessionReference Ticket,long Sid) Identity()
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid)||ticket==null)throw new InternalRequestReadException(401,"SESSION_INVALID");
        return(ticket,sid);
    }
    [HttpGet] public Task<IReadOnlyList<RequestTypeView>> List([FromQuery]long afterId,CancellationToken ct)
    {var(t,s)=Identity();return service.ListAsync(t,s,afterId,ct);}
    [HttpPost] public Task<RequestTypeView> Create(RequestTypeInput input,CancellationToken ct)
    {var(t,s)=Identity();return service.SaveAsync(t,s,null,null,input,ct);}
    [HttpPut("{id:long}")] public Task<RequestTypeView> Save(long id,RequestTypeInput input,CancellationToken ct)
    {var(t,s)=Identity();var tags=Request.Headers.IfMatch;if(tags.Count!=1)throw new InternalRequestReadException(428,"IF_MATCH_REQUIRED");return service.SaveAsync(t,s,id,tags[0],input,ct);}
}

