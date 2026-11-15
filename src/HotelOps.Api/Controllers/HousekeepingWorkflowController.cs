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
    [HttpGet("api/branches/{branchId:long}/inspections/{id:long}/reassignment-options")]
    public Task<IActionResult> ReassignmentOptions(long branchId, long id, CancellationToken ct)
        => Execute((ticket,sid) => service.ReadInspectionReassignmentAsync(ticket,sid,branchId,id,ct),false);
    [HttpPost("api/branches/{branchId:long}/inspections/{id:long}/reassign")]
    public Task<IActionResult> Reassign(long branchId, long id, InspectionReassignmentRequest input, CancellationToken ct)
        => Execute((ticket,sid) => service.ReassignInspectionAsync(ticket,sid,branchId,id,Request.Headers.IfMatch.ToString(),Request.Headers["Idempotency-Key"].ToString(),input,ct),true);
    [HttpPost("api/branches/{branchId:long}/inspections/{id:long}/reject")]
    public Task<IActionResult> Reject(long branchId, long id, RejectInspectionRequest input, CancellationToken ct)
        => Execute((ticket,sid) => service.RejectInspectionAsync(ticket,sid,branchId,id,Request.Headers.IfMatch.ToString(),input,ct),true);
    [HttpPost("api/branches/{branchId:long}/inspections/{id:long}/approve")]
    public Task<IActionResult> Approve(long branchId, long id, CancellationToken ct)
        => Execute((ticket,sid) => service.ApproveInspectionAsync(ticket,sid,branchId,id,Request.Headers.IfMatch.ToString(),ct),true);
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
    [HttpPut("api/branches/{branchId:long}/inspections/{id:long}/defects/{defectId:long}")]
    public Task<IActionResult> EditDefect(long branchId, long id, long defectId, RecordInspectionDefectRequest input, CancellationToken ct)
        => Execute((ticket,sid) => service.ExecuteInspectionAsync(ticket,sid,branchId,id,Request.Headers.IfMatch.ToString(),"edit-defect",null,input,null,Root,ct,defectId),true);
    [HttpPost("api/branches/{branchId:long}/cleaning-tasks/{id:long}/assign")]
    public Task<IActionResult> AssignCleaning(long branchId, long id, AssignCleaningRequest input, CancellationToken ct)
        => Execute((ticket,sid) => service.AssignCleaningCandidateAsync(ticket,sid,branchId,id,Request.Headers.IfMatch.ToString(),Request.Headers["Idempotency-Key"].ToString(),input,ct),true);
    [HttpPost("api/branches/{branchId:long}/cleaning-tasks/{id:long}/inspections")]
    public Task<IActionResult> AssignInspection(long branchId, long id, InitialInspectionAssignmentRequest input, CancellationToken ct)
        => Execute((ticket,sid) => service.AssignInitialInspectionAsync(ticket,sid,branchId,id,Request.Headers.IfMatch.ToString(),Request.Headers["Idempotency-Key"].ToString(),input,ct),true,true);
    [HttpGet("api/branches/{branchId:long}/inspections/pending-assignment")]
    public Task<IActionResult> InitialQueue(long branchId, CancellationToken ct, long? roomId=null, int page=1, int pageSize=25)
        => Execute((ticket,sid) => service.ReadInitialInspectionQueueAsync(ticket,sid,branchId,roomId,page,pageSize,ct),false);
    [HttpGet("api/branches/{branchId:long}/cleaning-tasks/{id:long}/initial-inspection-options")]
    public Task<IActionResult> InitialOptions(long branchId, long id, CancellationToken ct)
        => Execute((ticket,sid) => service.ReadInitialInspectionOptionsAsync(ticket,sid,branchId,id,ct),false);
    [HttpPost("api/branches/{branchId:long}/reworks/{id:long}/reinspection")]
    public Task<IActionResult> AssignReinspection(long branchId, long id, AssignInspectionRequest input, CancellationToken ct)
        => Execute((ticket,sid) => service.AssignReinspectionAsync(ticket,sid,branchId,id,Request.Headers.IfMatch.ToString(),
            Request.Headers["Idempotency-Key"].ToString(),input,Root,ct),true,true);
    [HttpGet("api/branches/{branchId:long}/reworks/{id:long}/reinspection-options")]
    public Task<IActionResult> ReinspectionOptions(long branchId, long id, CancellationToken ct)
        => Execute((ticket,sid) => service.ReadReinspectionAssignmentAsync(ticket,sid,branchId,id,Root,ct),false);
    [HttpGet("api/branches/{branchId:long}/reinspections/{id:long}/execution")]
    public Task<IActionResult> ReadReinspection(long branchId, long id, CancellationToken ct)
        => Execute((ticket,sid) => service.ReadReinspectionExecutionAsync(ticket,sid,branchId,id,Root,ct),false);
    [HttpPost("api/branches/{branchId:long}/reinspections/{id:long}/conclude")]
    public Task<IActionResult> ConcludeReinspection(long branchId, long id, ConcludeReinspectionRequest input, CancellationToken ct)
        => Execute((ticket,sid) => service.ConcludeReinspectionAsync(ticket,sid,branchId,id,Request.Headers.IfMatch.ToString(),
            Request.Headers["Idempotency-Key"].ToString(),input,Root,ct),true);
    [HttpGet("api/branches/{branchId:long}/reinspections/{id:long}/original")]
    public Task<IActionResult> ReinspectionOriginal(long branchId, long id, CancellationToken ct)
        => Execute((ticket,sid) => service.ReadReinspectionOriginalAsync(ticket,sid,branchId,id,Root,ct),false);
    [HttpGet("api/branches/{branchId:long}/reinspections/{id:long}/original/evidence/{attachmentId:long}")]
    public Task<IActionResult> ReinspectionOriginalEvidence(long branchId, long id, long attachmentId, CancellationToken ct)
        => Execute((ticket,sid) => service.ReadReinspectionOriginalAsync(ticket,sid,branchId,id,Root,ct,attachmentId),false);
    [HttpGet("api/branches/{branchId:long}/reinspections/{id:long}/evidence/{attachmentId:long}")]
    public Task<IActionResult> ReinspectionEvidence(long branchId, long id, long attachmentId, CancellationToken ct)
        => Execute((ticket,sid) => service.ReadReinspectionExecutionAsync(ticket,sid,branchId,id,Root,ct,attachmentId),false);
    [HttpPost("api/branches/{branchId:long}/inspections/{id:long}/reworks")]
    public Task<IActionResult> Rework(long branchId, long id, RequestReworkRequest input, CancellationToken ct)
        => Execute((ticket,sid) => service.RequestCleaningReworkAsync(ticket,sid,branchId,id,Request.Headers.IfMatch.ToString(),input,ct,Root),true,true);
    [HttpGet("api/branches/{branchId:long}/inspections/{id:long}/rework-options")]
    public Task<IActionResult> ReworkOptions(long branchId, long id, CancellationToken ct)
        => Execute((ticket,sid) => service.ReadInspectionReworkOptionsAsync(ticket,sid,branchId,id,ct),false);
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
            if(result.Status==RoomTypeBatchStatus.Success)
            {
                if(result.ETag!=null)Response.Headers.ETag=result.ETag;
                if(result.Value is HotelOps.Api.DTOs.HotelStructure.PrivateHistoryFileDto file)
                { Response.Headers["X-Content-Type-Options"]="nosniff"; return File(file.Content,file.ContentType,file.Name); }
                return StatusCode(created?201:200,result.Value);
            }
            if(result.Status==RoomTypeBatchStatus.InvalidReference && result.Value is InspectionIncompleteDto incomplete)
                return Problem(statusCode:400,title:"INSPECTION_INCOMPLETE",extensions:new Dictionary<string,object?>
                    { ["messageCode"]="MSG03", ["inspectionId"]=incomplete.InspectionId, ["branchId"]=incomplete.BranchId,
                      ["versionId"]=incomplete.VersionId, ["etag"]=incomplete.ETag, ["missingItems"]=incomplete.MissingItems });
            if(result.Status==RoomTypeBatchStatus.Inactive && result.Value is InspectionLockedDto locked)
                return Problem(statusCode:409,title:"INSPECTION_CONCLUDED",extensions:new Dictionary<string,object?>
                    { ["messageCode"]="MSG24", ["inspectionId"]=locked.InspectionId, ["branchId"]=locked.BranchId,
                      ["state"]=locked.State, ["etag"]=locked.ETag });
            return Problem(statusCode:result.Status switch{RoomTypeBatchStatus.SessionInvalid=>401,RoomTypeBatchStatus.Forbidden=>403,RoomTypeBatchStatus.NotFound=>404,
                RoomTypeBatchStatus.InvalidReference=>400,RoomTypeBatchStatus.Stale=>412,RoomTypeBatchStatus.Inactive or RoomTypeBatchStatus.Duplicate=>409,_=>503},title:result.Status.ToString().ToUpperInvariant());
        }
        catch(HotelOps.Api.Services.Housekeeping.EvidenceInputException)
        { return Problem(statusCode:400,title:"INVALID_ATTACHMENT",extensions:new Dictionary<string,object?> { ["messageCode"]="MSG23" }); }
        catch(ValidationException error){return Problem(statusCode:400,title:"VALIDATION_FAILED",detail:error.Message);}
    }
}
