using HotelOps.Api.Security;

namespace HotelOps.Api.Middleware;

/// <summary>Stops invalid cookies even on existing anonymous endpoints; no RBAC policy.</summary>
public sealed class InvalidCookieMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
        => context.Request.Cookies.ContainsKey(HotelOpsCookieTicket.CookieName)
            && context.User.Identity?.IsAuthenticated != true
            && !CookieRequestRules.IsCsrfBootstrap(context.Request)
            && !CookieRequestRules.IsLogin(context.Request) // Still anonymous: CSRF validation remains mandatory.
            && !CookieRequestRules.IsLogout(context.Request)
            ? AuthenticationProblem.WriteAsync(context, 401, "SESSION_INVALID")
            : next(context);
}
