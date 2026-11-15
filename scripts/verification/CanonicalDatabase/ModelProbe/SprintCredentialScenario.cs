using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal static class SprintCredentialScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,List<object> checks,CancellationToken ct)
    {
        var name="D3CRED"+Guid.NewGuid().ToString("N")[..10];
        string Password()=>Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))+"aA1!";
        var password=Password();var newPassword=Password();var finalPassword=Password();
        var user=new User{EmployeeCode=name,Username=name,Email=name+"@example.invalid",FullName="Synthetic credential acceptance",
            AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",CreatedAt=DateTime.UtcNow,TemporaryPasswordExpiresAt=DateTime.UtcNow.AddHours(1)};
        var hasher=new PasswordHasher<User>();user.PasswordHash=hasher.HashPassword(user,password);
        var originalHash=user.PasswordHash;var trigger="TR_DEV3_CredentialFault_"+Guid.NewGuid().ToString("N")[..8];var triggerCreated=false;
        void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(new{credentialCheck=label});}
        try
        {
            db.Users.Add(user);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add("frontend/scripts/sprintCredentialsBrowser.mjs");
            using(var process=Process.Start(start)??throw new Exception("Browser start failed"))
            {
                var output=process.StandardOutput.ReadToEndAsync(ct);var errors=process.StandardError.ReadToEndAsync(ct);
                try
                {
                    await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username=name,email=user.Email,password,newPassword,finalPassword}));process.StandardInput.Close();
                    await process.WaitForExitAsync(ct);var result=JsonSerializer.Deserialize<JsonElement>(await output);_=await errors;
                    checks.Add(new{credentialBrowser=result});
                    Check(process.ExitCode==0&&result.GetProperty("success").GetBoolean(),"canonical credential browser acceptance");
                }
                finally{if(!process.HasExited)process.Kill(entireProcessTree:true);}
            }
            var current=await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==user.UserId,ct);
            Check(current.TemporaryPasswordExpiresAt==null&&current.PasswordHash!=originalHash
                &&hasher.VerifyHashedPassword(current,current.PasswordHash!,finalPassword)!=PasswordVerificationResult.Failed
                &&hasher.VerifyHashedPassword(current,current.PasswordHash!,password)==PasswordVerificationResult.Failed,"SQL stores final hash and clears temporary marker");
            Check(await db.UserSessions.CountAsync(s=>s.UserId==user.UserId,ct)>=5
                &&await db.UserSessions.Where(s=>s.UserId==user.UserId).AllAsync(s=>s.RevokedAt!=null,ct),"all browser-created sessions durably revoked after logout");
            var audits=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="users"&&a.EntityId==user.UserId.ToString()).ToArrayAsync(ct);
            Check(audits.Count(a=>a.NewValues?.Contains("PASSWORD_CHANGE")==true)==2,"two password changes have committed audits");
            Check(audits.Count(a=>a.Action=="LOGOUT")>=2&&audits.Where(a=>a.Action=="LOGOUT").All(a=>a.ActorUserId==user.UserId),"logout audits record correct actor");
            Check(audits.All(a=>new[]{password,newPassword,finalPassword,originalHash!,current.PasswordHash!}.All(secret=>!(a.NewValues??"").Contains(secret)&&!(a.OldValues??"").Contains(secret))),"audit contains no passwords or hashes");
            using var handler=new HttpClientHandler{CookieContainer=new CookieContainer(),AllowAutoRedirect=false};
            using var http=new HttpClient(handler){BaseAddress=new Uri("https://localhost:7142")};
            async Task<HttpResponseMessage> Post(string path,object body)
            {
                var csrf=await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);
                using var message=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};
                message.Headers.Add("X-CSRF-TOKEN",csrf.GetProperty("csrfToken").GetString());return await http.SendAsync(message,ct);
            }
            using(var login=await Post("/api/auth/login",new{username=name,password=finalPassword}))login.EnsureSuccessStatusCode();
            var sessionsBefore=await db.UserSessions.CountAsync(s=>s.UserId==user.UserId,ct);
            var auditBefore=await db.AuditLogs.CountAsync(a=>a.EntityId==user.UserId.ToString()&&a.EntityType=="users",ct);
            // Exact disposable identity was checked by SprintAuthProbe; this new owned trigger only injects failure for this synthetic user.
            await db.Database.ExecuteSqlRawAsync($"CREATE TRIGGER dbo.[{trigger}] ON dbo.audit_logs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE entity_type='users' AND entity_id='{user.UserId}' AND (action='LOGOUT' OR new_values LIKE '%PASSWORD_CHANGE%')) THROW 51993,'Synthetic credential audit fault',1; END",ct);triggerCreated=true;
            using(var failed=await Post("/api/auth/change-password",new{currentPassword=finalPassword,newPassword=Password(),confirmNewPassword="mismatch"}))Check((int)failed.StatusCode==400,"invalid input rejected before mutation");
            var attempt=Password();
            using(var failed=await Post("/api/auth/change-password",new{currentPassword=finalPassword,newPassword=attempt,confirmNewPassword=attempt}))Check((int)failed.StatusCode==500,"audit fault prevents password-change success");
            using(var failed=await Post("/api/auth/logout",new{}))Check((int)failed.StatusCode==500,"audit fault prevents logout success");
            Check(await db.Users.Where(u=>u.UserId==user.UserId).Select(u=>u.PasswordHash).SingleAsync(ct)==current.PasswordHash
                &&await db.UserSessions.CountAsync(s=>s.UserId==user.UserId,ct)==sessionsBefore
                &&await db.UserSessions.CountAsync(s=>s.UserId==user.UserId&&s.RevokedAt==null,ct)==1
                &&await db.AuditLogs.CountAsync(a=>a.EntityId==user.UserId.ToString()&&a.EntityType=="users",ct)==auditBefore,"audit faults roll back credentials, revocations, replacement sessions and audit atomically");
            using(var valid=await http.GetAsync("/api/auth/session",ct))Check((int)valid.StatusCode==200,"failed mutations retain usable original session");
            await db.Database.ExecuteSqlRawAsync($"DROP TRIGGER dbo.[{trigger}]",ct);triggerCreated=false;
            using(var logout=await Post("/api/auth/logout",new{}))Check((int)logout.StatusCode==204,"explicit retry after fault succeeds");
            using(var login=await Post("/api/auth/login",new{username=name,password=finalPassword}))login.EnsureSuccessStatusCode();
            await db.UserSessions.Where(s=>s.UserId==user.UserId&&s.RevokedAt==null)
                .ExecuteUpdateAsync(s=>s.SetProperty(v=>v.CreatedAt,DateTime.UtcNow.AddHours(-2)).SetProperty(v=>v.ExpiresAt,DateTime.UtcNow.AddHours(-1)),ct);
            using(var expired=await Post("/api/auth/logout",new{}))Check((int)expired.StatusCode==204,"expired session logout is idempotent success");
            using(var denied=await http.GetAsync("/api/auth/session",ct))Check((int)denied.StatusCode==401,"expired session cannot be reused after logout");
        }
        finally
        {
            db.ChangeTracker.Clear();
            if(triggerCreated)await db.Database.ExecuteSqlRawAsync($"DROP TRIGGER dbo.[{trigger}]",CancellationToken.None);
            await using var tx=await db.Database.BeginTransactionAsync(CancellationToken.None);
            await db.AuditLogs.Where(a=>a.EntityType=="users"&&a.EntityId==user.UserId.ToString()).ExecuteDeleteAsync(CancellationToken.None);
            await db.UserSessions.Where(s=>s.UserId==user.UserId).ExecuteDeleteAsync(CancellationToken.None);
            await db.Users.Where(u=>u.UserId==user.UserId&&u.Username==name).ExecuteDeleteAsync(CancellationToken.None);
            await tx.CommitAsync(CancellationToken.None);checks.Add(new{credentialCleanup=true});
        }
    }
}
