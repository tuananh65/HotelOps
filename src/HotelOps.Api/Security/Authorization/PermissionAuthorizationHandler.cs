using HotelOps.Api.Security;
using Microsoft.AspNetCore.Authorization;

namespace HotelOps.Api.Security.Authorization;

public sealed class PermissionAuthorizationHandler(IPermissionService permissions,
    ICurrentUserAccessor currentUser, IHttpContextAccessor httpAccessor)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var http = httpAccessor.HttpContext;
        // Authorization must concern this request's validated cookie principal, not an arbitrary supplied principal.
        if (http is null || !ReferenceEquals(context.User, http.User)
            || context.User.Identity?.IsAuthenticated != true
            || context.User.Identity.AuthenticationType != HotelOpsCookieTicket.Scheme
            || !currentUser.TryGetCurrentUser(out var user) || user.IsRestricted)
        {
            context.Fail();
            return;
        }

        var ct = http.RequestAborted;
        PermissionEvaluation result;
        switch (requirement.Check)
        {
            case PermissionCheck.Global:
                result = await permissions.EvaluateGlobalAsync(requirement.PermissionCode, ct);
                break;
            case PermissionCheck.Branch:
                var branch = context.Resource as PermissionBranchContext
                    ?? (context.Resource as HttpContext)?.Features.Get<PermissionBranchContext>();
                if (branch is null) { context.Fail(); return; }
                result = await permissions.EvaluateBranchAsync(requirement.PermissionCode, branch.BranchId, ct);
                break;
            case PermissionCheck.Resource:
                var resource = context.Resource as PermissionResource
                    ?? (context.Resource as HttpContext)?.Features.Get<PermissionResource>();
                if (resource is null) { context.Fail(); return; }
                result = await permissions.EvaluateResourceAsync(requirement.PermissionCode, resource, ct);
                break;
            default:
                context.Fail();
                return;
        }
        if (result.IsGranted) context.Succeed(requirement);
        else context.Fail();
    }
}
