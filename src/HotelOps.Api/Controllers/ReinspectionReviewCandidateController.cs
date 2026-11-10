using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

// Intentionally not discovered by MVC. Activation needs accepted exact binding/schema/read policy.
[NonController]
[Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[UserAccountActivationErrors]
[Route("api/branches/{branchId:long}/inspections/{inspectionId:long}/reinspection-candidate")]
public sealed class ReinspectionReviewCandidateController(RoomTypeCreationService service):ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(long branchId,long inspectionId,long reworkId,string inspectionTag,string reworkTag,CancellationToken ct)
    {
        if(!Request.IsHttps)return Problem(statusCode:400,title:"HTTPS_REQUIRED");
        if(!HotelOpsCookieTicket.TryRead(User,out var ticket,out var sid))return Problem(statusCode:401,title:"SESSION_INVALID");
        var result=await service.ReadReinspectionReviewCandidateAsync(ticket!,sid,branchId,inspectionId,reworkId,inspectionTag,reworkTag,ct);
        if(result.Status==RoomTypeBatchStatus.Success){Response.Headers.ETag=result.ETag;return Ok(result.Value);}
        return Problem(statusCode:result.Status switch {RoomTypeBatchStatus.SessionInvalid=>401,RoomTypeBatchStatus.Forbidden=>403,
            RoomTypeBatchStatus.NotFound=>404,RoomTypeBatchStatus.Stale=>412,RoomTypeBatchStatus.InvalidReference=>400,RoomTypeBatchStatus.Inactive=>409,_=>503},title:result.Status.ToString().ToUpperInvariant());
    }
}
