using HotelOps.Api.DTOs.WorkTime;
using HotelOps.Api.Security;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ScheduledWorkdayTrial;

[ApiController, Route("api/branches/{branchId:long}/workdays")]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TrialScheduledController(WorkdayService service, TrialConfiguration trial) : ControllerBase
{
    [HttpPost]
    public Task<IActionResult> Start(long branchId, StartWorkdayRequest request, CancellationToken ct)
        => branchId != trial.BranchId || request.ShiftAssignmentId is null || request.Reason is not null
            || (request.ShiftAssignmentId != trial.AssignmentId && request.ShiftAssignmentId != trial.RaceAssignmentId)
            ? Task.FromResult<IActionResult>(Problem(statusCode: 400, title: "TRIAL_SCHEDULED_ONLY"))
            : Execute(() => service.StartAsync(branchId, request, ct));

    [HttpPost("{workdayId:long}/end")]
    public Task<IActionResult> End(long branchId, long workdayId, EndWorkdayRequest request, CancellationToken ct)
    {
        var header = Request.Headers.IfMatch;
        if (branchId != trial.BranchId || header.Count != 1 || header[0] is not { Length: 14 } text || text[0] != '"' || text[^1] != '"' || request.PauseAll != false)
            return Task.FromResult<IActionResult>(Problem(statusCode: 400, title: "INVALID_IF_MATCH_OR_TRIAL_PAUSE"));
        byte[] version;
        try { version = Convert.FromBase64String(text[1..^1]); }
        catch (FormatException) { return Task.FromResult<IActionResult>(Problem(statusCode: 400, title: "INVALID_IF_MATCH")); }
        if (version.Length != 8 || Convert.ToBase64String(version) != text[1..^1])
            return Task.FromResult<IActionResult>(Problem(statusCode: 400, title: "INVALID_IF_MATCH"));
        return Execute(() => service.EndAsync(branchId, workdayId, version, false, ct));
    }
    async Task<IActionResult> Execute(Func<Task<WorkdayResult>> action)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        var result = await action();
        if (result.Status >= 400) return Problem(statusCode: result.Status, title: result.Code);
        if (result.Value is WorkdayResponse d) Response.Headers.ETag = '"' + d.RowVersion + '"';
        if (result.Status == 201) Response.Headers.Location = $"/api/branches/{RouteData.Values["branchId"]}/workdays/current";
        return StatusCode(result.Status, result.Value);
    }
}
