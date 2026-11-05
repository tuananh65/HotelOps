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
if(args[0] != "HotelOps_Canonical_20261104_133500_c03885b1_Fresh") throw new InvalidOperationException("Exact new Task85B trial only" );
if(await db.Users.CountAsync(ct)!=24 || await db.UserRoles.CountAsync(ct)!=18 || await db.Rooms.CountAsync(ct)!=3 || !await db.Users.AllAsync(u=>u.Username.StartsWith("t85b"),ct)) throw new Exception("Retained accepted fixture checkpoint required");
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
foreach (var name in new[] { "supervisor" })
{
    var branch = name == "other" ? b : a;
    var user = new User { EmployeeCode = "T85BORACLE_" + name, Username = "t85boracle." + name, Email = "t85boracle." + name + "@example.invalid",
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
    var code = pair.Key switch { "admin" => "SYS_ADMIN", "own" => "HK_STAFF", "denied" => "RECEPTION", _ => "HK_SUPERVISOR" };
    db.Add(new UserRole { UserId = pair.Value.UserId, RoleId = roles[code].RoleId,
        BranchId = pair.Key == "admin" ? null : branch, IsActive = true, AssignedAt = now.AddMinutes(-1) });
    if (pair.Key != "admin") db.Add(new UserBranchAccess { UserId = pair.Value.UserId, BranchId = branch,
        AccessLevel = "STANDARD", IsPrimary = true, GrantedAt = now.AddMinutes(-1) });
}
await db.SaveChangesAsync(ct); // Persist newly added fixture grants before clearing the tracker.
var roomType=await db.RoomTypes.SingleAsync(x=>x.TypeCode=="T85B_TYPE",ct);
var room=await db.Rooms.Where(x=>x.RoomNumber=="T85B_ROOM").Select(x=>x.RoomId).SingleAsync(ct);
var emptyRoom=await db.Rooms.Where(x=>x.RoomNumber=="T85B_EMPTY").Select(x=>x.RoomId).SingleAsync(ct);
var foreignRoom=await db.Rooms.Where(x=>x.RoomNumber=="T85B_FOREIGN").Select(x=>x.RoomId).SingleAsync(ct);
var cycle=await db.CleaningTasks.SingleAsync(x=>x.TaskNo=="T85B_CYCLE",ct);
var cancelled=await db.CleaningTasks.SingleAsync(x=>x.TaskNo=="T85B_CANCELLED",ct);
var foreignCycle=await db.CleaningTasks.SingleAsync(x=>x.TaskNo=="T85B_FOREIGN",ct);
var version=await db.InspectionChecklistVersions.SingleAsync(x=>x.Checklist.ChecklistCode=="T85B_HISTORY",ct);
var at=DateTime.SpecifyKind(cycle.ActualStartAt!.Value,DateTimeKind.Utc);
var attachment=await db.Attachments.SingleAsync(x=>x.EntityId==cycle.CleaningTaskId && x.DeletedAt==null && x.StorageKey=="history.pdf",ct);
var unrelated=await db.Attachments.SingleAsync(x=>x.EntityId==foreignCycle.CleaningTaskId,ct);
var deleted=await db.Attachments.SingleAsync(x=>x.DeletedAt!=null,ct);
var traversal=await db.Attachments.SingleAsync(x=>x.StorageKey=="../history.pdf",ct);
var evidenceRoot=Path.Combine(root,"scripts/verification/Task85/PhaseB/private-evidence");
var pdf=await File.ReadAllBytesAsync(Path.Combine(evidenceRoot,"history.pdf"),ct);
db.ChangeTracker.Clear();

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
start.ArgumentList.Add(Path.Combine(root, "scripts/verification/Task85/PhaseB/artifacts/integrated/bin/HotelOps.Api/debug/HotelOps.Api.dll"));
start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; start.Environment["DOTNET_ENVIRONMENT"] = "Development";
start.Environment["ASPNETCORE_URLS"] = "https://127.0.0.1:" + port;
start.Environment["ConnectionStrings__HotelOpsDb"] = connection.ConnectionString;
start.Environment["Kestrel__Certificates__Default__Path"] = certFile;
start.Environment["Kestrel__Certificates__Default__Password"] = certPassword;
start.Environment["Logging__LogLevel__Default"] = "None";
start.Environment["Evidence__PrivateRoot"] = evidenceRoot;
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
    var supervisor = await Login("supervisor");
    var anonymous = new Jar();
    async Task<JsonElement?> Expect(Jar jar, string path, int status, string label)
    {
        using var response = await Send(HttpMethod.Get, path, jar);
        Check((int)response.StatusCode == status && response.Headers.Location is null, label + $" expected={status} actual={(int)response.StatusCode}");
        var content = await response.Content.ReadAsStringAsync(ct);
        Check(!content.Contains("passwordHash", StringComparison.OrdinalIgnoreCase)
            && !content.Contains("sessionKey", StringComparison.OrdinalIgnoreCase)
            && !content.Contains("SqlException", StringComparison.OrdinalIgnoreCase), label + " no sensitive/provider disclosure");
        if (!response.IsSuccessStatusCode) return null;
        Check(response.Headers.CacheControl?.NoStore == true, label + " no-store");
        using var json = JsonDocument.Parse(content); return json.RootElement.Clone();
    }
    var route=$"/api/rooms/{room}/turnover-history?branchId={a}";
    var list=(await Expect(supervisor,route,200,"field-oracle list"))!.Value;
    var cycleDto=list.GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("id").GetInt64()==cycle.CleaningTaskId);
    var typeName=await db.ReferenceValues.Where(x=>x.ReferenceId==cycle.CleaningTypeId).Select(x=>x.DisplayName).SingleAsync(ct);
    Check(cycleDto.GetProperty("cleaningType").GetString()==typeName,"cleaning type equals authoritative SQL reference display name");
    Check(cycleDto.GetProperty("start").GetDateTime().Ticks==cycle.ActualStartAt!.Value.Ticks
        && cycleDto.GetProperty("end").GetDateTime().Ticks==cycle.ActualEndAt!.Value.Ticks,"start/end exact SQL timestamp ticks");
    var assignees=await db.CleaningAssignments.Where(x=>x.CleaningTaskId==cycle.CleaningTaskId).OrderBy(x=>x.AssignedAt).ThenBy(x=>x.AssignmentId).Select(x=>x.AssigneeUserId).ToArrayAsync(ct);
    Check(cycleDto.GetProperty("assignees").EnumerateArray().Select(x=>x.GetProperty("userId").GetInt64()).SequenceEqual(assignees),"historical assignee identities/order match persisted assignment rows");
    Check(cycleDto.GetProperty("cancellationReason").ValueKind==JsonValueKind.Null,"no cancellation reason invented for uncancelled cycle");
    var body=(await Expect(supervisor,$"/api/rooms/{room}/turnover-history/{cycle.CleaningTaskId}?branchId={a}",200,"field-oracle detail"))!.Value;
    Check(body.GetProperty("inspections").EnumerateArray().All(x=>x.GetProperty("inspection").GetProperty("versionId").GetInt64()==version.VersionId
        && x.GetProperty("inspection").GetProperty("versionNumber").GetInt32()==version.VersionNo),"stored checklist version identity and number");
    var inspections=await db.RoomInspections.Where(x=>x.CleaningTaskId==cycle.CleaningTaskId && x.RoomId==room).Select(x=>new{x.InspectionId,x.Result}).ToArrayAsync(ct);
    Check(body.GetProperty("inspections").EnumerateArray().All(x=>inspections.Any(i=>i.InspectionId==x.GetProperty("inspection").GetProperty("id").GetInt64()
        && i.Result==x.GetProperty("inspection").GetProperty("result").GetString())),"inspection identities and PASS/FAIL values match exact stored cycle");
    var rework=await db.CleaningReworks.AsNoTracking().SingleAsync(x=>x.CleaningTaskId==cycle.CleaningTaskId,ct);
    var reworkDto=body.GetProperty("reworks")[0];
    Check(reworkDto.GetProperty("id").GetInt64()==rework.ReworkId && reworkDto.GetProperty("reason").GetString()==rework.Reason
        && reworkDto.GetProperty("assigneeId").GetInt64()==rework.AssigneeUserId
        && reworkDto.GetProperty("completedAt").GetDateTime().Ticks==rework.CompletedAt!.Value.Ticks,"rework identity/reason/assignee/completed timestamp match SQL, no completer inferred");
    var evidence=body.GetProperty("evidence").EnumerateArray().Single(x=>x.GetProperty("id").GetInt64()==attachment.AttachmentId);
    Check(evidence.GetProperty("name").GetString()==attachment.OriginalFileName && evidence.GetProperty("mimeType").GetString()==attachment.MimeType
        && evidence.GetProperty("bytes").GetInt64()==attachment.FileSizeBytes
        && evidence.GetProperty("entityId").GetInt64()==cycle.CleaningTaskId,"authorized attachment metadata equals actual private source row");
    var empty=(await Expect(supervisor,route+"&page=100",200,"empty page beyond total"))!.Value;
    Check(empty.GetProperty("items").GetArrayLength()==0 && empty.GetProperty("total").GetInt32()==2,"out-of-range page preserves total without invented cycles");
    await Expect(supervisor,$"/api/rooms/{room}/turnover-history/0?branchId={a}",400,"invalid cycle ID real HTTP validation");
    await Expect(supervisor,route+"&roomId="+foreignRoom,400,"conflicting route/filter room rejected");
    Check(catalogBefore==await Fingerprint("roles","permissions","role_permissions","branches","departments","reference_values"),"oracle uses unchanged RBAC/reference catalog");
    await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(new {success=true,database=args[0],checks=cases.Count,cases,
        kind="ADDITIONAL_CURRENT_SOURCE_AUTHENTICATED_HTTPS_SQL_FIELD_ORACLES_NO_PRODUCTION_CHANGES"},new JsonSerializerOptions{WriteIndented=true}),ct);
    Console.WriteLine($"PASS {cases.Count} HTTPS/SQL field-oracle checks");
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
