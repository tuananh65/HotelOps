using HotelOps.Api.Security;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.MaterialRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme), InternalRequestReadErrors]
[Route("api/material-requests/creation-branches")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MaterialCreationAccessController(MaterialCreationAccess service) : ControllerBase
{
    [HttpGet]
    public Task<MaterialCreationBranch[]> Read(CancellationToken ct)
    {
        if (!Request.IsHttps) throw new InternalRequestReadException(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) throw new InternalRequestReadException(401, "SESSION_INVALID");
        return service.ReadAsync(ticket!, sid, ct);
    }
}
