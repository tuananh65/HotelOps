using System.Runtime.ExceptionServices;
using HotelOps.Api.Data;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HotelOps.Api.Controllers.Filters;

// TypeFilter retains the same SQL read boundary for the directory and access review.
public sealed class UserDirectoryReadFilter(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, TimeProvider clock, UserAccessReviewService reviews) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        if (!HotelOpsCookieTicket.TryRead(context.HttpContext.User, out var ticket, out _))
        {
            context.Result = Error(401, "SESSION_INVALID"); return;
        }
        ActionExecutedContext? executed = null;
        var metadata = context.ActionDescriptor.EndpointMetadata.OfType<UserAccessReviewAttribute>().SingleOrDefault();
        var review = metadata != null && (!metadata.WhenIncluded
            || context.ActionArguments.TryGetValue("includeAccess", out var include) && include is true);
        AuthorizedAccessReviewScope? original = null;
        async Task<PermissionEvaluation> Authorize(CancellationToken ct)
        {
            if (!review) return await permissions.EvaluateGlobalAsync("USER_VIEW", ct);
            if (!context.ActionArguments.TryGetValue("userId", out var value) || value is not long id)
                return new(PermissionDecision.Denied);
            var scope = await reviews.ResolveScopeAsync(id, ct);
            if (scope is null || original != null && !scope.Covers(original)) return new(PermissionDecision.Denied);
            original ??= scope;
            UserAccessReviewAttribute.Store(context.HttpContext, original);
            return new(PermissionDecision.Granted);
        }
        var guarded = await new UserDirectoryReadGuard(db, current, permissions, clock).RunAuthorizedAsync(ticket!, Authorize, async () =>
        {
            executed = await next();
            if (executed.Exception is { } error && !executed.ExceptionHandled)
                ExceptionDispatchInfo.Capture(error).Throw();
            return executed;
        }, context.HttpContext.RequestAborted);
        var result = guarded.Status switch
        {
            UserDirectoryReadStatus.Success => guarded.Value!.Result,
            UserDirectoryReadStatus.SessionInvalid => Error(401, "SESSION_INVALID"),
            UserDirectoryReadStatus.Forbidden => Error(403, "PERMISSION_DENIED"),
            UserDirectoryReadStatus.AuthorizationUnavailable => Error(503, "AUTH_SERVICE_UNAVAILABLE"),
            _ => throw new InvalidOperationException("Unknown user directory read status.")
        };
        // After next(), MVC uses ActionExecutedContext.Result. Replacing only the
        // executing context would accidentally publish data rejected by the final gate.
        if (executed is null) context.Result = result;
        else executed.Result = result;
    }

    private static ObjectResult Error(int status, string code) => new(new ProblemDetails
    {
        Status = status, Type = "about:blank",
        Title = status switch { 401 => "Authentication required", 403 => "Access denied", _ => "Authentication service unavailable" },
        Extensions = { ["code"] = code }
    }) { StatusCode = status, ContentTypes = { "application/problem+json" } };
}
