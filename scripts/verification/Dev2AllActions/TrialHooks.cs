using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Dev2Authorization;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

[assembly: HostingStartup(typeof(TrialFaultHostingStartup))]

// Loaded ONLY by the explicit Windows verification runner. No shared Program/DI changes.
public static class StartupHook { public static void Initialize() { } }
public sealed class TrialFaultHostingStartup:IHostingStartup
{
    public void Configure(IWebHostBuilder builder)=>builder.ConfigureServices(services=>
    {
        if(Environment.GetEnvironmentVariable("DEV2_DEMO_AUTHORITY_V1")!=Dev2ExpandedPolicyIdentity.Sha256
            ||string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEV2_TRIAL_HOOK_DIRECTORY")))throw new InvalidOperationException("Trial verification hooks require exact V2 policy");
        services.AddScoped<TrialSaveFault>();services.AddScoped<TrialAdmissionBarrier>();
        services.AddTransient<IStartupFilter,TrialAuthObservation>();
        services.AddDbContext<HotelOpsDbContext>((sp,options)=>options.AddInterceptors(sp.GetRequiredService<TrialSaveFault>()));
        services.Configure<MvcOptions>(options=>options.Filters.AddService<TrialAdmissionBarrier>());
    });
}
// Observation only, before the unchanged Auth pipeline; no principal, cookie or status edits.
public sealed class TrialAuthObservation:IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)=>app=>
    {
        app.Use(async(http,invoke)=>
        {
            var probe=http.Request.Headers["X-Dev2-Auth-Probe"].ToString();
            var observe=Guid.TryParseExact(probe,"N",out _);
            var received=observe&&http.Request.Cookies.ContainsKey(HotelOpsCookieTicket.CookieName);
            await invoke();
            if(observe)
            {
                var snapshot=http.Items[typeof(SessionSnapshot)] as SessionSnapshot;
                var target=Path.Combine(Environment.GetEnvironmentVariable("DEV2_TRIAL_HOOK_DIRECTORY")!,"auth-"+probe+".json");
                await File.WriteAllTextAsync(target+".tmp",System.Text.Json.JsonSerializer.Serialize(new{cookieReceived=received,authenticated=http.User.Identity?.IsAuthenticated==true,sqlSnapshotConfirmed=snapshot is not null,isRestricted=snapshot?.IsRestricted,httpStatus=http.Response.StatusCode}));
                File.Move(target+".tmp",target);
            }
        });
        next(app);
    };
}
internal static class TrialBarrier
{
    internal static bool Fixture(HttpContext? http)
    {
        if(http is null||Environment.GetEnvironmentVariable("DEV2_DEMO_AUTHORITY_V1")!=Dev2ExpandedPolicyIdentity.Sha256)return false;
        var folder=Environment.GetEnvironmentVariable("DEV2_TRIAL_HOOK_DIRECTORY")!;
        if(!File.Exists(Path.Combine(folder,"fixture-users.txt")))return false;
        return http.RequestServices.GetRequiredService<ICurrentUserAccessor>().TryGetCurrentUser(out var user)
            &&File.ReadAllLines(Path.Combine(folder,"fixture-users.txt")).Contains(user.UserId.ToString(),StringComparer.Ordinal);
    }
    internal static async Task Wait(string name,CancellationToken ct)
    {
        var folder=Environment.GetEnvironmentVariable("DEV2_TRIAL_HOOK_DIRECTORY")!;
        await File.WriteAllTextAsync(Path.Combine(folder,name+".waiting"),"WAITING",ct);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(20));
        while(!File.Exists(Path.Combine(folder,name+".release")))await Task.Delay(25,timeout.Token);
    }
}
public sealed class TrialAdmissionBarrier:IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context,ResourceExecutionDelegate next)
    {
        if(TrialBarrier.Fixture(context.HttpContext)&&context.HttpContext.Request.Headers["X-Dev2-Admission-Barrier"]=="1")await TrialBarrier.Wait("admission",context.HttpContext.RequestAborted);
        await next();
    }
}
public sealed class TrialSaveFault(IHttpContextAccessor http):SaveChangesInterceptor
{
    private bool held;
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
    {
        if(!TrialBarrier.Fixture(http.HttpContext))return result;
        if(data.Context!.Database.GetDbConnection().Database!=Dev2DemoPolicyIdentity.Database)throw new InvalidOperationException("Hooks prohibit other database");
        if(!held&&http.HttpContext!.Request.Headers["X-Dev2-Save-Barrier"]=="1") {held=true;await TrialBarrier.Wait("save",ct);}
        if(http.HttpContext!.Request.Headers["X-Dev2-Audit-Fault"]=="1"&&data.Context.ChangeTracker.Entries<AuditLog>().Any(x=>x.State==EntityState.Added))
        {
            var probe=http.HttpContext.Request.Headers["X-Dev2-Audit-Probe"].ToString();
            if(Guid.TryParseExact(probe,"N",out _))
            {
                var target=Path.Combine(Environment.GetEnvironmentVariable("DEV2_TRIAL_HOOK_DIRECTORY")!,"audit-"+probe+".json");
                var observation=new{
                    inventoryAuditPending=data.Context.ChangeTracker.Entries<AuditLog>().Any(x=>x.State==EntityState.Added&&x.Entity.EntityType=="stock_documents"),
                    postedDocumentTracked=data.Context.ChangeTracker.Entries<StockDocument>().Any(x=>x.Entity.DocumentType=="RECEIPT"&&x.Entity.Status=="POSTED"),
                    transactionActive=data.Context.Database.CurrentTransaction is not null};
                await File.WriteAllTextAsync(target+".tmp",System.Text.Json.JsonSerializer.Serialize(observation),ct);
                File.Move(target+".tmp",target);
            }
            throw new DbUpdateException("Intentional isolated fixture audit failure");
        }
        return result;
    }
}
