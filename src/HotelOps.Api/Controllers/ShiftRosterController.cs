using HotelOps.Api.Security;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;
[ApiController,Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme),InternalRequestReadErrors]
[Route("api/branches/{branchId:long}/departments/{departmentId:long}/shift-roster")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class ShiftRosterController(ShiftRosterService service):ControllerBase
{
    [HttpGet] public Task<RosterPage> Read(long branchId,long departmentId,[FromQuery]DateOnly from,[FromQuery]long afterId,CancellationToken ct)
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid)||ticket==null)throw new InternalRequestReadException(401,"SESSION_INVALID");
        return service.ReadAsync(ticket,sid,branchId,departmentId,from,afterId,ct);
    }
}
