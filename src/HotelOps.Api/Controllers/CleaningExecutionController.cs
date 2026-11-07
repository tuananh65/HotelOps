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
[RequestSizeLimit(72 * 1024 * 1024)]
public sealed class CleaningExecutionController(RoomTypeCreationService service, IWebHostEnvironment? environment = null, IConfiguration? configuration = null) : ControllerBase
{
    private string? EvidenceRoot => configuration?["Evidence:PrivateRoot"] ?? (environment == null ? null : Path.Combine(environment.ContentRootPath, "App_Data", "evidence"));
    [HttpGet("api/branches/{branchId:long}/reworks/{id:long}/execution")]
    public Task<IActionResult> ReadRework(long branchId, long id, CancellationToken ct)
        => Rework(branchId, id, "read", null, ct);
    [HttpGet("api/branches/{branchId:long}/reworks/{id:long}/evidence/{attachmentId:long}")]
    public Task<IActionResult> ReworkEvidence(long branchId, long id, long attachmentId, CancellationToken ct)
        => Run(false, (ticket, sid) => service.ExecuteReworkAsync(ticket, sid, branchId, id, "read", null, null, null, EvidenceRoot, ct, attachmentId));
    [HttpPost("api/branches/{branchId:long}/reworks/{id:long}/accept")]
    public Task<IActionResult> AcceptRework(long branchId, long id, ReworkActionRequest request, CancellationToken ct)
        => Rework(branchId, id, "accept", request, ct);
    [HttpPost("api/branches/{branchId:long}/reworks/{id:long}/start")]
    public Task<IActionResult> StartRework(long branchId, long id, ReworkActionRequest request, CancellationToken ct)
        => Rework(branchId, id, "start", request, ct);
    [HttpPost("api/branches/{branchId:long}/reworks/{id:long}/complete")]
    public Task<IActionResult> CompleteRework(long branchId, long id, ReworkActionRequest request, CancellationToken ct)
        => Rework(branchId, id, "complete", request, ct);
    private Task<IActionResult> Rework(long branch, long id, string action, ReworkActionRequest? input, CancellationToken ct)
        => Run(action != "read", (ticket, sid) => service.ExecuteReworkAsync(ticket, sid, branch, id, action,
            Request.Headers.IfMatch.ToString(), Request.Headers["Idempotency-Key"].ToString(), input, EvidenceRoot, ct));
    [HttpGet("api/branches/{branchId:long}/cleaning-tasks/{id:long}/execution")]
    public Task<IActionResult> Read(long branchId, long id, CancellationToken ct)
        => Run(false, (ticket, sid) => service.ReadCleaningExecutionAsync(ticket, sid, branchId, id, ct));
    [HttpPost("api/branches/{branchId:long}/cleaning-tasks/{id:long}/start")]
    public Task<IActionResult> Start(long branchId, long id, StartCleaningRequest request, CancellationToken ct)
        => Run(true, (ticket, sid) => service.StartCleaningAsync(ticket, sid, branchId, id, Request.Headers.IfMatch.ToString(), request, ct));
    [HttpPost("api/branches/{branchId:long}/cleaning-tasks/{id:long}/reassign")]
    public Task<IActionResult> Reassign(long branchId, long id, ReassignCleaningRequest request, CancellationToken ct)
        => Run(true, (ticket, sid) => service.ReassignCleaningAsync(ticket, sid, branchId, id, Request.Headers.IfMatch.ToString(), Request.Headers["Idempotency-Key"].ToString(), request, ct));
    [HttpPost("api/branches/{branchId:long}/cleaning-tasks/{id:long}/blockers")]
    public Task<IActionResult> ReportBlocker(long branchId, long id, ReportCleaningBlockerRequest request, CancellationToken ct)
        => Run(true, (ticket, sid) => service.ReportCleaningBlockerAsync(ticket, sid, branchId, id,
            Request.Headers.IfMatch.ToString(), Request.Headers["Idempotency-Key"].ToString(), request, ct, EvidenceRoot));
    [HttpPost("api/branches/{branchId:long}/cleaning-tasks/{id:long}/complete")]
    public Task<IActionResult> Complete(long branchId, long id, CompleteCleaningRequest request, CancellationToken ct)
        => Run(true, (ticket, sid) => service.CompleteCleaningAsync(ticket, sid, branchId, id,
            Request.Headers.IfMatch.ToString(), Request.Headers["Idempotency-Key"].ToString(), request, ct, EvidenceRoot));
    private async Task<IActionResult> Run(bool conditional, Func<SessionReference, long, Task<RoomTypeBatchResult>> action)
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
                if (result.Value is HotelOps.Api.DTOs.HotelStructure.PrivateHistoryFileDto file)
                { Response.Headers["X-Content-Type-Options"] = "nosniff"; return File(file.Content, file.ContentType, file.Name); }
                return Ok(result.Value);
            }
            return Problem(statusCode: result.Status switch { RoomTypeBatchStatus.SessionInvalid => 401, RoomTypeBatchStatus.Forbidden => 403,
                RoomTypeBatchStatus.NotFound => 404, RoomTypeBatchStatus.InvalidReference => 400, RoomTypeBatchStatus.Stale => 412,
                RoomTypeBatchStatus.Inactive or RoomTypeBatchStatus.Duplicate => 409, _ => 503 },
                title: result.Value is CleaningLifecycleFailure failure ? failure.Code : result.Status.ToString().ToUpperInvariant());
        }
        catch (ValidationException error) { return Problem(statusCode: 400, title: "VALIDATION_FAILED", detail: error.Message); }
    }
}
