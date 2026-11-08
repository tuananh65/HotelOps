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
[Route("api/branches/{branchId:long}")]
public sealed class InspectionReadsController(RoomTypeCreationService service) : ControllerBase
{
    [HttpGet("inspection-severities")]
    public Task<IActionResult> Severities(long branchId, CancellationToken ct)
        => Execute((ticket, sid) => service.InspectionSeveritiesAsync(ticket, sid, branchId, ct));
    [HttpGet("inspections/assigned")]
    public Task<IActionResult> Assigned(long branchId, [FromQuery] InspectionReadQuery query, CancellationToken ct)
        => Execute((ticket, sid) => service.AssignedInspectionsAsync(ticket, sid, branchId, query, ct));
    [HttpGet("inspection-assignment-tasks")]
    public Task<IActionResult> Tasks(long branchId, [FromQuery] InspectionReadQuery query, CancellationToken ct)
        => Execute((ticket, sid) => service.InspectionAssignmentTasksAsync(ticket, sid, branchId, query, ct));
    private async Task<IActionResult> Execute(Func<SessionReference, long, Task<RoomTypeBatchResult>> operation)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) return Problem(statusCode: 401, title: "SESSION_INVALID");
        try
        {
            var r = await operation(ticket!, sid);
            return r.Status == RoomTypeBatchStatus.Success ? Ok(r.Value) : Problem(statusCode: r.Status switch
            {
                RoomTypeBatchStatus.SessionInvalid => 401, RoomTypeBatchStatus.Forbidden => 403,
                RoomTypeBatchStatus.NotFound => 404, RoomTypeBatchStatus.InvalidReference => 400,
                RoomTypeBatchStatus.Inactive => 409, _ => 503
            }, title: r.Status.ToString().ToUpperInvariant());
        }
        catch (ValidationException e) { return Problem(statusCode: 400, title: "VALIDATION_FAILED", detail: e.Message); }
    }
}
