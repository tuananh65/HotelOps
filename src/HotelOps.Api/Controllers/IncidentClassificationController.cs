using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize, InternalRequestReadErrors]
[Route("api/incidents/{id:long}/classification")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(16384)]
public sealed class IncidentClassificationController(IncidentClassificationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IncidentClassificationContext> Context(long id, CancellationToken ct)
    {
        if (!Request.IsHttps) throw new InternalRequestReadException(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid) || ticket == null)
            throw new InternalRequestReadException(401, "SESSION_INVALID");
        return await service.ContextAsync(ticket, sid, id, ct);
    }

    [HttpPost]
    public async Task<ActionResult<IncidentClassificationSaved>> Save(long id, IncidentClassificationInput input, CancellationToken ct)
    {
        if (!Request.IsHttps) throw new InternalRequestReadException(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid) || ticket == null)
            throw new InternalRequestReadException(401, "SESSION_INVALID");
        var tags = Request.Headers.IfMatch;
        if (tags.Count != 1 || tags[0] is not { Length: 18 } tag || tag[0] != '"' || tag[^1] != '"'
            || tag[1..^1].Any(c => !char.IsAsciiHexDigitUpper(c))) throw new InternalRequestReadException(428, "INCIDENT_VERSION_REQUIRED");
        try
        {
            return await service.SaveAsync(ticket, sid, id, tag, input, ct);
        }
        catch (ValidationException error) { return Problem(statusCode: 400, title: "Invalid incident classification", detail: error.Message); }
    }
}
