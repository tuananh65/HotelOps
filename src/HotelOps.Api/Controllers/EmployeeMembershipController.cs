using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;
[ApiController,Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme),InternalRequestReadErrors]
[Route("api/users/{userId:long}/branches/{branchId:long}/departments")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None),RequestSizeLimit(16384)]
public sealed class EmployeeMembershipController(EmployeeMembershipService service):ControllerBase
{
    private (SessionReference Ticket,long Sid) Identity()
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid)||ticket==null)throw new InternalRequestReadException(401,"SESSION_INVALID");return(ticket,sid);
    }
    [HttpGet] public Task<EmployeeMembershipPage> Read(long userId,long branchId,CancellationToken ct)
    {var(t,s)=Identity();return service.ReadAsync(t,s,userId,branchId,ct);}
    [HttpGet("/api/users/{userId:long}/department-membership-branches")]
    public Task<IReadOnlyList<EmployeeMembershipBranch>> Branches(long userId,CancellationToken ct)
    {var(t,s)=Identity();return service.BranchesAsync(t,s,userId,ct);}
    [HttpPost] public async Task<IActionResult> Add(long userId,long branchId,EmployeeMembershipInput input,CancellationToken ct)
    {
        var(t,s)=Identity();await service.AddAsync(t,s,userId,branchId,Version(),input,ct);return NoContent();
    }
    [HttpPost("remove")] public async Task<IActionResult> Remove(long userId,long branchId,EmployeeMembershipInput input,CancellationToken ct)
    {
        var(t,s)=Identity();var dependencies=await service.RemoveAsync(t,s,userId,branchId,Version(),input,ct);
        if(dependencies.Count==0)return NoContent();
        var problem=new ProblemDetails{Status=409,Title="MEMBERSHIP_DEPENDENCIES",Detail="Resolve the current responsibilities before removing membership."};
        problem.Extensions["dependencies"]=dependencies;return Conflict(problem);
    }
    [HttpPost("restore")] public async Task<IActionResult> Restore(long userId,long branchId,EmployeeMembershipInput input,CancellationToken ct)
    {
        var(t,s)=Identity();await service.AddAsync(t,s,userId,branchId,Version(),input,ct,restore:true);return NoContent();
    }
    private byte[] Version()
    {
        var tags=Request.Headers.IfMatch;
        if(tags.Count!=1||tags[0] is not {Length:14} tag||tag[0]!='"'||tag[^1]!='"')throw new InternalRequestReadException(428,"IF_MATCH_REQUIRED");
        byte[] version;try{version=Convert.FromBase64String(tag[1..^1]);}catch(FormatException){throw new InternalRequestReadException(400,"INVALID_IF_MATCH");}
        if(version.Length!=8||Convert.ToBase64String(version)!=tag[1..^1])throw new InternalRequestReadException(400,"INVALID_IF_MATCH");
        return version;
    }
}
