using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;

namespace HotelOps.Api.Middleware;

public sealed class RestrictedSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (context.Items[typeof(SessionSnapshot)] is not SessionSnapshot snapshot)
            {
                await AuthenticationProblem.WriteAsync(context, 401, "SESSION_INVALID"); return;
            }
            // Snapshot comes from SQL validation, not a client-supplied claim/header.
            if (snapshot.IsRestricted && !CookieRequestRules.IsRestrictedAllowed(context.Request))
            {
                await AuthenticationProblem.WriteAsync(context, 403, "PASSWORD_CHANGE_REQUIRED"); return;
            }
        }
        await next(context);
    }
}
