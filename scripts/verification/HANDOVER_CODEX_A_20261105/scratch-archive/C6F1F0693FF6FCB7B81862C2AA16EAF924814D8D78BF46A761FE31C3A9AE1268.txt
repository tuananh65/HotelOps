using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Security;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/room-types")]
[Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[UserAccountActivationErrors]
public sealed class RoomTypesController(RoomTypeCreationService roomTypes):ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(32*1024)]
    public async Task<IActionResult> Create([FromBody] CreateRoomTypeRequest request,CancellationToken ct)
    {
        if(!Request.IsHttps)return Error(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid))return Error(401,"SESSION_INVALID");
        try
        {
            var result=await roomTypes.CreateAsync(ticket!,sid,request,ct);
            return result.Status switch
            {
                RoomTypeCreationStatus.Success=>StatusCode(201,result.Value),
                RoomTypeCreationStatus.SessionInvalid=>Error(401,"SESSION_INVALID"),
                RoomTypeCreationStatus.Forbidden=>Error(403,"PERMISSION_DENIED"),
                RoomTypeCreationStatus.DuplicateCode=>Error(409,"ROOM_TYPE_CODE_EXISTS"),
                RoomTypeCreationStatus.Unavailable=>Error(503,"ROOM_TYPE_CREATE_UNAVAILABLE"),
                RoomTypeCreationStatus.SchemaRequired=>Error(503,"ROOM_TYPE_SCHEMA_REQUIRED"),
                RoomTypeCreationStatus.InvalidChecklist=>Error(400,"DEFAULT_CHECKLIST_UNAVAILABLE"),
                _=>throw new InvalidOperationException("Unknown room type creation result.")
            };
        }
        catch(ValidationException error){return Problem(statusCode:400,title:"VALIDATION_FAILED",detail:error.Message);}
    }
    private ObjectResult Error(int status,string code)=>Problem(statusCode:status,title:code,extensions:new Dictionary<string,object?>{["code"]=code});
}
