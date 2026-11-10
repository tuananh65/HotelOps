using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;
[ApiController,NonController,Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme)]
[Route("api/branches/{branchId:long}/inspection-assignment-candidate")]
public sealed class InspectionAssignmentCandidateController(RoomTypeCreationService service):ControllerBase
{
    [HttpGet]public Task<IActionResult> Queue(long branchId,CancellationToken ct)=>Run(t=>service.ReadInspectionAssignmentQueueAsync(t.Item1,t.Item2,branchId,ct));
    [HttpGet("{taskId:long}")]public Task<IActionResult> Options(long branchId,long taskId,CancellationToken ct)=>Run(t=>service.ReadInspectionAssignmentOptionsAsync(t.Item1,t.Item2,branchId,taskId,ct));
    [HttpPost("{taskId:long}")]public Task<IActionResult> Confirm(long branchId,long taskId,[FromBody] InspectionAssignmentCandidateRequest input,CancellationToken ct)
    {
        if(!Request.Headers.ContainsKey("If-Match"))return Task.FromResult<IActionResult>(Problem(statusCode:428,title:"IF_MATCH_REQUIRED"));
        return Run(t=>service.PreflightInspectionAssignmentAsync(t.Item1,t.Item2,branchId,taskId,Request.Headers.IfMatch.ToString(),input,ct));
    }
    private async Task<IActionResult> Run(Func<(SessionReference,long),Task<RoomTypeBatchResult>> call){
        if(!Request.IsHttps)return Problem(statusCode:400,title:"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid))return Problem(statusCode:401,title:"SESSION_INVALID");
        RoomTypeBatchResult r;try{r=await call((ticket!,sid));}catch(System.ComponentModel.DataAnnotations.ValidationException){return Problem(statusCode:400,title:"INVALID_REQUEST");}if(r.Status==RoomTypeBatchStatus.Success){Response.Headers.ETag=r.ETag;return Ok(r.Value);}
        return Problem(statusCode:r.Status switch{RoomTypeBatchStatus.Forbidden=>403,RoomTypeBatchStatus.SessionInvalid=>401,RoomTypeBatchStatus.NotFound=>404,RoomTypeBatchStatus.Stale=>412,RoomTypeBatchStatus.InvalidReference=>400,RoomTypeBatchStatus.Duplicate or RoomTypeBatchStatus.Inactive=>409,_=>503},title:r.Status==RoomTypeBatchStatus.Unavailable?"UC041_ASSIGNMENT_NOTIFICATION_NOT_ACCEPTED":r.Status.ToString());
    }
}
