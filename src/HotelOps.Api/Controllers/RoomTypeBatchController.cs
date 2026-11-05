using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Security;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[UserAccountActivationErrors]
public sealed class RoomTypeBatchController(RoomTypeCreationService service) : ControllerBase
{
    [HttpGet("api/room-types")]
    public Task<IActionResult> List([FromQuery] long? branchId, [FromQuery] bool? isActive, [FromQuery] string? search,
        CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "code", [FromQuery] bool descending = false)
        => Execute((ticket, sid) => service.ListRoomTypesAsync(ticket, sid, branchId, isActive, search, page, pageSize, ct, sort, descending));

    [HttpGet("api/room-types/{id:long}")]
    public Task<IActionResult> Detail(long id, [FromQuery] long? branchId, CancellationToken ct)
        => Execute((ticket, sid) => service.ReadRoomTypeAsync(ticket, sid, branchId, id, ct));

    [HttpPut("api/room-types/{id:long}")]
    [RequestSizeLimit(32 * 1024)]
    public Task<IActionResult> Update(long id, [FromQuery] long branchId, [FromBody] UpdateRoomTypeRequest request, CancellationToken ct)
    {
        if (!Request.Headers.ContainsKey("If-Match")) return Task.FromResult<IActionResult>(Problem(statusCode: 428, title: "IF_MATCH_REQUIRED"));
        return Execute((ticket, sid) => service.UpdateRoomTypeAsync(ticket, sid, branchId, id, Request.Headers.IfMatch.ToString(), request, ct));
    }

    [HttpPost("api/room-types/{id:long}/deactivate")]
    [RequestSizeLimit(32 * 1024)]
    public Task<IActionResult> DeactivateType(long id, [FromQuery] long branchId, [FromBody] DeactivateRoomTypeRequest request, CancellationToken ct)
    {
        if (!Request.Headers.ContainsKey("If-Match")) return Task.FromResult<IActionResult>(Problem(statusCode: 428, title: "IF_MATCH_REQUIRED"));
        return Execute((ticket, sid) => service.DeactivateRoomTypeAsync(ticket, sid, branchId, id, Request.Headers.IfMatch.ToString(), request, ct));
    }
    [HttpGet("api/rooms/{id:long}/edit")]
    public Task<IActionResult> EditRoom(long id, [FromQuery] long branchId, CancellationToken ct)
        => Execute((ticket, sid) => service.ReadEditableRoomAsync(ticket, sid, branchId, id, ct));
    [HttpPut("api/rooms/{id:long}")]
    [RequestSizeLimit(32 * 1024)]
    public Task<IActionResult> UpdateRoom(long id, [FromQuery] long branchId, [FromBody] UpdateRoomInformationRequest request, CancellationToken ct)
    {
        if (!Request.Headers.ContainsKey("If-Match")) return Task.FromResult<IActionResult>(Problem(statusCode: 428, title: "IF_MATCH_REQUIRED"));
        return Execute((ticket, sid) => service.UpdateRoomInformationAsync(ticket, sid, branchId, id, Request.Headers.IfMatch.ToString(), request, ct));
    }

    [HttpPost("api/rooms")]
    [RequestSizeLimit(32 * 1024)]
    public Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request, CancellationToken ct)
        => Execute((ticket, sid) => service.CreateRoomAsync(ticket, sid, request, ct), true);

    [HttpPost("api/branches/{branchId:long}/departments")]
    [RequestSizeLimit(32 * 1024)]
    public Task<IActionResult> CreateDepartment(long branchId, [FromBody] CreateDepartmentRequest request, CancellationToken ct)
        => Execute((ticket, sid) => service.CreateDepartmentAsync(ticket, sid, branchId, request, ct), true);
    [HttpGet("api/branches/{branchId:long}/departments/{id:long}")]
    public Task<IActionResult> Department(long branchId, long id, CancellationToken ct)
        => Execute((ticket, sid) => service.ReadDepartmentAsync(ticket, sid, branchId, id, ct));
    [HttpPut("api/branches/{branchId:long}/departments/{id:long}")]
    [RequestSizeLimit(32 * 1024)]
    public Task<IActionResult> UpdateDepartment(long branchId, long id, [FromBody] UpdateDepartmentRequest request, CancellationToken ct)
    {
        if (!Request.Headers.ContainsKey("If-Match")) return Task.FromResult<IActionResult>(Problem(statusCode: 428, title: "IF_MATCH_REQUIRED"));
        return Execute((ticket, sid) => service.UpdateDepartmentAsync(ticket, sid, branchId, id, Request.Headers.IfMatch.ToString(), request, ct));
    }

    private async Task<IActionResult> Execute(Func<HotelOps.Api.Services.Authentication.SessionReference, long, Task<RoomTypeBatchResult>> action, bool created = false)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) return Problem(statusCode: 401, title: "SESSION_INVALID");
        try
        {
            var result = await action(ticket!, sid);
            if (result.Status == RoomTypeBatchStatus.Success)
            {
                if (result.ETag != null) Response.Headers.ETag = result.ETag;
                return StatusCode(created ? 201 : 200, result.Value);
            }
            if (result.Status == RoomTypeBatchStatus.DependenciesExist)
                return Problem(statusCode: 409, title: "ACTIVE_ROOMS_EXIST", extensions: new Dictionary<string, object?> { ["rooms"] = result.Value });
            return Problem(statusCode: result.Status switch
            {
                RoomTypeBatchStatus.SessionInvalid => 401, RoomTypeBatchStatus.Forbidden => 403,
                RoomTypeBatchStatus.NotFound => 404, RoomTypeBatchStatus.Stale => 412,
                RoomTypeBatchStatus.InvalidReference => 400, RoomTypeBatchStatus.Inactive or RoomTypeBatchStatus.Duplicate => 409,
                _ => 503
            }, title: result.Status.ToString().ToUpperInvariant());
        }
        catch (ValidationException error) { return Problem(statusCode: 400, title: "VALIDATION_FAILED", detail: error.Message); }
    }
}
