using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

await new B11Trial().Run(args);

sealed class B11Trial
{
    readonly string root = Directory.GetCurrentDirectory();
    readonly List<string> checks = [];
    readonly Dictionary<string, User> users = [];
    readonly Dictionary<string, long> assignments = [];
    readonly string password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "aA1-";
    string connection = "";
    long branch, otherBranch, cleaningId;
    CancellationToken ct;
    HttpClient client = null!;
    HotelOpsDbContext Db() => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
    void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException("FAIL " + label); checks.Add(label); Console.WriteLine("PASS " + label); }
    static DateTimeOffset Utc(DateTime d) => new(DateTime.SpecifyKind(d, DateTimeKind.Utc));
    public async Task Run(string[] args)
    {
        if (args.Length != 2 || !Regex.IsMatch(args[0], @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$") || File.Exists(args[1]))
            throw new ArgumentException("New guarded empty trial and unused receipt required");
        var source = Environment.GetEnvironmentVariable("TASK78_TRIAL_SQL_SOURCE") ?? throw new Exception("Explicit trial source required");
        connection = new SqlConnectionStringBuilder {DataSource=source, InitialCatalog=args[0], IntegratedSecurity=true,
            TrustServerCertificate=true, ConnectTimeout=10, ApplicationName="HotelOps Task78 B11 trial"}.ConnectionString;
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(10)); ct = deadline.Token;
        await using (var db=Db())
        {
            var actual=await db.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),DB_NAME()) AS [Value]").SingleAsync(ct);
            var marker=await db.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),value) AS [Value] FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UserDirectory.Trial'").SingleAsync(ct);
            if (actual!=args[0] || marker!="Task17" || await db.Users.AnyAsync(ct)) throw new Exception("Unused owned trial rejected");
            await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(Path.Combine(root,"scripts/verification/UserProfileUpdate/rowversion-forward.sql"),ct),ct);
            await db.Database.OpenConnectionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'HotelOps.Task77Approved',@value=1",ct);
            await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(Path.Combine(root,"scripts/verification/Task77/proposal/01_WorkTime.forward.sql"),ct),ct);
            await Fixtures(db);
        }
        using var rsa=RSA.Create(2048);
        var request=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        var san=new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback); request.CertificateExtensions.Add(san.Build());
        using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));
        var certPassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        var certPath=Path.Combine(root,"obj","task78-b11-"+Guid.NewGuid().ToString("N")+".pfx");
        await File.WriteAllBytesAsync(certPath,cert.Export(X509ContentType.Pfx,certPassword),ct);
        var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var start=new ProcessStartInfo("dotnet") {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,
            WorkingDirectory=Path.Combine(root,"src/HotelOps.Api"),RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add(Path.Combine(root,"src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll"));
        start.Environment["ASPNETCORE_ENVIRONMENT"]="Development"; start.Environment["DOTNET_ENVIRONMENT"]="Development";
        start.Environment["ASPNETCORE_URLS"]="https://127.0.0.1:"+port; start.Environment["ConnectionStrings__HotelOpsDb"]=connection;
        start.Environment["Kestrel__Certificates__Default__Path"]=certPath; start.Environment["Kestrel__Certificates__Default__Password"]=certPassword;
        start.Environment["Logging__LogLevel__Microsoft.AspNetCore.Authentication"]="Debug";
        Process? process=null; Task<string>? stdout=null,stderr=null;
        try
        {
            process=Process.Start(start)??throw new Exception("API launch failed");
            stdout=process.StandardOutput.ReadToEndAsync(); stderr=process.StandardError.ReadToEndAsync();
            using var handler=new HttpClientHandler {UseCookies=false,AllowAutoRedirect=false,
                ServerCertificateCustomValidationCallback=(_,c,_,_)=>c?.GetCertHashString()==cert.GetCertHashString()};
            using var http=new HttpClient(handler) {BaseAddress=new Uri("https://127.0.0.1:"+port),Timeout=TimeSpan.FromSeconds(75)}; client=http;
            var ready=false;
            for(var i=0;i<100&&!process.HasExited;i++)
            {
                try {using var r=await http.GetAsync("/api/health/database",ct); if(r.IsSuccessStatusCode){ready=true;break;}} catch(HttpRequestException){}
                await Task.Delay(200,ct);
            }
            Check(ready,"Actual production Program startup/readiness with B1 DI");
            await Acceptance();
            await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(new {success=true,database=args[0],checks=checks.Count,cases=checks,
                actualProductionProgram=true,realHttpsSql=true,rbacCatalogMutated=false,notificationImplemented=false,pauseAllImplemented=false,
                uc139="BLOCKED_EXTERNAL",finishedUtc=DateTimeOffset.UtcNow},new JsonSerializerOptions {WriteIndented=true}),ct);
        }
        finally
        {
            if(process is not null){if(!process.HasExited)process.Kill(entireProcessTree:true);await process.WaitForExitAsync();process.Dispose();}
            if(stdout is not null)await File.WriteAllTextAsync(Path.Combine(root,"obj/task78-b11-production.stdout.local.log"),await stdout);
            if(stderr is not null)await File.WriteAllTextAsync(Path.Combine(root,"obj/task78-b11-production.stderr.local.log"),await stderr);
            File.Delete(certPath); // Exact PFX created by this invocation only. No database or directory cleanup.
        }
    }
    async Task Fixtures(HotelOpsDbContext db)
    {
        branch=(await db.Branches.SingleAsync(b=>b.BranchCode=="BR001",ct)).BranchId;
        otherBranch=(await db.Branches.SingleAsync(b=>b.BranchCode=="BR002",ct)).BranchId;
        var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct),DateTimeKind.Utc);
        var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now,TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")));
        var role=await db.Roles.SingleAsync(r=>r.RoleCode=="HK_STAFF",ct);
        var hash=new PasswordCredentialService(Options.Create(new PasswordHasherOptions {IterationCount=PasswordCredentialService.BaselineIterationCount}));
        foreach(var name in new[]{"owner","peer","foreign","denied","restricted","inactive","race","fault","permissionwait","sessionwait","unscheduled","revoked"})
        {
            var b=name=="foreign"?otherBranch:branch;
            var department=await db.Departments.SingleAsync(d=>d.BranchId==b&&d.DepartmentCode=="HK",ct);
            var u=new User {Username="b11."+name,EmployeeCode="B11_"+name,FullName="B11 "+name,Email="b11."+name+"@example.invalid",
                PrimaryBranchId=b,DepartmentId=department.DepartmentId,AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",CreatedAt=now};
            // Existing auth baseline interprets a SQL-read short temporary-expiry cap as
            // local DateTimeOffset. Use a valid longer-lived credential here so the session
            // takes the existing UTC 15-minute restricted cap. Short-cap bug is reported,
            // not fixed through an unapproved shared authentication change.
            if(name=="restricted")u.TemporaryPasswordExpiresAt=now.AddHours(1);
            u.PasswordHash=hash.HashPassword(u,password); db.Add(u); await db.SaveChangesAsync(ct);users[name]=u;
            db.Add(new UserBranchAccess {UserId=u.UserId,BranchId=b,AccessLevel="STANDARD",IsPrimary=true,GrantedAt=now.AddMinutes(-1)});
            if(name!="denied")db.Add(new UserRole {UserId=u.UserId,BranchId=b,RoleId=role.RoleId,AssignedAt=now.AddMinutes(-1),IsActive=true});
            if(name=="unscheduled"){await db.SaveChangesAsync(ct);continue;}
            var template=new ShiftTemplate {BranchId=b,ShiftCode="B11_"+name,ShiftName=name,StartTime=new(0,0),EndTime=new(23,59),IsActive=true};
            db.Add(template);await db.SaveChangesAsync(ct);
            var shift=new WorkShift {BranchId=b,DepartmentId=department.DepartmentId,ShiftTemplateId=template.ShiftTemplateId,
                ShiftDate=today,ScheduledStartAt=now.AddHours(-1),ScheduledEndAt=now.AddHours(1),Status="PLANNED",RequiredStaffCount=1,CreatedBy=u.UserId,CreatedAt=now};
            db.Add(shift);await db.SaveChangesAsync(ct);
            var a=new ShiftAssignment {ShiftId=shift.ShiftId,UserId=u.UserId,AssignmentStatus="ASSIGNED",AssignedBy=u.UserId,AssignedAt=now.AddMinutes(-1)};
            db.Add(a);await db.SaveChangesAsync(ct);assignments[name]=a.ShiftAssignmentId;
            if(name=="owner")
            {
                db.Add(new ShiftAttendance {ShiftAssignmentId=a.ShiftAssignmentId,AttendanceStatus="NOT_STARTED"});
                var floor=new Floor {BranchId=b,FloorCode="B11",FloorName="B11",IsActive=true};
                var type=new RoomType {TypeCode="B11",TypeName="B11",IsActive=true};db.AddRange(floor,type);await db.SaveChangesAsync(ct);
                var room=new Room {BranchId=b,FloorId=floor.FloorId,RoomTypeId=type.RoomTypeId,RoomNumber="B11",OperationalStatus="CLEANING",IsActive=true,CreatedAt=now};
                db.Add(room);await db.SaveChangesAsync(ct);
                var cleaningType=await db.ReferenceValues.FirstAsync(v=>v.GroupCode=="CLEANING_TYPE"&&v.IsActive,ct);
                var task=new CleaningTask {RoomId=room.RoomId,TaskNo="B11_OWNER",CleaningTypeId=cleaningType.ReferenceId,Status="IN_PROGRESS",CreatedBy=u.UserId,CreatedAt=now};
                db.Add(task);await db.SaveChangesAsync(ct);cleaningId=task.CleaningTaskId;
                db.Add(new CleaningAssignment {CleaningTaskId=cleaningId,AssigneeUserId=u.UserId,AssignedBy=u.UserId,AssignedAt=now.AddMinutes(-1),AssignmentStatus="ACTIVE"});
            }
            await db.SaveChangesAsync(ct);
        }
        db.ChangeTracker.Clear();
    }
    string Base(long? b=null)=>$"/api/branches/{b??branch}/workdays";
    async Task<HttpResponseMessage> Send(HttpMethod method,string path,Jar jar,object? body=null,string? version=null,bool csrf=true)
    {
        using var message=new HttpRequestMessage(method,path);
        lock(jar){if(jar.Cookies.Count>0)message.Headers.Add("Cookie",string.Join("; ",jar.Cookies.Select(x=>x.Key+"="+x.Value)));}
        if(csrf&&jar.Token is not null)message.Headers.Add(HotelOpsAntiforgery.HeaderName,jar.Token);
        if(version is not null)message.Headers.TryAddWithoutValidation("If-Match",version);
        if(body is not null)message.Content=JsonContent.Create(body);
        var response=await client.SendAsync(message,ct);
        lock(jar){if(response.Headers.TryGetValues("Set-Cookie",out var cookies))foreach(var header in cookies){var p=header.Split(';')[0].Split('=',2);if(p.Length==2)jar.Cookies[p[0]]=p[1];}}
        return response;
    }
    async Task<Jar> Login(string name)
    {
        var jar=new Jar();
        using(var r=await Send(HttpMethod.Get,"/api/auth/csrf",jar)){Check(r.IsSuccessStatusCode,"Production CSRF bootstrap "+name);using var j=JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));jar.Token=j.RootElement.GetProperty("csrfToken").GetString();}
        using(var r=await Send(HttpMethod.Post,"/api/auth/login",jar,new {users[name].Username,Password=password})){Check(r.IsSuccessStatusCode,"Real credential/cookie/SQL session login "+name);using var j=JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));jar.Token=j.RootElement.GetProperty("csrfToken").GetString();}
        return jar;
    }
    async Task<(JsonElement? Json,string? Etag)> Expect(HttpMethod method,string path,Jar jar,int status,string label,object? body=null,string? version=null,bool csrf=true)
    {
        using var r=await Send(method,path,jar,body,version,csrf);var text=await r.Content.ReadAsStringAsync(ct);
        Check((int)r.StatusCode==status,label+" HTTP "+(int)r.StatusCode+" expected "+status+((int)r.StatusCode==status?"":" body "+text));
        Check(r.Headers.Location is null || status==201,label+" no authentication redirect");
        Check(!text.Contains("SqlException",StringComparison.OrdinalIgnoreCase)&&!text.Contains("sessionKey",StringComparison.OrdinalIgnoreCase),label+" no SQL/session secret disclosure");
        if(!r.IsSuccessStatusCode||string.IsNullOrEmpty(text))return(null,r.Headers.ETag?.Tag);
        using var doc=JsonDocument.Parse(text);return(doc.RootElement.Clone(),r.Headers.ETag?.Tag);
    }
    async Task Acceptance()
    {
        await using var check=Db();
        var catalogBefore=await Fingerprint(check,"roles","permissions","role_permissions","shift_attendance");
        var jars=new Dictionary<string,Jar>();foreach(var name in users.Keys)jars[name]=await Login(name);
        var owner=jars["owner"]; var peer=jars["peer"];var empty=new Jar();
        Check(owner.Cookies.ContainsKey(HotelOpsCookieTicket.CookieName),"Production login emitted auth cookie");
        await Expect(HttpMethod.Get,"/api/auth/session",owner,200,"Production session round trip before Workday calls");
        await Expect(HttpMethod.Get,Base()+"/current",empty,401,"Anonymous current denied");
        empty.Cookies[HotelOpsCookieTicket.CookieName]="invalid";
        await Expect(HttpMethod.Get,Base()+"/current",empty,401,"Invalid cookie denied");
        await Expect(HttpMethod.Get,Base()+"/current",jars["restricted"],403,"Restricted session denied");
        await Expect(HttpMethod.Get,Base()+"/current",jars["denied"],403,"Missing SQL permission denied");
        await Expect(HttpMethod.Get,Base()+"/own-shifts",jars["denied"],403,"Missing SHIFT_VIEW denied");
        await Expect(HttpMethod.Get,Base()+"/current",jars["foreign"],403,"Foreign branch denied");
        await Expect(HttpMethod.Get,Base(otherBranch)+"/current",owner,403,"Owner cannot select another branch");
        await Expect(HttpMethod.Get,Base()+"/current",owner,204,"No own Working day");
        var shifts=await Expect(HttpMethod.Get,Base()+"/own-shifts",owner,200,"Own eligible shifts");
        Check(shifts.Json!.Value.EnumerateArray().Select(x=>x.GetProperty("shiftAssignmentId").GetInt64()).SequenceEqual(new[]{assignments["owner"]}),"Shift query includes only actor's assignment");
        await Expect(HttpMethod.Post,Base(),owner,400,"Unknown actor JSON rejected",new {shiftAssignmentId=assignments["owner"],userId=users["peer"].UserId});
        await Expect(HttpMethod.Post,Base(),owner,400,"Client timestamp JSON rejected",new {shiftAssignmentId=assignments["owner"],startedAt="2000-01-01T00:00:00Z"});
        await Expect(HttpMethod.Post,Base(),owner,400,"CSRF protects mutation",new{shiftAssignmentId=assignments["owner"]},csrf:false);
        await Expect(HttpMethod.Post,Base(),owner,403,"Foreign own assignment refused",new{shiftAssignmentId=assignments["peer"]});
        await Expect(HttpMethod.Post,Base(),jars["unscheduled"],400,"Unscheduled reason mandatory",new{});
        await Expect(HttpMethod.Post,Base(),jars["unscheduled"],503,"Unscheduled adapter fails closed",new{reason="Approved extra work"});
        Check(!await check.Workdays.AnyAsync(d=>d.UserId==users["unscheduled"].UserId,ct),"Unavailable unscheduled creates no workday");
        var lower=await SqlNow(check);
        var day=await Expect(HttpMethod.Post,Base(),owner,201,"Scheduled production Start",new{shiftAssignmentId=assignments["owner"]});
        var upper=await SqlNow(check);
        var id=day.Json!.Value.GetProperty("workdayId").GetInt64();var version=day.Etag!;
        Check(version=='"'+day.Json.Value.GetProperty("rowVersion").GetString()+'"',"Start emits strong rowversion ETag");
        var persisted=await check.Workdays.AsNoTracking().SingleAsync(d=>d.WorkdayId==id,ct);
        Check(persisted.UserId==users["owner"].UserId&&persisted.BranchId==branch&&persisted.StartedAt>=lower&&persisted.StartedAt<=upper,"Persisted server-time/actor/branch verified independently");
        Check(await check.AuditLogs.AnyAsync(a=>a.EntityType=="workdays"&&a.EntityId==id.ToString()&&a.Action=="CREATE",ct),"Start audit committed with workday");
        await Expect(HttpMethod.Post,Base(),owner,409,"Duplicate Start refused",new{shiftAssignmentId=assignments["owner"]});
        var current=await Expect(HttpMethod.Get,Base()+"/current",owner,200,"Current Working snapshot");
        Check(current.Json!.Value.GetProperty("workdayId").GetInt64()==id,"Current is own persisted day");
        await Expect(HttpMethod.Post,Base()+$"/{id}/end",peer,403,"Cross-user End denied",new{pauseAll=false},version);
        await Expect(HttpMethod.Post,Base()+$"/{id}/end",owner,400,"Missing If-Match denied",new{pauseAll=false});
        await Expect(HttpMethod.Post,Base()+$"/{id}/end",owner,400,"Weak If-Match denied",new{pauseAll=false},"W/"+version);
        await Expect(HttpMethod.Post,Base()+$"/{id}/end",owner,412,"Stale If-Match denied",new{pauseAll=false},'"'+Convert.ToBase64String(new byte[8])+'"');
        await Expect(HttpMethod.Post,Base()+$"/{id}/end",owner,503,"Pause All unavailable without mutation",new{pauseAll=true},version);
        await using(var db=Db())await using(var tx=await db.Database.BeginTransactionAsync(ct))
        {
            var p=new SqlWorkTimeTransactionParticipant(db);await p.LockUsersAsync([users["owner"].UserId],ct);
            await p.OpenIntervalAsync(new(users["owner"].UserId,users["owner"].UserId,branch,"CLEANING",cleaningId),ct);await tx.CommitAsync(ct);
        }
        await Expect(HttpMethod.Post,Base()+$"/{id}/end",owner,409,"End refuses open interval MSG31",new{pauseAll=false},version);
        var interval=await check.WorkIntervals.AsNoTracking().SingleAsync(i=>i.UserId==users["owner"].UserId&&i.ClosedAt==null,ct);
        Check(await check.Workdays.AnyAsync(d=>d.WorkdayId==id&&d.Status=="WORKING",ct)&&interval.ClosedAt is null
            &&await check.CleaningTasks.AnyAsync(t=>t.CleaningTaskId==cleaningId&&t.Status=="IN_PROGRESS",ct),"Rejected End preserves day/interval/source");
        await ScopeAndDelegationChecks(id);
        await using(var db=Db())await using(var tx=await db.Database.BeginTransactionAsync(ct))
        {
            var p=new SqlWorkTimeTransactionParticipant(db);await p.LockUsersAsync([users["owner"].UserId],ct);
            await p.CloseIntervalAsync(new(users["owner"].UserId,users["owner"].UserId,branch,"CLEANING",cleaningId),interval.IntervalId,interval.RowVersion,ct);await tx.CommitAsync(ct);
        }
        var ended=await Expect(HttpMethod.Post,Base()+$"/{id}/end",owner,200,"End with no open intervals",new{pauseAll=false},version);
        Check(ended.Json!.Value.GetProperty("status").GetString()=="ENDED"&&ended.Etag!=version,"End returns ENDED with new rowversion");
        Check(await check.Workdays.AnyAsync(d=>d.WorkdayId==id&&d.Status=="ENDED"&&d.EndedAt!=null,ct)
            &&await check.AuditLogs.AnyAsync(a=>a.EntityType=="workdays"&&a.EntityId==id.ToString()&&a.Action=="UPDATE",ct),"End and audit committed consistently");
        await Expect(HttpMethod.Get,Base()+"/current",owner,204,"Ended day is no longer current");
        await check.Users.Where(u=>u.UserId==users["inactive"].UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.EmploymentStatus,"INACTIVE"),ct);
        await Expect(HttpMethod.Get,Base()+"/current",jars["inactive"],403,"Inactive employment rejected after login");
        await check.UserSessions.Where(s=>s.UserId==users["revoked"].UserId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.RevokedAt,DateTime.UtcNow),ct);
        await Expect(HttpMethod.Get,Base()+"/current",jars["revoked"],401,"Revoked session rejected");
        var raceTasks=Enumerable.Range(0,2).Select(_=>Send(HttpMethod.Post,Base(),jars["race"],new{shiftAssignmentId=assignments["race"]})).ToArray();
        var responses=await Task.WhenAll(raceTasks);try{Check(responses.Select(r=>(int)r.StatusCode).Order().SequenceEqual(new[]{201,409}),"Concurrent authenticated Start yields one success and one conflict");}finally{foreach(var r in responses)r.Dispose();}
        Check(await check.Workdays.CountAsync(d=>d.UserId==users["race"].UserId&&d.Status=="WORKING",ct)==1,"Concurrent Start persists exactly one Working day");
        await RevocationWhileWaiting("permissionwait",jars["permissionwait"],false);
        await RevocationWhileWaiting("sessionwait",jars["sessionwait"],true);
        await AuditRollback(jars["fault"]);
        Check(catalogBefore==await Fingerprint(check,"roles","permissions","role_permissions","shift_attendance"),"RBAC catalog/grants and ShiftAttendance unchanged");
        using var openapi=await client.GetAsync("/openapi/v1.json",ct);var api=await openapi.Content.ReadAsStringAsync(ct);
        Check(openapi.IsSuccessStatusCode&&api.Contains("/api/branches/{branchId}/workdays/current")&&!api.Contains("/api/verification/task77"),"Production OpenAPI exposes Workday and no verification driver");
        Check(typeof(HotelOps.Api.Controllers.WorkdaysController).GetMethods().Count(m=>m.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute),true).Length>0)==4,"Exactly four Workday actions shipped");
    }
    async Task ScopeAndDelegationChecks(long dayId)
    {
        await using var db=Db();var cleaning=new CleaningTaskResourceResolver(new EfCleaningTaskAccessReader(db),TimeProvider.System);
        var wrapper=new WorkdayPermissionResourceResolver(db,cleaning);
        foreach(var actor in new[]{users["owner"].UserId,users["peer"].UserId})
        foreach(var code in new[]{"CLEANING_EXECUTE","CLEANING_VIEW","ATTENDANCE_RECORD"})
        foreach(var resource in new[]{new PermissionResource("cleaning_task",cleaningId),new PermissionResource("unknown",cleaningId)})
            Check(await wrapper.ResolveAsync(code,resource,actor,ct)==await cleaning.ResolveAsync(code,resource,actor,ct),"Cleaning delegation unchanged "+actor+"/"+code+"/"+resource.ResourceType);
        foreach(var scope in new[]{"OWN","BRANCH","ALL"})
        {
            var ownerIdentity=new Identity(new(users["owner"].UserId,1,false));var peerIdentity=new Identity(new(users["peer"].UserId,1,false));
            var data=new WideGrant(scope,branch);
            var ownerPermission=new PermissionService(ownerIdentity,data,wrapper,TimeProvider.System);
            var peerPermission=new PermissionService(peerIdentity,data,wrapper,TimeProvider.System);
            Check((await ownerPermission.EvaluateResourceAsync("ATTENDANCE_RECORD",new("workday",dayId),ct)).IsGranted,"Real SQL ownership accepted with synthetic "+scope+" path");
            Check(!(await peerPermission.EvaluateResourceAsync("ATTENDANCE_RECORD",new("workday",dayId),ct)).IsGranted,"Real SQL foreign ownership refused even with synthetic "+scope+" path");
            Check(!(await peerPermission.EvaluateResourceAsync("SHIFT_VIEW",new("shift_assignment",assignments["owner"]),ct)).IsGranted,"Foreign shift refused even with synthetic "+scope+" path");
        }
    }
    async Task RevocationWhileWaiting(string name,Jar jar,bool session)
    {
        await using var block=Db();await block.Database.OpenConnectionAsync(ct);await using var tx=await block.Database.BeginTransactionAsync(ct);
        var resource="HotelOps:WorkTime:User:"+users[name].UserId;
        await block.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=1000",ct);
        var spid=await block.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
        var pending=Send(HttpMethod.Post,Base(),jar,new{shiftAssignmentId=assignments[name]});
        await WaitBlocked(spid,"Production Start blocked on A1 time lock before "+name);
        await using(var revoke=Db())
        {
            if(session)await revoke.UserSessions.Where(s=>s.UserId==users[name].UserId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.RevokedAt,DateTime.UtcNow),ct);
            else await revoke.UserRoles.Where(r=>r.UserId==users[name].UserId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,false),ct);
        }
        await tx.CommitAsync(ct);
        using var response=await pending;Check((int)response.StatusCode==(session?401:403),"Revocation committed during A1 lock wait is revalidated: "+name);
        await using var check=Db();Check(!await check.Workdays.AnyAsync(d=>d.UserId==users[name].UserId,ct),"No workday after wait-time revocation: "+name);
    }
    async Task AuditRollback(Jar jar)
    {
        await using var blocker=Db();await blocker.Database.OpenConnectionAsync(ct);await using var tx=await blocker.Database.BeginTransactionAsync(ct);
        await blocker.Database.ExecuteSqlRawAsync("SELECT TOP(1) audit_id FROM dbo.audit_logs WITH(TABLOCKX,HOLDLOCK)",ct);
        var spid=await blocker.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
        var pending=Send(HttpMethod.Post,Base(),jar,new{shiftAssignmentId=assignments["fault"]});
        await WaitBlocked(spid,"Production Start blocked on real SQL audit insert");
        await using(var inspect=Db())
        {
            var actor=users["fault"].UserId;
            var dirty=await inspect.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.workdays WITH(READUNCOMMITTED) WHERE user_id={actor}").SingleAsync(ct);
            Check(dirty==1,"Actual Workday INSERT occurred before audit write failure");
        }
        using(var response=await pending)Check((int)response.StatusCode==503,"Real SQL audit lock timeout fails production Start closed");
        await tx.RollbackAsync(ct);
        await using var check=Db();Check(!await check.Workdays.AnyAsync(d=>d.UserId==users["fault"].UserId,ct),"Audit timeout rolled back inserted Workday");
        Check(!await check.AuditLogs.AnyAsync(a=>a.EntityType=="workdays"&&a.ActorUserId==users["fault"].UserId,ct),"Audit failure leaves no false workday audit");
    }
    async Task WaitBlocked(int blocker,string label)
    {
        await using var monitor=Db();var found=false;
        for(var i=0;i<180;i++){if(await monitor.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.dm_exec_requests WHERE blocking_session_id={blocker}").SingleAsync(ct)>0){found=true;break;}await Task.Delay(25,ct);}
        Check(found,label+" (DMV proof)");
    }
    static async Task<DateTime> SqlNow(HotelOpsDbContext db)=>await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync();
    async Task<string> Fingerprint(HotelOpsDbContext db,params string[] tables)
    {
        await db.Database.OpenConnectionAsync(ct);var all="";
        foreach(var table in tables){if(!Regex.IsMatch(table,"^[a-z_]+$"))throw new Exception("Invalid table");await using var command=db.Database.GetDbConnection().CreateCommand();command.CommandText=$"SELECT * FROM dbo.[{table}] ORDER BY 1 FOR JSON PATH,INCLUDE_NULL_VALUES";await using var r=await command.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))all+=r.GetString(0);}
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(all)));
    }
    sealed class Jar {public Dictionary<string,string> Cookies {get;}=[];public string? Token {get;set;}}
    sealed class Identity(CurrentUserInfo actor):ICurrentUserAccessor {public bool TryGetCurrentUser(out CurrentUserInfo user){user=actor;return true;}}
    sealed class WideGrant(string scope,long branch):IPermissionDataSource
    {
        public Task<bool> IsActiveUserAsync(long id,CancellationToken token)=>Task.FromResult(true);
        public Task<bool> IsActiveBranchAsync(long id,CancellationToken token)=>Task.FromResult(true);
        public Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long id,string code,DateTime now,CancellationToken token)
            =>Task.FromResult<IReadOnlyList<PermissionGrantPath>>([new(code,scope,scope=="ALL"?null:branch,true,true,now.AddDays(-1),null,true,true)]);
    }
}
