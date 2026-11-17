using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController,Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme),InternalRequestReadErrors]
[Route("api/shift-handovers"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class ShiftHandoversController(ShiftHandoverService service):ControllerBase
{
    private (SessionReference Ticket,long Sid) Session()
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid))throw new InternalRequestReadException(401,"SESSION_INVALID");
        return(ticket!,sid);
    }
    private string Tag()
    {
        var tag=Request.Headers.IfMatch.ToString();
        if(Request.Headers.IfMatch.Count!=1||tag.Length<3||tag[0]!='"'||tag[^1]!='"'||tag.StartsWith("W/"))
            throw new InternalRequestReadException(428,"HANDOVER_VERSION_REQUIRED");
        return tag;
    }
    [HttpGet]
    public Task<ShiftHandoverSummary[]> List([FromQuery]long branchId,[FromQuery]int page=1,CancellationToken ct=default)
    {var auth=Session();return service.ListAsync(auth.Ticket,auth.Sid,branchId,page,ct);}
    [HttpGet("preview/{workdayId:long}")]
    public async Task<ShiftHandoverPreview> Preview(long workdayId,CancellationToken ct)
    {var auth=Session();var result=await service.PreviewAsync(auth.Ticket,auth.Sid,workdayId,ct);Response.Headers.ETag=result.Version;return result;}
    [HttpGet("{id:long}")]
    public async Task<ShiftHandoverView> Read(long id,CancellationToken ct)
    {var auth=Session();var result=await service.ReadAsync(auth.Ticket,auth.Sid,id,ct);Response.Headers.ETag=result.Version;return result;}
    [HttpPost("workdays/{workdayId:long}"),ValidateAntiForgeryToken,RequestSizeLimit(2*1024*1024)]
    public async Task<object> Submit(long workdayId,[FromBody]ShiftHandoverSubmit input,CancellationToken ct)
    {var auth=Session();var id=await service.SubmitAsync(auth.Ticket,auth.Sid,workdayId,Tag(),input,ct);return new{id,message="MSG08"};}
    [HttpPost("{id:long}/decision"),ValidateAntiForgeryToken,RequestSizeLimit(32*1024)]
    public async Task<object> Decide(long id,[FromBody]ShiftHandoverDecision input,CancellationToken ct)
    {
        var auth=Session();var tag=Tag();byte[] version;
        try{version=Convert.FromBase64String(tag[1..^1]);}catch(FormatException){throw new InternalRequestReadException(400,"HANDOVER_VERSION_INVALID");}
        if(version.Length!=8||Convert.ToBase64String(version)!=tag[1..^1])throw new InternalRequestReadException(400,"HANDOVER_VERSION_INVALID");
        await service.DecideAsync(auth.Ticket,auth.Sid,id,version,input,ct);return new{id,message="MSG08"};
    }
}
