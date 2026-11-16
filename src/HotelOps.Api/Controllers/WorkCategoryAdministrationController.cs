using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;

[ApiController,Authorize,InternalRequestReadErrors]
[Route("api/work-categories")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[RequestSizeLimit(16384)]
public sealed class WorkCategoryAdministrationController(WorkCategoryAdministrationService service):ControllerBase
{
    private (SessionReference Ticket,long Sid) Identity()
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid)||ticket==null)throw new InternalRequestReadException(401,"SESSION_INVALID");
        return(ticket,sid);
    }
    [HttpGet]
    public Task<WorkCategoryPage> List(CancellationToken ct,[FromQuery]long? branchId=null,[FromQuery]long afterId=0)
    {var(t,s)=Identity();return service.ListAsync(t,s,branchId,afterId,ct);}
    [HttpPost]
    public Task<WorkCategoryView> Create(WorkCategoryInput input,CancellationToken ct,[FromQuery]long? branchId=null)
    {var(t,s)=Identity();return service.SaveAsync(t,s,branchId,null,null,input,ct);}
    [HttpPut("{id:long}")]
    public Task<WorkCategoryView> Update(long id,WorkCategoryInput input,CancellationToken ct,[FromQuery]long? branchId=null)
    {
        var(t,s)=Identity();var tags=Request.Headers.IfMatch;
        if(tags.Count!=1)throw new InternalRequestReadException(428,"IF_MATCH_REQUIRED");
        return service.SaveAsync(t,s,branchId,id,tags[0],input,ct);
    }
}
