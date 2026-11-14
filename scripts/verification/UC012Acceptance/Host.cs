using System.Reflection;
using System.Net;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Authentication;
using HotelOps.Api.Middleware;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace UC012Acceptance;

public sealed class FaultSwitch { public bool Armed; public int Hits; }
public sealed class AuditFault(FaultSwitch fault) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (fault.Armed && data.Context!.ChangeTracker.Entries<HotelOps.Api.Entities.AuditLog>().Any(e => e.Entity.EntityType == "branches"))
        { fault.Hits++; throw new DbUpdateException("UC012_TRIAL_AUDIT_FAULT"); }
        return ValueTask.FromResult(result);
    }
}
public static class Host
{
    public static readonly Type[] Controllers = [typeof(AuthController), typeof(CsrfController), typeof(BranchMaintenanceController)];
    public static async Task<WebApplication> StartAsync(TrialReceipt receipt, string sql, FaultSwitch fault, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) throw new GuardFailure("WINDOWS_DPAPI_REQUIRED");
        Preflight.Offline(receipt);
        Guards.Require(Environment.GetEnvironmentVariable("UC012_EXECUTE") == "APPROVED", "EXECUTION_SWITCH_REQUIRED");
        await Preflight.SqlAsync(receipt, sql, ct);
        var b = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Development" });
        b.Logging.ClearProviders();
        var origin = new Uri(receipt.Origin); var cert = Preflight.Certificate();
        b.WebHost.ConfigureKestrel(o => { o.Configure(new ConfigurationBuilder().Build()); o.ListenLocalhost(origin.Port, l => l.UseHttps(cert)); });
        b.Services.AddDbContext<HotelOpsDbContext>(o => o.UseSqlServer(sql).AddInterceptors(new AuditFault(fault)));
        b.Services.AddSingleton(TimeProvider.System);
        b.Services.Configure<PasswordHasherOptions>(o => { o.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3; o.IterationCount = PasswordCredentialService.BaselineIterationCount; });
        b.Services.AddScoped<IPasswordCredentialService, PasswordCredentialService>();
        b.Services.AddScoped<ICredentialLockoutService, CredentialLockoutService>();
        b.Services.AddScoped<ISessionRevocationService, SessionRevocationService>();
        b.Services.AddScoped<ISqlSessionService, SqlSessionService>();
        b.Services.AddScoped<IAuthenticationLoginService, AuthenticationLoginService>();
        b.Services.AddScoped<IAuthenticationCredentialWorkflow, AuthenticationCredentialWorkflow>();
        b.Services.AddHotelOpsCookieInfrastructure(b.Environment);
        b.Services.Configure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(HotelOpsCookieTicket.Scheme, o => o.Cookie.Name = "__Host-HotelOps.UC012.Trial");
        b.Services.AddDataProtection().SetApplicationName("HotelOps.UC012.Trial.v1").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HotelOps", "UC012TrialKeys"))).ProtectKeysWithDpapi(false);
        b.Services.AddHotelOpsAntiforgery();
        b.Services.Configure<Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions>(o => o.Cookie.Name = "__Host-HotelOps.UC012.Trial.Csrf");
        b.Services.AddHttpContextAccessor();
        b.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        b.Services.AddScoped<IPermissionDataSource, EfPermissionDataSource>();
        b.Services.AddScoped<IPermissionResourceResolver, UnresolvedPermissionResourceResolver>();
        b.Services.AddScoped<IPermissionService, PermissionService>();
        b.Services.AddHotelOpsPermissionAuthorization();
        b.Services.AddScoped<BranchAdministrationService>();
        b.Services.AddControllers(o => { o.Conventions.Add(new Actions()); o.Filters.Add(new DedicatedLogins(receipt)); })
            .ConfigureApplicationPartManager(p => { p.ApplicationParts.Clear(); p.FeatureProviders.Clear(); p.FeatureProviders.Add(new Selected()); })
            .ConfigureApiBehaviorOptions(AuthenticationApiBehavior.Configure);
        var app = b.Build();
        app.Use(async (context, next) =>
        {
            if (!context.Request.IsHttps || !IPAddress.IsLoopback(context.Connection.RemoteIpAddress ?? IPAddress.None)
                || context.Request.Host.Value != origin.Authority || DateTimeOffset.UtcNow >= receipt.LeaseEndUtc)
            { context.Response.StatusCode = 403; return; }
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, ct); context.RequestAborted = limit.Token;
            await next(); // No broad exception conversion: audit-fault500/503 must be observed, never manufactured by host.
        });
        app.UseMiddleware<AuthenticationExceptionMiddleware>(); app.UseAuthentication();
        app.Use(async (context, next) =>
        {
            if (context.Request.Cookies.ContainsKey("__Host-HotelOps.UC012.Trial") && context.User.Identity?.IsAuthenticated != true
                && !CookieRequestRules.IsCsrfBootstrap(context.Request) && !CookieRequestRules.IsLogin(context.Request) && !CookieRequestRules.IsLogout(context.Request))
            { await AuthenticationProblem.WriteAsync(context, 401, "SESSION_INVALID"); return; }
            if (context.User.Identity?.IsAuthenticated == true && (!context.RequestServices.GetRequiredService<ICurrentUserAccessor>().TryGetCurrentUser(out var actor)
                || actor.UserId != receipt.ActorId && actor.UserId != receipt.DeniedActorId)) { context.Response.StatusCode = 403; return; }
            await next();
        });
        app.UseMiddleware<InvalidCookieMiddleware>(); app.UseMiddleware<ApiCsrfMiddleware>(); app.UseMiddleware<RestrictedSessionMiddleware>(); app.UseAuthorization(); app.UseMiddleware<SessionTouchMiddleware>(); app.MapControllers();
        await app.StartAsync(ct); return app;
    }
    sealed class Selected : IApplicationFeatureProvider<ControllerFeature>
    { public void PopulateFeature(IEnumerable<ApplicationPart> p, ControllerFeature f) { foreach (var t in Controllers) f.Controllers.Add(t.GetTypeInfo()); } }
    sealed class Actions : IApplicationModelConvention
    {
        public void Apply(ApplicationModel m)
        {
            foreach (var c in m.Controllers)
                foreach (var a in c.Actions.Where(a => c.ControllerType.AsType() == typeof(AuthController) && a.ActionName is not ("Login" or "Logout" or "Session")
                    || c.ControllerType.AsType() == typeof(BranchMaintenanceController) && a.ActionName is not ("Create" or "Read" or "Update")).ToArray()) c.Actions.Remove(a);
        }
    }
    sealed class DedicatedLogins(TrialReceipt r) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext c, ActionExecutionDelegate next)
        {
            if (c.Controller is AuthController && c.ActionDescriptor.RouteValues["action"] == "Login"
                && !c.ActionArguments.Values.OfType<LoginRequest>().Any(x => x.Username == r.ActorUsername || x.Username == r.DeniedUsername))
            { c.Result = new StatusCodeResult(403); return; }
            await next();
        }
    }
}
