using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize, InsightErrors]
[Route("api/reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReportsController(ReportService service) : ControllerBase
{
    private (SessionReference Ticket, long Sid) Session()
    {
        if (!Request.IsHttps) throw new InsightException(400, "HTTPS_REQUIRED", "HTTPS required.");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid))
            throw new InsightException(401, "SESSION_INVALID", "Session unavailable.");
        return (ticket!, sid);
    }

    [HttpGet("options")]
    public Task<InsightOptions> Options(CancellationToken ct)
    {
        var session = Session();
        return service.OptionsAsync(session.Ticket, session.Sid, ct);
    }

    [HttpPost("{key}/preview")]
    public Task<ReportResult> Preview(string key, ReportQuery query, CancellationToken ct)
    {
        var session = Session();
        return service.PreviewAsync(session.Ticket, session.Sid, key, query, ct);
    }

    [HttpPost("snapshots/{id}/export")]
    public async Task<IActionResult> Export(string id, ExportRequest request, CancellationToken ct)
    {
        var session = Session();
        var file = await service.ExportAsync(session.Ticket, session.Sid, id, request, ct);
        return File(file.Content, file.ContentType, file.Name);
    }
}
