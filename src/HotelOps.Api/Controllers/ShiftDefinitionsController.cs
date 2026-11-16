using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;
[ApiController,Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme),InternalRequestReadErrors]
[Route("api/branches/{branchId:long}/departments/{departmentId:long}/shift-definitions")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[RequestSizeLimit(16384)]
public sealed class ShiftDefinitionsController(ShiftDefinitionService service):ControllerBase
{
    private (SessionReference Ticket,long Sid) Identity()
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid)||ticket==null)throw new InternalRequestReadException(401,"SESSION_INVALID");
        return(ticket,sid);
    }
    [HttpGet] public Task<IReadOnlyList<ShiftDefinitionView>> List(long branchId,long departmentId,[FromQuery]long afterId,CancellationToken ct)
    {var(t,s)=Identity();return service.ListAsync(t,s,branchId,departmentId,afterId,ct);}
    [HttpPost] public Task<ShiftDefinitionView> Create(long branchId,long departmentId,ShiftDefinitionInput input,CancellationToken ct)
    {var(t,s)=Identity();return service.SaveAsync(t,s,branchId,departmentId,null,null,input,ct);}
    [HttpPut("{id:long}")] public Task<ShiftDefinitionView> Save(long branchId,long departmentId,long id,ShiftDefinitionInput input,CancellationToken ct)
    {var(t,s)=Identity();var tags=Request.Headers.IfMatch;if(tags.Count!=1)throw new InternalRequestReadException(428,"IF_MATCH_REQUIRED");return service.SaveAsync(t,s,branchId,departmentId,id,tags[0],input,ct);}
}
