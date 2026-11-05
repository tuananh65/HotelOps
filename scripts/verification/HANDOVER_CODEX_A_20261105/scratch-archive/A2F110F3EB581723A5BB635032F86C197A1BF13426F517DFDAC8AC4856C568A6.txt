using HotelOps.Api.DTOs.Authentication;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(IAuthenticationLoginService authentication, IAuthenticationCredentialWorkflow credentials,
    IAntiforgery antiforgery, TimeProvider clock) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<SessionDto>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Failure(409, "AUTHENTICATED_SESSION_EXISTS");
        var result = await authentication.LoginAsync(request, cancellationToken);
        if (result.Status != LoginStatus.Succeeded)
            return result.Status switch
            {
                LoginStatus.Locked => Failure(423, "ACCOUNT_LOCKED", result.LockedUntil),
                LoginStatus.TemporaryExpired => Failure(403, "TEMPORARY_PASSWORD_EXPIRED"),
                _ => Failure(401, "INVALID_CREDENTIALS")
            };
        var created = result.CreatedSession ?? throw new InvalidOperationException("Committed login has no session.");
        var user = result.User ?? throw new InvalidOperationException("Committed login has no user.");
        var ticket = HotelOpsCookieTicket.Create(created, clock);
        await HttpContext.SignInAsync(ticket.AuthenticationScheme, ticket.Principal, ticket.Properties);
        HttpContext.User = ticket.Principal;
        return BuildSession(user, created.Session);
    }

    [HttpGet("session")]
    [Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
    public async Task<ActionResult<SessionDto>> Session(CancellationToken cancellationToken)
    {
        if (!HotelOpsCookieTicket.TryRead(User, out var reference, out var sessionId))
            return Failure(401, "SESSION_INVALID");
        var current = await authentication.GetSessionAsync(reference!, sessionId, cancellationToken);
        return current is null ? Failure(401, "SESSION_INVALID") : BuildSession(current.User, current.Session);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var hasReference = HotelOpsCookieTicket.TryRead(User, out var reference, out _);
        if (User.Identity?.IsAuthenticated == true && !hasReference) return Failure(401, "SESSION_INVALID");
        await credentials.LogoutAsync(reference, cancellationToken);
        // Never clear the cookie if revoke/commit failed or validity was indeterminate.
        await HttpContext.SignOutAsync(HotelOpsCookieTicket.Scheme);
        HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());
        HttpContext.Items.Remove(typeof(SessionSnapshot));
        Response.Headers.CacheControl = "no-store";
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
    public async Task<ActionResult<SessionDto>> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!HotelOpsCookieTicket.TryRead(User, out var reference, out var sessionId))
            return Failure(401, "SESSION_INVALID");
        var result = await credentials.ChangePasswordAsync(reference!, sessionId, request, cancellationToken);
        if (result.Status != ChangePasswordStatus.Succeeded)
            return result.Status switch
            {
                ChangePasswordStatus.ValidationFailed => Failure(400, "VALIDATION_FAILED"),
                ChangePasswordStatus.PolicyViolation => Failure(400, "PASSWORD_POLICY_VIOLATION"),
                ChangePasswordStatus.CurrentPasswordInvalid => Failure(400, "CURRENT_PASSWORD_INVALID"),
                ChangePasswordStatus.SessionInvalid => Failure(401, "SESSION_INVALID"),
                ChangePasswordStatus.StateChanged => Failure(409, "AUTH_STATE_CHANGED"),
                _ => throw new InvalidOperationException("Unsupported password change result.")
            };
        var created = result.CreatedSession ?? throw new InvalidOperationException("Committed password change has no session.");
        var user = result.User ?? throw new InvalidOperationException("Committed password change has no user.");
        if (created.Session.IsRestricted) throw new InvalidOperationException("Password change returned a restricted session.");
        var ticket = HotelOpsCookieTicket.Create(created, clock);
        await HttpContext.SignInAsync(ticket.AuthenticationScheme, ticket.Principal, ticket.Properties);
        HttpContext.User = ticket.Principal;
        HttpContext.Items[typeof(SessionSnapshot)] = created.Session;
        return BuildSession(user, created.Session);
    }

    private SessionDto BuildSession(AuthUserDto user, SessionSnapshot session)
        => new(user, session.ExpiresAtUtc, session.AbsoluteExpiresAtUtc, session.IsRestricted,
            HotelOpsAntiforgery.Issue(HttpContext, antiforgery).CsrfToken);

    private ObjectResult Failure(int status, string code, DateTime? lockedUntil = null)
    {
        Response.Headers.CacheControl = "no-store";
        var problem = new ProblemDetails { Status = status, Title = code, Type = "about:blank",
            Extensions = { ["code"] = code, ["traceId"] = HttpContext.TraceIdentifier } };
        if (lockedUntil.HasValue) problem.Extensions["parameters"] = new { lockedUntil };
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
