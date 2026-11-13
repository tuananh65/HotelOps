using HotelOps.Api.Services.Incidents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HotelOps.Api.Controllers;

[AttributeUsage(AttributeTargets.Class)]
public sealed class IncidentAccessFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not IncidentAccessException exception) return;
        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = exception.StatusCode,
            Title = exception.StatusCode switch
            {
                401 => "Authentication required",
                503 => "Incident authorization unavailable",
                _ => "Incident access denied"
            }
        }) { StatusCode = exception.StatusCode };
        context.ExceptionHandled = true;
    }
}
