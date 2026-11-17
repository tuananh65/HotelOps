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
        [FromQuery] InternalRequestHistoryFilter filter, CancellationToken ct, [FromQuery] bool? atRisk = null)
    {
        var session = Session();
        return service.ListAsync(session.Ticket, session.Sid, branchId, filter, ct, atRisk);
    }

    [HttpGet("options")]
    public Task<InternalRequestFilterOptions> Options(CancellationToken ct)
    {
        var session = Session();
        return service.OptionsAsync(session.Ticket, session.Sid, ct);
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
            ValidationException when context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor reopen
                && reopen.ControllerTypeInfo.AsType()==typeof(InternalRequestReopenController) => (400,"INVALID_REOPEN_INPUT"),
            ValidationException when context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor escalation
                && escalation.ControllerTypeInfo.AsType()==typeof(InternalRequestEscalationController) => (400,"INVALID_ESCALATION_INPUT"),
            ValidationException when context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor completion
                && completion.ControllerTypeInfo.AsType()==typeof(InternalRequestCompletionController) => (400,"INVALID_COMPLETION_INPUT"),
            ValidationException when context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor handover
                && handover.ControllerTypeInfo.AsType()==typeof(InternalRequestHandoverController) => (400,"INVALID_HANDOVER_INPUT"),
            ValidationException when context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor intake
                && intake.ControllerTypeInfo.AsType()==typeof(InternalRequestIntakeController) => (400,"INVALID_INTAKE_INPUT"),
            ValidationException when context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor action
                && action.ControllerTypeInfo.AsType()==typeof(InternalRequestDecisionController) => (400,"INVALID_DECISION_INPUT"),
            ValidationException when context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor assignment
                && assignment.ControllerTypeInfo.AsType()==typeof(InternalRequestAssignmentController) => (400,"INVALID_ASSIGNMENT_INPUT"),
            ValidationException when context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor route
                && route.ControllerTypeInfo.AsType()==typeof(InternalRequestRouteController) => (400,"INVALID_ROUTE_INPUT"),
            ValidationException => (400,"INVALID_HISTORY_QUERY"),
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
