using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

await new ExpiryLive().Run(args);
sealed class ExpiryLive
{
    readonly string root=Directory.GetCurrentDirectory();readonly List<string> cases=[];
    readonly string password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))+"Aa1-";
    readonly Dictionary<string,User> users=[];string connection="";CancellationToken ct;HttpClient http=null!;
    readonly string prefix="t79."+Guid.NewGuid().ToString("N")[..8]+".";
    HotelOpsDbContext Db()=>new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
    void Check(bool ok,string label){if(!ok)throw new InvalidOperationException("FAIL "+label);cases.Add(label);Console.WriteLine("PASS "+label);}
    public async Task Run(string[] args)
    {
        const string approvedTrial="HotelOps_Canonical_20261104_063549_81d4bc64_Fresh";
        if(args.Length!=2||args[0] is not("before" or "after")||File.Exists(args[1]))throw new ArgumentException("phase and unused receipt required");
        var source=Environment.GetEnvironmentVariable("TASK79_TRIAL_SQL_SOURCE")??throw new Exception("Explicit SQL source required");
        connection=new SqlConnectionStringBuilder{DataSource=source,InitialCatalog=approvedTrial,IntegratedSecurity=true,TrustServerCertificate=true,ConnectTimeout=10,ApplicationName="HotelOps Task79 expiry trial"}.ConnectionString;
        using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(4));ct=deadline.Token;
        await using(var db=Db())
        {
            var actual=await db.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),DB_NAME()) AS [Value]").SingleAsync(ct);
            var marker=await db.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),value) AS [Value] FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.WorkTime.Contract'").SingleAsync(ct);
            if(actual!=approvedTrial||marker!="Task77-v1")throw new Exception("Approved disposable trial rejected");
            var hash=new PasswordCredentialService(Options.Create(new PasswordHasherOptions{IterationCount=PasswordCredentialService.BaselineIterationCount}));
            var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct),DateTimeKind.Utc);
            foreach(var name in args[0]=="before"?new[]{"short"}:new[]{"normal","long","exact","short","near","expired","change"})
            {
                var u=new User{Username=prefix+name,EmployeeCode="T79_"+Guid.NewGuid().ToString("N")[..10],FullName="Expiry fixture "+name,
                    Email=prefix+name+"@example.invalid",AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",CreatedAt=now};
                u.TemporaryPasswordExpiresAt=name switch{"normal"=>null,"long"=>now.AddMinutes(30),"exact"=>now.AddMinutes(15),"expired"=>now.AddMinutes(-1),_=>now.AddMinutes(10)};
                u.PasswordHash=hash.HashPassword(u,password);db.Add(u);users[name]=u;
            }
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            var sqlUser=await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==users["short"].UserId,ct);
            Check(sqlUser.TemporaryPasswordExpiresAt!.Value.Kind==DateTimeKind.Unspecified,"Actual SQL datetime2 materializes temporary expiry as Unspecified");
        }
        using var rsa=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        var san=new SubjectAlternativeNameBuilder();san.AddIpAddress(IPAddress.Loopback);request.CertificateExtensions.Add(san.Build());
        using var certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddMinutes(20));
        var certPassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));var path=Path.Combine(root,"obj","task79-"+Guid.NewGuid().ToString("N")+".pfx");
        await File.WriteAllBytesAsync(path,certificate.Export(X509ContentType.Pfx,certPassword),ct);
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.Combine(root,"src/HotelOps.Api"),RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add(Path.Combine(root,"src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll"));
        start.Environment["DOTNET_ENVIRONMENT"]="Development";start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";
        start.Environment["ASPNETCORE_URLS"]="https://127.0.0.1:"+port;start.Environment["ConnectionStrings__HotelOpsDb"]=connection;
        start.Environment["Kestrel__Certificates__Default__Path"]=path;start.Environment["Kestrel__Certificates__Default__Password"]=certPassword;
        start.Environment["Logging__LogLevel__Microsoft.AspNetCore.Authentication"]="Debug";
        Process? process=null;Task<string>? stdout=null,stderr=null;
        try
        {
            process=Process.Start(start)??throw new Exception("API launch failed");stdout=process.StandardOutput.ReadToEndAsync();stderr=process.StandardError.ReadToEndAsync();
            using var handler=new HttpClientHandler{UseCookies=false,AllowAutoRedirect=false,ServerCertificateCustomValidationCallback=(_,c,_,_)=>c?.GetCertHashString()==certificate.GetCertHashString()};
            using var client=new HttpClient(handler){BaseAddress=new Uri("https://127.0.0.1:"+port),Timeout=TimeSpan.FromSeconds(30)};http=client;
            var ready=false;for(var i=0;i<100&&!process.HasExited;i++){try{using var r=await http.GetAsync("/api/health/database",ct);if(r.IsSuccessStatusCode){ready=true;break;}}catch(HttpRequestException){}await Task.Delay(150,ct);}
            Check(ready,"Actual production Program HTTPS startup");
            if(args[0]=="before")
            {
                var jar=await Login("short",200);
                using var response=await Send(HttpMethod.Get,"/api/auth/session",jar);
                Check(response.StatusCode==HttpStatusCode.Unauthorized,"BASELINE DEFECT reproduced: valid short credential login200 followed by cookie session401 on UTC+07");
            }
            else await Acceptance();
            await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(new{phase=args[0],success=true,checks=cases.Count,cases,
                trial=approvedTrial,actualProductionProgram=true,operatingSystemTimezone=TimeZoneInfo.Local.Id,
                utcOperatingSystemExecuted=false,finishedUtc=DateTimeOffset.UtcNow},new JsonSerializerOptions{WriteIndented=true}),ct);
        }
        finally
        {
            if(process is not null){if(!process.HasExited)process.Kill(entireProcessTree:true);await process.WaitForExitAsync();process.Dispose();}
            if(stdout is not null)await File.WriteAllTextAsync(Path.Combine(root,"obj/task79-"+args[0]+"-production.stdout.local.log"),await stdout);
            if(stderr is not null)await File.WriteAllTextAsync(Path.Combine(root,"obj/task79-"+args[0]+"-production.stderr.local.log"),await stderr);
            File.Delete(path);
        }
    }
    async Task<HttpResponseMessage> Send(HttpMethod method,string path,Jar jar,object? body=null)
    {
        using var message=new HttpRequestMessage(method,path);if(jar.Cookies.Count>0)message.Headers.Add("Cookie",string.Join("; ",jar.Cookies.Select(x=>x.Key+"="+x.Value)));
        if(jar.Token is not null)message.Headers.Add(HotelOpsAntiforgery.HeaderName,jar.Token);if(body is not null)message.Content=JsonContent.Create(body);
        var r=await http.SendAsync(message,ct);if(r.Headers.TryGetValues("Set-Cookie",out var cookies))foreach(var header in cookies){var p=header.Split(';')[0].Split('=',2);if(p.Length==2)jar.Cookies[p[0]]=p[1];}return r;
    }
    async Task<Jar> Login(string name,int status)
    {
        var jar=new Jar();using(var bootstrap=await Send(HttpMethod.Get,"/api/auth/csrf",jar)){Check(bootstrap.IsSuccessStatusCode,"CSRF bootstrap "+name);using var j=JsonDocument.Parse(await bootstrap.Content.ReadAsStringAsync(ct));jar.Token=j.RootElement.GetProperty("csrfToken").GetString();}
        using var r=await Send(HttpMethod.Post,"/api/auth/login",jar,new{Username=users[name].Username,Password=password});
        Check((int)r.StatusCode==status,"Real HTTPS credential login "+name+" expected "+status);
        if(r.IsSuccessStatusCode){using var j=JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));jar.Token=j.RootElement.GetProperty("csrfToken").GetString();}
        return jar;
    }
    async Task Status(Jar jar,string path,int status,string label)
    {using var r=await Send(HttpMethod.Get,path,jar);Check((int)r.StatusCode==status,label);}
    async Task Acceptance()
    {
        foreach(var name in new[]{"normal","long","exact","short"})
        {
            var jar=await Login(name,200);await Status(jar,"/api/auth/session",200,"Cookie/SQL session round trip "+name);
            if(name!="normal")await Status(jar,"/api/branches/1/workdays/current",403,"Restricted cookie blocked from business API "+name);
            await using var db=Db();var u=await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==users[name].UserId,ct);
            var s=await db.UserSessions.AsNoTracking().SingleAsync(s=>s.UserId==u.UserId&&s.RevokedAt==null,ct);
            var expected=u.TemporaryPasswordExpiresAt is{} cap?new[]{s.CreatedAt.AddMinutes(15),cap}.Min():s.CreatedAt.AddHours(12);
            Check(s.ExpiresAt<=expected&&s.ExpiresAt>s.CreatedAt,"SQL session lifetime never exceeds original cap "+name);
            if(name=="short")
            {
                var old=jar.Clone();using var logout=await Send(HttpMethod.Post,"/api/auth/logout",jar);
                Check(logout.IsSuccessStatusCode,"Logout succeeds for repaired short restricted session");
                await Status(old,"/api/auth/session",401,"Logged-out cookie cannot authenticate");
                Check(await db.UserSessions.AnyAsync(x=>x.SessionId==s.SessionId&&x.RevokedAt!=null,ct),"Logout revocation persisted in SQL");
            }
        }
        await Login("expired",403);
        await using(var db=Db())Check(!await db.UserSessions.AnyAsync(s=>s.UserId==users["expired"].UserId,ct),"Expired temporary credential creates no SQL session");
        var oldSession=await Login("change",200);var changed=await Login("change",200);var newPassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))+"Bb2-";
        using(var response=await Send(HttpMethod.Post,"/api/auth/change-password",changed,new{CurrentPassword=password,NewPassword=newPassword,ConfirmNewPassword=newPassword}))
        {
            Check(response.IsSuccessStatusCode,"Change Password succeeds with short temporary credential after fix");
            using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));changed.Token=doc.RootElement.GetProperty("csrfToken").GetString();
            Check(!doc.RootElement.GetProperty("mustChangePassword").GetBoolean(),"Password change rotates into normal session");
        }
        await Status(oldSession,"/api/auth/session",401,"Password change revokes previous restricted session");
        await Status(changed,"/api/auth/session",200,"Rotated normal SQL session works");
        await using(var db=Db())
        {
            var uid=users["change"].UserId;
            Check(await db.Users.AnyAsync(u=>u.UserId==uid&&u.TemporaryPasswordExpiresAt==null,ct),"Temporary credential cleared only by password change");
            Check(await db.UserSessions.CountAsync(s=>s.UserId==uid&&s.RevokedAt==null,ct)==1,"Exactly one fresh normal session after password rotation");
        }
        await using(var db=Db())await db.Users.Where(u=>u.UserId==users["near"].UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.TemporaryPasswordExpiresAt,DateTime.UtcNow.AddSeconds(8)),ct);
        var near=await Login("near",200);await Status(near,"/api/auth/session",200,"Near-expiry valid credential/session works before deadline");
        DateTime expiry;await using(var db=Db())expiry=DateTime.SpecifyKind((await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==users["near"].UserId,ct)).TemporaryPasswordExpiresAt!.Value,DateTimeKind.Utc);
        var delay=expiry-DateTime.UtcNow+TimeSpan.FromMilliseconds(200);if(delay>TimeSpan.Zero)await Task.Delay(delay,ct);
        await Status(near,"/api/auth/session",401,"Cookie/session rejected after unchanged temporary-credential deadline");
        await Login("near",403);
    }
    sealed class Jar
    {
        public Dictionary<string,string> Cookies{get;}=[];public string? Token{get;set;}
        public Jar Clone(){var n=new Jar{Token=Token};foreach(var p in Cookies)n.Cookies[p.Key]=p.Value;return n;}
    }
}
