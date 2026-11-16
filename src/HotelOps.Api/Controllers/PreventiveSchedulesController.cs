using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Security;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.PreventiveMaintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController,Authorize,InternalRequestReadErrors]
[Route("api/branches/{branch:long}/preventive-schedules")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[RequestSizeLimit(262144)]
public sealed class PreventiveSchedulesController(PreventiveScheduleService service) : ControllerBase
{
    public sealed record Deactivation(string Reason);
    private (HotelOps.Api.Services.Authentication.SessionReference Ticket,long Sid) Identity()
    {
        if(!Request.IsHttps)throw new InternalRequestReadException(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid)||ticket==null)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        return(ticket,sid);
    }
    private string Version()
    {
        var tags=Request.Headers.IfMatch;
        if(tags.Count!=1 || tags[0] is not {Length:18} tag || tag[0]!='"' || tag[^1]!='"'
            || tag[1..^1].Any(c=>!char.IsAsciiHexDigitUpper(c)))throw new InternalRequestReadException(428,"SCHEDULE_VERSION_REQUIRED");
        return tag;
    }
    [HttpGet("targets")]
    public async Task<ActionResult<IReadOnlyList<PreventiveTargetOption>>> Targets(long branch,CancellationToken ct,
        [FromQuery] string kind="ROOM",[FromQuery] long afterId=0)
    {
        var (ticket,sid)=Identity();
        try{return Ok(await service.TargetsAsync(ticket,sid,branch,kind,afterId,ct));}
        catch(ValidationException e){return Problem(statusCode:400,title:"Invalid target page",detail:e.Message);}
    }
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PreventiveScheduleView>>> List(long branch,CancellationToken ct,
        [FromQuery] long afterId=0,[FromQuery] int limit=50)
    {
        var (ticket,sid)=Identity();
        try{return Ok(await service.ListAsync(ticket,sid,branch,afterId,limit,ct));}
        catch(ValidationException e){return Problem(statusCode:400,title:"Invalid schedule page",detail:e.Message);}
    }
    [HttpGet("{id:long}")]
    public async Task<PreventiveScheduleView> Get(long branch,long id,CancellationToken ct)
    {
        var (ticket,sid)=Identity();
        return await service.ReadAsync(ticket,sid,branch,id,ct);
    }
    [HttpPost]
    public async Task<ActionResult<PreventiveScheduleView>> Create(long branch,PreventiveScheduleInput input,CancellationToken ct)
    {
        var (ticket,sid)=Identity();
        try { var saved=await service.SaveAsync(ticket,sid,branch,null,null,input,ct);
            return CreatedAtAction(nameof(Get),new{branch,id=saved.ScheduleId},saved); }
        catch(ValidationException e){return Problem(statusCode:400,title:"Invalid preventive schedule",detail:e.Message);}
    }
    [HttpPut("{id:long}")]
    public async Task<ActionResult<PreventiveScheduleView>> Edit(long branch,long id,PreventiveScheduleInput input,CancellationToken ct)
    {
        var (ticket,sid)=Identity();var tag=Version();
        try{return await service.SaveAsync(ticket,sid,branch,id,tag,input,ct);}
        catch(ValidationException e){return Problem(statusCode:400,title:"Invalid preventive schedule",detail:e.Message);}
    }
    [HttpPost("{id:long}/deactivate")]
    public async Task<ActionResult<PreventiveScheduleView>> Deactivate(long branch,long id,Deactivation input,CancellationToken ct)
    {
        var (ticket,sid)=Identity();var tag=Version();
        try{return await service.DeactivateAsync(ticket,sid,branch,id,tag,input.Reason,ct);}
        catch(ValidationException e){return Problem(statusCode:400,title:"Invalid deactivation reason",detail:e.Message);}
    }
}
