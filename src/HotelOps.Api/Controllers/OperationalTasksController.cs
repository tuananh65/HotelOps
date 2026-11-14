using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme), UserAccountActivationErrors]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[Route("api/branches/{branchId:long}/operational-tasks")]
public sealed class OperationalTasksController(RoomTypeCreationService service):ControllerBase
{
    [HttpGet] public Task<IActionResult> List(long branchId,[FromQuery] OperationalTaskQuery query,CancellationToken ct)=>Read(branchId,null,query,ct);
    [HttpGet("{id:long}")] public Task<IActionResult> Detail(long branchId,long id,[FromQuery] OperationalTaskQuery query,CancellationToken ct)=>Read(branchId,id,query,ct);
    private async Task<IActionResult> Read(long branch,long? id,OperationalTaskQuery query,CancellationToken ct)
    {
        if(!Request.IsHttps)return Problem(statusCode:400,title:"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid))return Problem(statusCode:401,title:"SESSION_INVALID");
        try
        {
            var result=await service.ReadOperationalTasksAsync(ticket!,sid,branch,query,id,ct);
            if(result.Status==RoomTypeBatchStatus.Success)return Ok(result.Value);
            return Problem(statusCode:result.Status switch {RoomTypeBatchStatus.SessionInvalid=>401,RoomTypeBatchStatus.Forbidden=>403,
                RoomTypeBatchStatus.NotFound=>404,RoomTypeBatchStatus.Inactive=>409,_=>503},title:result.Status.ToString().ToUpperInvariant());
        }
        catch(ValidationException e){return Problem(statusCode:400,title:"INVALID_FILTERS",detail:e.Message);}
    }
}
