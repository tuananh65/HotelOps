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
[RequestSizeLimit(32 * 1024)]
public sealed class BranchMaintenanceController(RoomTypeCreationService service) : ControllerBase
{
    [HttpPost("api/branches")]
    public Task<IActionResult> Create(CreateBranchRequest input, CancellationToken ct) => Execute(null, input, null, ct);
    [HttpGet("api/branches/{id:long}/edit")]
    public Task<IActionResult> Read(long id, CancellationToken ct) => Execute(id, null, null, ct);
    [HttpPut("api/branches/{id:long}")]
    public Task<IActionResult> Update(long id, UpdateBranchRequest input, CancellationToken ct) => Execute(id, null, input, ct);

    private async Task<IActionResult> Execute(long? id, CreateBranchRequest? create, UpdateBranchRequest? update, CancellationToken ct)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) return Problem(statusCode: 401, title: "SESSION_INVALID");
        if (update != null && !Request.Headers.ContainsKey("If-Match")) return Problem(statusCode: 428, title: "IF_MATCH_REQUIRED");
        try
        {
            var result = await service.MaintainBranchAsync(ticket!, sid, id, Request.Headers.IfMatch.ToString(), create, update, ct);
            if (result.Status == RoomTypeBatchStatus.Success)
            {
                Response.Headers.ETag = result.ETag;
                return StatusCode(create == null ? 200 : 201, result.Value);
            }
            return Problem(statusCode: result.Status switch
            {
                RoomTypeBatchStatus.SessionInvalid => 401, RoomTypeBatchStatus.Forbidden => 403,
                RoomTypeBatchStatus.NotFound => 404, RoomTypeBatchStatus.Stale => 412,
                RoomTypeBatchStatus.Duplicate => 409, _ => 503
            }, title: result.Status.ToString().ToUpperInvariant());
        }
        catch (ValidationException error) { return Problem(statusCode: 400, title: "VALIDATION_FAILED", detail: error.Message); }
    }
}
