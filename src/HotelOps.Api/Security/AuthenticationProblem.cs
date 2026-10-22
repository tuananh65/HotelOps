using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Security;

public static class AuthenticationProblem
{
    public static Task WriteAsync(HttpContext context, int status, string code)
    {
        context.Response.StatusCode = status;
        context.Response.Headers.Remove("Location");
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Type = "about:blank", Status = status,
            Title = status switch { 400 => "Request validation failed", 401 => "Authentication required", 403 => "Access denied",
                503 => "Authentication service unavailable", _ => "Internal server error" },
            Extensions = { ["code"] = code, ["traceId"] = context.TraceIdentifier }
        }, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
