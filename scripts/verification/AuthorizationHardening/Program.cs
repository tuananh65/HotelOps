using System.Net;
using System.Text.Json;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Verification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;

var count=0;
void Check(bool condition,string name){if(!condition)throw new InvalidOperationException("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
foreach(var sql in new[]{"SELECT DB_NAME()","SELECT [u].[user_id] FROM [dbo].[users] AS [u] WHERE [u].[account_status]=N'ACTIVE'"}){
    ReadOnlyVerification.AssertSelect(sql);Check(true,"SELECT guard accepts read command");
}
foreach(var sql in new[]{"UPDATE dbo.users SET account_status='ACTIVE'","SELECT 1; DELETE FROM dbo.users","SELECT * INTO copy FROM dbo.users","EXEC dbo.anything","-- SELECT\nDELETE FROM dbo.users","SELECT NEXT VALUE FOR dbo.counter"}){
    var rejected=false;try{ReadOnlyVerification.AssertSelect(sql);}catch(InvalidOperationException){rejected=true;}Check(rejected,"write/batch guard rejects before SQL");
}
var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Development",ApplicationName=typeof(HealthDatabase).Assembly.FullName});
var catalog=new CancellationCatalog();builder.Services.AddSingleton<IPermissionPolicyCatalog>(catalog);
builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<HotelOpsDbContext>(p=>new HealthDatabase(p.GetRequiredService<IHttpContextAccessor>().HttpContext?.Request.Headers["X-Test-Health"].ToString()??"healthy"));
builder.Services.AddControllers().AddApplicationPart(typeof(HealthController).Assembly);
await using var app=builder.Build();app.MapControllers();await app.StartAsync();
try{
    var url=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=new Uri(url)};
    foreach(var scenario in new[]{"healthy","unhealthy","exception"}){
        using var request=new HttpRequestMessage(HttpMethod.Get,"/api/health/database");request.Headers.Add("X-Test-Health",scenario);
        using var response=await client.SendAsync(request);var body=await response.Content.ReadAsStringAsync();using var json=JsonDocument.Parse(body);
        Check(response.StatusCode==(scenario=="healthy"?HttpStatusCode.OK:HttpStatusCode.ServiceUnavailable),"anonymous health "+scenario+" status");
        Check(json.RootElement.EnumerateObject().Count()==1&&json.RootElement.GetProperty("status").GetString()==(scenario=="healthy"?"ok":"error"),"health "+scenario+" minimal contract");
        Check(!body.Contains("sentinel")&&!body.Contains("HotelOpsDB")&&!body.Contains("database",StringComparison.OrdinalIgnoreCase)&&response.Headers.Location is null,"health "+scenario+" no diagnostic disclosure/redirect");
    }
}finally{await app.StopAsync();}
using(var cancelled=new CancellationTokenSource()){
    cancelled.Cancel();var propagated=false;
    try{await new HealthController(new HealthDatabase("cancel")).CheckDatabase(cancelled.Token);}catch(OperationCanceledException){propagated=true;}
    Check(propagated,"health propagates request cancellation");
}
var services=app.Services;
{
    using var cancelled=new CancellationTokenSource();cancelled.Cancel();
    var accessor=new HttpContextAccessor{HttpContext=new DefaultHttpContext{RequestAborted=cancelled.Token}};
    var provider=new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()),services.GetRequiredService<IServiceScopeFactory>(),accessor);
    var propagated=false;try{await provider.GetPolicyAsync(PermissionPolicy.Name("ROOM_VIEW"));}catch(OperationCanceledException){propagated=true;}
    Check(propagated&&catalog.LastToken.IsCancellationRequested,"policy catalog observes aborted request; never grants");
    accessor.HttpContext=null;
    Check(await provider.GetPolicyAsync(PermissionPolicy.Name("ROOM_VIEW")) is not null&&catalog.LastToken.CanBeCanceled,"non-HTTP policy lookup retains bounded cancellable token");
    catalog.Block=true;var timedOut=false;
    try{await provider.GetPolicyAsync(PermissionPolicy.Name("ROOM_VIEW")).WaitAsync(TimeSpan.FromSeconds(10));}
    catch(OperationCanceledException){timedOut=catalog.LastToken.IsCancellationRequested;}
    Check(timedOut,"policy catalog 5-second budget cancels stalled lookup without granting");
}
if(args.Contains("--readonly-probe")){
    try{await using var db=await ReadOnlyVerification.OpenAsync(CancellationToken.None);Check(true,"dedicated read-only principal accepted");}
    catch(InvalidOperationException error)when(error.Message.StartsWith("READ_ONLY_")){Console.WriteLine("BLOCKED: "+error.Message);Environment.ExitCode=2;}
}
Console.WriteLine($"PASS: {count} hardening checks; health uses database doubles, no SQL session creation.");

sealed class HealthDatabase : HotelOpsDbContext{
    private readonly DatabaseFacade facade;
    public HealthDatabase(string scenario):base(new DbContextOptionsBuilder<HotelOpsDbContext>().Options){facade=new HealthFacade(this,scenario);}
    public override DatabaseFacade Database=>facade;
}
sealed class HealthFacade(DbContext db,string scenario):DatabaseFacade(db){
    public override Task<bool> CanConnectAsync(CancellationToken ct=default){ct.ThrowIfCancellationRequested();return scenario switch{"exception"=>throw new InvalidOperationException("sentinel infrastructure failure; HotelOpsDB"),"healthy"=>Task.FromResult(true),_=>Task.FromResult(false)};}
}
sealed class CancellationCatalog:IPermissionPolicyCatalog{
    public CancellationToken LastToken{get;private set;}
    public bool Block{get;set;}
    public async Task<bool> ContainsAsync(string code,CancellationToken ct){LastToken=ct;ct.ThrowIfCancellationRequested();if(Block)await Task.Delay(Timeout.InfiniteTimeSpan,ct);return true;}
}
