using System.Reflection;
using System.Threading.RateLimiting;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

namespace HotelOps.Api.Compact;

public static class CompactHost
{
    public static async Task RunAsync(string[] args)
    {
        var builder=WebApplication.CreateBuilder(args);
        var connection=builder.Configuration.GetConnectionString("HotelOpsDb")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:HotelOpsDb for Compact v2 using local secrets or environment.");
        builder.Services.AddDbContext<HotelOpsCompactDbContext>(o=>o.UseSqlServer(connection));
        builder.Services.AddHttpContextAccessor();
        builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(
            options=>ProxyConfiguration.Configure(options,builder.Configuration));
        builder.Services.AddHttpsRedirection(options=>options.HttpsPort=443);
        builder.Services.AddSingleton(TimeProvider.System);
        var dataProtection=builder.Services.AddDataProtection().SetApplicationName("HotelOps.Compact");
        if(builder.Configuration["Email:DataProtectionKeysPath"] is {Length:>0} keyPath)dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        builder.Services.AddScoped<AccountInvitationService>();
        builder.Services.AddScoped<InventoryLifecycleService>();
        builder.Services.AddScoped<ReusableInventoryService>();
        builder.Services.AddHttpClient("hotelops-advisory",client=>client.Timeout=TimeSpan.FromSeconds(25));
        builder.Services.AddScoped<IAdvisoryProvider,ConfiguredAdvisoryProvider>();
        builder.Services.AddScoped<AdvisoryService>();
        builder.Services.AddScoped<IInvitationEmail,SmtpInvitationEmail>();
        builder.Services.AddHostedService<InvitationDeliveryWorker>();
        builder.Services.AddScoped<Actor>();builder.Services.AddScoped<Access>();builder.Services.AddScoped<Atomic>();builder.Services.AddScoped<Audit>();
        builder.Services.AddScoped<IPasswordHasher<User>,PasswordHasher<User>>();
        builder.Services.Configure<PasswordHasherOptions>(o=>o.IterationCount=210000);
        builder.Services.AddScoped<AuthService>();builder.Services.AddScoped<UserService>();builder.Services.AddScoped<CatalogService>();
        builder.Services.AddScoped<AccountManagementService>();builder.Services.AddScoped<IPasswordResetEmail,PasswordResetEmail>();
        builder.Services.AddScoped<WorkService>();builder.Services.AddScoped<RoomService>();builder.Services.AddScoped<DocumentService>();
        builder.Services.AddScoped<RoomReadinessService>();
        builder.Services.AddScoped<SupplyConfigurationService>();builder.Services.AddScoped<StaffSupplyService>();
        builder.Services.AddScoped<RequestWorkflowService>();
        builder.Services.AddScoped<LowStockService>();
        builder.Services.AddScoped<AssetService>();builder.Services.AddScoped<TemplateService>();builder.Services.AddScoped<InsightService>();
        builder.Services.AddScoped<NotificationService>();builder.Services.AddScoped<EvidenceService>();
        builder.Services.AddSingleton<NotificationRealtimeReader>();
        builder.Services.AddSignalR(options=>options.MaximumParallelInvocationsPerClient=1);
        builder.Services.AddScoped<WorkdayService>();
        builder.Services.AddScoped<ShiftExpiryService>();
        builder.Services.AddHostedService<ShiftExpiryWorker>();
        builder.Services.AddScoped<HotelOps.Api.Compact.Controllers.WorkspaceService>();
        builder.Services.AddHostedService<OperationsWorker>();
        builder.Services.AddAuthentication(SessionAuthentication.SchemeName)
            .AddScheme<AuthenticationSchemeOptions,SessionAuthentication>(SessionAuthentication.SchemeName,_=>{});
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<ShiftCommandFilter>();
        builder.Services.AddControllers(options=>options.Filters.AddService<ShiftCommandFilter>()).ConfigureApplicationPartManager(m=>m.FeatureProviders.Add(new CompactControllersOnly()));
        builder.Services.AddOpenApi();
        builder.Services.AddRateLimiter(o=>{
            o.RejectionStatusCode=429;
            o.AddPolicy("advisory",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",
                _=>new FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
            o.AddPolicy("account-reset",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",
                _=>new FixedWindowRateLimiterOptions{PermitLimit=5,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
            o.AddPolicy("login",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",
                _=>new FixedWindowRateLimiterOptions{PermitLimit=15,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
        });
        var origins=builder.Configuration.GetSection("Compact:CorsOrigins").Get<string[]>()??[];
        builder.Services.AddCors(o=>o.AddDefaultPolicy(p=>{if(origins.Length>0)p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();}));
        var app=builder.Build();
        await using(var scope=app.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<HotelOpsCompactDbContext>();
            await SchemaGate.Validate(db,app.Lifetime.ApplicationStopping);
            if(args.Contains("--bootstrap-admin"))
            {
                await Bootstrap.Run(db,scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(),builder.Configuration,app.Lifetime.ApplicationStopping);
                return;
            }
        }
        app.UseForwardedHeaders();
        app.UseMiddleware<ErrorMiddleware>();
        if(!app.Environment.IsDevelopment())app.UseHttpsRedirection();
        app.UseCors();app.UseRateLimiter();app.UseAuthentication();
        app.Use(async(ctx,next)=>{
            ctx.Response.Headers.CacheControl="no-store";
            if(ctx.User.HasClaim("restricted","true") && !new[]{"/api/v2/auth/me","/api/v2/auth/logout","/api/v2/auth/password"}.Contains(ctx.Request.Path.Value))
                throw new ApiException(403,"PASSWORD_CHANGE_REQUIRED");
            await next();
        });
        app.UseAuthorization();
        // A published frontend is optional: API-only deployments keep their
        // existing routes, while local Release can serve the built SPA.
        var frontendIndex=app.Environment.WebRootFileProvider.GetFileInfo("index.html");
        if(frontendIndex.Exists && frontendIndex.PhysicalPath is { } indexPath)
        {
            app.UseWhen(ctx=>!ctx.Request.Path.StartsWithSegments("/api"),frontend=>{
                frontend.UseDefaultFiles();
                frontend.UseStaticFiles();
            });
            app.MapFallback("{*path:nonfile}",async context=>{
                if(context.Request.Path.StartsWithSegments("/api") ||
                   (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)))
                {
                    context.Response.StatusCode=StatusCodes.Status404NotFound;
                    return;
                }
                await Results.File(indexPath,"text/html; charset=utf-8").ExecuteAsync(context);
            });
        }
        if(app.Environment.IsDevelopment())app.MapOpenApi();
        app.MapGet("/health",()=>new{status="ok",schema="3.0.0",tables=57,delta="restructure-v2-in-progress"});
        app.MapHub<HotelOps.Api.Compact.Realtime.NotificationHub>("/api/v2/realtime/notifications",
            options=>options.Transports=Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling);
        app.MapControllers();await app.RunAsync();
    }
    private sealed class CompactControllersOnly:IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts,ControllerFeature feature)
        {
            foreach(var type in feature.Controllers.Where(t=>t.Namespace!="HotelOps.Api.Compact.Controllers").ToArray())feature.Controllers.Remove(type);
        }
    }
}
