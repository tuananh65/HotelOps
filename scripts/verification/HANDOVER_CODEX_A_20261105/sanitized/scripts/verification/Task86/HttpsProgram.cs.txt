using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// Compile is safe. Execution requires separate approval for this exact fresh trial and fixtures.
const string target="HotelOps_Canonical_20261104_223000_c01286a1_Fresh";
const string instance=@"REDACTED_LOCAL_SQL_HOST\SQLEXPRESS";
if(args.Length!=2 || args[0]!=target || File.Exists(args[1]))throw new ArgumentException("Exact Task86 trial and unused receipt required");
var root=Directory.GetCurrentDirectory();
var connection=new SqlConnectionStringBuilder{DataSource=instance,InitialCatalog=target,IntegratedSecurity=true,TrustServerCertificate=true,ConnectTimeout=10,ApplicationName="HotelOps Task86 HTTPS acceptance"}.ConnectionString;
using var budget=new CancellationTokenSource(TimeSpan.FromMinutes(10));var ct=budget.Token;
HotelOpsDbContext Open()=>new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
await using var db=Open();await db.Database.OpenConnectionAsync(ct);
await using(var identity=db.Database.GetDbConnection().CreateCommand())
{
    identity.CommandText="SELECT CONVERT(nvarchar(128),@@SERVERNAME),DB_NAME(),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')";
    await using var reader=await identity.ExecuteReaderAsync(ct);
    if(!await reader.ReadAsync(ct)||reader.GetString(0)!=instance||reader.GetString(1)!=target||reader.IsDBNull(2)||reader.GetString(2)!="Task17")throw new Exception("Wrong trial identity");
}
if(await db.Users.AnyAsync(ct)||await db.Rooms.AnyAsync(ct))throw new Exception("Fresh fixtures required; no reset/replay over existing data");
var cases=new List<string>();string? failure=null;Process? process=null;Task<string>? stdout=null,stderr=null;
string? certFile=null;bool ownConstraint=false;
void Check(bool ok,string label){if(!ok){failure=label;throw new Exception(label);}cases.Add(label);Console.WriteLine("PASS: "+label);}
async Task<string> Fingerprint(params string[] tables)
{
    var result=new StringBuilder();
    foreach(var table in tables)
    {
        await using var cmd=db.Database.GetDbConnection().CreateCommand();cmd.CommandText=$"SELECT * FROM dbo.[{table}] ORDER BY 1 FOR JSON PATH,INCLUDE_NULL_VALUES";
        var text=new StringBuilder();await using var reader=await cmd.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))text.Append(reader.GetString(0));
        result.Append(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))));
    }
    return result.ToString();
}
try
{
    var catalog=await Fingerprint("roles","permissions","role_permissions","reference_values");
    var a=await db.Branches.Where(x=>x.BranchCode=="BR001").Select(x=>x.BranchId).SingleAsync(ct);
    var b=await db.Branches.Where(x=>x.BranchCode=="BR002").Select(x=>x.BranchId).SingleAsync(ct);
    var deps=await db.Departments.AsNoTracking().ToArrayAsync(ct);var roles=await db.Roles.ToDictionaryAsync(x=>x.RoleCode,ct);
    var now=DateTime.UtcNow;var password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))+"Aa1-";
    var hasher=new PasswordCredentialService(Options.Create(new PasswordHasherOptions{CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3,IterationCount=PasswordCredentialService.BaselineIterationCount}));
    var users=new Dictionary<string,User>();
    foreach(var name in new[]{"admin","admin2","denied","branch","restricted","revoke","expired","account","employment"})
    {
        var branch=name=="branch"?b:a;
        var user=new User{EmployeeCode="T86_"+name,Username="t86."+name,Email="t86."+name+"@example.invalid",FullName="Task86 "+name,PrimaryBranchId=branch,DepartmentId=deps.Single(x=>x.BranchId==branch&&x.DepartmentCode=="HK").DepartmentId,AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",CreatedAt=now,TemporaryPasswordExpiresAt=name=="restricted"?now.AddHours(24):null};
        user.PasswordHash=hasher.HashPassword(user,password);db.Users.Add(user);users[name]=user;
    }
    await db.SaveChangesAsync(ct);
    foreach(var (name,user)in users)
    {
        db.UserRoles.Add(new UserRole{UserId=user.UserId,RoleId=roles[name=="denied"?"RECEPTION":"SYS_ADMIN"].RoleId,BranchId=name=="branch"?b:null,IsActive=true,AssignedAt=now.AddMinutes(-1)});
        if(name=="branch")db.UserBranchAccesses.Add(new UserBranchAccess{UserId=user.UserId,BranchId=b,AccessLevel="STANDARD",IsPrimary=true,GrantedAt=now.AddMinutes(-1)});
    }
    var floor=new Floor{BranchId=a,FloorCode="T86_F",FloorName="Task86 floor",IsActive=true};
    var type=new RoomType{TypeCode="T86_TYPE",TypeName="Task86 type",StandardCapacity=2,IsActive=true};
    db.AddRange(floor,type);await db.SaveChangesAsync(ct);
    long room;
    await using(var cmd=db.Database.GetDbConnection().CreateCommand())
    {
        cmd.CommandText="INSERT dbo.rooms(branch_id,floor_id,room_type_id,room_number,operational_status,is_active) VALUES(@b,@f,@t,'T86_ROOM','DIRTY',1); SELECT CONVERT(bigint,SCOPE_IDENTITY());";
        cmd.Parameters.Add(new SqlParameter("@b",a));cmd.Parameters.Add(new SqlParameter("@f",floor.FloorId));cmd.Parameters.Add(new SqlParameter("@t",type.RoomTypeId));room=Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    }
    var cycle=new CleaningTask{RoomId=room,TaskNo="T86_CYCLE",Status="PENDING",CreatedBy=users["admin"].UserId,CreatedAt=now,CleaningTypeId=await db.ReferenceValues.Where(x=>x.GroupCode=="CLEANING_TYPE").Select(x=>x.ReferenceId).FirstAsync(ct)};
    db.Add(cycle);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
    Check(await db.Branches.AsNoTracking().CountAsync(ct)==2,"Legacy EF Branch mapping reads fresh rowversion schema");
    using var rsa=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
    var san=new SubjectAlternativeNameBuilder();san.AddIpAddress(IPAddress.Loopback);request.CertificateExtensions.Add(san.Build());
    using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));
    certFile=Path.Combine(Path.GetTempPath(),"HotelOps-Task86-"+Guid.NewGuid().ToString("N")+".pfx");var certPassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
    await File.WriteAllBytesAsync(certFile,cert.Export(X509ContentType.Pfx,certPassword),ct);
    var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
    var start=new ProcessStartInfo("dotnet"){WorkingDirectory=Path.Combine(root,"src/HotelOps.Api"),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
    start.ArgumentList.Add(Path.Combine(root,"scripts/verification/Task86/artifacts/integrated/bin/HotelOps.Api/debug/HotelOps.Api.dll"));
    start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";start.Environment["DOTNET_ENVIRONMENT"]="Development";
    start.Environment["ASPNETCORE_URLS"]="https://127.0.0.1:"+port;start.Environment["ConnectionStrings__HotelOpsDb"]=connection;
    start.Environment["Kestrel__Certificates__Default__Path"]=certFile;start.Environment["Kestrel__Certificates__Default__Password"]=certPassword;
    process=Process.Start(start)??throw new Exception("Startup failed");stdout=process.StandardOutput.ReadToEndAsync();stderr=process.StandardError.ReadToEndAsync();
    using var handler=new HttpClientHandler{UseCookies=false,AllowAutoRedirect=false,ServerCertificateCustomValidationCallback=(_,peer,_,_)=>peer?.GetCertHashString()==cert.GetCertHashString()};
    using var http=new HttpClient(handler){BaseAddress=new Uri("https://127.0.0.1:"+port),Timeout=TimeSpan.FromSeconds(25)};
    var ready=false;for(var i=0;i<80&&!process.HasExited;i++){try{using var r=await http.GetAsync("/api/health/database",ct);if(r.IsSuccessStatusCode){ready=true;break;}}catch(HttpRequestException){}await Task.Delay(250,ct);}
    Check(ready,"Actual Program startup/schema validator and pinned HTTPS listener");
    async Task<HttpResponseMessage> Send(HttpMethod method,string path,Jar jar,object? body=null,string? tag=null,bool csrf=true)
    {
        using var message=new HttpRequestMessage(method,path);
        if(jar.Cookies.Count>0)message.Headers.Add("Cookie",string.Join("; ",jar.Cookies.Select(x=>x.Key+"="+x.Value)));
        if(csrf&&jar.Token!=null)message.Headers.Add(HotelOpsAntiforgery.HeaderName,jar.Token);
        if(tag!=null)message.Headers.TryAddWithoutValidation("If-Match",tag);
        if(body!=null)message.Content=JsonContent.Create(body);
        var response=await http.SendAsync(message,ct);jar.Read(response);return response;
    }
    async Task<Jar> Login(string name)
    {
        var jar=new Jar();using(var r=await Send(HttpMethod.Get,"/api/auth/csrf",jar)){Check(r.IsSuccessStatusCode,"CSRF bootstrap "+name);using var j=JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));jar.Token=j.RootElement.GetProperty("csrfToken").GetString();}
        using var response=await Send(HttpMethod.Post,"/api/auth/login",jar,new{Username=users[name].Username,Password=password});
        Check(response.IsSuccessStatusCode,"Real credential login/SQL session "+name);using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));jar.Token=json.RootElement.GetProperty("csrfToken").GetString();return jar;
    }
    var jars=new Dictionary<string,Jar>();foreach(var name in users.Keys)jars[name]=await Login(name);
    var admin=jars["admin"];var admin2=jars["admin2"];
    async Task<(JsonElement? Body,string? Tag)> Expect(HttpMethod method,string path,Jar jar,object? body,int status,string label,string? tag=null,bool csrf=true)
    {
        using var r=await Send(method,path,jar,body,tag,csrf);Check((int)r.StatusCode==status,label+" status "+(int)r.StatusCode);
        var text=await r.Content.ReadAsStringAsync(ct);Check(!text.Contains("SqlException")&&!text.Contains("passwordHash",StringComparison.OrdinalIgnoreCase),label+" no provider/credential disclosure");
        JsonElement? value=null;if(r.IsSuccessStatusCode){using var j=JsonDocument.Parse(text);value=j.RootElement.Clone();}
        return(value,r.Headers.ETag?.ToString());
    }
    object Create(string code,string name="Task86 branch")=>new{Code=code,Name=name,Address="Task86 address",Phone="+84 123 456",Email="t86@example.invalid"};
    object Update(string name)=>new{Name=name,Address="Updated address",Phone="123456",Email="updated@example.invalid",Reason="Task86 verified update"};
    var created=await Expect(HttpMethod.Post,"/api/branches",admin,Create(" t86_new "),201,"Create branch");
    var id=created.Body!.Value.GetProperty("id").GetInt64();var etag=created.Tag!;
    Check(created.Body.Value.GetProperty("code").GetString()=="T86_NEW"&&created.Body.Value.GetProperty("timezone").GetString()=="Asia/Ho_Chi_Minh"&&created.Body.Value.GetProperty("isActive").GetBoolean(),"Server canonical code/timezone/active");
    Check(Convert.FromBase64String(etag.Trim('"')).Length==8,"Response ETag is actual eight-byte SQL rowversion");
    Check(await db.AuditLogs.CountAsync(x=>x.EntityType=="branches"&&x.EntityId==id.ToString()&&x.Action=="CREATE",ct)==1,"Create audit persisted exactly once");
    await Expect(HttpMethod.Post,"/api/branches",admin,Create("t86_new"),409,"Case insensitive duplicate rejected");
    foreach(var body in new object[]{new{Code="T86_BAD",Name="",Address="A"},new{Code="T86_BAD",Name="N",Address=""},new{Code="T86_BAD",Name="N",Address="A",Phone="invalid"},new{Code="T86_BAD",Name="N",Address="A",Email="invalid"},new{Code="T86_BAD",Name="N",Address="A",IsActive=false}})
        await Expect(HttpMethod.Post,"/api/branches",admin,body,400,"Invalid create rejected");
    await Expect(HttpMethod.Post,"/api/branches",admin,Create("T86_CSRF"),400,"Missing CSRF rejected",csrf:false);
    await Expect(HttpMethod.Get,$"/api/branches/{id}/edit",new Jar(),null,401,"Anonymous denied");
    foreach(var name in new[]{"denied","branch"})await Expect(HttpMethod.Put,$"/api/branches/{id}",jars[name],Update("Must not write"),403,"No global permission "+name,etag);
    await Expect(HttpMethod.Get,$"/api/branches/{id}/edit",jars["restricted"],null,403,"Restricted session denied");
    var racing=await Task.WhenAll(Send(HttpMethod.Post,"/api/branches",admin,Create("T86_RACE")),Send(HttpMethod.Post,"/api/branches",admin2,Create("t86_race")));
    try{Check(racing.Select(x=>(int)x.StatusCode).Order().SequenceEqual(new[]{201,409}),"Concurrent same-code create: exactly 201/409, no deadlock retry");}finally{foreach(var r in racing)r.Dispose();}
    Check(await db.Branches.CountAsync(x=>x.BranchCode=="T86_RACE",ct)==1,"Concurrent create persisted exactly one branch");
    var edit=await Expect(HttpMethod.Get,$"/api/branches/{id}/edit",admin,null,200,"Read edit");
    await Expect(HttpMethod.Put,$"/api/branches/{id}",admin,Update("Changed"),428,"Missing If-Match");
    await Expect(HttpMethod.Put,$"/api/branches/{id}",admin,Update("Changed"),400,"Weak If-Match rejected","W/"+etag);
    var updated=await Expect(HttpMethod.Put,$"/api/branches/{id}",admin,Update("Changed"),200,"Update branch",edit.Tag);
    Check(updated.Tag!=etag,"Actual rowversion changes on update");
    await Expect(HttpMethod.Put,$"/api/branches/{id}",admin,Update("Stale write"),412,"Stale update rejected",etag);
    var beforeNoop=await Fingerprint("branches","audit_logs");
    var noop=await Expect(HttpMethod.Put,$"/api/branches/{id}",admin,Update("Changed"),200,"No-op update",updated.Tag);
    Check(noop.Tag==updated.Tag&&beforeNoop==await Fingerprint("branches","audit_logs"),"No-op preserves rowversion/UpdatedAt/audit");
    var edits=await Task.WhenAll(Send(HttpMethod.Put,$"/api/branches/{id}",admin,Update("Concurrent A"),updated.Tag),Send(HttpMethod.Put,$"/api/branches/{id}",admin2,Update("Concurrent B"),updated.Tag));
    try{Check(edits.Select(x=>(int)x.StatusCode).Order().SequenceEqual(new[]{200,412}),"Concurrent update: exactly one commit and one stale response");}finally{foreach(var r in edits)r.Dispose();}
    await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(Path.Combine(root,"scripts/verification/Task86/AuditFailureConstraint.sql.proposal"),ct),ct);ownConstraint=true;
    var beforeFailure=await Fingerprint("branches","audit_logs");
    await Expect(HttpMethod.Post,"/api/branches",admin,Create("T86_FAIL_CREATE"),503,"Real SQL audit failure rolls back Create");
    Check(beforeFailure==await Fingerprint("branches","audit_logs"),"No partial create or audit commit");
    var fresh=await Expect(HttpMethod.Get,$"/api/branches/{id}/edit",admin,null,200,"Fresh edit before audit failure");
    await Expect(HttpMethod.Put,$"/api/branches/{id}",admin,Update("T86_FAIL_UPDATE"),503,"Real SQL audit failure rolls back Update",fresh.Tag);
    Check(beforeFailure==await Fingerprint("branches","audit_logs"),"No partial update or audit commit");
    await Expect(HttpMethod.Post,$"/api/branches/{id}/deactivate",admin,new{Reason="Task86 deactivation",Confirmation=true},503,"Empty branch still fails closed without consumer coordination",fresh.Tag);
    var branchA=await Expect(HttpMethod.Get,$"/api/branches/{a}/edit",admin,null,200,"Existing branch edit");
    await Expect(HttpMethod.Post,$"/api/branches/{a}/deactivate",admin,new{Reason="Task86 deactivation",Confirmation=true},409,"Active rooms block deactivation",branchA.Tag);
    Check(await db.Branches.Where(x=>x.BranchId==id||x.BranchId==a).AllAsync(x=>x.IsActive,ct),"Blocked deactivate never mutates active branches");
    await Expect(HttpMethod.Get,"/api/room-types",admin,null,200,"UC142 HTTPS list regression");
    await Expect(HttpMethod.Get,$"/api/room-types/{type.RoomTypeId}",admin,null,200,"UC142 HTTPS detail regression");
    await Expect(HttpMethod.Get,$"/api/rooms/{room}/turnover-history?branchId={a}",admin,null,200,"UC038 HTTPS empty history regression");
    await Expect(HttpMethod.Get,$"/api/branches/{a}/cleaning-monitor",admin,null,200,"UC037 HTTPS list regression");
    await Expect(HttpMethod.Get,$"/api/branches/{a}/cleaning-monitor/cleaning/{cycle.CleaningTaskId}",admin,null,200,"UC037 HTTPS detail regression");
    await db.UserRoles.Where(x=>x.UserId==users["revoke"].UserId).ExecuteUpdateAsync(x=>x.SetProperty(r=>r.IsActive,false),ct);
    await Expect(HttpMethod.Put,$"/api/branches/{id}",jars["revoke"],Update("Forbidden"),403,"Permission revocation before mutation",fresh.Tag);
    await db.UserSessions.Where(x=>x.UserId==users["expired"].UserId).ExecuteUpdateAsync(x=>x.SetProperty(s=>s.ExpiresAt,s=>s.CreatedAt),ct);
    await Expect(HttpMethod.Get,$"/api/branches/{id}/edit",jars["expired"],null,401,"Expired SQL session denied");
    await db.UserSessions.Where(x=>x.UserId==users["revoke"].UserId).ExecuteUpdateAsync(x=>x.SetProperty(s=>s.RevokedAt,DateTime.UtcNow),ct);
    await Expect(HttpMethod.Get,$"/api/branches/{id}/edit",jars["revoke"],null,401,"Revoked SQL session denied");
    await db.Users.Where(x=>x.UserId==users["account"].UserId).ExecuteUpdateAsync(x=>x.SetProperty(u=>u.AccountStatus,"INACTIVE"),ct);
    await Expect(HttpMethod.Get,$"/api/branches/{id}/edit",jars["account"],null,401,"Inactive account denied");
    await db.Users.Where(x=>x.UserId==users["employment"].UserId).ExecuteUpdateAsync(x=>x.SetProperty(u=>u.EmploymentStatus,"INACTIVE"),ct);
    await Expect(HttpMethod.Get,$"/api/branches/{id}/edit",jars["employment"],null,401,"Inactive employment denied");
    Check(catalog==await Fingerprint("roles","permissions","role_permissions","reference_values"),"Catalog/grants unchanged by fixtures and requests");
}
catch(Exception e){failure??=e.GetType().Name;Environment.ExitCode=1;Console.WriteLine("FAIL: "+failure);}
finally
{
    if(ownConstraint)await db.Database.ExecuteSqlRawAsync("IF DB_NAME()<>N'HotelOps_Canonical_20261104_223000_c01286a1_Fresh' THROW 51986,'Wrong target',1; ALTER TABLE dbo.audit_logs DROP CONSTRAINT CK_Task86_BranchAuditFailure",CancellationToken.None);
    if(process!=null){if(!process.HasExited)process.Kill(entireProcessTree:true);await process.WaitForExitAsync();process.Dispose();}
    if(certFile!=null)File.Delete(certFile); // Exact temporary certificate created by this invocation only.
    // No raw server output, cookie, password, connection string or SQL session credential is serialized.
    await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(new{success=failure==null,passed=cases.Count,failed=failure==null?0:1,failure,cases,database=target,instance,
        notRun=new[]{"Successful deactivation","Deactivate/create race","DEV2 writer inactive-branch guards","Complete dependency scope/terminal-state review","Permission/session revocation during observed SQL lock wait"},
        uc012="PARTIAL",completeUCs=26,verifiedPoints=60},new JsonSerializerOptions{WriteIndented=true}));
}

sealed class Jar
{
    public Dictionary<string,string> Cookies{get;}=[];public string? Token{get;set;}
    public void Read(HttpResponseMessage r){if(!r.Headers.TryGetValues("Set-Cookie",out var values))return;foreach(var value in values){var pair=value.Split(';')[0].Split('=',2);if(pair.Length==2)Cookies[pair[0]]=pair[1];}}
}
