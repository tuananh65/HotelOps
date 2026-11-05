using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;

namespace HotelOps.Api.Middleware;

public sealed class AuthenticationExceptionMiddleware(RequestDelegate next, ILogger<AuthenticationExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            context.Response.Clear();
            var unavailable = exception is SessionStoreUnavailableException;
            logger.LogError("Request pipeline failed: {ErrorType}; trace {TraceId}.",
                exception.GetType().FullName, context.TraceIdentifier);
            // No exception/ticket logging: SQL/provider messages may carry sensitive
            // values. All other programming/schema errors remain generic 500.
            await AuthenticationProblem.WriteAsync(context, unavailable ? 503 : 500,
                unavailable ? "AUTH_SERVICE_UNAVAILABLE" : "INTERNAL_SERVER_ERROR");
        }
    }
}
