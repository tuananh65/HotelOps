using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Route("api/users"), Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ValidateAntiForgeryToken, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[UserAccountActivationErrors]
public sealed class UserDeactivationCandidateController(UserAccountDeactivationService service) : ControllerBase
{
    [HttpPatch("{userId:long}/deactivate"), RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Deactivate([Range(1, long.MaxValue)] long userId, [FromBody] JsonElement body, CancellationToken ct)
    {
        if (!Request.IsHttps) return Failure(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sessionId)) return Failure(401, "SESSION_INVALID");
        if (ticket!.IsRestricted || ticket.UserId == userId) return Failure(403, "DEACTIVATION_NOT_ALLOWED");
        if (!Request.Headers.ContainsKey("If-Match")) return Failure(428, "IF_MATCH_REQUIRED");
        var tag = Request.Headers.IfMatch.ToString();
        if (Request.Headers.IfMatch.Count != 1 || tag.Length != 18 || tag[0] != '"' || tag[^1] != '"'
            || !tag[1..^1].All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F')) return Failure(400, "INVALID_IF_MATCH");
        if (!UserAccountDeactivationRequest.TryParse(body, out var request)) return Failure(400, "VALIDATION_FAILED");
        var result = await service.DeactivateAsync(ticket, sessionId, userId, Convert.FromHexString(tag[1..^1]), request!, ct);
        if (result.Status == AccountDeactivationStatus.Success)
        {
            Response.Headers.ETag = '"' + Convert.ToHexString(result.Version!) + '"';
            return Ok(new UserDeactivationCandidateResponse(userId, "INACTIVE"));
        }
        return result.Status switch
        {
            AccountDeactivationStatus.SessionInvalid => Failure(401, "SESSION_INVALID"),
            AccountDeactivationStatus.Forbidden => Failure(403, "DEACTIVATION_NOT_ALLOWED"),
            AccountDeactivationStatus.NotFound => Failure(404, "USER_NOT_FOUND"),
            AccountDeactivationStatus.Stale => Failure(412, "STALE_USER_VERSION"),
            AccountDeactivationStatus.Conflict => Failure(409, "INVALID_STATUS_TRANSITION"),
            AccountDeactivationStatus.ResponsibilitiesBlocked => Failure(409, "OPEN_RESPONSIBILITIES"),
            AccountDeactivationStatus.ResponsibilitiesUnresolved => Failure(409, "RESPONSIBILITIES_REQUIRE_REVIEW"),
            AccountDeactivationStatus.LastAdministrator => Failure(409, "LAST_DURABLE_ADMIN"),
            _ => Failure(503, "DEACTIVATION_UNAVAILABLE")
        };
    }
    private ObjectResult Failure(int status, string code) => Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code });
}
public sealed record UserDeactivationCandidateResponse(long UserId, string AccountStatus);
