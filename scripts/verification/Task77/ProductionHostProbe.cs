using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class ProductionHostProbe
{
    public static async Task RunAsync(string database,string receipt)
    {
        if(!Regex.IsMatch(database,@"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$")||File.Exists(receipt)) throw new ArgumentException("Guarded owned trial and NEW receipt required");
        var t=new TimeTrial(database); using var budget=new CancellationTokenSource(TimeSpan.FromMinutes(3)); var ct=budget.Token;
        await using(var db=t.Open())
        {
            await db.Database.OpenConnectionAsync(ct);
            var name=await db.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),DB_NAME()) AS [Value]").SingleAsync(ct);
            var marker=await db.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),value) AS [Value] FROM sys.extended_properties WHERE class=0 AND name='HotelOps.WorkTime.Contract'").SingleAsync(ct);
            if(name!=database||marker!="Task77-v1") throw new InvalidOperationException("Owned A1 trial rejected");
            var branch=await db.Branches.SingleAsync(x=>x.BranchCode=="BR002",ct);
            var department=await db.Departments.SingleAsync(x=>x.BranchId==branch.BranchId&&x.DepartmentCode=="HK",ct);
            var suffix=Guid.NewGuid().ToString("N")[..8];
            var user=new User {Username="wt.host."+suffix,EmployeeCode="WTH_"+suffix,Email="wt.host."+suffix+"@example.invalid",FullName="Host regression fixture",
                PrimaryBranchId=branch.BranchId,DepartmentId=department.DepartmentId,AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",CreatedAt=DateTime.UtcNow};
            user.PasswordHash=new PasswordCredentialService(Options.Create(new PasswordHasherOptions {IterationCount=PasswordCredentialService.BaselineIterationCount})).HashPassword(user,t.Password);
            db.Add(user); await db.SaveChangesAsync(ct); t.Users["host"]=user;
        }
        using var rsa=RSA.Create(2048);
        var request=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        var san=new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback); request.CertificateExtensions.Add(san.Build());
        using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));
        var password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        var certPath=Path.Combine(t.Root,"obj","task77-host-"+Guid.NewGuid().ToString("N")+".pfx");
        await File.WriteAllBytesAsync(certPath,cert.Export(X509ContentType.Pfx,password),ct);
        var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var start=new ProcessStartInfo("dotnet") {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,
            WorkingDirectory=Path.Combine(t.Root,"src/HotelOps.Api"),RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add(Path.Combine(t.Root,"src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll"));
        start.Environment["ASPNETCORE_ENVIRONMENT"]="Development"; start.Environment["DOTNET_ENVIRONMENT"]="Development";
        start.Environment["ASPNETCORE_URLS"]="https://127.0.0.1:"+port;
        start.Environment["ConnectionStrings__HotelOpsDb"]=t.ConnectionString;
        start.Environment["Kestrel__Certificates__Default__Path"]=certPath; start.Environment["Kestrel__Certificates__Default__Password"]=password;
        start.Environment["Logging__LogLevel__Default"]="None";
        Process? process=null;
        try
        {
            process=Process.Start(start)??throw new InvalidOperationException("API process failed");
            var stdout=process.StandardOutput.ReadToEndAsync(ct); var stderr=process.StandardError.ReadToEndAsync(ct);
            using var handler=new HttpClientHandler {UseCookies=false,AllowAutoRedirect=false,ServerCertificateCustomValidationCallback=(_,c,_,_)=>c?.GetCertHashString()==cert.GetCertHashString()};
            using var http=new HttpClient(handler) {BaseAddress=new Uri("https://127.0.0.1:"+port),Timeout=TimeSpan.FromSeconds(10)};
            var ready=false;
            for(var i=0;i<80&&!process.HasExited;i++)
            {
                try {using var response=await http.GetAsync("/api/health/database",ct); if(response.IsSuccessStatusCode) {ready=true;break;}}
                catch(HttpRequestException){}
                await Task.Delay(250,ct);
            }
            t.Check(ready,"Actual production Program.cs starts against guarded A1 SQL and readiness returns200");
            var cookies=new Dictionary<string,string>(); string? token=null;
            async Task<HttpResponseMessage> Send(HttpMethod method,string path,object? body=null)
            {
                using var message=new HttpRequestMessage(method,path);
                if(cookies.Count>0) message.Headers.Add("Cookie",string.Join("; ",cookies.Select(x=>x.Key+"="+x.Value)));
                if(token is not null) message.Headers.Add("X-CSRF-TOKEN",token);
                if(body is not null) message.Content=JsonContent.Create(body);
                var response=await http.SendAsync(message,ct);
                if(response.Headers.TryGetValues("Set-Cookie",out var headers)) foreach(var header in headers) {var pair=header.Split(';')[0].Split('=',2); if(pair.Length==2) cookies[pair[0]]=pair[1];}
                return response;
            }
            using(var csrf=await Send(HttpMethod.Get,"/api/auth/csrf"))
            {
                t.Check(csrf.IsSuccessStatusCode,"Production Program CSRF route works");
                using var json=JsonDocument.Parse(await csrf.Content.ReadAsStringAsync(ct)); token=json.RootElement.GetProperty("csrfToken").GetString();
            }
            using(var login=await Send(HttpMethod.Post,"/api/auth/login",new {Username=t.Users["host"].Username,Password=t.Password}))
            {
                t.Check(login.IsSuccessStatusCode,"Production Program credential login and SQL-backed Cookie session work");
                using var json=JsonDocument.Parse(await login.Content.ReadAsStringAsync(ct)); token=json.RootElement.GetProperty("csrfToken").GetString();
            }
            using(var session=await Send(HttpMethod.Get,"/api/auth/session")) t.Check(session.IsSuccessStatusCode,"Production Program validates issued SQL session");
            using(var absent=await Send(HttpMethod.Get,"/api/workdays")) t.Check(absent.StatusCode==HttpStatusCode.NotFound,"No unapproved Workday public route shipped");
            using(var absent=await Send(HttpMethod.Post,"/api/verification/task77/cleaning/1/open",new {})) t.Check(absent.StatusCode==HttpStatusCode.NotFound,"Verification driver is absent from production assembly/routes");
            using(var api=await Send(HttpMethod.Get,"/openapi/v1.json")) t.Check(api.IsSuccessStatusCode&&!(await api.Content.ReadAsStringAsync(ct)).Contains("/api/verification/task77",StringComparison.Ordinal),"Actual Development OpenAPI has no verification endpoint");
            await File.WriteAllTextAsync(receipt,JsonSerializer.Serialize(new {success=true,checks=t.Cases.Count,cases=t.Cases,database,actualProductionProgram=true,publicWorkdayEndpoints=false},new JsonSerializerOptions {WriteIndented=true}),ct);
            process.Kill(entireProcessTree:true); await process.WaitForExitAsync(CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(t.Root,"obj/task77-a1-production-host.stdout.local.log"),await stdout,ct);
            await File.WriteAllTextAsync(Path.Combine(t.Root,"obj/task77-a1-production-host.stderr.local.log"),await stderr,ct);
            Console.WriteLine($"PASS: {t.Cases.Count} actual production Program HTTPS/startup/authentication checks; no Workday public API claim.");
        }
        finally
        {
            if(process is not null) {if(!process.HasExited) process.Kill(entireProcessTree:true); await process.WaitForExitAsync(CancellationToken.None); process.Dispose();}
            File.Delete(certPath); // Only this invocation's exact certificate path; no recursive cleanup.
        }
    }
}
