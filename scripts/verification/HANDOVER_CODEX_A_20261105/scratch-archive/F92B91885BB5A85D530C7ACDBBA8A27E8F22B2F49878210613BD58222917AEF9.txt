using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Authentication;
using HotelOps.Api.Middleware;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var directory=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../.."));
using var runLock=new FileStream(Path.Combine(directory,"run.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None,1,FileOptions.DeleteOnClose);
using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(8));
var ct=timeout.Token;
var target=new TestDatabase(Path.Combine(directory,"environment.json"));
var fixtures=await Fixtures.Prepare(target,Path.Combine(directory,"fixtures.json"),ct);
using var rsa=RSA.Create(2048);
var certificateRequest=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
var san=new SubjectAlternativeNameBuilder();san.AddIpAddress(IPAddress.Loopback);san.AddDnsName("localhost");certificateRequest.CertificateExtensions.Add(san.Build());
using var generatedCertificate=certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));
// Schannel requires a named user key container rather than an ephemeral CNG handle.
using var certificate=X509CertificateLoader.LoadPkcs12(generatedCertificate.Export(X509ContentType.Pfx),null,X509KeyStorageFlags.UserKeySet);
var builder=WebApplication.CreateBuilder(new WebApplicationOptions {EnvironmentName="Development",ApplicationName=typeof(Fixtures).Assembly.FullName});
builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(k=>k.Listen(IPAddress.Loopback,0,l=>l.UseHttps(certificate)));
builder.Services.AddDbContext<HotelOpsDbContext>(o=>o.UseSqlServer(target.ConnectionString).AddInterceptors(target));
builder.Services.Configure<PasswordHasherOptions>(o=>{o.CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3;o.IterationCount=PasswordCredentialService.BaselineIterationCount;});
builder.Services.AddScoped<IPasswordCredentialService,PasswordCredentialService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICredentialLockoutService,CredentialLockoutService>();
builder.Services.AddScoped<ISessionRevocationService,SessionRevocationService>();
builder.Services.AddScoped<ITemporaryCredentialService,TemporaryCredentialService>();
builder.Services.AddScoped<ISqlSessionService,SqlSessionService>();
builder.Services.AddScoped<AuthenticationLoginService>();
builder.Services.AddSingleton<SessionJournal>();
builder.Services.AddScoped<IAuthenticationLoginService,ObservedLogin>();
builder.Services.AddScoped<IAuthenticationCredentialWorkflow,AuthenticationCredentialWorkflow>();
builder.Services.AddHotelOpsCookieInfrastructure(builder.Environment);
// Only key persistence is isolated. Cookie events and SQL validators are production implementations.
builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
builder.Services.AddHotelOpsAntiforgery();builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor,CurrentUserAccessor>();
builder.Services.AddScoped<IPermissionDataSource,EfPermissionDataSource>();
builder.Services.AddScoped<ICleaningTaskAccessReader,EfCleaningTaskAccessReader>();
builder.Services.AddScoped<IPermissionResourceResolver,CleaningTaskResourceResolver>();
builder.Services.AddScoped<IPermissionService,PermissionService>();
builder.Services.AddHotelOpsPermissionAuthorization();
builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly).ConfigureApiBehaviorOptions(AuthenticationApiBehavior.Configure);
await using var app=builder.Build();
app.UseMiddleware<AuthenticationExceptionMiddleware>();app.UseRouting();app.UseAuthentication();
app.UseMiddleware<InvalidCookieMiddleware>();app.UseMiddleware<ApiCsrfMiddleware>();app.UseMiddleware<RestrictedSessionMiddleware>();
app.Use(async (context,next)=>{
    if(context.Request.RouteValues.TryGetValue("id",out var raw)&&long.TryParse(raw?.ToString(),out var id)) context.Features.Set(new PermissionResource(CleaningTaskResourceResolver.ResourceType,id));
    TestDatabase.InjectSqlFailure.Value=context.Request.Path.StartsWithSegments("/api/integration/sql-failure");
    try { await next(context); } finally { TestDatabase.InjectSqlFailure.Value=false; }
});
app.UseAuthorization();app.UseMiddleware<SessionTouchMiddleware>();app.MapControllers();
app.MapGet("/api/integration/identity",(ICurrentUserAccessor current)=>current.TryGetCurrentUser(out _)?Results.Ok():Results.Unauthorized()).RequireAuthorization();
app.MapGet("/api/integration/cleaning/{id:long}",()=>Results.Ok()).RequireAuthorization(PermissionPolicy.Name("CLEANING_EXECUTE"));
app.MapPost("/api/integration/cleaning/{id:long}",()=>Results.NoContent()).RequireAuthorization(PermissionPolicy.Name("CLEANING_EXECUTE"));
app.MapGet("/api/integration/multiple/{id:long}",()=>Results.Ok()).RequireAuthorization(PermissionPolicy.Name("CLEANING_EXECUTE")).RequireAuthorization(p=>p.RequireClaim("never-issued"));
app.MapGet("/api/integration/sql-failure/{id:long}",()=>Results.Ok()).RequireAuthorization(PermissionPolicy.Name("CLEANING_EXECUTE"));
await app.StartAsync(ct);
var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
using var handler=new HttpClientHandler {UseCookies=false,AllowAutoRedirect=false,ServerCertificateCustomValidationCallback=(_,cert,_,_)=>cert?.GetCertHashString()==certificate.GetCertHashString()};
using var client=new HttpClient(handler){BaseAddress=new Uri(address)};
var secrets=TestDatabase.Configuration();
var results=new List<string>();
void Check(bool ok,string name){if(!ok)throw new InvalidOperationException("FAIL: "+name);results.Add(name);Console.WriteLine("PASS: "+name);}
async Task<HttpResponseMessage> Send(string method,string path,Jar jar,object? body=null,bool csrf=true){
    using var request=new HttpRequestMessage(new HttpMethod(method),path);
    if(jar.Cookies.Count>0)request.Headers.Add("Cookie",string.Join("; ",jar.Cookies.Select(p=>p.Key+"="+p.Value)));
    if(csrf&&jar.Token is not null)request.Headers.Add(HotelOpsAntiforgery.HeaderName,jar.Token);
    if(body is not null)request.Content=JsonContent.Create(body);
    var response=await client.SendAsync(request,ct);jar.Read(response);return response;
}
async Task<Jar> Login(string name){
    var jar=new Jar();using(var bootstrap=await Send("GET","/api/auth/csrf",jar)){
        Check(bootstrap.StatusCode==HttpStatusCode.OK,"CSRF bootstrap "+name);
        using var json=JsonDocument.Parse(await bootstrap.Content.ReadAsStringAsync(ct));jar.Token=json.RootElement.GetProperty("csrfToken").GetString();
    }
    using var response=await Send("POST","/api/auth/login",jar,new {Username="it06."+name,Password=secrets["Passwords:"+name]});
    Check(response.StatusCode==HttpStatusCode.OK,"real credential login + SQL commit "+name);
    using var loginJson=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));jar.Token=loginJson.RootElement.GetProperty("csrfToken").GetString();
    Check(loginJson.RootElement.GetProperty("mustChangePassword").GetBoolean()==(name=="restricted"),"restricted marker in real session response "+name);
    Check(response.Headers.TryGetValues("Set-Cookie",out var cookies)&&cookies.Any(c=>c.StartsWith(HotelOpsCookieTicket.CookieName+"=")&&c.Contains("secure",StringComparison.OrdinalIgnoreCase)&&c.Contains("httponly",StringComparison.OrdinalIgnoreCase)),"secure HttpOnly authentication cookie "+name);
    return jar;
}
async Task Expect(Jar jar,string path,int status,string name,string method="GET",bool csrf=true,string? code=null){
    using var response=await Send(method,path,jar,csrf:csrf);Check((int)response.StatusCode==status,name+" HTTP "+status);Check(response.Headers.Location is null,name+" no redirect");
    if(status>=400){var body=await response.Content.ReadAsStringAsync(ct);Check(!body.Contains("SqlException")&&!body.Contains("51907")&&!body.Contains("stackTrace",StringComparison.OrdinalIgnoreCase)&&!body.Contains(target.ConnectionString,StringComparison.Ordinal),name+" no internal error disclosure");}
    if(code is not null){using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));Check(json.RootElement.GetProperty("code").GetString()==code,name+" problem code "+code);}
}
async Task State(string status){await using var db=target.Open();var id=fixtures.Users["inactive"];Check(await db.Users.Where(u=>u.UserId==id&&u.Username=="it06.inactive").ExecuteUpdateAsync(s=>s.SetProperty(u=>u.AccountStatus,status),CancellationToken.None)==1,"owned inactive fixture status "+status);}
try{
    await Expect(new Jar(),"/api/integration/identity",401,"anonymous");
    var invalid=new Jar();invalid.Cookies[HotelOpsCookieTicket.CookieName]="invalid";await Expect(invalid,"/api/integration/identity",401,"invalid cookie");
    var assigned=await Login("assigned");
    var assignedSession=app.Services.GetRequiredService<SessionJournal>().Sessions.Single(s=>s.Session.UserId==fixtures.Users["assigned"]);
    await Task.Delay(TimeSpan.FromMilliseconds(1100),ct);
    await Expect(assigned,"/api/integration/identity",200,"validated current user and session touch");
    await using(var sessionDb=target.Open()){
        var row=await sessionDb.UserSessions.AsNoTracking().SingleAsync(s=>s.SessionId==assignedSession.Session.SessionId,ct);
        Check(row.LastUsedAt>assignedSession.Session.CreatedAtUtc&&row.ExpiresAt>assignedSession.Session.ExpiresAtUtc,"SQL last_used_at and idle expiry extended by real session toucher");
    }
    await Expect(assigned,$"/api/integration/cleaning/{fixtures.TaskA}",200,"OWN current assignee");
    await Expect(assigned,$"/api/integration/cleaning/{fixtures.TaskB}?branchId={fixtures.BranchA}&owner=true",403,"cross branch client spoof rejected");
    await Expect(assigned,"/api/integration/cleaning/9223372036854775807",403,"missing resource fail closed");
    await Expect(assigned,$"/api/integration/multiple/{fixtures.TaskA}",403,"additional requirement preserved");
    await Expect(assigned,$"/api/integration/sql-failure/{fixtures.TaskA}",403,"controlled real SQL engine read error fails closed");
    Check(TestDatabase.InjectedFailures==1,"controlled failure reached actual SQL resource command");
    await Expect(assigned,$"/api/integration/cleaning/{fixtures.TaskA}",400,"CSRF missing","POST",false,"CSRF_VALIDATION_FAILED");
    var goodToken=assigned.Token;assigned.Token="invalid";await Expect(assigned,$"/api/integration/cleaning/{fixtures.TaskA}",400,"CSRF invalid","POST");assigned.Token=goodToken;
    await Expect(assigned,$"/api/integration/cleaning/{fixtures.TaskA}",204,"CSRF valid protected POST probe","POST");
    var expired=assigned.Copy();var options=app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(HotelOpsCookieTicket.Scheme);
    var ticket=options.TicketDataFormat.Unprotect(expired.Cookies[HotelOpsCookieTicket.CookieName])!;ticket.Properties.ExpiresUtc=DateTimeOffset.UtcNow.AddMinutes(-1);expired.Cookies[HotelOpsCookieTicket.CookieName]=options.TicketDataFormat.Protect(ticket);
    await Expect(expired,"/api/integration/identity",401,"cryptographically valid expired cookie");
    var former=await Login("former");await Expect(former,$"/api/integration/cleaning/{fixtures.TaskA}",403,"former assignee denied");
    var none=await Login("none");await Expect(none,$"/api/integration/cleaning/{fixtures.TaskNone}",403,"resource owner without permission denied");
    var mixed=await Login("mixed");await Expect(mixed,$"/api/integration/cleaning/{fixtures.TaskMixed}",403,"no role branch grant mixing");
    var branchb=await Login("branchb");await Expect(branchb,$"/api/integration/cleaning/{fixtures.TaskB}",200,"OWN valid branch B");
    var restricted=await Login("restricted");await Expect(restricted,"/api/auth/session",200,"restricted session allowed self inspection");
    await Expect(restricted,"/api/integration/identity",403,"restricted middleware blocks otherwise permitted authenticated probe",code:"PASSWORD_CHANGE_REQUIRED");
    await Expect(restricted,$"/api/integration/cleaning/{fixtures.TaskA}",403,"restricted middleware blocks business",code:"PASSWORD_CHANGE_REQUIRED");
    var revoked=await Login("revoked");var stale=revoked.Copy();await Expect(revoked,"/api/auth/logout",204,"real logout revokes SQL session","POST");await Expect(stale,"/api/integration/identity",401,"revoked SQL session cookie replay");
    await State("ACTIVE");var inactive=await Login("inactive");await State("INACTIVE");await Expect(inactive,"/api/integration/identity",401,"inactive account invalidates real session");
    await using var scope=app.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<HotelOpsDbContext>();
    Check(await db.Roles.CountAsync(ct)==12&&await db.Roles.CountAsync(r=>r.IsActive,ct)==10&&await db.Permissions.CountAsync(ct)==62&&await db.RolePermissions.CountAsync(ct)==140,"canonical RBAC catalog unchanged");
}finally{
    await State("INACTIVE");
    foreach(var created in app.Services.GetRequiredService<SessionJournal>().Sessions){
        await using var scope=app.Services.CreateAsyncScope();await scope.ServiceProvider.GetRequiredService<IAuthenticationCredentialWorkflow>().LogoutAsync(created.Ticket,CancellationToken.None);
    }
    await using var db=target.Open();var ids=app.Services.GetRequiredService<SessionJournal>().Sessions.Select(s=>s.Session.SessionId).ToArray();
    Check(!await db.UserSessions.AnyAsync(s=>ids.Contains(s.SessionId)&&s.RevokedAt==null,CancellationToken.None),"all sessions created by this run revoked via FE01");
    await app.StopAsync(CancellationToken.None);
}
await File.WriteAllTextAsync(Path.Combine(directory,"results.json"),JsonSerializer.Serialize(new{database=TestDatabase.Name,passed=results.Count,cases=results,completedUtc=DateTime.UtcNow},new JsonSerializerOptions{WriteIndented=true}),ct);
Console.WriteLine($"PASS: {results.Count} real HTTPS + SQL assertions");

internal sealed class Jar{
    internal Dictionary<string,string> Cookies {get;}=new();internal string? Token {get;set;}
    internal void Read(HttpResponseMessage response){if(response.Headers.TryGetValues("Set-Cookie",out var values))foreach(var value in values){var pair=value.Split(';')[0].Split('=',2);if(pair.Length==2){if(pair[1].Length==0)Cookies.Remove(pair[0]);else Cookies[pair[0]]=pair[1];}}}
    internal Jar Copy(){var copy=new Jar{Token=Token};foreach(var pair in Cookies)copy.Cookies.Add(pair.Key,pair.Value);return copy;}
}
internal sealed class SessionJournal{internal ConcurrentBag<CreatedSqlSession> Sessions {get;}=new();}
internal sealed class ObservedLogin(AuthenticationLoginService actual,SessionJournal journal):IAuthenticationLoginService{
    public async Task<LoginResult> LoginAsync(LoginRequest request,CancellationToken ct=default){var result=await actual.LoginAsync(request,ct);if(result.CreatedSession is{} created)journal.Sessions.Add(created);return result;}
    public Task<CurrentSession?> GetSessionAsync(SessionReference reference,long id,CancellationToken ct=default)=>actual.GetSessionAsync(reference,id,ct);
}
