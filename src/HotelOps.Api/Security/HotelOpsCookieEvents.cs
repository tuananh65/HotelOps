using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HotelOps.Api.Security;

public sealed class HotelOpsCookieEvents(ICookieSessionValidator validator) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        context.ShouldRenew = false;
        if (!HotelOpsCookieTicket.TryRead(context.Principal, out var reference, out var sessionId)
            || context.Properties.IsPersistent || context.Properties.ExpiresUtc is null)
        {
            context.RejectPrincipal(); return;
        }

        // Per-request SQL validation only. Middleware touches AFTER security checks.
        // Never manufacture a replacement session from an invalid cookie.
        var result = await validator.ValidateAsync(reference!, context.HttpContext.RequestAborted);
        var s = result.Session;
        if (!result.IsValid || s is null || s.SessionId != sessionId || s.UserId != reference!.UserId
            || s.CreatedAtUtc != reference.CreatedAtUtc || s.IsRestricted != reference.IsRestricted
            || context.Properties.ExpiresUtc.Value.UtcDateTime > s.AbsoluteExpiresAtUtc)
        {
            context.RejectPrincipal(); return;
        }
        // Retain only server-understood identity fields, never role claims from a ticket.
        context.ReplacePrincipal(HotelOpsCookieTicket.BuildPrincipal(reference, sessionId));
        context.HttpContext.Items[typeof(SessionSnapshot)] = s; // Request-local only.
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
        => AuthenticationProblem.WriteAsync(context.HttpContext, 401, "AUTHENTICATION_REQUIRED");

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
        => AuthenticationProblem.WriteAsync(context.HttpContext, 403, "FORBIDDEN");
}
