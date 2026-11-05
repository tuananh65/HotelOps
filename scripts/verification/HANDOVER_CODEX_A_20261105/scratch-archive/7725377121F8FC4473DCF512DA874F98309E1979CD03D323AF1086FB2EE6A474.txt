using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;

namespace HotelOps.Api.Middleware;

public sealed class SessionTouchMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICookieSessionToucher toucher)
    {
        // Logout revokes under its own transaction, even if the session expired/revoked
        // after cookie validation. Do not renew it or turn idempotent logout into 401.
        if (CookieRequestRules.IsApi(context.Request) && !CookieRequestRules.IsLogout(context.Request)
            && context.User.Identity?.IsAuthenticated == true)
        {
            if (context.Items[typeof(SessionSnapshot)] is not SessionSnapshot original
                || !HotelOpsCookieTicket.TryRead(context.User, out var ticket, out var sessionId))
            {
                await AuthenticationProblem.WriteAsync(context, 401, "SESSION_INVALID"); return;
            }
            // Separate short SQL transaction revalidates after prior checks. Reset,
            // revoke, expiry or temporary-state changes in the gap fail closed here.
            var touched = await toucher.TouchAsync(ticket!, context.RequestAborted);
            var current = touched.Session;
            if (!touched.IsValid || current is null || current.SessionId != sessionId
                || current.UserId != original.UserId || current.CreatedAtUtc != original.CreatedAtUtc
                || current.IsRestricted != original.IsRestricted || current.AbsoluteExpiresAtUtc != original.AbsoluteExpiresAtUtc)
            {
                context.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());
                context.Items.Remove(typeof(SessionSnapshot));
                await AuthenticationProblem.WriteAsync(context, 401, "SESSION_INVALID"); return;
            }
            context.Items[typeof(SessionSnapshot)] = current;
        }
        await next(context);
    }
}
