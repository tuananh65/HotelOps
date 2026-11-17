using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Security;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;
[ApiController, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme), UserAccountActivationErrors]
[Route("api/rooms/{room:long}/departments")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RoomDepartmentsController(RoomTypeCreationService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Read(long room, [FromQuery] long branchId, CancellationToken ct) => Run(room, branchId, null, ct);
    [HttpPut, ValidateAntiForgeryToken, RequestSizeLimit(16384)]
    public Task<IActionResult> Update(long room, [FromQuery] long branchId, RoomDepartmentInput input, CancellationToken ct) => Run(room, branchId, input, ct);
    private async Task<IActionResult> Run(long room, long branch, RoomDepartmentInput? input, CancellationToken ct)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) return Unauthorized();
        if (input != null && Request.Headers.IfMatch.Count != 1) return Problem(statusCode: 428, title: "IF_MATCH_REQUIRED");
        try
        {
            var result = await service.RoomDepartmentsAsync(ticket!, sid, branch, room, Request.Headers.IfMatch.ToString(), input, ct);
            if (result.Status == RoomTypeBatchStatus.Success) { Response.Headers.ETag = result.ETag; return Ok(result.Value); }
            return Problem(statusCode: result.Status switch { RoomTypeBatchStatus.SessionInvalid => 401, RoomTypeBatchStatus.Forbidden => 403,
                RoomTypeBatchStatus.NotFound => 404, RoomTypeBatchStatus.Stale => 412, RoomTypeBatchStatus.InvalidReference => 409, _ => 503 }, title: result.Status.ToString());
        }
        catch (ValidationException e) { return Problem(statusCode: 400, title: "VALIDATION_FAILED", detail: e.Message); }
    }
}
