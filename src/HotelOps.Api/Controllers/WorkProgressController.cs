using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.DTOs.OperationalTasks;
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
    [HttpGet("api/branches/{branch:long}/operational-tasks/{id:long}/pause-reasons")]
    public Task<IActionResult> PauseReasons(long branch, long id, CancellationToken ct)
        => Execute((ticket, sid) => service.ReadTaskPauseReasonsAsync(ticket, sid, branch, id, ct), false);
    [HttpPost("api/branches/{branch:long}/operational-tasks/{id:long}/pause")]
    public Task<IActionResult> PauseTask(long branch, long id, PauseOperationalTaskRequest input, CancellationToken ct)
        => Execute((ticket, sid) => service.PauseOperationalTaskAsync(ticket, sid, branch, id, Request.Headers.IfMatch.ToString(), input, ct), true);
    [HttpPost("api/branches/{branch:long}/operational-tasks/{id:long}/resume")]
    public Task<IActionResult> ResumeTask(long branch, long id, CancellationToken ct)
        => Execute((ticket, sid) => service.ResumeOperationalTaskAsync(ticket, sid, branch, id, Request.Headers.IfMatch.ToString(), ct), true);
    [HttpPost("api/branches/{branch:long}/operational-tasks/{id:long}/accept")]
    public Task<IActionResult> AcceptTask(long branch, long id, CancellationToken ct)
        => Execute((ticket, sid) => service.EnterOperationalTaskAsync(ticket, sid, branch, id, Request.Headers.IfMatch.ToString(), false, ct), true);
    [HttpPost("api/branches/{branch:long}/operational-tasks/{id:long}/start")]
    public Task<IActionResult> StartTask(long branch, long id, CancellationToken ct)
        => Execute((ticket, sid) => service.EnterOperationalTaskAsync(ticket, sid, branch, id, Request.Headers.IfMatch.ToString(), true, ct), true);
    [HttpPost("api/branches/{branch:long}/operational-tasks/{id:long}/decline")]
    public Task<IActionResult> DeclineTask(long branch, long id, DeclineOperationalTaskRequest input, CancellationToken ct)
        => Execute((ticket, sid) => service.DeclineOperationalTaskAsync(ticket, sid, branch, id, Request.Headers.IfMatch.ToString(), input, ct), true);
    [HttpGet("api/branches/{branch:long}/cleaning-tasks/{id:long}/progress")]
    public Task<IActionResult> ReadCleaning(long branch, long id, CancellationToken ct) => Progress(branch, id, true, null, ct);
    [HttpPost("api/branches/{branch:long}/operational-tasks/{id:long}/submit")]
    public Task<IActionResult> SubmitTask(long branch,long id,SubmitOperationalTaskRequest input,CancellationToken ct)
        =>Execute((ticket,sid)=>service.SubmitOperationalTaskAsync(ticket,sid,branch,id,Request.Headers.IfMatch.ToString(),input,Root,ct),true);
    [HttpGet("api/branches/{branch:long}/operational-tasks/{id:long}/verification")]
    public Task<IActionResult> ReadVerification(long branch,long id,CancellationToken ct)
        =>Execute((ticket,sid)=>service.ReadTaskVerificationAsync(ticket,sid,branch,id,ct),false);
    [HttpPost("api/branches/{branch:long}/operational-tasks/{id:long}/verification")]
    public Task<IActionResult> VerifyTask(long branch,long id,VerifyOperationalTaskRequest input,CancellationToken ct)
        =>Execute((ticket,sid)=>service.VerifyOperationalTaskAsync(ticket,sid,branch,id,Request.Headers.IfMatch.ToString(),input,ct),true);
    [HttpGet("api/branches/{branch:long}/operational-tasks/{id:long}/submission")]
    public Task<IActionResult> TaskSubmission(long branch,long id,CancellationToken ct)
        =>Execute((ticket,sid)=>service.ReadTaskSubmissionAsync(ticket,sid,branch,id,Root,ct),false);
    [HttpGet("api/branches/{branch:long}/operational-tasks/{id:long}/submission/evidence/{attachment:long}")]
    public Task<IActionResult> TaskSubmissionEvidence(long branch,long id,long attachment,CancellationToken ct)
        =>Execute((ticket,sid)=>service.ReadTaskSubmissionAsync(ticket,sid,branch,id,Root,ct,attachment),false);
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
            }, title: result.Status.ToString().ToUpperInvariant(), extensions:
                result.Value is RoomTypeCreationService.TaskEntryFailure { MessageCode: { } code }
                    ? new Dictionary<string, object?> { ["messageCode"] = code }
                    : result.Value is TaskSubmissionFailure failure ? new Dictionary<string,object?> { ["messageCode"]=failure.MessageCode,["missingCriteria"]=failure.MissingCriteria } : null);
        }
        catch (ValidationException error) { return Problem(statusCode: 400, title: "VALIDATION_FAILED", detail: error.Message); }
    }
}
