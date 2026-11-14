using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Authentication;
using HotelOps.Api.Middleware;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace ScheduledWorkdayTrial;

public static class TrialHost
{
    // No production application-part discovery. Four explicitly selected controller types only.
    public static readonly Type[] Controllers = [typeof(AuthController), typeof(CsrfController), typeof(WorkdayReadsController), typeof(TrialScheduledController)];
    public static bool Admits(HttpContext context, TrialConfiguration receipt, DateTimeOffset now)
        => context.Request.IsHttps && System.Net.IPAddress.IsLoopback(context.Connection.RemoteIpAddress ?? System.Net.IPAddress.None)
            && string.Equals(context.Request.Host.Value, new Uri(receipt.Origin).Authority, StringComparison.OrdinalIgnoreCase)
            && receipt.LeaseStartsUtc <= now && receipt.LeaseEndsUtc > now;
    public static void Register(WebApplicationBuilder builder, TrialConfiguration receipt, string connection, string username)
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Windows Development encrypted trial keys required");
        builder.Logging.ClearProviders(); // no body/SQL/credential/error telemetry; host prints sanitized status only.
        builder.Services.AddSingleton(receipt);
        builder.Services.AddDbContext<HotelOpsDbContext>(o => o.UseSqlServer(connection));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.Configure<PasswordHasherOptions>(o => { o.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3; o.IterationCount = PasswordCredentialService.BaselineIterationCount; });
        builder.Services.AddScoped<IPasswordCredentialService, PasswordCredentialService>();
        builder.Services.AddScoped<ICredentialLockoutService, CredentialLockoutService>();
        builder.Services.AddScoped<ISessionRevocationService, SessionRevocationService>();
        builder.Services.AddScoped<ISqlSessionService, SqlSessionService>();
        builder.Services.AddScoped<IAuthenticationLoginService, AuthenticationLoginService>();
        builder.Services.AddScoped<IAuthenticationCredentialWorkflow, AuthenticationCredentialWorkflow>();
        builder.Services.AddHotelOpsCookieInfrastructure(builder.Environment);
        builder.Services.Configure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(HotelOpsCookieTicket.Scheme,
            o => o.Cookie.Name = "__Host-HotelOps.UC139.Trial");
        // Distinct purpose and encrypted key ring; never share production/development authentication tickets.
        builder.Services.AddDataProtection().SetApplicationName("HotelOps.UC139.Trial.v1")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HotelOps", "UC139TrialKeys")))
            .ProtectKeysWithDpapi(protectToLocalMachine: false);
        builder.Services.AddHotelOpsAntiforgery();
        builder.Services.Configure<Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions>(o => o.Cookie.Name = "__Host-HotelOps.UC139.Trial.Csrf");
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        builder.Services.AddScoped<IPermissionDataSource, EfPermissionDataSource>();
        builder.Services.AddScoped<ICleaningTaskAccessReader, EfCleaningTaskAccessReader>();
        builder.Services.AddScoped<IPermissionResourceResolver>(sp => new WorkdayPermissionResourceResolver(sp.GetRequiredService<HotelOpsDbContext>(), ActivatorUtilities.CreateInstance<CleaningTaskResourceResolver>(sp)));
        builder.Services.AddScoped<IPermissionService, PermissionService>();
        builder.Services.AddHotelOpsPermissionAuthorization();
        builder.Services.AddScoped<SqlWorkTimeTransactionParticipant>();
        builder.Services.AddScoped<OwnWorkdaySnapshotReader>();
        builder.Services.AddScoped<WorkdayService>();
        builder.Services.AddControllers(o => { o.Conventions.Add(new AuthActionsOnly()); o.Filters.Add(new DedicatedLogin(username)); })
            .ConfigureApplicationPartManager(parts => { parts.ApplicationParts.Clear(); parts.FeatureProviders.Clear(); parts.FeatureProviders.Add(new ExplicitControllers()); })
            .ConfigureApiBehaviorOptions(AuthenticationApiBehavior.Configure);
    }
    public static void Pipeline(WebApplication app, TrialConfiguration receipt)
    {
        app.Use(async (context, next) =>
        {
            if (!Admits(context, receipt, DateTimeOffset.UtcNow))
            { context.Response.StatusCode = 403; return; }
            // Never emit exception details, connection strings or ProblemDetails from an unhandled host fault.
            using var expiry = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            expiry.CancelAfter(TimeSpan.FromTicks(Math.Max(0, (receipt.LeaseEndsUtc - DateTimeOffset.UtcNow).Ticks))); context.RequestAborted = expiry.Token;
            try { await next(); } catch when (!context.Response.HasStarted) { context.Response.Clear(); context.Response.StatusCode = 503; }
        });
        app.UseMiddleware<AuthenticationExceptionMiddleware>();
        app.UseAuthentication();
        app.Use(async (context, next) =>
        {
            if (context.Request.Cookies.ContainsKey("__Host-HotelOps.UC139.Trial") && context.User.Identity?.IsAuthenticated != true
                && !CookieRequestRules.IsCsrfBootstrap(context.Request) && !CookieRequestRules.IsLogin(context.Request) && !CookieRequestRules.IsLogout(context.Request))
            { await AuthenticationProblem.WriteAsync(context, 401, "SESSION_INVALID"); return; }
            await next();
        });
        app.UseMiddleware<InvalidCookieMiddleware>();
        app.Use(async (context, next) =>
        {
            if (context.User.Identity?.IsAuthenticated == true && (!context.RequestServices.GetRequiredService<ICurrentUserAccessor>().TryGetCurrentUser(out var actor) || actor.UserId != receipt.ActorId))
            { context.Response.StatusCode = 403; return; }
            await next();
        });
        app.UseMiddleware<ApiCsrfMiddleware>();
        app.UseMiddleware<RestrictedSessionMiddleware>();
        app.UseAuthorization();
        app.UseMiddleware<SessionTouchMiddleware>();
        app.MapControllers();
    }
    sealed class ExplicitControllers : IApplicationFeatureProvider<ControllerFeature>
    { public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature) { foreach (var type in Controllers) feature.Controllers.Add(type.GetTypeInfo()); } }
    sealed class AuthActionsOnly : IApplicationModelConvention
    { public void Apply(ApplicationModel model) { foreach (var controller in model.Controllers.Where(c => c.ControllerType.AsType() == typeof(AuthController))) foreach (var action in controller.Actions.Where(a => a.ActionName is not ("Login" or "Logout" or "Session")).ToArray()) controller.Actions.Remove(action); } }
    sealed class DedicatedLogin(string username) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.Controller is AuthController && context.ActionDescriptor.RouteValues["action"] == "Login"
                && !context.ActionArguments.Values.OfType<LoginRequest>().Any(r => string.Equals(r.Username, username, StringComparison.Ordinal)))
            { context.Result = new Microsoft.AspNetCore.Mvc.StatusCodeResult(403); return; }
            await next();
        }
    }
}
