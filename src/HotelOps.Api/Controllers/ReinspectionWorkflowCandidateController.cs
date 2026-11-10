using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[NonController] // Candidate test host only; no production route/DI/router activation.
[Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[UserAccountActivationErrors]
[Route("api/branches/{branchId:long}/reinspection-workflow-candidate")]
public sealed class ReinspectionWorkflowCandidateController(RoomTypeCreationService service):ControllerBase
{
    [HttpGet] public Task<IActionResult> List(long branchId,CancellationToken ct)=>Run((t,s)=>service.ListReinspectionWorkflowAsync(t,s,branchId,ct));
    [HttpGet("{inspectionId:long}")] public Task<IActionResult> Read(long branchId,long inspectionId,long reworkId,CancellationToken ct)=>Run((t,s)=>service.ReadReinspectionWorkflowAsync(t,s,branchId,inspectionId,reworkId,ct));
    [HttpPost("{inspectionId:long}/bind")] public Task<IActionResult> Bind(long branchId,long inspectionId,[FromBody] ReinspectionBindCandidateRequest input,CancellationToken ct)=>Run((t,s)=>service.BindReinspectionWorkflowAsync(t,s,branchId,inspectionId,input,ct));
    [HttpPut("{inspectionId:long}/results")]
    public Task<IActionResult> Save(long branchId,long inspectionId,[FromBody] ReinspectionSaveCandidateRequest input,CancellationToken ct)
    {
        if(!Request.Headers.ContainsKey("If-Match"))return Task.FromResult<IActionResult>(Problem(statusCode:428,title:"IF_MATCH_REQUIRED"));
        return Run((t,s)=>service.SaveReinspectionWorkflowAsync(t,s,branchId,inspectionId,Request.Headers.IfMatch.ToString(),input,ct));
    }
    [HttpGet("{inspectionId:long}/conclusion-proposal")] public Task<IActionResult> ReadProposal(long branchId,long inspectionId,long reworkId,CancellationToken ct)
        =>Run((t,s)=>service.ReadReinspectionConclusionAsync(t,s,branchId,inspectionId,reworkId,ct));
    [HttpPost("{inspectionId:long}/conclusion-proposal")] public Task<IActionResult> PrepareProposal(long branchId,long inspectionId,[FromBody] ReinspectionConclusionProposalRequest input,CancellationToken ct)
    {
        if(!Request.Headers.ContainsKey("If-Match"))return Task.FromResult<IActionResult>(Problem(statusCode:428,title:"IF_MATCH_REQUIRED"));
        return Run((t,s)=>service.PrepareReinspectionConclusionAsync(t,s,branchId,inspectionId,Request.Headers.IfMatch.ToString(),input,ct));
    }
    [HttpPost("{inspectionId:long}/conclude")] public Task<IActionResult> Conclude(long branchId,long inspectionId,long reworkId,CancellationToken ct)
        =>Run(async(t,s)=>{var source=await service.ReadReinspectionWorkflowAsync(t,s,branchId,inspectionId,reworkId,ct);return source.Status==RoomTypeBatchStatus.Success
            ?new(RoomTypeBatchStatus.Unavailable,new InspectionMutationBlocked("REINSPECTION_LIFECYCLE_NOT_ACCEPTED",["CHECKLIST_BINDING_POLICY","REWORK_COMPLETED_FAILED_TRANSITIONS","REQUIRED_STAGING_AND_PROJECTION"])):source;});
    private async Task<IActionResult> Run(Func<SessionReference,long,Task<RoomTypeBatchResult>> action)
    {
        if(!Request.IsHttps)return Problem(statusCode:400,title:"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid))return Problem(statusCode:401,title:"SESSION_INVALID");
        RoomTypeBatchResult result;
        try{result=await action(ticket!,sid);}
        catch(System.ComponentModel.DataAnnotations.ValidationException){return Problem(statusCode:400,title:"INVALID_REQUEST");}
        if(result.Status==RoomTypeBatchStatus.Success){if(result.ETag!=null)Response.Headers.ETag=result.ETag;return Ok(result.Value);}
        return Problem(statusCode:result.Status switch{RoomTypeBatchStatus.SessionInvalid=>401,RoomTypeBatchStatus.Forbidden=>403,RoomTypeBatchStatus.NotFound=>404,
            RoomTypeBatchStatus.Stale=>412,RoomTypeBatchStatus.InvalidReference=>400,RoomTypeBatchStatus.Inactive or RoomTypeBatchStatus.Duplicate=>409,_=>503},title:result.Value is InspectionMutationBlocked b?b.Code:result.Status.ToString().ToUpperInvariant());
    }
}
