using HotelOps.Api.Security;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.MaterialRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme), InternalRequestReadErrors]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/material-requests/{id:long}/linked-tasks")]
public sealed class MaterialTaskLinksController(MaterialTaskLinks service) : ControllerBase
{
    [HttpGet]
    public async Task<MaterialTaskLinksView> Read(long id, bool candidates = false, int page = 1, CancellationToken ct = default)
    {
        if (!Request.IsHttps) throw new InternalRequestReadException(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) throw new InternalRequestReadException(401, "SESSION_INVALID");
        var result = await service.ReadAsync(ticket!, sid, id, candidates, page, ct);
        Response.Headers.ETag = '"' + result.Version + '"';
        return result;
    }
    [HttpPost]
    public async Task<IActionResult> Link(long id, MaterialTaskLinkInput input, CancellationToken ct)
    {
        if (!Request.IsHttps) throw new InternalRequestReadException(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) throw new InternalRequestReadException(401, "SESSION_INVALID");
        var version = await service.LinkAsync(ticket!, sid, id, input, ct);
        Response.Headers.ETag = '"' + version + '"';
        return Ok(new { requestId = id, taskId = input.TaskId, version });
    }
}
