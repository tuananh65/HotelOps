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
public sealed class CleaningExecutionController(RoomTypeCreationService service) : ControllerBase
{
    [HttpGet("api/branches/{branchId:long}/cleaning-tasks/{id:long}/execution")]
    public Task<IActionResult> Read(long branchId, long id, CancellationToken ct)
        => Run(false, (ticket, sid) => service.ReadCleaningExecutionAsync(ticket, sid, branchId, id, ct));
    [HttpPost("api/branches/{branchId:long}/cleaning-tasks/{id:long}/start")]
    public Task<IActionResult> Start(long branchId, long id, StartCleaningRequest request, CancellationToken ct)
        => Run(true, (ticket, sid) => service.StartCleaningAsync(ticket, sid, branchId, id, Request.Headers.IfMatch.ToString(), request, ct));
    [HttpPost("api/branches/{branchId:long}/cleaning-tasks/{id:long}/reassign")]
    public Task<IActionResult> Reassign(long branchId, long id, ReassignCleaningRequest request, CancellationToken ct)
        => Run(true, (ticket, sid) => service.ReassignCleaningAsync(ticket, sid, branchId, id, Request.Headers.IfMatch.ToString(), Request.Headers["Idempotency-Key"].ToString(), request, ct));
    private async Task<IActionResult> Run(bool conditional, Func<SessionReference, long, Task<RoomTypeBatchResult>> action)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) return Problem(statusCode: 401, title: "SESSION_INVALID");
        if (conditional && !Request.Headers.ContainsKey("If-Match")) return Problem(statusCode: 428, title: "IF_MATCH_REQUIRED");
        try
        {
            var result = await action(ticket!, sid);
            if (result.Status == RoomTypeBatchStatus.Success) { if (result.ETag != null) Response.Headers.ETag = result.ETag; return Ok(result.Value); }
            return Problem(statusCode: result.Status switch { RoomTypeBatchStatus.SessionInvalid => 401, RoomTypeBatchStatus.Forbidden => 403,
                RoomTypeBatchStatus.NotFound => 404, RoomTypeBatchStatus.InvalidReference => 400, RoomTypeBatchStatus.Stale => 412,
                RoomTypeBatchStatus.Inactive or RoomTypeBatchStatus.Duplicate => 409, _ => 503 },
                title: result.Value is CleaningLifecycleFailure failure ? failure.Code : result.Status.ToString().ToUpperInvariant());
        }
        catch (ValidationException error) { return Problem(statusCode: 400, title: "VALIDATION_FAILED", detail: error.Message); }
    }
}
