using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HotelOps.Api.Security.Authorization;

public static class PermissionAuthorizationRegistration
{
    public static IServiceCollection AddHotelOpsPermissionAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Endpoints without authorization metadata still require a validated SQL session.
            // Explicit permission policies retain their own requirements and scope checks.
            options.FallbackPolicy = new AuthorizationPolicyBuilder(HotelOpsCookieTicket.Scheme)
                .RequireAuthenticatedUser().Build();
        });
        services.AddHttpContextAccessor();
        services.TryAddScoped<IPermissionPolicyCatalog, EfPermissionPolicyCatalog>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, PermissionAuthorizationHandler>());
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>());
        return services;
    }
}
