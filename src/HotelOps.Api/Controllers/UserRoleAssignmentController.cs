using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[UserAccountActivationErrors]
public sealed class UserRoleAssignmentController(UserRoleAssignmentService assignments) : ControllerBase
{
    [HttpPost("{userId}/roles")]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Assign([FromRoute, Range(1, long.MaxValue)] long userId, [FromBody] JsonElement body, CancellationToken ct)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS is required");
        if (!HotelOpsCookieTicket.TryRead(User, out var reference, out var sessionId)) return Failure(401, "SESSION_INVALID");
        if (!Request.Headers.TryGetValue("If-Match", out var header)) return Failure(428, "IF_MATCH_REQUIRED");
        var tag = header.ToString();
        if (header.Count != 1 || tag.Length != 18 || tag[0] != '"' || tag[^1] != '"'
            || !tag.AsSpan(1, 16).ToString().All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F')) return Failure(400, "INVALID_IF_MATCH");
        if (!AssignUserRoleRequest.TryParse(body, out var request)) return Failure(400, "VALIDATION_FAILED");
        var result = await assignments.AssignAsync(reference!, sessionId, userId, request!, Convert.FromHexString(tag[1..^1]), ct);
        switch (result.Status)
        {
            case AssignRoleStatus.Success:
                Response.Headers.ETag = "\"" + Convert.ToHexString(result.Version!) + "\"";
                return StatusCode(201, result.Assignment);
            case AssignRoleStatus.SessionInvalid: return Failure(401, "SESSION_INVALID");
            case AssignRoleStatus.Forbidden: return Failure(403, "ROLE_ASSIGNMENT_NOT_ALLOWED");
            case AssignRoleStatus.NotFound: return Failure(404, "USER_NOT_FOUND");
            case AssignRoleStatus.Inactive: return Failure(409, "TARGET_NOT_ACTIVE");
            case AssignRoleStatus.Stale: return Failure(412, "STALE_USER_VERSION");
            case AssignRoleStatus.ScopeConflict: return Failure(409, "PRIMARY_SCOPE_CONFLICT");
            case AssignRoleStatus.RoleConflict: return Failure(409, "EXISTING_ROLE_PATH_CONFLICT");
            case AssignRoleStatus.LastAdministrator: return Failure(409, "LAST_DURABLE_ADMIN");
            case AssignRoleStatus.SerializationUnavailable: return Failure(503, "GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE");
            default: throw new InvalidOperationException("Unknown role assignment outcome.");
        }
    }
    private ObjectResult Failure(int status, string code) => Problem(statusCode: status, title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
