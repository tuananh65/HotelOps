using HotelOps.Api.Security;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;
[ApiController,Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme),InternalRequestReadErrors]
[Route("api/branches/{branchId:long}/departments/{departmentId:long}/shift-roster")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class ShiftRosterController(ShiftRosterService service,ShiftAssignmentCancellation cancellation):ControllerBase
{
    [HttpGet("~/api/shift-plans/options")]
    public Task<IReadOnlyList<ShiftBranchOption>> Options(CancellationToken ct)
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid)||ticket==null)throw new InternalRequestReadException(401,"SESSION_INVALID");
        return service.OptionsAsync(ticket,sid,ct);
    }
    [HttpGet("employees")]
    public Task<ShiftEmployeePage> Employees(long branchId,long departmentId,[FromQuery]long afterId,CancellationToken ct)
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid)||ticket==null)throw new InternalRequestReadException(401,"SESSION_INVALID");
        return service.EmployeesAsync(ticket,sid,branchId,departmentId,afterId,ct);
    }
    [HttpPost("assignments/{assignmentId:long}/cancel"),RequestSizeLimit(16384)]
    public async Task<IActionResult> Cancel(long branchId,long departmentId,long assignmentId,CancelShiftAssignmentInput input,CancellationToken ct)
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid)||ticket==null)throw new InternalRequestReadException(401,"SESSION_INVALID");
        var tags=Request.Headers.IfMatch;if(tags.Count!=1)throw new InternalRequestReadException(428,"IF_MATCH_REQUIRED");
        await cancellation.CancelAsync(ticket,sid,branchId,departmentId,assignmentId,tags[0],input,ct);return NoContent();
    }
    [HttpGet] public Task<RosterPage> Read(long branchId,long departmentId,[FromQuery]DateOnly from,[FromQuery]long afterId,CancellationToken ct)
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid)||ticket==null)throw new InternalRequestReadException(401,"SESSION_INVALID");
        return service.ReadAsync(ticket,sid,branchId,departmentId,from,afterId,ct);
    }
}
