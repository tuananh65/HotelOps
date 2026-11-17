using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Security;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DepartmentDeactivationCandidateController(RoomTypeCreationService service) : ControllerBase
{
    [HttpGet("api/branches/{branchId:long}/departments/{id:long}/deactivation-review")]
    public async Task<IActionResult> Review([Range(1,long.MaxValue)] long branchId,
        [Range(1,long.MaxValue)] long id, CancellationToken ct)
    {
        if (!Request.IsHttps) return Error(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) return Error(401, "SESSION_INVALID");
        return Map(await service.ReviewDepartmentDeactivationAsync(ticket!, sid, branchId, id, ct));
    }

    [HttpPost("api/branches/{branchId:long}/departments/{id:long}/deactivate")]
    [ValidateAntiForgeryToken, RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Deactivate([Range(1,long.MaxValue)] long branchId,
        [Range(1,long.MaxValue)] long id, DeactivateStructureRequest request, CancellationToken ct)
    {
        if (!Request.IsHttps) return Error(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) return Error(401, "SESSION_INVALID");
        if (!Request.Headers.ContainsKey("If-Match")) return Error(428, "IF_MATCH_REQUIRED");
        var tag = Request.Headers.IfMatch.ToString();
        if (Request.Headers.IfMatch.Count != 1 || !HotelStructureUpdateService.IsStrongTag(tag)) return Error(400, "INVALID_IF_MATCH");
        try { return Map(await service.DeactivateDepartmentCandidateAsync(ticket!, sid, branchId, id, tag, request, ct)); }
        catch (ValidationException) { return Error(400, "VALIDATION_FAILED"); }
    }

    private IActionResult Map(RoomTypeBatchResult result)
    {
        if (result.Status == RoomTypeBatchStatus.Success)
        {
            Response.Headers.ETag = result.ETag;
            return Ok(result.Value);
        }
        if (result.Status == RoomTypeBatchStatus.DependenciesExist)
            return Problem(statusCode: 409, title: "DEPARTMENT_DEPENDENCIES_EXIST",
                extensions: new Dictionary<string,object?> { ["code"] = "DEPARTMENT_DEPENDENCIES_EXIST", ["review"] = result.Value });
        return result.Status switch
        {
            RoomTypeBatchStatus.SessionInvalid => Error(401, "SESSION_INVALID"),
            RoomTypeBatchStatus.Forbidden or RoomTypeBatchStatus.InvalidReference => Error(403, "PERMISSION_DENIED"),
            RoomTypeBatchStatus.NotFound => Error(404, "DEPARTMENT_NOT_FOUND"),
            RoomTypeBatchStatus.Stale => Error(412, "STALE_DEPARTMENT_VERSION"),
            RoomTypeBatchStatus.Inactive => Error(409, "DEPARTMENT_STATE_CONFLICT"),
            RoomTypeBatchStatus.SchemaRequired => Error(503, "DEPARTMENT_SCHEMA_REQUIRED"),
            _ => Error(503, "DEPARTMENT_TEMPORARILY_UNAVAILABLE")
        };
    }
    private ObjectResult Error(int status, string code) => Problem(statusCode: status, title: code,
        extensions: new Dictionary<string,object?> { ["code"] = code });
}
