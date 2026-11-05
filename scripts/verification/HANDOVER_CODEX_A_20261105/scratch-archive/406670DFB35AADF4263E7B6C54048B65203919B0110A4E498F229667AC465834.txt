using HotelOps.Api.Security;
using Microsoft.AspNetCore.Antiforgery;

namespace HotelOps.Api.Middleware;

/// <summary>All /api unsafe methods, including anonymous login and future DEV2 writes.</summary>
public sealed class ApiCsrfMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (CookieRequestRules.IsApi(context.Request) && !CookieRequestRules.IsSafeMethod(context.Request))
        {
            if (!context.Request.Headers.TryGetValue(HotelOpsAntiforgery.HeaderName, out var token)
                || token.Count != 1 || string.IsNullOrWhiteSpace(token[0]))
            {
                await AuthenticationProblem.WriteAsync(context, 400, "CSRF_VALIDATION_FAILED"); return;
            }
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            {
                await AuthenticationProblem.WriteAsync(context, 400, "CSRF_VALIDATION_FAILED"); return;
            }
        }
        await next(context);
    }
}
