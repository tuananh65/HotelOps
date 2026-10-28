using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[UserAccountActivationErrors]
public sealed class UserAccountActivationController(UserAccountActivationService activation) : ControllerBase
{
    [HttpPatch("{userId}/activate")]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Activate([FromRoute, Range(1, long.MaxValue)] long userId,
        [FromBody] JsonElement body, CancellationToken ct)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS is required");
        if (!HotelOpsCookieTicket.TryRead(User, out var reference, out var sessionId)) return Failure(401, "SESSION_INVALID");
        if (!Request.Headers.TryGetValue("If-Match", out var header)) return Failure(428, "IF_MATCH_REQUIRED");
        var tag = header.ToString();
        if (header.Count != 1 || tag.Length != 18 || tag[0] != '"' || tag[^1] != '"'
            || !tag.AsSpan(1, 16).ToString().All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F'))
            return Failure(400, "INVALID_IF_MATCH");
        if (!UserAccountActivationRequest.TryParse(body, out var request)) return Failure(400, "VALIDATION_FAILED");
        var result = await activation.ActivateAsync(reference!, sessionId, userId, Convert.FromHexString(tag[1..^1]),
            new("ACTIVE", request!.Reason), ct);
        switch (result.Status)
        {
            case AccountActivationStatus.Success:
                Response.Headers.ETag = "\"" + Convert.ToHexString(result.Version!) + "\"";
                return Ok(new UserAccountActivationDto(userId, "ACTIVE"));
            case AccountActivationStatus.SessionInvalid: return Failure(401, "SESSION_INVALID");
            case AccountActivationStatus.Forbidden: return Failure(403, "ACTIVATION_NOT_ALLOWED");
            case AccountActivationStatus.NotFound: return Failure(404, "USER_NOT_FOUND");
            case AccountActivationStatus.Stale: return Failure(412, "STALE_USER_VERSION");
            case AccountActivationStatus.Conflict: return Failure(409, "INVALID_STATUS_TRANSITION");
            default: throw new InvalidOperationException("Unexpected activation result.");
        }
    }

    private ObjectResult Failure(int status, string code) => Problem(statusCode: status, title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}

/// <summary>Translate only the route's body limit; preserve existing middleware for all other failures.</summary>
public sealed class UserAccountActivationErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not BadHttpRequestException { StatusCode: 413 }) return;
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = 413, Title = "REQUEST_TOO_LARGE", Type = "about:blank",
            Extensions = { ["code"] = "REQUEST_TOO_LARGE", ["traceId"] = context.HttpContext.TraceIdentifier }
        }) { StatusCode = 413, ContentTypes = { "application/problem+json" } };
        context.ExceptionHandled = true;
    }
}
