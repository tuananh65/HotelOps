using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;

[NonController] // Source candidate; no MVC/DI/route activation before independent release acceptance.
[ApiController, Route("api/users"), Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ValidateAntiForgeryToken, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UserRoleRevocationCandidateController(UserRoleRevocationService service) : ControllerBase
{
    [HttpDelete("{userId:long}/roles/{userRoleId:long}")]
    public async Task<IActionResult> Revoke([Range(1, long.MaxValue)] long userId, [Range(1, long.MaxValue)] long userRoleId, CancellationToken ct)
    {
        if (!Request.IsHttps) return Failure(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var session)) return Failure(401, "SESSION_INVALID");
        if (!Request.Headers.ContainsKey("If-Match")) return Failure(428, "IF_MATCH_REQUIRED");
        if (Request.Headers.IfMatch.Count != 1 || !RevokeRoleVersion.TryParse(Request.Headers.IfMatch.ToString(), out var version)) return Failure(400, "INVALID_IF_MATCH");
        // Strictly bodyless contract, including chunked bodies. Do not deserialize arbitrary input.
        if (Request.ContentLength is > 0 || Request.Headers.ContainsKey("Transfer-Encoding")) return Failure(400, "BODY_NOT_ALLOWED");
        var result = await service.RevokeAsync(ticket!, session, userId, userRoleId, version, ct);
        if (result.Status == RevokeRoleStatus.Success) { Response.Headers.ETag = '"' + Convert.ToHexString(result.Version!) + '"'; return NoContent(); }
        return result.Status switch
        {
            RevokeRoleStatus.SessionInvalid => Failure(401, "SESSION_INVALID"),
            RevokeRoleStatus.Forbidden => Failure(403, "ROLE_REVOCATION_NOT_ALLOWED"),
            RevokeRoleStatus.NotFound => Failure(404, "ROLE_ASSIGNMENT_NOT_FOUND"),
            RevokeRoleStatus.Stale => Failure(412, "STALE_USER_VERSION"),
            RevokeRoleStatus.Conflict => Failure(409, "ROLE_ASSIGNMENT_CONFLICT"),
            RevokeRoleStatus.LastAdministrator => Failure(409, "LAST_DURABLE_ADMIN"),
            RevokeRoleStatus.ResponsibilitiesBlocked => Failure(409, "OPEN_RESPONSIBILITIES"),
            RevokeRoleStatus.ResponsibilitiesUnresolved => Failure(503, "RESPONSIBILITY_COVERAGE_REQUIRED"),
            _ => Failure(503, "ROLE_REVOCATION_RELEASE_REQUIRED")
        };
    }
    private ObjectResult Failure(int status, string code) => Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code });
}
