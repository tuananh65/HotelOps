using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using HotelOps.Api.DTOs.Rooms;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/rooms")]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class RoomsController(RoomDirectoryService rooms):ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] RoomDirectoryQuery query,CancellationToken ct) =>
        Execute(ticket=>rooms.ListAsync(ticket,query,ct),ct);
    [HttpGet("{roomId}")]
    public Task<IActionResult> Detail([FromRoute,Range(1,long.MaxValue)] long roomId,
        [FromQuery,Range(1,long.MaxValue)] long branchId,CancellationToken ct) =>
        Execute(ticket=>rooms.DetailAsync(ticket,branchId,roomId,ct),ct);
    [HttpGet("{roomId}/history")]
    public Task<IActionResult> History([FromRoute,Range(1,long.MaxValue)] long roomId,
        [FromQuery,Range(1,long.MaxValue)] long branchId,[FromQuery,Range(1,int.MaxValue)] int page=1,
        [FromQuery,Range(1,100)] int pageSize=20,CancellationToken ct=default) =>
        Execute(ticket=>rooms.HistoryAsync(ticket,branchId,roomId,page,pageSize,ct),ct);
    private async Task<IActionResult> Execute(Func<SessionReference,Task<RoomDirectoryResult>> work,CancellationToken ct)
    {
        if(!Request.IsHttps)return Error(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out _))return Error(401,"SESSION_INVALID");
        try
        {
            var result=await work(ticket!);
            if(result.Status==RoomDirectoryStatus.Success)
            {
                if(result.Detail is {} detail)return Ok(detail with{HistoryUrl=$"/api/rooms/{detail.Room.Id}/history?branchId={detail.Room.BranchId}"});
                return result.History is {} history?Ok(history):Ok(result.Directory);
            }
            return result.Status switch
            {
                RoomDirectoryStatus.SessionInvalid=>Error(401,"SESSION_INVALID"),
                RoomDirectoryStatus.Forbidden=>Error(403,"PERMISSION_DENIED"),
                RoomDirectoryStatus.NotFound=>Error(404,"ROOM_NOT_FOUND"),
                RoomDirectoryStatus.AuthorizationUnavailable=>Error(503,"ROOM_DIRECTORY_UNAVAILABLE"),
                _=>throw new InvalidOperationException("Unknown room directory result.")
            };
        }
        catch(ValidationException error){return Problem(statusCode:400,title:"VALIDATION_FAILED",detail:error.Message);}
        catch(Exception error) when(!ct.IsCancellationRequested && error is DbException or SessionStoreUnavailableException)
        {return Error(503,"ROOM_DIRECTORY_UNAVAILABLE");}
    }
    private ObjectResult Error(int status,string code)=>Problem(statusCode:status,title:code,extensions:new Dictionary<string,object?>{["code"]=code});
}
