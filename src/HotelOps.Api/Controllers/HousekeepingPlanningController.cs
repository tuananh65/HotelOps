using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[UserAccountActivationErrors]
[Route("api/branches/{branchId:long}/cleaning-tasks/{taskId:long}")]
public sealed class HousekeepingPlanningController(RoomTypeCreationService service) : ControllerBase
{
    [HttpGet("assignment-options")]
    public Task<IActionResult> Assignment(long branchId, long taskId, [FromQuery] HousekeepingPlanningQuery query, CancellationToken ct)
        => Execute(branchId, taskId, false, query, ct);
    [HttpGet("inspection-options")]
    public Task<IActionResult> Inspection(long branchId, long taskId, [FromQuery] HousekeepingPlanningQuery query, CancellationToken ct)
        => Execute(branchId, taskId, true, query, ct);
    private async Task<IActionResult> Execute(long branch, long task, bool inspection, HousekeepingPlanningQuery query, CancellationToken ct)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) return Problem(statusCode: 401, title: "SESSION_INVALID");
        try
        {
            var result = await service.HousekeepingPlanningAsync(ticket!, sid, branch, task, inspection, query, ct);
            if (result.Status == RoomTypeBatchStatus.Success) { if (result.ETag != null) Response.Headers.ETag = result.ETag; return Ok(result.Value); }
            return Problem(statusCode: result.Status switch
            {
                RoomTypeBatchStatus.SessionInvalid => 401, RoomTypeBatchStatus.Forbidden => 403, RoomTypeBatchStatus.NotFound => 404,
                RoomTypeBatchStatus.InvalidReference => 400, RoomTypeBatchStatus.Inactive => 409, _ => 503
            }, title: result.Status.ToString().ToUpperInvariant());
        }
        catch (ValidationException error) { return Problem(statusCode: 400, title: "VALIDATION_FAILED", detail: error.Message); }
    }
}
