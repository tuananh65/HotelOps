using HotelOps.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HotelOps.Api.Controllers.Filters;

/// <summary>
/// StageA exact action/session/contract admission; StageB asynchronous permission,
/// resource/workflow validation and service capability. Incomplete contracts deny
/// before binding. A complete read requires a scoped handler, not the old query.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class Dev2AuthorityPendingAttribute : AuthorizeAttribute, IAsyncAuthorizationFilter, IAsyncActionFilter, IOrderedFilter
{
    public Dev2AuthorityPendingAttribute() => AuthenticationSchemes = HotelOpsCookieTicket.Scheme;

    public int Order => int.MinValue;

    public void OnAuthorization(AuthorizationFilterContext context) => Dev2ActionAdmissionGuard.Authorize(context);

    public Task OnAuthorizationAsync(AuthorizationFilterContext context) => Dev2ActionAdmissionGuard.AuthorizeAsync(context);

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next) =>
        Dev2ActionAdmissionGuard.ExecuteAsync(context, next);
}
