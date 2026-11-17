using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Route("api/users")]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UserDeactivationPreviewController(UserDeactivationPreviewService service) : ControllerBase
{
    [HttpGet("{userId:long}/deactivation-preview")]
    public async Task<IActionResult> Read([Range(1, long.MaxValue)] long userId, CancellationToken ct)
    {
        if (!Request.IsHttps) return Failure(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var reference, out var session)) return Failure(401, "SESSION_INVALID");
        var result = await service.ReadAsync(reference!, session, userId, ct);
        if (result.Status == AccountDeactivationStatus.Success)
        {
            Response.Headers.ETag = '"' + result.Data!.Version + '"';
            return Ok(result.Data);
        }
        return result.Status switch
        {
            AccountDeactivationStatus.SessionInvalid => Failure(401, "SESSION_INVALID"),
            AccountDeactivationStatus.Forbidden => Failure(403, "DEACTIVATION_NOT_ALLOWED"),
            AccountDeactivationStatus.NotFound => Failure(404, "USER_NOT_FOUND"),
            _ => Failure(503, "DEACTIVATION_REVIEW_UNAVAILABLE")
        };
    }

    private ObjectResult Failure(int status, string code) => Problem(statusCode: status, title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
