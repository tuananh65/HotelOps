using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;
[ApiController]
[Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[UserAccountActivationErrors]
[Route("api/branches/{branchId:long}/inspections/{id:long}")]
public sealed class InspectionConclusionController(RoomTypeCreationService service):ControllerBase
{
    [HttpGet("reject-source")]
    public Task<IActionResult> Source(long branchId,long id,CancellationToken ct)=>Run((t,s)=>service.ReadRejectInspectionPreviewAsync(t,s,branchId,id,ct));
    [HttpGet("rework-source")]
    public Task<IActionResult> ReworkSource(long branchId,long id,CancellationToken ct)=>Run((t,s)=>service.ReadReworkSourcePreviewAsync(t,s,branchId,id,ct));
    [HttpPost("reject")]
    public Task<IActionResult> Reject(long branchId,long id,RejectInspectionRequest input,CancellationToken ct)
    {
        if(!Request.Headers.ContainsKey("If-Match"))return Task.FromResult<IActionResult>(Problem(statusCode:428,title:"IF_MATCH_REQUIRED"));
        return Run((t,s)=>service.RejectInspectionAsync(t,s,branchId,id,Request.Headers.IfMatch.ToString(),Request.Headers["Idempotency-Key"].ToString(),input,ct));
    }
    private async Task<IActionResult> Run(Func<SessionReference,long,Task<RoomTypeBatchResult>> action)
    {
        if(!Request.IsHttps)return Problem(statusCode:400,title:"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var t,out var sid))return Problem(statusCode:401,title:"SESSION_INVALID");
        try{var r=await action(t!,sid);if(r.Status==RoomTypeBatchStatus.Success){if(r.ETag!=null)Response.Headers.ETag=r.ETag;return Ok(r.Value);}
            return Problem(statusCode:r.Status switch{RoomTypeBatchStatus.SessionInvalid=>401,RoomTypeBatchStatus.Forbidden=>403,RoomTypeBatchStatus.NotFound=>404,
                RoomTypeBatchStatus.InvalidReference=>400,RoomTypeBatchStatus.Stale=>412,RoomTypeBatchStatus.Inactive=>409,_=>503},
                title:r.Value is InspectionMutationBlocked b?b.Code:r.Status.ToString().ToUpperInvariant());}
        catch(ValidationException e){return Problem(statusCode:400,title:"VALIDATION_FAILED",detail:e.Message);}
    }
}
