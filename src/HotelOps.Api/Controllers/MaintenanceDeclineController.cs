using HotelOps.Api.Security;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize, InternalRequestReadErrors]
[Route("api/maintenance-works/{id:long}/decline")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MaintenanceDeclineController(MaintenanceDeclineService service) : ControllerBase
{
    [HttpPost]
    public async Task<MaintenanceDeclineSaved> Decline(long id, MaintenanceDeclineInput input, CancellationToken ct)
    {
        if (!Request.IsHttps) throw new InternalRequestReadException(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) throw new InternalRequestReadException(401, "SESSION_INVALID");
        var tags = Request.Headers.IfMatch;
        if (tags.Count != 1 || tags[0] is not { Length: 18 } tag || tag[0] != '"' || tag[^1] != '"'
            || tag[1..^1].Any(c => !char.IsAsciiHexDigitUpper(c)))
            throw new InternalRequestReadException(428, "WORK_VERSION_REQUIRED");
        return await service.DeclineAsync(ticket!, sid, id, tag, input, ct);
    }
}
