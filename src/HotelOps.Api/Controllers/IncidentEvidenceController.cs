using HotelOps.Api.Security;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize, InternalRequestReadErrors]
[Route("api/incidents/{id:long}/evidence")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(72 * 1024 * 1024)]
public sealed class IncidentEvidenceController(IncidentEvidenceWrite service, IWebHostEnvironment environment, IConfiguration configuration) : ControllerBase
{
    [HttpPost]
    public async Task<IncidentEvidenceSaved> Add(long id, IncidentEvidenceInput input, CancellationToken ct)
    {
        if (!Request.IsHttps) throw new InternalRequestReadException(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) throw new InternalRequestReadException(401, "SESSION_INVALID");
        var tags = Request.Headers.IfMatch;
        if (tags.Count != 1 || tags[0] is not { Length: 46 } tag || tag[0] != '"' || tag[^1] != '"')
            throw new InternalRequestReadException(428, "INCIDENT_VERSION_REQUIRED");
        var version = tag[1..^1];
        try { if (Convert.FromBase64String(version).Length != 32 || Convert.ToBase64String(Convert.FromBase64String(version)) != version) throw new FormatException(); }
        catch (FormatException) { throw new InternalRequestReadException(400, "INCIDENT_VERSION_INVALID"); }
        var result = await service.AddAsync(ticket!, sid, id, version, input,
            configuration["Evidence:PrivateRoot"] ?? Path.Combine(environment.ContentRootPath, "App_Data", "evidence"), ct);
        Response.Headers.ETag = '"' + result.Version + '"';
        return result;
    }
}
