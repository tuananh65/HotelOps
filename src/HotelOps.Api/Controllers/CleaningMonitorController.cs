using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/branches/{branchId:long}/cleaning-monitor")]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CleaningMonitorController(CleaningMonitorService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(long branchId, [FromQuery] CleaningMonitorQuery query, CancellationToken ct)
        => Execute((ticket, sessionId) => service.ListAsync(ticket, sessionId, branchId, query, ct));

    [HttpGet("{kind}/{id:long}")]
    public Task<IActionResult> Detail(long branchId, string kind, long id,
        [FromQuery] CleaningMonitorTimelineQuery query, CancellationToken ct)
        => Execute((ticket, sessionId) => service.DetailAsync(ticket, sessionId, branchId, kind, id, query, ct));

    private async Task<IActionResult> Execute(Func<Services.Authentication.SessionReference, long, Task<CleaningMonitorResult>> operation)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sessionId))
            return Problem(statusCode: 401, title: "SESSION_INVALID");
        try
        {
            var result = await operation(ticket!, sessionId);
            if (result.Status == CleaningMonitorStatus.Success) return Ok(result.Value);
            return Problem(statusCode: result.Status switch
            {
                CleaningMonitorStatus.SessionInvalid => 401, CleaningMonitorStatus.Forbidden => 403,
                CleaningMonitorStatus.NotFound => 404, _ => 503
            }, title: result.Status.ToString().ToUpperInvariant());
        }
        catch (ValidationException error) { return Problem(statusCode: 400, title: "INVALID_FILTER", detail: error.Message); }
    }
}
