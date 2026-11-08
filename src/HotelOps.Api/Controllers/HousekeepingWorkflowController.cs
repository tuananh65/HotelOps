using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[UserAccountActivationErrors]
[RequestSizeLimit(72 * 1024 * 1024)]
public sealed class HousekeepingWorkflowController(RoomTypeCreationService service, IWebHostEnvironment environment, IConfiguration configuration) : ControllerBase
{
    private string Root => configuration["Evidence:PrivateRoot"] ?? Path.Combine(environment.ContentRootPath, "App_Data", "evidence");
    [HttpGet("api/branches/{branchId:long}/inspections/{id:long}/execution")]
    public Task<IActionResult> Read(long branchId, long id, CancellationToken ct) => Inspect(branchId,id,"read",null,null,null,ct);
    [HttpPost("api/branches/{branchId:long}/inspections/{id:long}/start")]
    public Task<IActionResult> Start(long branchId, long id, InspectionActionRequest input, CancellationToken ct) => Inspect(branchId,id,"start",null,null,input.Reason,ct);
    [HttpPut("api/branches/{branchId:long}/inspections/{id:long}/results")]
    public Task<IActionResult> Save(long branchId, long id, SaveInspectionRequest input, CancellationToken ct) => Inspect(branchId,id,"save",input,null,null,ct);
    [HttpPost("api/branches/{branchId:long}/inspections/{id:long}/finish")]
    public Task<IActionResult> Finish(long branchId, long id, InspectionActionRequest input, CancellationToken ct) => Inspect(branchId,id,"finish",null,null,input.Reason,ct);
    [HttpPost("api/branches/{branchId:long}/inspections/{id:long}/defects")]
    public Task<IActionResult> Defect(long branchId, long id, RecordInspectionDefectRequest input, CancellationToken ct) => Inspect(branchId,id,"defect",null,input,null,ct);
    [HttpPost("api/branches/{branchId:long}/cleaning-tasks/{id:long}/assign")]
    public Task<IActionResult> AssignCleaning(long branchId, long id, AssignCleaningRequest input, CancellationToken ct)
        => Execute((ticket,sid) => service.AssignCleaningCandidateAsync(ticket,sid,branchId,id,Request.Headers.IfMatch.ToString(),Request.Headers["Idempotency-Key"].ToString(),input,ct),true);
    [HttpPost("api/branches/{branchId:long}/cleaning-tasks/{id:long}/inspections")]
    public Task<IActionResult> AssignInspection(long branchId, long id, AssignInspectionRequest input, CancellationToken ct)
        => Execute((ticket,sid) => service.AssignInspectionAsync(ticket,sid,branchId,id,input,ct),false,true);
    [HttpPost("api/branches/{branchId:long}/inspections/{id:long}/reworks")]
    public Task<IActionResult> Rework(long branchId, long id, RequestReworkRequest input, CancellationToken ct)
        => Execute((ticket,sid) => service.RequestCleaningReworkAsync(ticket,sid,branchId,id,Request.Headers.IfMatch.ToString(),input,ct),true,true);
    private Task<IActionResult> Inspect(long branch,long id,string action,SaveInspectionRequest? save,RecordInspectionDefectRequest? defect,string? reason,CancellationToken ct)
        => Execute((ticket,sid) => service.ExecuteInspectionAsync(ticket,sid,branch,id,Request.Headers.IfMatch.ToString(),action,save,defect,reason,Root,ct),action!="read");
    private async Task<IActionResult> Execute(Func<SessionReference,long,Task<RoomTypeBatchResult>> action,bool conditional,bool created=false)
    {
        if(!Request.IsHttps)return Problem(statusCode:400,title:"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid))return Problem(statusCode:401,title:"SESSION_INVALID");
        if(conditional&&!Request.Headers.ContainsKey("If-Match"))return Problem(statusCode:428,title:"IF_MATCH_REQUIRED");
        try
        {
            var result=await action(ticket!,sid);
            if(result.Status==RoomTypeBatchStatus.Success){if(result.ETag!=null)Response.Headers.ETag=result.ETag;return StatusCode(created?201:200,result.Value);}
            return Problem(statusCode:result.Status switch{RoomTypeBatchStatus.SessionInvalid=>401,RoomTypeBatchStatus.Forbidden=>403,RoomTypeBatchStatus.NotFound=>404,
                RoomTypeBatchStatus.InvalidReference=>400,RoomTypeBatchStatus.Stale=>412,RoomTypeBatchStatus.Inactive or RoomTypeBatchStatus.Duplicate=>409,_=>503},title:result.Status.ToString().ToUpperInvariant());
        }
        catch(ValidationException error){return Problem(statusCode:400,title:"VALIDATION_FAILED",detail:error.Message);}
    }
}
