using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Security;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;
[NonController] // Technical source proposal only: GET edit already exists; no runtime activation.
[ApiController,Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme),ValidateAntiForgeryToken]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class RoomLifecycleCandidateController(RoomTypeCreationService service):ControllerBase
{
    [HttpPost("api/rooms/{roomId:long}/deactivate"),RequestSizeLimit(16*1024)]
    public Task<IActionResult> Deactivate([Range(1,long.MaxValue)]long roomId,[FromQuery,Range(1,long.MaxValue)]long branchId,RoomLifecycleRequest request,CancellationToken ct)=>Run(roomId,branchId,false,request,ct);
    [HttpPost("api/rooms/{roomId:long}/reactivate"),RequestSizeLimit(16*1024)]
    public Task<IActionResult> Reactivate([Range(1,long.MaxValue)]long roomId,[FromQuery,Range(1,long.MaxValue)]long branchId,RoomLifecycleRequest request,CancellationToken ct)=>Run(roomId,branchId,true,request,ct);
    private async Task<IActionResult> Run(long room,long branch,bool reactivate,RoomLifecycleRequest request,CancellationToken ct)
    {
        if(!Request.IsHttps)return Error(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid))return Error(401,"SESSION_INVALID");
        if(!Request.Headers.ContainsKey("If-Match"))return Error(428,"IF_MATCH_REQUIRED");
        var tag=Request.Headers.IfMatch.ToString();if(Request.Headers.IfMatch.Count!=1||!HotelStructureUpdateService.IsStrongTag(tag))return Error(400,"INVALID_IF_MATCH");
        try{var result=await service.ChangeRoomLifecycleCandidateAsync(ticket!,sid,branch,room,reactivate,tag,request,ct);
            return result.Status switch{RoomTypeBatchStatus.SessionInvalid=>Error(401,"SESSION_INVALID"),RoomTypeBatchStatus.Forbidden=>Error(403,"PERMISSION_DENIED"),RoomTypeBatchStatus.NotFound=>Error(404,"ROOM_NOT_FOUND"),RoomTypeBatchStatus.Stale=>Error(412,"STALE_ROOM_VERSION"),RoomTypeBatchStatus.DependenciesExist=>Error(409,"ROOM_DEPENDENCIES_EXIST"),RoomTypeBatchStatus.InvalidReference=>Error(409,"ROOM_PARENT_INELIGIBLE"),RoomTypeBatchStatus.Inactive=>Error(409,"ROOM_STATE_CONFLICT"),_=>Error(503,"ROOM_LIFECYCLE_RELEASE_REQUIRED")};
        }catch(ValidationException){return Error(400,"VALIDATION_FAILED");}
    }
    private ObjectResult Error(int status,string code)=>Problem(statusCode:status,title:code,extensions:new Dictionary<string,object?>{["code"]=code});
}
