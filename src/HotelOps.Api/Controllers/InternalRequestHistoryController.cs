using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize, InternalRequestReadErrors]
[Route("api/internal-requests")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InternalRequestHistoryController(InternalRequestReadService service) : ControllerBase
{
    private (SessionReference Ticket, long Sid) Session()
    {
        if (!Request.IsHttps) throw new InternalRequestReadException(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid))
            throw new InternalRequestReadException(401, "SESSION_INVALID");
        return (ticket!, sid);
    }

    [HttpGet]
    public Task<InternalRequestHistoryPage> List([FromQuery] long? branchId,
        [FromQuery] InternalRequestHistoryFilter filter, CancellationToken ct)
    {
        var session = Session();
        return service.ListAsync(session.Ticket, session.Sid, branchId, filter, ct);
    }

    [HttpGet("{id:long}/timeline")]
    public async Task<ActionResult<InternalRequestTimeline>> Timeline(long id,
        [FromQuery] InternalRequestTimelineQuery pagination, CancellationToken ct)
    {
        var session = Session();
        var result = await service.TimelineAsync(session.Ticket, session.Sid, id, pagination, ct);
        return result is null ? NotFound() : Ok(result);
    }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class InternalRequestReadErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var (status, code) = context.Exception switch
        {
            InternalRequestReadException e => (e.Status, e.Code),
            ValidationException => (400, "INVALID_HISTORY_QUERY"),
            DbException or TimeoutException => (503, "IR_SOURCE_UNAVAILABLE"),
            _ => (0, "")
        };
        if (status == 0) return;
        var problem = new ProblemDetails { Status = status, Title = code };
        problem.Extensions["code"] = code;
        context.Result = new ObjectResult(problem) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
