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
    TrustServerCertificate = true, ConnectTimeout = 10, ApplicationName = "HotelOps Task84 owned fixture continuation"
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
if(await db.Users.CountAsync(ct)!=12 || await db.Rooms.CountAsync(ct)!=6 || await db.RoomTypes.CountAsync(ct)!=4
    || await db.Users.AnyAsync(x=>x.Username=="rt84.account",ct)) throw new InvalidOperationException("Task84 accepted fixture checkpoint required; no overwrite");
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
var branch=await db.Branches.AsNoTracking().SingleAsync(x=>x.BranchCode=="BR001",ct);
var department=await db.Departments.AsNoTracking().SingleAsync(x=>x.BranchId==branch.BranchId && x.DepartmentCode=="HK",ct);
var role=await db.Roles.AsNoTracking().SingleAsync(x=>x.RoleCode=="SYS_ADMIN",ct);
var now=DateTime.UtcNow;
var password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))+"aA1-";
var hasher=new PasswordCredentialService(Options.Create(new PasswordHasherOptions {CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3,IterationCount=PasswordCredentialService.BaselineIterationCount}));
var user=new User {EmployeeCode="RT84_ACCOUNT",Username="rt84.account",Email="rt84.account@example.invalid",FullName="Task84 inactive account probe",PrimaryBranchId=branch.BranchId,DepartmentId=department.DepartmentId,AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",CreatedAt=now};
user.PasswordHash=hasher.HashPassword(user,password);db.Add(user);await db.SaveChangesAsync(ct);
db.Add(new UserRole {UserId=user.UserId,RoleId=role.RoleId,BranchId=branch.BranchId,IsActive=true,AssignedAt=now.AddMinutes(-1)});
db.Add(new UserBranchAccess {UserId=user.UserId,BranchId=branch.BranchId,AccessLevel="STANDARD",IsPrimary=true,GrantedAt=now.AddMinutes(-1)});
await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
var users=new Dictionary<string,User>{{"account",user}};
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
    var jar=await Login("account");
    var route="/api/room-types?branchId="+branch.BranchId;
    var before=await Fingerprint("room_types","rooms","audit_logs","roles","permissions","role_permissions");
    using(var positive=await Send(HttpMethod.Get,route,jar)) Check(positive.IsSuccessStatusCode,"fresh fixture account authorized before deactivation");
    Check(before==await Fingerprint("room_types","rooms","audit_logs","roles","permissions","role_permissions"),"valid read preserves business/audit/catalog");
    await db.Users.Where(x=>x.UserId==user.UserId).ExecuteUpdateAsync(x=>x.SetProperty(u=>u.AccountStatus,"INACTIVE"),ct);
    using(var denied=await Send(HttpMethod.Get,route,jar)) Check((int)denied.StatusCode==401,"inactive account invalidates actual SQL session");
    await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(new {success=true,checks=cases.Count,cases,database=args[0],kind="SUPPLEMENTAL_REAL_HTTPS_SQL_ACCOUNT_ELIGIBILITY",fixturesAdded=1,preexistingTask84FixtureRowsChanged=0},new JsonSerializerOptions{WriteIndented=true}),ct);
    Console.WriteLine($"PASS {cases.Count} supplemental actual HTTPS/SQL checks");
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
