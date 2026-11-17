using HotelOps.Api.DTOs.WorkTime;
using HotelOps.Api.Security;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/branches/{branchId:long}/time-corrections")]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TimeCorrectionsController(WorkdayService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(long branchId, long? departmentId, CancellationToken ct, int page = 1)
        => Execute(() => service.ListCorrectionsAsync(branchId, departmentId, page, ct));
    [HttpGet("{id:long}")]
    public Task<IActionResult> Read(long branchId, long id, CancellationToken ct)
        => Execute(() => service.ReadCorrectionAsync(branchId, id, ct));
    [HttpPost("intervals/{intervalId:long}")]
    public Task<IActionResult> RequestCorrection(long branchId, long intervalId, RequestTimeCorrection request, CancellationToken ct)
        => Mutate(version => service.RequestCorrectionAsync(branchId, intervalId, version, request, ct));
    [HttpPost("{id:long}/decision")]
    public Task<IActionResult> Decide(long branchId, long id, DecideTimeCorrection request, CancellationToken ct)
        => Mutate(version => service.DecideCorrectionAsync(branchId, id, version, request, ct));

    private Task<IActionResult> Mutate(Func<byte[], Task<WorkdayResult>> action)
    {
        var value = Request.Headers.IfMatch;
        if (value.Count != 1 || value[0] is not { Length: 14 } text || text[0] != '"' || text[^1] != '"')
            return Task.FromResult<IActionResult>(Problem(statusCode: 400, title: "INVALID_IF_MATCH"));
        try
        {
            var version = Convert.FromBase64String(text[1..^1]);
            if (version.Length == 8 && Convert.ToBase64String(version) == text[1..^1]) return Execute(() => action(version));
        }
        catch (FormatException) { }
        return Task.FromResult<IActionResult>(Problem(statusCode: 400, title: "INVALID_IF_MATCH"));
    }
    private async Task<IActionResult> Execute(Func<Task<WorkdayResult>> action)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        var result = await action();
        if (result.Status >= 400) return Problem(statusCode: result.Status, title: result.Code);
        if (result.Value is TimeCorrectionDetail detail) Response.Headers.ETag = '"' + detail.Correction.Version + '"';
        return StatusCode(result.Status, result.Value);
    }
}
