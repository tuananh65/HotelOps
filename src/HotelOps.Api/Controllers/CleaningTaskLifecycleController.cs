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
public sealed class CleaningTaskLifecycleController(RoomTypeCreationService service) : ControllerBase
{
    [HttpGet("api/branches/{branchId:long}/cleaning-task-options")]
    public Task<IActionResult> Options(long branchId, [FromQuery] long roomId, CancellationToken ct)
        => Run(false, (ticket, sid) => service.CleaningCreationOptionsAsync(ticket, sid, branchId, roomId, ct));
    [HttpGet("api/branches/{branchId:long}/cleaning-tasks/{id:long}")]
    public Task<IActionResult> Assigned(long branchId, long id, [FromQuery] long assignmentId, CancellationToken ct)
        => Run(false, (ticket, sid) => service.ReadAssignedCleaningAsync(ticket, sid, branchId, id, assignmentId, ct));
    [HttpPost("api/branches/{branchId:long}/cleaning-tasks")]
    public Task<IActionResult> Create(long branchId, CreateCleaningTaskRequest input, CancellationToken ct)
        => Run(true, (ticket, sid) => service.CreateCleaningTaskAsync(ticket, sid, branchId, input, ct));
    private async Task<IActionResult> Run(bool created, Func<HotelOps.Api.Services.Authentication.SessionReference, long, Task<RoomTypeBatchResult>> action)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) return Problem(statusCode: 401, title: "SESSION_INVALID");
        try
        {
            var result = await action(ticket!, sid);
            if (result.Status == RoomTypeBatchStatus.Success) { if (result.ETag != null) Response.Headers.ETag = result.ETag; return StatusCode(created ? 201 : 200, result.Value); }
            return Problem(statusCode: result.Status switch
            {
                RoomTypeBatchStatus.SessionInvalid => 401,
                RoomTypeBatchStatus.Forbidden => 403,
                RoomTypeBatchStatus.NotFound => 404,
                RoomTypeBatchStatus.InvalidReference => 400,
                RoomTypeBatchStatus.Duplicate or RoomTypeBatchStatus.Inactive => 409,
                RoomTypeBatchStatus.Stale => 412,
                _ => 503
            }, title: result.Status.ToString().ToUpperInvariant());
        }
        catch (ValidationException e) { return Problem(statusCode: 400, title: "VALIDATION_FAILED", detail: e.Message); }
    }
}
