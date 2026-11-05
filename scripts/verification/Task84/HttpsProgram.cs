using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

if (args.Length != 2 || !Regex.IsMatch(args[0], @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$") || File.Exists(args[1]))
    throw new ArgumentException("New guarded disposable trial and unused receipt path required.");
var root = Directory.GetCurrentDirectory();
var connection = new SqlConnectionStringBuilder
{
    DataSource = @"REDACTED_LOCAL_SQL_HOST\SQLEXPRESS", InitialCatalog = args[0], IntegratedSecurity = true,
    TrustServerCertificate = true, ConnectTimeout = 10, ApplicationName = "HotelOps UC037 disposable acceptance"
};
using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(8));
var ct = budget.Token;
HotelOpsDbContext Open() => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection.ConnectionString).Options);
await using var db = Open();
await db.Database.OpenConnectionAsync(ct);
await using (var identity = db.Database.GetDbConnection().CreateCommand())
{
    identity.CommandText = "SELECT CONVERT(nvarchar(128),@@SERVERNAME),DB_NAME(),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')";
    await using var reader = await identity.ExecuteReaderAsync(ct);
    if (!await reader.ReadAsync(ct) || reader.GetString(0) != connection.DataSource || reader.GetString(1) != args[0]
        || reader.IsDBNull(2) || reader.GetString(2) != "Task17") throw new InvalidOperationException("Trial identity rejected.");
}
if(args[0] != "HotelOps_Canonical_20261104_044211_7a6d6e19_Fresh") throw new InvalidOperationException("Explicit Task84 target required");
if(await db.Users.AnyAsync(ct) || await db.UserSessions.AnyAsync(ct) || await db.RoomTypes.AnyAsync(ct)) throw new InvalidOperationException("Fresh owned fixtures only; no reset");
var deployment=await db.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),value) AS [Value] FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.room_types') AND name=N'HotelOps.UC017.Deployment'").SingleAsync(ct);
if(deployment != "Task69-v1") throw new InvalidOperationException("Approved V1 schema required");
var cases = new List<string>();
void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + label);
    cases.Add(label); Console.WriteLine("PASS: " + label);
}
async Task<string> Fingerprint(params string[] tables)
{
    var hashes = new List<string>();
    foreach (var table in tables)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"SELECT * FROM dbo.[{table}] ORDER BY 1 FOR JSON PATH,INCLUDE_NULL_VALUES";
        var text = new StringBuilder();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) text.Append(reader.GetString(0));
        hashes.Add(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))));
    }
    return string.Join(":", hashes);
}
var catalogBefore = await Fingerprint("roles", "permissions", "role_permissions", "branches", "departments", "reference_values");
var branches = await db.Branches.OrderBy(x => x.BranchCode).ToArrayAsync(ct);
var a = branches.Single(x => x.BranchCode == "BR001").BranchId;
var b = branches.Single(x => x.BranchCode == "BR002").BranchId;
var departments = await db.Departments.ToArrayAsync(ct);
var roles = await db.Roles.ToDictionaryAsync(x => x.RoleCode, ct);
var now = DateTime.UtcNow;
var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "aA1-";
var hasher = new PasswordCredentialService(Options.Create(new PasswordHasherOptions
    { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
var users = new Dictionary<string, User>();
foreach (var name in new[] { "supervisor", "other", "denied", "admin", "restricted", "own" })
{
    var branch = name == "other" ? b : a;
    var user = new User { EmployeeCode = "CM_" + name, Username = "cm." + name, Email = "cm." + name + "@example.invalid",
        FullName = "Monitor " + name, PrimaryBranchId = branch,
        DepartmentId = departments.Single(x => x.BranchId == branch && x.DepartmentCode == "HK").DepartmentId,
        AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = now,
        TemporaryPasswordExpiresAt = name == "restricted" ? now.AddHours(24) : null };
    user.PasswordHash = hasher.HashPassword(user, password); db.Add(user); users[name] = user;
}
await db.SaveChangesAsync(ct);
foreach (var pair in users)
{
    var branch = pair.Value.PrimaryBranchId!.Value;
    var code = pair.Key is "own" or "denied" ? "HK_STAFF" : "SYS_ADMIN";
    db.Add(new UserRole { UserId = pair.Value.UserId, RoleId = roles[code].RoleId,
        BranchId = pair.Key == "admin" ? null : branch, IsActive = true, AssignedAt = now.AddMinutes(-1) });
    if (pair.Key != "admin") db.Add(new UserBranchAccess { UserId = pair.Value.UserId, BranchId = branch,
        AccessLevel = "STANDARD", IsPrimary = true, GrantedAt = now.AddMinutes(-1) });
}
var floorA = new Floor { BranchId = a, FloorCode = "RT84_A", FloorName = "RT84 A", IsActive = true };
var floorB = new Floor { BranchId = b, FloorCode = "RT84_B", FloorName = "RT84 B", IsActive = true };
var legacy = new RoomType { TypeCode = "RT84_LEGACY", TypeName = "Legacy type", IsActive = true, StandardCapacity = 2 };
db.AddRange(floorA, floorB, legacy); await db.SaveChangesAsync(ct);
var checklist = new InspectionChecklist { ChecklistCode = "RT84", ChecklistName = "Read fixture",
    RoomTypeId = legacy.RoomTypeId, IsActive = true, CreatedBy = users["admin"].UserId, CreatedAt = now };
db.Add(checklist); await db.SaveChangesAsync(ct);
async Task<long> InsertType(long branch, string code, string name, bool active)
{
    await using var command = db.Database.GetDbConnection().CreateCommand();
    command.CommandText = "INSERT dbo.room_types(branch_id,type_code,type_name,standard_cleaning_minutes,default_checklist_id,is_active) OUTPUT inserted.room_type_id VALUES(@branch,@code,@name,30,@checklist,@active)";
    command.Parameters.Add(new SqlParameter("@branch",branch)); command.Parameters.Add(new SqlParameter("@code",code));
    command.Parameters.Add(new SqlParameter("@name",name)); command.Parameters.Add(new SqlParameter("@checklist",checklist.ChecklistId));
    command.Parameters.Add(new SqlParameter("@active",active));
    return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
}
var typeA = await InsertType(a,"RT84_DUP","Alpha type",true);
var typeB = await InsertType(b,"RT84_DUP","Other branch type",true);
var inactiveType = await InsertType(a,"RT84_Z","Inactive type",false);
foreach (var room in new[] { (a,floorA.FloorId,typeA,true), (a,floorA.FloorId,typeA,true),
    (a,floorA.FloorId,typeA,false), (b,floorB.FloorId,typeB,true),
    (a,floorA.FloorId,legacy.RoomTypeId,true), (b,floorB.FloorId,legacy.RoomTypeId,true) }.Select((x,i)=>new Room {
        BranchId=x.Item1,FloorId=x.Item2,RoomTypeId=x.Item3,IsActive=x.Item4,RoomNumber="RT84_"+i,
        OperationalStatus="DIRTY",CreatedAt=now })) db.Add(room);
await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();

using var rsa = RSA.Create(2048);
var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback); request.CertificateExtensions.Add(san.Build());
using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
var certFile = Path.Combine(Path.GetTempPath(), "HotelOps-CM-" + Guid.NewGuid().ToString("N") + ".pfx");
var certPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
await File.WriteAllBytesAsync(certFile, certificate.Export(X509ContentType.Pfx, certPassword), ct);
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(root, "src/HotelOps.Api"), UseShellExecute = false,
    CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
start.ArgumentList.Add(Path.Combine(root, "scripts/verification/Task84/artifacts/production/bin/HotelOps.Api/debug/HotelOps.Api.dll"));
start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; start.Environment["DOTNET_ENVIRONMENT"] = "Development";
start.Environment["ASPNETCORE_URLS"] = "https://127.0.0.1:" + port;
start.Environment["ConnectionStrings__HotelOpsDb"] = connection.ConnectionString;
start.Environment["Kestrel__Certificates__Default__Path"] = certFile;
start.Environment["Kestrel__Certificates__Default__Password"] = certPassword;
start.Environment["Logging__LogLevel__Default"] = "None";
Process? process = null;
try
{
    process = Process.Start(start) ?? throw new InvalidOperationException("API process did not start.");
    var stdout = process.StandardOutput.ReadToEndAsync(ct); var stderr = process.StandardError.ReadToEndAsync(ct);
    using var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false,
        ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.GetCertHashString() == certificate.GetCertHashString() };
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(20) };
    var ready = false;
    for (var attempt = 0; attempt < 60 && !process.HasExited; attempt++)
    {
        try { using var response = await http.GetAsync("/api/health/database", ct); if (response.IsSuccessStatusCode) { ready = true; break; } }
        catch (HttpRequestException) { }
        await Task.Delay(250, ct);
    }
    Check(ready, "actual production API startup and prerequisite schema gate");
    async Task<HttpResponseMessage> Send(HttpMethod method, string path, Jar jar, object? body = null, CancellationToken? token = null)
    {
        using var message = new HttpRequestMessage(method, path);
        if (jar.Cookies.Count != 0) message.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(x => x.Key + "=" + x.Value)));
        if (jar.Token is not null) message.Headers.Add(HotelOpsAntiforgery.HeaderName, jar.Token);
        if (body is not null) message.Content = JsonContent.Create(body);
        var response = await http.SendAsync(message, token ?? ct); jar.Read(response); return response;
    }
    async Task<Jar> Login(string name)
    {
        var jar = new Jar();
        using (var bootstrap = await Send(HttpMethod.Get, "/api/auth/csrf", jar))
        {
            Check(bootstrap.IsSuccessStatusCode, "actual CSRF bootstrap " + name);
            using var json = JsonDocument.Parse(await bootstrap.Content.ReadAsStringAsync(ct)); jar.Token = json.RootElement.GetProperty("csrfToken").GetString();
        }
        using var response = await Send(HttpMethod.Post, "/api/auth/login", jar, new { Username = users[name].Username, Password = password });
        Check(response.IsSuccessStatusCode, "actual credential login and SQL session " + name);
        using var login = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); jar.Token = login.RootElement.GetProperty("csrfToken").GetString();
        return jar;
    }
    var supervisor = await Login("supervisor"); var other = await Login("other"); var denied = await Login("denied");
    var admin = await Login("admin"); var restricted = await Login("restricted"); var own = await Login("own");
    var anonymous = new Jar();
    async Task<JsonElement?> Expect(Jar jar, string path, int status, string label)
    {
        using var response = await Send(HttpMethod.Get, path, jar);
        Check((int)response.StatusCode == status && response.Headers.Location is null, label);
        var content = await response.Content.ReadAsStringAsync(ct);
        Check(!content.Contains("passwordHash", StringComparison.OrdinalIgnoreCase)
            && !content.Contains("sessionKey", StringComparison.OrdinalIgnoreCase)
            && !content.Contains("SqlException", StringComparison.OrdinalIgnoreCase), label + " no sensitive/provider disclosure");
        if (!response.IsSuccessStatusCode) return null;
        Check(response.Headers.CacheControl?.NoStore == true, label + " no-store");
        using var json = JsonDocument.Parse(content); return json.RootElement.Clone();
    }
    var route = "/api/room-types";
    var beforeReads = await Fingerprint("room_types","rooms","inspection_checklists","audit_logs");
    await Expect(anonymous,route,401,"anonymous denied");
    await Expect(denied,route,403,"missing MASTER_DATA_MANAGE denied");
    await Expect(own,route,403,"HK OWN permissions do not confer master-data permission");
    await Expect(restricted,route,403,"restricted session denied");
    var all = (await Expect(admin,route,200,"actual production global list"))!.Value;
    Check(all.GetProperty("total").GetInt32()==4,"global count includes exactly scoped and legacy fixtures");
    var local = (await Expect(supervisor,route,200,"automatic branch-bound role scope"))!.Value;
    Check(local.GetProperty("total").GetInt32()==2 && local.GetProperty("items").EnumerateArray()
        .All(x=>x.GetProperty("details").GetProperty("branchId").GetInt64()==a),"automatic scope/count exclude foreign and legacy ownership");
    var foreign = (await Expect(other,route,200,"other branch automatic scope"))!.Value;
    Check(foreign.GetProperty("total").GetInt32()==1,"branch B count has no A metadata leakage");
    var filtered = (await Expect(admin,route+"?branchId="+a,200,"explicit branch filter"))!.Value;
    Check(filtered.GetProperty("total").GetInt32()==2,"global actor respects requested branch filter");
    var inactive = (await Expect(admin,route+"?isActive=false",200,"inactive status filter"))!.Value;
    Check(inactive.GetProperty("total").GetInt32()==1 && inactive.GetProperty("items")[0].GetProperty("details").GetProperty("id").GetInt64()==inactiveType,"status filters actual stored state");
    foreach(var (search,expected) in new[]{("RT84_DUP",2),("Alpha",1),("not-found",0)})
    {
        var page=(await Expect(admin,route+"?search="+search,200,"keyword "+search))!.Value;
        Check(page.GetProperty("total").GetInt32()==expected,"SQL keyword filtered count "+search);
    }
    var first=(await Expect(admin,route+"?pageSize=1",200,"first tied-code page"))!.Value;
    var second=(await Expect(admin,route+"?pageSize=1&page=2",200,"second tied-code page"))!.Value;
    Check(first.GetProperty("items")[0].GetProperty("details").GetProperty("id").GetInt64()==typeA
        && second.GetProperty("items")[0].GetProperty("details").GetProperty("id").GetInt64()==typeB,
        "code ties order by authoritative branch/id without duplicates");
    foreach(var sort in new[]{"name","status"}) await Expect(admin,route+"?sort="+sort+"&descending=true",200,"supported sort "+sort);
    var empty=(await Expect(admin,route+"?page=20",200,"empty page"))!.Value;
    Check(empty.GetProperty("items").GetArrayLength()==0 && empty.GetProperty("total").GetInt32()==4,"empty page preserves total");
    var detail=(await Expect(supervisor,route+"/"+typeA+"?branchId="+a,200,"owned branch detail"))!.Value;
    Check(detail.GetProperty("activeRooms").GetInt32()==2 && detail.GetProperty("canUpdate").GetBoolean(),"active count excludes inactive rooms and scoped action metadata");
    var legacyDetail=(await Expect(admin,route+"/"+legacy.RoomTypeId,200,"global-only legacy detail"))!.Value;
    Check(legacyDetail.GetProperty("details").GetProperty("branchId").ValueKind==JsonValueKind.Null
        && legacyDetail.GetProperty("activeRooms").GetInt32()==2 && !legacyDetail.GetProperty("canUpdate").GetBoolean(),"legacy ownership not invented; count global; no branch update action");
    using(var tagged=await Send(HttpMethod.Get,route+"/"+typeA+"?branchId="+a,admin))
        Check(tagged.IsSuccessStatusCode && tagged.Headers.ETag is {IsWeak:false},"detail returns strong historical ETag");
    await Expect(supervisor,route+"?branchId="+b,403,"foreign explicit branch denied");
    await Expect(supervisor,route+"/"+typeB+"?branchId="+a,404,"foreign type masked within authorized branch");
    await Expect(supervisor,route+"/"+legacy.RoomTypeId,403,"branch actor cannot read NULL ownership globally");
    await Expect(supervisor,route+"/"+legacy.RoomTypeId+"?branchId="+a,404,"legacy is not adopted into client branch");
    await Expect(admin,route+"/9223372036854775807?branchId="+a,404,"missing detail");
    foreach(var query in new[]{"page=0","pageSize=0","pageSize=101","page=2147483647&pageSize=100",
        "sort=invalid","branchId=0","branchId=-1","isActive=invalid","search="+new string('x',151)})
        await Expect(admin,route+"?"+query,400,"invalid query "+query);
    await Expect(admin,route+"/0?branchId="+a,400,"invalid detail id");
    Check(beforeReads==await Fingerprint("room_types","rooms","inspection_checklists","audit_logs"),"list/detail/filter/validation preserve exact business and audit data");
    await using(var blocker=new SqlConnection(connection.ConnectionString))
    {
        await blocker.OpenAsync(ct); await using var tx=(SqlTransaction)await blocker.BeginTransactionAsync(ct);
        await using var command=blocker.CreateCommand();command.Transaction=tx;
        command.CommandText="UPDATE dbo.room_types SET type_name=N'Uncommitted RT84' WHERE room_type_id=@id;SELECT CONVERT(int,@@SPID)";
        command.Parameters.AddWithValue("@id",typeA);var spid=Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        var pending=Send(HttpMethod.Get,route+"/"+typeA+"?branchId="+a,supervisor);
        var observed=false;
        for(var i=0;i<40;i++)
        {
            await using var observer=db.Database.GetDbConnection().CreateCommand();
            observer.CommandText="SELECT COUNT(*) FROM sys.dm_exec_requests WHERE database_id=DB_ID() AND blocking_session_id=@spid";
            var p=observer.CreateParameter();p.ParameterName="@spid";p.Value=spid;observer.Parameters.Add(p);
            if(Convert.ToInt32(await observer.ExecuteScalarAsync(ct))>0){observed=true;break;}
            await Task.Delay(100,ct);
        }
        Check(observed && !pending.IsCompleted,"DMV confirms real HTTPS detail waits for SQL writer");
        await tx.RollbackAsync(ct);using var response=await pending;
        Check(response.IsSuccessStatusCode,"detail completes after actual SQL rollback");
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Check(json.RootElement.GetProperty("details").GetProperty("name").GetString()=="Alpha type","uncommitted room-type metadata never leaks");
    }
    Check(beforeReads==await Fingerprint("room_types","rooms","inspection_checklists","audit_logs"),"read consistency/rollback preserve exact business and audit data");
    await db.UserRoles.Where(x=>x.UserId==users["supervisor"].UserId).ExecuteUpdateAsync(x=>x.SetProperty(r=>r.IsActive,false),ct);
    await Expect(supervisor,route,403,"role revocation reflected through SQL");
    await db.UserRoles.Where(x=>x.UserId==users["supervisor"].UserId).ExecuteUpdateAsync(x=>x.SetProperty(r=>r.IsActive,true),ct);
    await db.UserBranchAccesses.Where(x=>x.UserId==users["supervisor"].UserId).ExecuteUpdateAsync(x=>x.SetProperty(r=>r.RevokedAt,now),ct);
    await Expect(supervisor,route,403,"branch access revocation reflected through SQL");
    await db.UserBranchAccesses.Where(x=>x.UserId==users["supervisor"].UserId).ExecuteUpdateAsync(x=>x.SetProperty(r=>r.RevokedAt,(DateTime?)null),ct);
    await db.Users.Where(x=>x.UserId==users["other"].UserId).ExecuteUpdateAsync(x=>x.SetProperty(r=>r.EmploymentStatus,"INACTIVE"),ct);
    await Expect(other,route,401,"inactive employment invalidates actual session");
    await db.UserSessions.Where(x=>x.UserId==users["own"].UserId).ExecuteUpdateAsync(x=>x.SetProperty(r=>r.ExpiresAt,DateTime.UtcNow.AddSeconds(-1)),ct);
    await Expect(own,route,401,"expired SQL session denied");
    await db.UserSessions.Where(x=>x.UserId==users["supervisor"].UserId).ExecuteUpdateAsync(x=>x.SetProperty(r=>r.RevokedAt,now),ct);
    await Expect(supervisor,route,401,"revoked SQL session denied");
    Check(catalogBefore==await Fingerprint("roles","permissions","role_permissions","branches","departments","reference_values"),"catalog/grants/master data unchanged by fixtures and requests");
    await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(new {database=args[0],success=true,checks=cases.Count,cases,
        kind="REAL_PRODUCTION_HTTPS_SQL_UC142",apiAssembly="scripts/verification/Task84/artifacts/production/bin/HotelOps.Api/debug/HotelOps.Api.dll",
        scopeNotes="Canonical MASTER_DATA_MANAGE is ALL; branch-bound role assignments tested. No invented BRANCH/OWN grants.",
        completedUCs=26,verifiedPoints=60},new JsonSerializerOptions{WriteIndented=true}),ct);
    Console.WriteLine($"PASS {cases.Count} actual production HTTPS/SQL UC142 checks; no duplicate completion points.");
}

finally
{
    try
    {
        if (process is not null)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None); process.Dispose();
        }
    }
    finally { File.Delete(certFile); } // Only this invocation's exact temporary certificate.
}

sealed class Jar
{
    public Dictionary<string, string> Cookies { get; } = new();
    public string? Token { get; set; }
    public void Read(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return;
        foreach (var value in values)
        {
            var pair = value.Split(';')[0].Split('=', 2);
            if (pair.Length == 2) Cookies[pair[0]] = pair[1];
        }
    }
}
