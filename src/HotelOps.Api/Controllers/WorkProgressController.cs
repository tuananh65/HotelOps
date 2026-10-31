using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
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
[RequestSizeLimit(72 * 1024 * 1024)]
public sealed class WorkProgressController(RoomTypeCreationService service, IWebHostEnvironment environment, IConfiguration configuration) : ControllerBase
{
    private string Root => configuration["Evidence:PrivateRoot"] ?? Path.Combine(environment.ContentRootPath, "App_Data", "evidence");
    [HttpPost("api/branches/{branch:long}/room-types/{id:long}/cleaning-items")]
    public Task<IActionResult> Template(long branch, long id, InitializeProgressRequest input, CancellationToken ct)
        => Initialize(branch, id, "template", input, ct);
    [HttpPost("api/branches/{branch:long}/cleaning-tasks/{id:long}/progress-initialization")]
    public Task<IActionResult> InitializeCleaning(long branch, long id, InitializeProgressRequest input, CancellationToken ct)
        => Initialize(branch, id, "cleaning", input, ct);
    [HttpPost("api/branches/{branch:long}/operational-tasks/{id:long}/progress-initialization")]
    public Task<IActionResult> InitializeTask(long branch, long id, InitializeProgressRequest input, CancellationToken ct)
        => Initialize(branch, id, "task", input, ct);
    [HttpGet("api/branches/{branch:long}/cleaning-tasks/{id:long}/progress")]
    public Task<IActionResult> ReadCleaning(long branch, long id, CancellationToken ct) => Progress(branch, id, true, null, ct);
    [HttpGet("api/branches/{branch:long}/operational-tasks/{id:long}/progress")]
    public Task<IActionResult> ReadTask(long branch, long id, CancellationToken ct) => Progress(branch, id, false, null, ct);
    [HttpPut("api/branches/{branch:long}/cleaning-tasks/{id:long}/progress")]
    public Task<IActionResult> SaveCleaning(long branch, long id, SaveProgressRequest input, CancellationToken ct) => Progress(branch, id, true, input, ct);
    [HttpPut("api/branches/{branch:long}/operational-tasks/{id:long}/progress")]
    public Task<IActionResult> SaveTask(long branch, long id, SaveProgressRequest input, CancellationToken ct) => Progress(branch, id, false, input, ct);
    [HttpGet("api/branches/{branch:long}/cleaning-tasks/{id:long}/progress/evidence/{attachment:long}")]
    public Task<IActionResult> CleaningEvidence(long branch, long id, long attachment, CancellationToken ct) => Progress(branch, id, true, null, ct, attachment);
    [HttpGet("api/branches/{branch:long}/operational-tasks/{id:long}/progress/evidence/{attachment:long}")]
    public Task<IActionResult> TaskEvidence(long branch, long id, long attachment, CancellationToken ct) => Progress(branch, id, false, null, ct, attachment);
    private Task<IActionResult> Initialize(long branch, long id, string kind, InitializeProgressRequest input, CancellationToken ct)
        => Execute((ticket, sid) => service.InitializeWorkProgressAsync(ticket, sid, branch, id, kind, input, ct), false, true);
    private Task<IActionResult> Progress(long branch, long id, bool cleaning, SaveProgressRequest? input, CancellationToken ct, long? attachment = null)
        => Execute((ticket, sid) => service.WorkProgressAsync(ticket, sid, branch, id, cleaning, Request.Headers.IfMatch.ToString(), input, Root, ct, attachment), input != null);
    private async Task<IActionResult> Execute(Func<SessionReference, long, Task<RoomTypeBatchResult>> action, bool conditional, bool created = false)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) return Problem(statusCode: 401, title: "SESSION_INVALID");
        if (conditional && !Request.Headers.ContainsKey("If-Match")) return Problem(statusCode: 428, title: "IF_MATCH_REQUIRED");
        try
        {
            var result = await action(ticket!, sid);
            if (result.Status == RoomTypeBatchStatus.Success)
            {
                if (result.ETag != null) Response.Headers.ETag = result.ETag;
                if (result.Value is PrivateHistoryFileDto file)
                {
                    Response.Headers.XContentTypeOptions = "nosniff";
                    return File(file.Content, file.ContentType, file.Name);
                }
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
