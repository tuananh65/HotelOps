using HotelOps.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace HotelOps.Api.Security;

public static class AuthenticationApiBehavior
{
    public static void Configure(ApiBehaviorOptions options)
    {
        var fallback = options.InvalidModelStateResponseFactory;
        options.InvalidModelStateResponseFactory = context =>
        {
            if (context.ActionDescriptor is not ControllerActionDescriptor action
                || action.ControllerTypeInfo.AsType() != typeof(AuthController))
                return fallback(context);
            // Binding errors can contain submitted secrets: never echo values or exception text.
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return new BadRequestObjectResult(new ProblemDetails
            {
                Status = 400, Title = "Request validation failed", Type = "about:blank",
                Extensions = { ["code"] = "VALIDATION_FAILED", ["traceId"] = context.HttpContext.TraceIdentifier }
            }) { ContentTypes = { "application/problem+json" } };
        };
    }
}
