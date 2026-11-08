using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[NonController] // FS-001: imported source; route/security activation awaits DEV1/DEV3 review.
[ApiController]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[UserAccountActivationErrors]
public sealed class TurnoverHistoryController(TurnoverHistoryService service, IWebHostEnvironment environment, IConfiguration configuration) : ControllerBase
{
    private string PrivateRoot => configuration["Evidence:PrivateRoot"] ?? Path.Combine(environment.ContentRootPath, "App_Data", "evidence");

    [HttpGet("api/rooms/{roomId:long}/turnover-history")]
    public Task<IActionResult> Turnovers(long roomId, [FromQuery] OperationalHistoryQuery query, CancellationToken ct)
        => Execute((ticket, sid) => service.TurnoverHistoryAsync(ticket, sid, roomId, query, null, ct));

    [HttpGet("api/rooms/{roomId:long}/turnover-history/{id:long}")]
    public Task<IActionResult> Turnover(long roomId, long id, [FromQuery] long? branchId, CancellationToken ct)
        => Execute((ticket, sid) => service.TurnoverHistoryAsync(ticket, sid, roomId, new(BranchId: branchId), id, ct));

    [HttpGet("api/rooms/{roomId:long}/turnover-history/{id:long}/evidence/{attachmentId:long}")]
    public Task<IActionResult> TurnoverEvidence(long roomId, long id, long attachmentId, [FromQuery] long? branchId, CancellationToken ct)
        => Execute((ticket, sid) => service.TurnoverHistoryAsync(ticket, sid, roomId, new(BranchId: branchId), id, ct, attachmentId, PrivateRoot));

    private async Task<IActionResult> Execute(Func<SessionReference, long, Task<TurnoverHistoryResult>> operation)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) return Problem(statusCode: 401, title: "SESSION_INVALID");
        try
        {
            var result = await operation(ticket!, sid);
            if (result.Status == TurnoverHistoryStatus.Success)
            {
                if (result.Value is PrivateHistoryFileDto file)
                {
                    Response.Headers["X-Content-Type-Options"] = "nosniff";
                    return File(file.Content, file.ContentType, file.Name);
                }
                return Ok(result.Value);
            }
            return Problem(statusCode: result.Status switch
            {
                TurnoverHistoryStatus.SessionInvalid => 401, TurnoverHistoryStatus.Forbidden => 403,
                TurnoverHistoryStatus.NotFound => 404,
                TurnoverHistoryStatus.Inactive => 409, _ => 503
            }, title: result.Status.ToString().ToUpperInvariant());
        }
        catch (ValidationException error) { return Problem(statusCode: 400, title: "VALIDATION_FAILED", detail: error.Message); }
    }
}
