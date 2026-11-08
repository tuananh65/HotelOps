using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
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
[RequestSizeLimit(256 * 1024)]
[Route("api/branches/{branchId:long}/inspection-checklists")]
public sealed class InspectionChecklistsController(RoomTypeCreationService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(long branchId, [FromQuery] ChecklistCollectionRequest query, CancellationToken ct)
        => Execute((ticket, sid) => service.ListChecklistsAsync(ticket, sid, branchId, query, ct));

    [HttpPost]
    public Task<IActionResult> Create(long branchId, CreateChecklistRequest request, CancellationToken ct)
        => Execute((ticket, sid) => service.CreateChecklistAsync(ticket, sid, branchId, request, ct), true);
    [HttpGet("{id:long}")]
    public Task<IActionResult> Read(long branchId, long id, CancellationToken ct)
        => Execute((ticket, sid) => service.ReadChecklistAsync(ticket, sid, branchId, id, ct));
    [HttpPut("{id:long}")]
    public Task<IActionResult> Update(long branchId, long id, UpdateChecklistRequest request, CancellationToken ct)
        => Conditional((ticket, sid) => service.UpdateChecklistAsync(ticket, sid, branchId, id, Request.Headers.IfMatch.ToString(), request, ct));
    [HttpPost("{id:long}/publish")]
    public Task<IActionResult> Publish(long branchId, long id, ChecklistTransitionRequest request, CancellationToken ct)
        => Transition(branchId, id, "publish", request, ct);
    [HttpPost("{id:long}/retire")]
    public Task<IActionResult> Retire(long branchId, long id, ChecklistTransitionRequest request, CancellationToken ct)
        => Transition(branchId, id, "retire", request, ct);
    private Task<IActionResult> Transition(long branch, long id, string action, ChecklistTransitionRequest request, CancellationToken ct)
        => Conditional((ticket, sid) => service.TransitionChecklistAsync(ticket, sid, branch, id, Request.Headers.IfMatch.ToString(), action, request, ct));
    private Task<IActionResult> Conditional(Func<SessionReference, long, Task<RoomTypeBatchResult>> operation)
        => !Request.Headers.ContainsKey("If-Match") ? Task.FromResult<IActionResult>(Problem(statusCode: 428, title: "IF_MATCH_REQUIRED")) : Execute(operation);
    private async Task<IActionResult> Execute(Func<SessionReference, long, Task<RoomTypeBatchResult>> operation, bool created = false)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) return Problem(statusCode: 401, title: "SESSION_INVALID");
        try
        {
            var result = await operation(ticket!, sid);
            if (result.Status == RoomTypeBatchStatus.Success)
            {
                Response.Headers.ETag = result.ETag;
                return StatusCode(created ? 201 : 200, result.Value);
            }
            return Problem(statusCode: result.Status switch
            {
                RoomTypeBatchStatus.SessionInvalid => 401, RoomTypeBatchStatus.Forbidden => 403, RoomTypeBatchStatus.NotFound => 404,
                RoomTypeBatchStatus.Stale => 412, RoomTypeBatchStatus.InvalidReference => 400,
                RoomTypeBatchStatus.Inactive or RoomTypeBatchStatus.Duplicate => 409, _ => 503
            }, title: result.Status.ToString().ToUpperInvariant());
        }
        catch (ValidationException error) { return Problem(statusCode: 400, title: "VALIDATION_FAILED", detail: error.Message); }
    }
}
