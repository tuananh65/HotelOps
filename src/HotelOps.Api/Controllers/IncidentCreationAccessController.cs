using HotelOps.Api.Security;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Incidents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme), InternalRequestReadErrors]
[Route("api/incidents/creation-branches")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IncidentCreationAccessController(IncidentCreationAccess service) : ControllerBase
{
    [HttpGet("{branchId:long}/locations")]
    public Task<IncidentLocationChoices> Locations(long branchId, CancellationToken ct)
    {
        if (!Request.IsHttps) throw new InternalRequestReadException(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) throw new InternalRequestReadException(401, "SESSION_INVALID");
        return service.LocationsAsync(ticket!, sid, branchId, ct);
    }
    [HttpGet]
    public Task<IncidentCreationBranch[]> Read(CancellationToken ct)
    {
        if (!Request.IsHttps) throw new InternalRequestReadException(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) throw new InternalRequestReadException(401, "SESSION_INVALID");
        return service.ReadAsync(ticket!, sid, ct);
    }
}
