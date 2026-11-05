using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Security;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[UserAccountActivationErrors]
public sealed class RoomTypeDirectoryController(RoomTypeDirectoryService service) : ControllerBase
{
    [HttpGet("api/room-types")]
    public Task<IActionResult> List([FromQuery] long? branchId, [FromQuery] bool? isActive, [FromQuery] string? search,
        CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "code", [FromQuery] bool descending = false)
        => Execute((ticket, sid) => service.ListAsync(ticket, sid, branchId, isActive, search, page, pageSize, sort, descending, ct));

    [HttpGet("api/room-types/{id:long}")]
    public Task<IActionResult> Detail(long id, [FromQuery] long? branchId, CancellationToken ct)
        => Execute((ticket, sid) => service.DetailAsync(ticket, sid, branchId, id, ct));

    private async Task<IActionResult> Execute(Func<Services.Authentication.SessionReference, long, Task<RoomTypeDirectoryResult>> action)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) return Problem(statusCode: 401, title: "SESSION_INVALID");
        try
        {
            var result = await action(ticket!, sid);
            if (result.Status == RoomTypeDirectoryStatus.Success)
            {
                if (result.ETag != null) Response.Headers.ETag = result.ETag;
                return Ok(result.Value);
            }
            return Problem(statusCode: result.Status switch
            {
                RoomTypeDirectoryStatus.SessionInvalid => 401, RoomTypeDirectoryStatus.Forbidden => 403,
                RoomTypeDirectoryStatus.NotFound => 404, RoomTypeDirectoryStatus.Inactive => 409, _ => 503
            }, title: result.Status.ToString().ToUpperInvariant());
        }
        catch (ValidationException error) { return Problem(statusCode: 400, title: "VALIDATION_FAILED", detail: error.Message); }
    }
}
