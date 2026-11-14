using System.Data.Common;
using HotelOps.Api.Services.Insights;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HotelOps.Api.Controllers;

[AttributeUsage(AttributeTargets.Class)]
public sealed class InsightErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        int status;
        string code;
        string detail;
        if (context.Exception is InsightException error)
            (status, code, detail) = (error.Status, error.Code, error.Message);
        else if (context.Exception is DbException or TimeoutException)
            (status, code, detail) = (503, "INSIGHT_SOURCE_UNAVAILABLE", "Source data unavailable. No zero values were substituted.");
        else return;
        var problem = new ProblemDetails { Status = status, Title = code, Detail = detail };
        problem.Extensions["code"] = code;
        context.Result = new ObjectResult(problem) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
