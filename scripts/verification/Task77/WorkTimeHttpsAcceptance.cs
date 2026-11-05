using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.Middleware;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;

// Verification-only adapter. Never included in the production API assembly or mapped by Program.cs.
// It uses existing CLEANING_EXECUTE source authorization to exercise a trusted caller boundary;
// it does not define the public UC139 permission binding.
internal static class WorkTimeHttpsAcceptance
{
    public static async Task RunAsync(TimeTrial t, CancellationToken ct)
    {
        using var rsa=RSA.Create(2048);
        var request=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        var san=new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback); request.CertificateExtensions.Add(san.Build());
        using var generated=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));
        var certificatePassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        // Windows Schannel needs an imported key container; self-signed ephemeral RSA handles
        // can close the TLS handshake. No PFX file, persisted key flag or secret log is used.
        using var certificate=X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx,certificatePassword),
            certificatePassword,X509KeyStorageFlags.UserKeySet);
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName="Development",Args=[] });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options=>options.Listen(IPAddress.Loopback,0,listen=>listen.UseHttps(certificate)));
        var s=builder.Services;
        s.AddDbContext<HotelOpsDbContext>(options=>options.UseSqlServer(t.ConnectionString));
        s.AddSingleton<TimeProvider>(TimeProvider.System);
        s.Configure<PasswordHasherOptions>(o=>o.IterationCount=PasswordCredentialService.BaselineIterationCount);
        s.AddScoped<IPasswordCredentialService,PasswordCredentialService>();
        s.AddScoped<ICredentialLockoutService,CredentialLockoutService>();
        s.AddScoped<ISessionRevocationService,SessionRevocationService>();
        s.AddScoped<ISqlSessionService,SqlSessionService>();
        s.AddScoped<IAuthenticationLoginService,AuthenticationLoginService>();
        s.AddScoped<IAuthenticationCredentialWorkflow,AuthenticationCredentialWorkflow>();
        s.AddHotelOpsCookieInfrastructure(builder.Environment);
        s.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        s.AddHotelOpsAntiforgery(); s.AddHttpContextAccessor();
        s.AddScoped<ICurrentUserAccessor,CurrentUserAccessor>();
        s.AddScoped<IPermissionDataSource,EfPermissionDataSource>();
        s.AddScoped<ICleaningTaskAccessReader,EfCleaningTaskAccessReader>();
        s.AddScoped<IPermissionResourceResolver,CleaningTaskResourceResolver>();
        s.AddScoped<IPermissionService,PermissionService>();
        s.AddHotelOpsPermissionAuthorization();
        s.AddScoped<IWorkTimeTransactionParticipant,SqlWorkTimeTransactionParticipant>();
        s.AddControllers().AddApplicationPart(typeof(AuthController).Assembly)
            .ConfigureApplicationPartManager(m=>m.FeatureProviders.Add(new AuthOnly()));
        await using var app=builder.Build();
        app.UseMiddleware<AuthenticationExceptionMiddleware>(); app.UseAuthentication();
        app.UseMiddleware<InvalidCookieMiddleware>(); app.UseMiddleware<ApiCsrfMiddleware>();
        app.UseMiddleware<RestrictedSessionMiddleware>(); app.UseAuthorization(); app.UseMiddleware<SessionTouchMiddleware>();
        app.MapControllers();
        app.MapPost("/api/verification/task77/cleaning/{id:long}/{action}", Drive)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes=HotelOpsCookieTicket.Scheme });
        await app.StartAsync(ct);
        var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var handler=new HttpClientHandler { UseCookies=false,AllowAutoRedirect=false,
            ServerCertificateCustomValidationCallback=(_,cert,_,_)=>cert?.GetCertHashString()==certificate.GetCertHashString() };
        using var http=new HttpClient(handler) { BaseAddress=new Uri(address),Timeout=TimeSpan.FromSeconds(30) };
        async Task<HttpResponseMessage> Send(HttpMethod method,string path,Jar jar,object? body=null,bool csrf=true)
        {
            using var message=new HttpRequestMessage(method,path);
            if(jar.Cookies.Count>0) message.Headers.Add("Cookie",string.Join("; ",jar.Cookies.Select(x=>x.Key+"="+x.Value)));
            if(csrf&&jar.Token is not null) message.Headers.Add(HotelOpsAntiforgery.HeaderName,jar.Token);
            if(body is not null) message.Content=JsonContent.Create(body);
            var response=await http.SendAsync(message,ct); jar.Read(response); return response;
        }
        async Task<Jar> Login(string name,bool login=true)
        {
            var jar=new Jar();
            using(var bootstrap=await Send(HttpMethod.Get,"/api/auth/csrf",jar))
            {
                t.Check(bootstrap.IsSuccessStatusCode,"HTTPS production CSRF bootstrap "+name);
                using var json=JsonDocument.Parse(await bootstrap.Content.ReadAsStringAsync(ct)); jar.Token=json.RootElement.GetProperty("csrfToken").GetString();
            }
            if(!login) return jar;
            using var response=await Send(HttpMethod.Post,"/api/auth/login",jar,new { Username=t.Users[name].Username,Password=t.Password });
            t.Check(response.IsSuccessStatusCode,"HTTPS production credential login/SQL session "+name);
            using var result=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); jar.Token=result.RootElement.GetProperty("csrfToken").GetString();
            return jar;
        }
        async Task<JsonElement?> Expect(Jar jar,string action,int status,string label,object? body=null,bool csrf=true,long? taskId=null)
        {
            var path=$"/api/verification/task77/cleaning/{taskId??t.Tasks["http"].CleaningTaskId}/{action}";
            using var response=await Send(HttpMethod.Post,path,jar,body??new Command(),csrf);
            var text=await response.Content.ReadAsStringAsync(ct);
            t.Check((int)response.StatusCode==status&&response.Headers.Location is null,label+" status");
            t.Check(!text.Contains("SqlException",StringComparison.OrdinalIgnoreCase)&&!text.Contains("sessionKey",StringComparison.OrdinalIgnoreCase),label+" no provider/session disclosure");
            if(!response.IsSuccessStatusCode) return null;
            using var json=JsonDocument.Parse(text); return json.RootElement.Clone();
        }
        try
        {
            var anonymous=await Login("http",false); var owner=await Login("http");
            var foreign=await Login("other"); var denied=await Login("denied"); var peer=await Login("own");
            await Expect(anonymous,"start-day",401,"Anonymous cannot invoke component caller");
            await Expect(owner,"start-day",400,"Real CSRF failure prevents time writes",csrf:false);
            await Expect(denied,"start-day",403,"User without SQL grant cannot use source");
            await Expect(foreign,"start-day",403,"Cross-branch user denied");
            await Expect(peer,"start-day",403,"Same-branch other assignee denied");
            var day=(await Expect(owner,"start-day",200,"HTTPS scheduled foundation start"))!.Value;
            await Expect(owner,"start-day",409,"HTTPS repeated day start");
            var opened=(await Expect(owner,"open",200,"HTTPS production SQL participant opens interval"))!.Value;
            await Expect(owner,"open",409,"HTTPS repeated Open conflicts");
            var dayId=day.GetProperty("id").GetInt64(); var dayVersion=day.GetProperty("version").GetString();
            var intervalId=opened.GetProperty("id").GetInt64(); var version=opened.GetProperty("version").GetString();
            await Expect(owner,"end-day",409,"HTTPS End refuses open interval",new Command(dayId,dayVersion));
            await Expect(owner,"close",412,"HTTPS stale interval version",new Command(intervalId,Convert.ToBase64String(new byte[8])));
            await Expect(owner,"close",200,"HTTPS close persists",new Command(intervalId,version));
            await Expect(owner,"end-day",200,"HTTPS End persists",new Command(dayId,dayVersion));
            await using(var check=t.Open())
            {
                t.Check(await check.WorkIntervals.AnyAsync(x=>x.IntervalId==intervalId&&x.ClosedAt!=null,ct)
                    &&await check.Workdays.AnyAsync(x=>x.WorkdayId==dayId&&x.Status=="ENDED",ct),"HTTPS component writes verified by independent SQL context");
                await check.UserRoles.Where(x=>x.UserId==t.Users["http"].UserId).ExecuteUpdateAsync(u=>u.SetProperty(x=>x.IsActive,false),ct);
            }
            await Expect(owner,"start-day",403,"SQL role revocation respected by next HTTPS source call");
            await using(var check=t.Open()) await check.UserSessions.Where(x=>x.UserId==t.Users["http"].UserId)
                .ExecuteUpdateAsync(u=>u.SetProperty(x=>x.RevokedAt,DateTime.UtcNow),ct);
            await Expect(owner,"start-day",401,"SQL session revocation invalidates real cookie");
        }
        finally { await app.StopAsync(CancellationToken.None); }
    }

    private static async Task<IResult> Drive(long id,string action,Command input,HttpContext http,HotelOpsDbContext db,
        IWorkTimeTransactionParticipant participant,IPermissionService permissions,ICurrentUserAccessor current,CancellationToken ct)
    {
        if(!current.TryGetCurrentUser(out var user)) return Results.StatusCode(401);
        // Production middleware/accessor has already bound the SQL snapshot to the protected cookie.
        Task<bool> SessionStillValid() => db.UserSessions.AsNoTracking().AnyAsync(x=>x.SessionId==user.SessionId
            &&x.UserId==user.UserId&&x.RevokedAt==null&&x.ExpiresAt>DateTime.UtcNow,ct);
        var resource=new PermissionResource("cleaning_task",id);
        if(!(await permissions.EvaluateResourceAsync("CLEANING_EXECUTE",resource,ct)).IsGranted) return Results.StatusCode(403);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        try
        {
            await participant.LockUsersAsync([user.UserId],ct);
            var task=await db.CleaningTasks.FromSqlInterpolated($"SELECT * FROM dbo.cleaning_tasks WITH(UPDLOCK,HOLDLOCK) WHERE cleaning_task_id={id}")
                .AsNoTracking().Select(x=>new {x.CleaningTaskId,x.Room.BranchId}).SingleOrDefaultAsync(ct);
            if(task is null) return Results.StatusCode(403);
            if(!await SessionStillValid()
                ||!(await permissions.EvaluateResourceAsync("CLEANING_EXECUTE",resource,ct)).IsGranted) return Results.StatusCode(403);
            var sql=(SqlWorkTimeTransactionParticipant)participant;
            object result;
            var operation=new WorkTimeOperation(user.UserId,user.UserId,task.BranchId,"CLEANING",id);
            if(action=="start-day")
            {
                var assignment=await db.ShiftAssignments.AsNoTracking().Where(x=>x.UserId==user.UserId&&x.Shift.BranchId==task.BranchId&&x.AssignmentStatus=="ASSIGNED")
                    .Select(x=>x.ShiftAssignmentId).SingleAsync(ct);
                var day=await sql.StartScheduledWorkdayAsync(user.UserId,task.BranchId,assignment,ct);
                result=new {id=day.WorkdayId,version=Convert.ToBase64String(day.RowVersion),day.Status,day.StartedAt};
            }
            else if(action=="end-day")
            {
                var day=await sql.EndWorkdayAsync(user.UserId,task.BranchId,input.Id,Convert.FromBase64String(input.Version??""),ct);
                result=new {id=day.WorkdayId,version=Convert.ToBase64String(day.RowVersion),day.Status,day.EndedAt};
            }
            else if(action is "open" or "close")
            {
                var row=action=="open" ? await participant.OpenIntervalAsync(operation,ct)
                    : await participant.CloseIntervalAsync(operation,input.Id,Convert.FromBase64String(input.Version??""),ct);
                result=new {id=row.IntervalId,version=Convert.ToBase64String(row.RowVersion),row.OpenedAtUtc,row.ClosedAtUtc};
            }
            else return Results.NotFound();
            if(!await SessionStillValid()
                ||!(await permissions.EvaluateResourceAsync("CLEANING_EXECUTE",resource,ct)).IsGranted) return Results.StatusCode(403);
            await tx.CommitAsync(ct); return Results.Json(result);
        }
        catch(WorkTimeContractException error)
        {
            await tx.RollbackAsync(ct);
            return Results.Json(new {error.Code},statusCode:error.Code=="TIME_STALE"?412:error.Code=="TIME_SCOPE_DENIED"?403:409);
        }
        catch(FormatException) { await tx.RollbackAsync(ct); return Results.BadRequest(); }
    }
    internal sealed record Command(long Id=0,string? Version=null);
    private sealed class AuthOnly:IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts,ControllerFeature feature)
        {
            foreach(var controller in feature.Controllers.ToArray())
                if(controller.AsType()!=typeof(AuthController)&&controller.AsType()!=typeof(CsrfController)) feature.Controllers.Remove(controller);
        }
    }
    private sealed class Jar
    {
        public Dictionary<string,string> Cookies {get;}=[];
        public string? Token {get;set;}
        public void Read(HttpResponseMessage response)
        {
            if(!response.Headers.TryGetValues("Set-Cookie",out var values)) return;
            foreach(var value in values) {var pair=value.Split(';')[0].Split('=',2); if(pair.Length==2) Cookies[pair[0]]=pair[1];}
        }
    }
}
