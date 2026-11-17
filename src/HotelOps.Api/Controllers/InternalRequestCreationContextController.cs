using HotelOps.Api.Security;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize, InternalRequestReadErrors]
[Route("api/internal-requests/{id:long}/creation-context")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InternalRequestCreationContextController(InternalRequestCreationContext service) : ControllerBase
{
    [HttpGet]
    public async Task<InternalRequestCreationContextView> Read(long id, CancellationToken ct)
    {
        if (!Request.IsHttps) throw new InternalRequestReadException(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) throw new InternalRequestReadException(401, "SESSION_INVALID");
        var result = await service.ReadAsync(ticket!, sid, id, ct);
        Response.Headers.ETag = '"' + result.Version + '"';
        return result;
    }
}
