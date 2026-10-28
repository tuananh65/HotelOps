using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminCredentialResetController(AdminCredentialResetWorkflow resets) : ControllerBase
{
    [HttpPost("{userId}/reset-credential")]
    public async Task<IActionResult> Reset([FromRoute, Range(1, long.MaxValue)] long userId,
        [FromBody] JsonElement body, CancellationToken ct)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS is required");
        if (!HotelOpsCookieTicket.TryRead(User, out var reference, out var sessionId)) return Failure(401, "SESSION_INVALID");
        if (!Request.Headers.TryGetValue("If-Match", out var header)) return Failure(428, "IF_MATCH_REQUIRED");
        var tag = header.ToString();
        if (header.Count != 1 || tag.Length != 18 || tag[0] != '"' || tag[^1] != '"'
            || !tag.AsSpan(1, 16).ToString().All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F'))
            return Failure(400, "INVALID_IF_MATCH");
        if (body.ValueKind != JsonValueKind.Object) return Failure(400, "VALIDATION_FAILED");
        var fields = body.EnumerateObject().ToArray();
        if (fields.Length != 1 || fields[0].Name != "identityVerificationConfirmed" || fields[0].Value.ValueKind != JsonValueKind.True)
            return Failure(400, "IDENTITY_CONFIRMATION_REQUIRED");
        using var result = await resets.ResetAsync(reference!, sessionId, userId, Convert.FromHexString(tag[1..^1]), ct);
        switch (result.Status)
        {
            case ConditionalCredentialResetStatus.Success:
                Response.Headers.ETag = "\"" + Convert.ToHexString(result.Version!) + "\"";
                return Ok(new { temporaryCredential = new InitialCredentialHandoffDto(result.Credential!.TakePasswordOnce(), result.Credential.ExpiresAtUtc) });
            case ConditionalCredentialResetStatus.SessionInvalid: return Failure(401, "SESSION_INVALID");
            case ConditionalCredentialResetStatus.ReauthenticationRequired: return Failure(403, "REAUTHENTICATION_REQUIRED");
            case ConditionalCredentialResetStatus.SelfDenied:
            case ConditionalCredentialResetStatus.Forbidden: return Failure(403, "RESET_NOT_ALLOWED");
            case ConditionalCredentialResetStatus.NotFound: return Failure(404, "USER_NOT_FOUND");
            case ConditionalCredentialResetStatus.Inactive: return Failure(409, "TARGET_NOT_ACTIVE");
            case ConditionalCredentialResetStatus.Stale: return Failure(412, "STALE_USER_VERSION");
            case ConditionalCredentialResetStatus.LastAdministrator: return Failure(409, "LAST_DURABLE_ADMIN");
            case ConditionalCredentialResetStatus.SerializationUnavailable: return Failure(503, "GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE");
            default: throw new InvalidOperationException("Unknown credential reset result.");
        }
    }

    private ObjectResult Failure(int status, string code) => Problem(statusCode: status, title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
