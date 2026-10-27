using System.Text.Json;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/auth/reauthenticate")]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminReauthenticationController(AdminReauthenticationService authentication) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Reauthenticate([FromBody] JsonElement body, CancellationToken ct)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS is required");
        if (!HotelOpsCookieTicket.TryRead(User, out var reference, out var sessionId))
            return Failure(401, "SESSION_INVALID");
        if (body.ValueKind != JsonValueKind.Object) return Failure(400, "VALIDATION_FAILED");
        var fields = body.EnumerateObject().ToArray();
        if (fields.Length != 1 || fields[0].Name != "currentPassword" || fields[0].Value.ValueKind != JsonValueKind.String)
            return Failure(400, "VALIDATION_FAILED");
        var password = fields[0].Value.GetString();
        if (string.IsNullOrWhiteSpace(password) || password.Length > 256) return Failure(400, "VALIDATION_FAILED");
        var result = await authentication.ReauthenticateAsync(reference!, sessionId, password, ct);
        return result.Status switch
        {
            ReauthenticationStatus.Succeeded => Ok(new { expiresAtUtc = result.ExpiresAtUtc }),
            ReauthenticationStatus.Locked => Failure(423, "ACCOUNT_LOCKED"),
            ReauthenticationStatus.Restricted => Failure(403, "PASSWORD_CHANGE_REQUIRED"),
            ReauthenticationStatus.SessionInvalid => Failure(401, "SESSION_INVALID"),
            _ => Failure(401, "INVALID_CREDENTIALS")
        };
    }

    private ObjectResult Failure(int status, string code) => Problem(statusCode: status, title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
