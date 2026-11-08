using System.Reflection;
using HotelOps.Api.Controllers;
using HotelOps.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace HotelOps.Dev1.RegressionTests;

public sealed class CleaningMonitorRouteTests
{
    [Fact]
    public void Monitor_exposes_exactly_two_cookie_authenticated_GETs_without_collisions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers().AddApplicationPart(typeof(CleaningMonitorController).Assembly);
        using var provider = services.BuildServiceProvider();
        var actions = provider.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>().ToArray();
        var monitor = actions.Where(a => a.ControllerTypeInfo.AsType() == typeof(CleaningMonitorController)).ToArray();
        Assert.Equal(2, monitor.Length);
        Assert.Equal(new[] { "api/branches/{branchId:long}/cleaning-monitor", "api/branches/{branchId:long}/cleaning-monitor/{kind}/{id:long}" },
            monitor.Select(a => a.AttributeRouteInfo!.Template).OrderBy(x => x).ToArray());
        Assert.All(monitor, action =>
        {
            Assert.NotNull(action.MethodInfo.GetCustomAttribute<HttpGetAttribute>());
            Assert.Null(action.MethodInfo.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Null(action.ControllerTypeInfo.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Equal(HotelOpsCookieTicket.Scheme, action.ControllerTypeInfo.GetCustomAttribute<AuthorizeAttribute>()!.AuthenticationSchemes);
            Assert.Single(actions, a => a.AttributeRouteInfo?.Template == action.AttributeRouteInfo!.Template);
        });
    }
}
