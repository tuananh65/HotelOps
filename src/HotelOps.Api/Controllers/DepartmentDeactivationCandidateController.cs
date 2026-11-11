using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Security;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;
[NonController] // Proposed HTTP source only; no DI/Program or production activation.
[ApiController,Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme),ValidateAntiForgeryToken]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class DepartmentDeactivationCandidateController(RoomTypeCreationService service):ControllerBase
{
    [HttpPost("api/branches/{branchId:long}/departments/{id:long}/deactivate"),RequestSizeLimit(16*1024)]
    public async Task<IActionResult> Deactivate([Range(1,long.MaxValue)]long branchId,[Range(1,long.MaxValue)]long id,DeactivateStructureRequest request,CancellationToken ct)
    {
        if(!Request.IsHttps)return Error(400,"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid))return Error(401,"SESSION_INVALID");
        if(!Request.Headers.ContainsKey("If-Match"))return Error(428,"IF_MATCH_REQUIRED");
        var tag=Request.Headers.IfMatch.ToString();if(Request.Headers.IfMatch.Count!=1||!HotelStructureUpdateService.IsStrongTag(tag))return Error(400,"INVALID_IF_MATCH");
        try{var r=await service.DeactivateDepartmentCandidateAsync(ticket!,sid,branchId,id,tag,request,ct);return r.Status switch{
            RoomTypeBatchStatus.SessionInvalid=>Error(401,"SESSION_INVALID"),RoomTypeBatchStatus.Forbidden=>Error(403,"PERMISSION_DENIED"),RoomTypeBatchStatus.NotFound=>Error(404,"DEPARTMENT_NOT_FOUND"),RoomTypeBatchStatus.Stale=>Error(412,"STALE_DEPARTMENT_VERSION"),RoomTypeBatchStatus.Inactive=>Error(409,"DEPARTMENT_STATE_CONFLICT"),RoomTypeBatchStatus.DependenciesExist=>Error(409,"DEPARTMENT_DEPENDENCIES_EXIST"),_=>Error(503,"DEPARTMENT_RELEASE_REQUIRED")};
        }catch(ValidationException){return Error(400,"VALIDATION_FAILED");}
    }
    private ObjectResult Error(int status,string code)=>Problem(statusCode:status,title:code,extensions:new Dictionary<string,object?>{["code"]=code});
}
