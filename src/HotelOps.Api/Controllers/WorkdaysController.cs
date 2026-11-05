using HotelOps.Api.DTOs.WorkTime;
using HotelOps.Api.Security;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/branches/{branchId:long}/workdays")]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WorkdaysController(WorkdayService service) : ControllerBase
{
    [HttpGet("current")]
    public Task<IActionResult> Current(long branchId, CancellationToken ct)
        => Execute(() => service.CurrentAsync(branchId, ct));

    [HttpGet("own-shifts")]
    public Task<IActionResult> OwnShifts(long branchId, CancellationToken ct)
        => Execute(() => service.OwnShiftsAsync(branchId, ct));

    [HttpPost]
    public Task<IActionResult> Start(long branchId, StartWorkdayRequest request, CancellationToken ct)
        => Execute(() => service.StartAsync(branchId, request, ct));

    [HttpPost("{workdayId:long}/end")]
    public Task<IActionResult> End(long branchId, long workdayId, EndWorkdayRequest request, CancellationToken ct)
    {
        var header = Request.Headers.IfMatch;
        if (header.Count != 1 || header[0] is not { Length: 14 } text || text[0] != '"' || text[^1] != '"')
            return Task.FromResult<IActionResult>(Problem(statusCode: 400, title: "INVALID_IF_MATCH"));
        byte[] version;
        try { version = Convert.FromBase64String(text[1..^1]); }
        catch (FormatException) { return Task.FromResult<IActionResult>(Problem(statusCode: 400, title: "INVALID_IF_MATCH")); }
        if (version.Length != 8 || Convert.ToBase64String(version) != text[1..^1] || request.PauseAll is null)
            return Task.FromResult<IActionResult>(Problem(statusCode: 400, title: "INVALID_IF_MATCH"));
        return Execute(() => service.EndAsync(branchId, workdayId, version, request.PauseAll.Value, ct));
    }

    private async Task<IActionResult> Execute(Func<Task<WorkdayResult>> action)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        var result = await action();
        if (result.Status >= 400) return Problem(statusCode: result.Status, title: result.Code);
        var version = result.Value switch
        {
            WorkdayResponse d => d.RowVersion,
            OwnWorkingDaySnapshot d => d.RowVersion,
            _ => null
        };
        if (version is not null) Response.Headers.ETag = '"' + version + '"';
        if (result.Status == 201) Response.Headers.Location = Url.Action(nameof(Current), new { branchId = RouteData.Values["branchId"] });
        return result.Status == 204 ? NoContent() : StatusCode(result.Status, result.Value);
    }
}
