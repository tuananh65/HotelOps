using System.Data.Common;
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
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

if (args.Length == 2 && args[0] == "--entity-reads")
{
    if (!Regex.IsMatch(args[1], @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$")) throw new ArgumentException("Trial name required");
    var readConnection = new SqlConnectionStringBuilder { DataSource = @"DESKTOP-JI8U50T\SQLEXPRESS", InitialCatalog = args[1], IntegratedSecurity = true, TrustServerCertificate = true };
    using var readBudget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    await using var readDb = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(readConnection.ConnectionString).Options);
    await readDb.Database.OpenConnectionAsync(readBudget.Token);
    await using var identity = readDb.Database.GetDbConnection().CreateCommand();
    identity.CommandText = "SELECT CONVERT(nvarchar(128),@@SERVERNAME),DB_NAME(),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')";
    await using (var reader = await identity.ExecuteReaderAsync(readBudget.Token))
        if (!await reader.ReadAsync(readBudget.Token) || reader.GetString(0) != readConnection.DataSource || reader.GetString(1) != args[1] || reader.IsDBNull(2) || reader.GetString(2) != "Task17") throw new InvalidOperationException("Trial identity rejected");
    var setMethod = typeof(DbContext).GetMethods().Single(m => m.Name == "Set" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
    var takeMethod = typeof(Queryable).GetMethods().Single(m => m.Name == "Take" && m.GetParameters()[1].ParameterType == typeof(int));
    var count = 0;
    foreach (var entity in readDb.Model.GetEntityTypes().Where(e => !e.IsOwned() && e.BaseType == null).OrderBy(e => e.Name))
    {
        var set = setMethod.MakeGenericMethod(entity.ClrType).Invoke(readDb, null)!;
        var emptyQuery = (IQueryable)takeMethod.MakeGenericMethod(entity.ClrType).Invoke(null, [set, 0])!;
        await using var cmd = readDb.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = emptyQuery.ToQueryString();
        await using var rows = await cmd.ExecuteReaderAsync(readBudget.Token);
        if (await rows.ReadAsync(readBudget.Token)) throw new InvalidOperationException("Zero-row probe unexpectedly returned data");
        count++;
    }
    Console.WriteLine($"PASS: {count} EF entity SQL projections execute with zero rows, including DEV2 model tables; no business records materialized or mutated.");
    return;
}

if (args.Length != 3 || !Regex.IsMatch(args[0], @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$")
    || !Regex.IsMatch(args[2], @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$") || args[0] == args[2])
    throw new ArgumentException("Usage: UserDirectory.Verification <new UC141 database> <new evidence JSON path>");
if (File.Exists(args[1])) throw new InvalidOperationException("Evidence already exists; do not overwrite");
var connection = new SqlConnectionStringBuilder
{
    DataSource = @"DESKTOP-JI8U50T\SQLEXPRESS", InitialCatalog = args[0], IntegratedSecurity = true,
    TrustServerCertificate = true, ConnectTimeout = 10, ApplicationName = "HotelOps UC141 Isolated Verification"
};
using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(8));
var ct = budget.Token;
HotelOpsDbContext Open(DbCommandInterceptor? interceptor = null)
{
    var options = new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection.ConnectionString);
    if (interceptor != null) options.AddInterceptors(interceptor);
    return new(options.Options);
}
await using var db = Open();
await db.Database.OpenConnectionAsync(ct);
await using (var cmd = db.Database.GetDbConnection().CreateCommand())
{
    cmd.CommandText = "SELECT CONVERT(nvarchar(128),@@SERVERNAME),DB_NAME(),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')";
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    if (!await reader.ReadAsync(ct) || reader.GetString(0) != connection.DataSource || reader.GetString(1) != args[0]
        || reader.IsDBNull(2) || reader.GetString(2) != "Task17") throw new InvalidOperationException("Trial identity/receipt rejected");
}
if (await db.Users.AnyAsync(ct) || await db.UserSessions.AnyAsync(ct))
    throw new InvalidOperationException("Harness requires an unused, empty personnel trial; no reset or reuse");

var passed = new List<string>();
void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + label);
    passed.Add(label); Console.WriteLine("PASS: " + label);
}
async Task<string> Fingerprint(params string[] tables)
{
    var parts = new List<string>();
    foreach (var table in tables)
    {
        // Table names are only internal constants, never API inputs.
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = $"SELECT * FROM dbo.[{table}] ORDER BY 1 FOR JSON PATH,INCLUDE_NULL_VALUES";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var json = new StringBuilder(); while (await reader.ReadAsync(ct)) json.Append(reader.GetString(0));
        parts.Add(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json.ToString()))));
    }
    return string.Join(":", parts); // Only hashes retained, no account/credential data in evidence.
}
var catalogBefore = await Fingerprint("roles", "permissions", "role_permissions", "branches", "departments", "reference_values");
var empty = await new UserDirectoryService(db, TimeProvider.System).ListAsync(new(), ct);
Check(empty.TotalCount == 0 && empty.Items.Count == 0 && empty.PageSize == 20, "unused canonical database returns an empty page");

// Isolated fixture accounts only. Random password stays in memory, never evidence/arguments/files.
var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "aA1-";
var hasher = new PasswordCredentialService(Options.Create(new PasswordHasherOptions
    { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
var now = DateTime.UtcNow;
var branches = await db.Branches.AsNoTracking().OrderBy(b => b.BranchCode).ToArrayAsync(ct);
var departments = await db.Departments.AsNoTracking().ToArrayAsync(ct);
var roles = await db.Roles.AsNoTracking().ToDictionaryAsync(r => r.RoleCode, ct);
long A = branches.Single(b => b.BranchCode == "BR001").BranchId;
long B = branches.Single(b => b.BranchCode == "BR002").BranchId;
long DA = departments.Single(d => d.BranchId == A && d.DepartmentCode == "HK").DepartmentId;
long DB = departments.Single(d => d.BranchId == B && d.DepartmentCode == "HK").DepartmentId;
var identities = new Dictionary<string, User>();
foreach (var key in new[] { "admin", "denied", "restricted", "branch", "assignment", "owner", "inactiveRole", "expiredAssignment", "futureAssignment" })
{
    var user = new User { EmployeeCode = "UD_" + key, Username = "ud." + key, Email = "ud." + key + "@example.invalid",
        FullName = "00 Fixture " + key, PrimaryBranchId = A, DepartmentId = DA, AccountStatus = "ACTIVE",
        EmploymentStatus = "ACTIVE", CreatedAt = now, TemporaryPasswordExpiresAt = key == "restricted" ? now.AddHours(24) : null };
    user.PasswordHash = hasher.HashPassword(user, password); identities[key] = user; db.Add(user);
}
var targets = new List<User>();
for (var i = 0; i < 27; i++)
{
    var user = new User { EmployeeCode = $"UD_TARGET_{i:00}", Username = $"ud.target{i:00}", Email = $"ud.target{i:00}@example.invalid",
        FullName = i is 5 or 6 ? "Target Duplicate" : $"Target {i:00}", Phone = i == 0 ? "0123456789" : null, PrimaryBranchId = i % 2 == 0 ? A : B,
        DepartmentId = i % 2 == 0 ? DA : DB, AccountStatus = i == 4 ? "INACTIVE" : "ACTIVE",
        EmploymentStatus = "ACTIVE", CreatedAt = now, DeletedAt = i == 26 ? now : null };
    if (i == 25) { user.PrimaryBranchId = null; user.DepartmentId = null; }
    targets.Add(user); db.Add(user);
}
await db.SaveChangesAsync(ct);
void Assign(User user, string role, long? branch = null, bool active = true, DateTime? expires = null, DateTime? assigned = null)
    => db.Add(new UserRole { UserId = user.UserId, RoleId = roles[role].RoleId, BranchId = branch,
        IsActive = active, AssignedAt = assigned ?? now.AddMinutes(-1), ExpiresAt = expires });
Assign(identities["admin"], "SYS_ADMIN"); Assign(identities["restricted"], "SYS_ADMIN");
Assign(identities["branch"], "SYS_ADMIN", A); Assign(identities["assignment"], "SYS_ADMIN", active: false);
Assign(identities["owner"], "OWNER"); Assign(identities["inactiveRole"], "HK_INSPECTOR", A);
Assign(identities["expiredAssignment"], "SYS_ADMIN", expires: now.AddMinutes(-1));
Assign(identities["futureAssignment"], "SYS_ADMIN", assigned: now.AddHours(1));
db.Add(new UserBranchAccess { UserId = identities["branch"].UserId, BranchId = A, AccessLevel = "STANDARD", GrantedAt = now.AddMinutes(-1), IsPrimary = true });
Assign(targets[0], "HK_STAFF", A); Assign(targets[0], "HK_SUPERVISOR", A);
Assign(targets[1], "HK_STAFF", B, active: false); Assign(targets[2], "HK_STAFF", A, expires: now.AddMinutes(-1));
Assign(targets[3], "HK_STAFF", B, assigned: now.AddHours(1)); Assign(targets[4], "HK_INSPECTOR", A);
await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
var expectedTotal = await db.Users.AsNoTracking().CountAsync(u => u.DeletedAt == null, ct);

// Direct service SQL checks: ensure filtering/projection are provider-executed and bounded.
var capture = new QueryCapture();
await using (var reads = Open(capture))
{
    var service = new UserDirectoryService(reads, TimeProvider.System);
    var page = await service.ListAsync(new() { PageSize = 100 }, ct);
    Check(page.TotalCount == expectedTotal && page.Items.Count == expectedTotal, "service total excludes soft-deleted accounts");
    Check(capture.Commands.Count == 2, "count and page use two SQL queries independent of user/role count");
    Check(capture.Commands.All(s => !s.Contains("[password_hash]", StringComparison.OrdinalIgnoreCase)
        && !s.Contains("[session_key_hash]", StringComparison.OrdinalIgnoreCase)
        && !s.Contains("[locked_until]", StringComparison.OrdinalIgnoreCase)), "directory SQL projects no credential or lock fields");
    Check(capture.Commands.Any(s => s.Contains("OFFSET", StringComparison.OrdinalIgnoreCase)), "pagination executes in SQL");
    Check(!reads.ChangeTracker.Entries().Any(), "directory leaves EF change tracker empty");
    using var canceled = new CancellationTokenSource(); canceled.Cancel();
    var propagated = false;
    try { await service.ListAsync(new(), canceled.Token); } catch (OperationCanceledException) { propagated = true; }
    Check(propagated, "service propagates cancellation");
    capture.Commands.Clear();
    var detail = await service.GetAsync(targets[0].UserId, ct);
    Check(detail?.Id == targets[0].UserId && detail.Phone == targets[0].Phone, "detail service returns approved scalar fields");
    Check(capture.Commands.Count == 1, "detail projection uses exactly one SQL query without N+1");
    Check(!capture.Commands[0].Contains("[password_hash]", StringComparison.OrdinalIgnoreCase)
        && !capture.Commands[0].Contains("[locked_until]", StringComparison.OrdinalIgnoreCase)
        && !capture.Commands[0].Contains("user_roles", StringComparison.OrdinalIgnoreCase)
        && !capture.Commands[0].Contains("user_sessions", StringComparison.OrdinalIgnoreCase), "detail SQL excludes credential and access graphs");
    Check(!reads.ChangeTracker.Entries().Any(), "detail leaves change tracker empty");
    Check(await service.GetAsync(targets[26].UserId, ct) is null, "detail service hides soft-deleted users");
    propagated = false;
    try { await service.GetAsync(targets[0].UserId, canceled.Token); } catch (OperationCanceledException) { propagated = true; }
    Check(propagated, "detail propagates cancellation");
}

// Test exact USER_VIEW ALL/BRANCH/OWN decisions without changing the SQL RBAC catalog.
var current = new FixtureIdentity(); var paths = new FixturePermissions();
var permissionService = new PermissionService(current, paths, new UnusedResolver(), TimeProvider.System);
foreach (var scope in new[] { "ALL", "BRANCH", "OWN", "DEPARTMENT" })
{
    paths.Path = new("USER_VIEW", scope, scope == "ALL" ? null : A, true, true, now.AddHours(-1), null, true, true);
    Check((await permissionService.EvaluateGlobalAsync("USER_VIEW", ct)).IsGranted == (scope == "ALL"), "global USER_VIEW decision for " + scope);
}
paths.Path = new("USER_VIEW", "ALL", null, true, false, now.AddHours(-1), null, true, true);
Check(!(await permissionService.EvaluateGlobalAsync("USER_VIEW", ct)).IsGranted, "inactive role cannot grant global USER_VIEW even with an explicit path");

// Launch the real API assembly: exercises actual Program.cs DI, routes, middleware and cookie/session store.
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
var api = Path.Combine(root, "src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll");
if (!File.Exists(api)) throw new InvalidOperationException("Build Debug API before verification");
using var rsa = RSA.Create(2048);
var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback); request.CertificateExtensions.Add(san.Build());
using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
var certFile = Path.Combine(Path.GetTempPath(), "HotelOps-UC141-" + Guid.NewGuid().ToString("N") + ".pfx");
var certPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
await File.WriteAllBytesAsync(certFile, certificate.Export(X509ContentType.Pfx, certPassword), ct);
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(root, "src/HotelOps.Api"), UseShellExecute = false,
    CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
start.ArgumentList.Add(api);
start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; start.Environment["DOTNET_ENVIRONMENT"] = "Development";
start.Environment["ASPNETCORE_URLS"] = "https://127.0.0.1:" + port;
start.Environment["ConnectionStrings__HotelOpsDb"] = connection.ConnectionString;
start.Environment["Kestrel__Certificates__Default__Path"] = certFile;
start.Environment["Kestrel__Certificates__Default__Password"] = certPassword;
start.Environment["Logging__LogLevel__Default"] = "None";
Process? process = null;
var jars = new Dictionary<string, Jar>();
var runtimeRoleIds = new List<long>();
try
{
    async Task RejectStartup(string candidate, SchemaCompatibility expected, string label)
    {
        await using var candidateDb = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(candidate).Options);
        Check(await new StartupSchemaValidator(candidateDb).ValidateAsync(ct) == expected, label + " validator classification");
        start.Environment["ConnectionStrings__HotelOpsDb"] = candidate;
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Child not started");
        var output = child.StandardOutput.ReadToEndAsync(ct); var error = child.StandardError.ReadToEndAsync(ct);
        var elapsed = Stopwatch.StartNew();
        var served = false;
        try
        {
            using var probeHandler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.GetCertHashString() == certificate.GetCertHashString() };
            using var probe = new HttpClient(probeHandler) { BaseAddress = new Uri("https://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(1) };
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct); limit.CancelAfter(TimeSpan.FromSeconds(45));
            while (!child.HasExited)
            {
                foreach (var route in new[] { "/api/health/database", "/api/users", "/api/auth/csrf" })
                {
                    try { using var response = await probe.GetAsync(route, limit.Token); served = true; }
                    catch (HttpRequestException) { }
                    catch (TaskCanceledException) when (!limit.IsCancellationRequested) { }
                }
                await Task.Delay(100, limit.Token);
            }
            await child.WaitForExitAsync(limit.Token);
            Check(child.ExitCode != 0 && !served, label + " fails before HTTP traffic (health/users/CSRF)");
            var log = await output + await error;
            Check(log.Contains("Startup schema validation failed: " + expected) && !log.Contains(candidate)
                && !log.Contains(connection.DataSource) && !log.Contains("Password="), label + " sanitized failure category");
            Console.WriteLine($"TIMING: {label} rejected in {elapsed.ElapsedMilliseconds}ms");
        }
        finally { if (!child.HasExited) child.Kill(entireProcessTree: true); await child.WaitForExitAsync(CancellationToken.None); }
    }
    var oldConnection = new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = args[2] };
    await using (var oldSql = new SqlConnection(oldConnection.ConnectionString))
    {
        await oldSql.OpenAsync(ct);
        await using var command = oldSql.CreateCommand();
        command.CommandText = "SELECT CASE WHEN DB_NAME()=@name AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial' AND CONVERT(nvarchar(128),value)='Task17') AND NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.users') AND name='row_version') THEN 1 ELSE 0 END";
        command.Parameters.AddWithValue("@name", args[2]);
        Check((int)(await command.ExecuteScalarAsync(ct))! == 1, "negative schema target is verified unpatched trial");
        await RejectStartup(oldConnection.ConnectionString, SchemaCompatibility.MissingRequiredSchema, "unpatched database");
        command.Parameters.Clear();
        command.CommandText = "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.users') AND name='row_version'";
        Check((int)(await command.ExecuteScalarAsync(ct))! == 0, "unpatched validation does not repair schema");
        // Deliberately malformed disposable fixture; never alter the shared canonical scripts.
        command.CommandText = "ALTER TABLE dbo.users ADD row_version binary(8) NOT NULL DEFAULT 0x0000000000000000";
        await command.ExecuteNonQueryAsync(ct);
        await RejectStartup(oldConnection.ConnectionString, SchemaCompatibility.MissingRequiredSchema, "wrong-type database");
        command.CommandText = "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.users') AND name='row_version' AND system_type_id=173";
        Check((int)(await command.ExecuteScalarAsync(ct))! == 1, "wrong-type fixture unchanged after rejection");
    }
    var absent = new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = "HotelOps_Canonical_Absent_" + Guid.NewGuid().ToString("N"), ConnectTimeout = 3 };
    await RejectStartup(absent.ConnectionString, SchemaCompatibility.DatabaseInaccessible, "inaccessible database");
    await using (var compatible = Open())
        Check(await new StartupSchemaValidator(compatible).ValidateAsync(ct) == SchemaCompatibility.Compatible, "patched schema validator compatible");
    await using (var invalidConfigurationDb = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer("not a connection string").Options))
        Check(await new StartupSchemaValidator(invalidConfigurationDb).ValidateAsync(ct) == SchemaCompatibility.UnexpectedValidationError, "unexpected configuration error is classified without provider details");
    await using (var metadataDb = Open())
    {
        await metadataDb.Database.OpenConnectionAsync(ct);
        await using var metadataCommand = metadataDb.Database.GetDbConnection().CreateCommand();
        metadataCommand.CommandText = "CREATE USER T28_MetadataOnly WITHOUT LOGIN; CREATE USER T28_MetadataHidden WITHOUT LOGIN; GRANT SELECT ON OBJECT::dbo.users TO T28_MetadataOnly;";
        await metadataCommand.ExecuteNonQueryAsync(ct);
        try
        {
            metadataCommand.CommandText = "EXECUTE AS USER='T28_MetadataOnly'";
            await metadataCommand.ExecuteNonQueryAsync(ct);
            Check(await new StartupSchemaValidator(metadataDb).ValidateAsync(ct) == SchemaCompatibility.Compatible, "object SELECT principal passes actual validator without DDL/db_owner/sysadmin");
            metadataCommand.CommandText = "SELECT COALESCE(IS_ROLEMEMBER('db_owner'),0)+COALESCE(HAS_PERMS_BY_NAME('dbo.users','OBJECT','ALTER'),0)";
            Check(Convert.ToInt32(await metadataCommand.ExecuteScalarAsync(ct)) == 0, "metadata principal has no db_owner or table ALTER");
            metadataCommand.CommandText = "REVERT; EXECUTE AS USER='T28_MetadataHidden'";
            await metadataCommand.ExecuteNonQueryAsync(ct);
            Check(await new StartupSchemaValidator(metadataDb).ValidateAsync(ct) == SchemaCompatibility.MissingRequiredSchema, "hidden metadata principal fails closed in actual validator");
        }
        finally
        {
            metadataCommand.CommandText = "REVERT; DROP USER T28_MetadataOnly; DROP USER T28_MetadataHidden;";
            await metadataCommand.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
    using (var cancelled = new CancellationTokenSource())
    {
        cancelled.Cancel(); var observed = false;
        try { await new StartupSchemaValidator(db).ValidateAsync(cancelled.Token); }
        catch (OperationCanceledException) { observed = true; }
        Check(observed, "validator honors caller cancellation");
    }
    start.Environment["ConnectionStrings__HotelOpsDb"] = connection.ConnectionString;
    process = Process.Start(start) ?? throw new InvalidOperationException("API did not start");
    var stdout = process.StandardOutput.ReadToEndAsync(ct); var stderr = process.StandardError.ReadToEndAsync(ct);
    using var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false,
        ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.GetCertHashString() == certificate.GetCertHashString() };
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(60) };
    async Task<HttpResponseMessage> Send(HttpMethod method, string path, Jar jar, object? body = null, bool csrf = true)
    {
        using var message = new HttpRequestMessage(method, path);
        if (jar.Cookies.Count != 0) message.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(c => c.Key + "=" + c.Value)));
        if (csrf && jar.Token != null) message.Headers.Add(HotelOpsAntiforgery.HeaderName, jar.Token);
        if (body != null) message.Content = JsonContent.Create(body);
        var response = await http.SendAsync(message, ct); jar.Read(response); return response;
    }
    var ready = false;
    for (var attempt = 0; attempt < 40 && !process.HasExited; attempt++)
    {
        // Warm the real permission catalog/EF model as well as Kestrel. A CSRF
        // response alone does not prove the protected pipeline is ready.
        try { using var response = await http.GetAsync("/api/users", ct); if (response.StatusCode == HttpStatusCode.Unauthorized) { ready = true; break; } }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { }
        await Task.Delay(250, ct);
    }
    Check(ready, "actual API startup and production DI registration resolve");
    using (var health = await http.GetAsync("/api/health/database", ct)) Check(health.StatusCode == HttpStatusCode.OK && (await health.Content.ReadAsStringAsync(ct)) == "{\"status\":\"ok\"}", "patched database health contract preserved");
    async Task<Jar> Login(string key)
    {
        var jar = new Jar();
        using (var boot = await Send(HttpMethod.Get, "/api/auth/csrf", jar))
        { Check(boot.StatusCode == HttpStatusCode.OK, "CSRF bootstrap " + key); using var json = JsonDocument.Parse(await boot.Content.ReadAsStringAsync(ct)); jar.Token = json.RootElement.GetProperty("csrfToken").GetString(); }
        using var login = await Send(HttpMethod.Post, "/api/auth/login", jar, new { Username = identities[key].Username, Password = password });
        Check(login.StatusCode == HttpStatusCode.OK, "real credential login " + key);
        using var result = JsonDocument.Parse(await login.Content.ReadAsStringAsync(ct)); jar.Token = result.RootElement.GetProperty("csrfToken").GetString();
        jars[key] = jar; return jar;
    }
    async Task Expect(Jar jar, string path, int status, string label)
    {
        using var response = await Send(HttpMethod.Get, path, jar);
        Check((int)response.StatusCode == status && response.Headers.Location == null, label);
        if (status >= 400)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            Check(!body.Contains("totalCount", StringComparison.OrdinalIgnoreCase) && !body.Contains("password_hash", StringComparison.OrdinalIgnoreCase)
                && !body.Contains("SqlException", StringComparison.OrdinalIgnoreCase), label + " has no directory/provider disclosure");
        }
    }
    await Expect(new(), "/api/users", 401, "anonymous users request denied");
    var invalid = new Jar(); invalid.Cookies[HotelOpsCookieTicket.CookieName] = "invalid-cookie";
    await Expect(invalid, "/api/users", 401, "invalid cookie denied");
    foreach (var key in identities.Keys) await Login(key);
    var admin = jars["admin"];
    foreach (var key in identities.Keys.Where(k => k != "admin"))
        await Expect(jars[key], "/api/users?branchId=" + B, 403, "no global USER_VIEW: " + key);
    await Expect(jars["branch"], "/api/users?branchId=" + A, 403, "branch-bound ALL cannot become global through matching filter");

    var beforeReads = await Fingerprint("users", "user_roles", "user_branch_access");
    var sessionCount = await db.UserSessions.AsNoTracking().CountAsync(ct);
    var detailPath = "/api/users/" + targets[0].UserId;
    await Expect(new(), detailPath, 401, "anonymous detail denied");
    await Expect(invalid, detailPath, 401, "invalid-cookie detail denied");
    foreach (var key in identities.Keys.Where(k => k != "admin"))
    {
        await Expect(jars[key], detailPath, 403, "detail denies non-global/restricted caller: " + key);
        await Expect(jars[key], "/api/users/9223372036854775807", 403, "unauthorized missing target does not reveal existence: " + key);
    }
    foreach (var id in new[] { "0", "-1", "abc", "9223372036854775808" })
        await Expect(admin, "/api/users/" + id, 400, "invalid detail ID rejected: " + id);
    await Expect(admin, "/api/users/9223372036854775807", 404, "missing detail returns 404");
    await Expect(admin, "/api/users/" + targets[26].UserId, 404, "soft-deleted detail returns 404");
    foreach (var target in new[] { targets[0], targets[1], targets[4], targets[25] })
    {
        using var response = await Send(HttpMethod.Get, "/api/users/" + target.UserId, admin);
        Check(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true, "global detail allows active/inactive/other-branch/null-scope target with no-store");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var body = json.RootElement;
        var fields = body.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Check(fields.SequenceEqual(new[] { "id", "employeeCode", "username", "fullName", "email", "phone", "accountStatus", "primaryBranch", "department" }.Order()), "detail exact DTO allowlist excludes secrets and roles/permissions");
        Check(body.GetProperty("id").GetInt64() == target.UserId && body.GetProperty("employeeCode").GetString() == target.EmployeeCode
            && body.GetProperty("username").GetString() == target.Username && body.GetProperty("fullName").GetString() == target.FullName
            && body.GetProperty("email").GetString() == target.Email && body.GetProperty("phone").GetString() == target.Phone
            && body.GetProperty("accountStatus").GetString() == target.AccountStatus, "detail scalar values match fixture");
        foreach (var property in new[] { "primaryBranch", "department" })
        {
            var nested = body.GetProperty(property);
            Check(nested.ValueKind == JsonValueKind.Null || nested.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "code", "id", "name" }), "detail nested reference allowlist: " + property);
        }
        Check(target.PrimaryBranchId is null ? body.GetProperty("primaryBranch").ValueKind == JsonValueKind.Null
            : body.GetProperty("primaryBranch").GetProperty("id").GetInt64() == target.PrimaryBranchId, "detail primary branch matches fixture");
        Check(target.DepartmentId is null ? body.GetProperty("department").ValueKind == JsonValueKind.Null
            : body.GetProperty("department").GetProperty("id").GetInt64() == target.DepartmentId, "detail department matches fixture");
    }
    async Task<JsonElement> Page(string query)
    {
        using var response = await Send(HttpMethod.Get, "/api/users" + query, admin);
        Check(response.StatusCode == HttpStatusCode.OK, "directory query " + (query.Length == 0 ? "default" : "scenario"));
        Check(response.Headers.CacheControl?.NoStore == true, "directory response is no-store");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var clone = json.RootElement.Clone(); json.Dispose(); return clone;
    }
    var first = await Page("");
    Check(first.GetProperty("items").GetArrayLength() == 20 && first.GetProperty("totalCount").GetInt32() == expectedTotal, "default pagination and total count");
    var all = await Page("?pageSize=100"); var items = all.GetProperty("items").EnumerateArray().ToArray();
    Check(items.Length == expectedTotal && items.Select(i => i.GetProperty("id").GetInt64()).Distinct().Count() == expectedTotal, "multiple roles do not duplicate directory users");
    var duplicate = items.Where(i => i.GetProperty("fullName").GetString() == "Target Duplicate").Select(i => i.GetProperty("id").GetInt64()).ToArray();
    Check(duplicate.SequenceEqual(duplicate.Order()), "equal names use user ID as deterministic tie-breaker");
    var expectedOrder = await db.Users.AsNoTracking().Where(u => u.DeletedAt == null).OrderBy(u => u.FullName).ThenBy(u => u.UserId).Select(u => u.UserId).ToArrayAsync(ct);
    Check(items.Select(i => i.GetProperty("id").GetInt64()).SequenceEqual(expectedOrder), "HTTP sort matches database FullName/UserId order");
    var second = await Page("?page=2&pageSize=20");
    Check(!first.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt64()).Intersect(second.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt64())).Any(), "adjacent pages do not overlap");
    foreach (var search in new[] { "Target Duplicate", "ud.target00", "ud.target00@example.invalid" })
    {
        var result = await Page("?search=" + Uri.EscapeDataString("  " + search + "  "));
        Check(result.GetProperty("totalCount").GetInt32() == (search == "Target Duplicate" ? 2 : 1), "trimmed full-name/username/email search");
    }
    foreach (var search in new[] { "no-such-directory-user", "%", "_not_a_fixture_", "' OR 1=1--" })
    {
        var result = await Page("?search=" + Uri.EscapeDataString(search));
        Check(result.GetProperty("totalCount").GetInt32() == 0, "search literal or no-match returns empty page");
    }
    var branchResult = await Page("?branchId=" + B + "&pageSize=100");
    Check(branchResult.GetProperty("items").EnumerateArray().All(i => i.GetProperty("primaryBranch").GetProperty("id").GetInt64() == B)
        && branchResult.GetProperty("totalCount").GetInt32() > 0, "authorized global ALL can narrow to other primary branch");
    var departmentResult = await Page("?branchId=" + A + "&departmentId=" + DA + "&pageSize=100");
    Check(departmentResult.GetProperty("items").EnumerateArray().All(i => i.GetProperty("department").GetProperty("id").GetInt64() == DA), "consistent branch/department filter");
    var roleResult = await Page("?roleId=" + roles["HK_STAFF"].RoleId);
    Check(roleResult.GetProperty("totalCount").GetInt32() == 1 && roleResult.GetProperty("items")[0].GetProperty("roles").GetArrayLength() == 2, "role filter excludes inactive/expired/future assignments without duplicates");
    var inactiveRoles = await Page("?roleId=" + roles["HK_INSPECTOR"].RoleId);
    Check(inactiveRoles.GetProperty("totalCount").GetInt32() == 0, "inactive roles are not returned as active assignment filters");
    var inactive = await Page("?accountStatus=Inactive");
    Check(inactive.GetProperty("totalCount").GetInt32() == 1, "inactive account is visible under explicit status filter");
    var active = await Page("?accountStatus=Active&pageSize=100");
    Check(active.GetProperty("totalCount").GetInt32() == expectedTotal - 1, "active status filter excludes inactive account");
    Check(items.Any(i => i.GetProperty("primaryBranch").ValueKind == JsonValueKind.Null && i.GetProperty("department").ValueKind == JsonValueKind.Null), "null branch and department are handled");
    var combined = await Page("?search=ud.target00&branchId=" + A + "&departmentId=" + DA + "&roleId=" + roles["HK_STAFF"].RoleId + "&accountStatus=Active");
    Check(combined.GetProperty("totalCount").GetInt32() == 1, "all filters compose in database");
    foreach (var query in new[] { "page=0", "page=-1", "page=x", "pageSize=0", "pageSize=101", "page=2147483647&pageSize=100", "branchId=0", "departmentId=-1", "roleId=0", "branchId=9223372036854775808", "accountStatus=Locked", "search=" + new string('x', 201), "branchId=" + A + "&departmentId=" + DB })
        await Expect(admin, "/api/users?" + query, 400, "invalid query rejected: " + query.Split('=')[0]);
    var farPage = await Page("?page=100000&pageSize=20");
    Check(farPage.GetProperty("items").GetArrayLength() == 0 && farPage.GetProperty("totalCount").GetInt32() == expectedTotal, "valid beyond-last page is empty with filtered total");
    var allowedProperties = new HashSet<string>(new[] { "items", "page", "pageSize", "totalCount", "id", "employeeCode", "username", "fullName", "email", "primaryBranch", "department", "roles", "accountStatus", "code", "name", "branch" });
    void AssertProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object) foreach (var property in element.EnumerateObject()) { if (!allowedProperties.Contains(property.Name)) throw new InvalidOperationException("Unexpected DTO property"); AssertProperties(property.Value); }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) AssertProperties(child);
    }
    AssertProperties(all); Check(true, "recursive DTO allowlist excludes all secret/security/navigation fields");
    Check(await Fingerprint("users", "user_roles", "user_branch_access") == beforeReads, "directory requests preserve all user and assignment data");
    Check(await db.UserSessions.AsNoTracking().CountAsync(ct) == sessionCount, "directory reads do not create sessions");

    // Fixture-only auth regression: existing security services may update session activity.
    // Profile-only HTTP tests use the same real API, cookies and SQL session validation.
    var profilePath = "/api/users/" + targets[0].UserId + "/profile";
    var patchPath = "/api/users/" + targets[0].UserId;
    await Expect(new(), profilePath, 401, "profile read requires authenticated SQL session");
    var badProfileCookie = new Jar(); badProfileCookie.Cookies[HotelOpsCookieTicket.CookieName] = "bad-profile-cookie";
    await Expect(badProfileCookie, profilePath, 401, "profile read rejects invalid cookie");
    foreach (var badId in new[] { "0", "-1", "bad", "9223372036854775808" })
        await Expect(admin, "/api/users/" + badId + "/profile", 400, "profile read rejects invalid Int64 ID");
    foreach (var key in identities.Keys.Where(k => k != "admin"))
        await Expect(jars[key], profilePath, 403, "profile read denies ineligible USER_MANAGE: " + key);
    await Expect(admin, "/api/users/9223372036854775807/profile", 404, "global management missing profile returns 404");
    await Expect(admin, "/api/users/" + targets[26].UserId + "/profile", 404, "management hides soft-deleted user");
    async Task<(string ETag, JsonElement Body)> Profile(long id)
    {
        using var response = await Send(HttpMethod.Get, "/api/users/" + id + "/profile", admin);
        Check(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true, "management read-token is no-store and available");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Check(doc.RootElement.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "email", "fullName", "id", "phone" }), "profile exact response whitelist");
        return (response.Headers.ETag?.Tag ?? throw new Exception("No ETag"), doc.RootElement.Clone());
    }
    async Task<HttpResponseMessage> Patch(Jar jar, string? etag, string json, bool csrf = true)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Patch, patchPath);
        requestMessage.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(c => c.Key + "=" + c.Value)));
        if (csrf && jar.Token != null) requestMessage.Headers.Add(HotelOpsAntiforgery.HeaderName, jar.Token);
        if (etag != null) requestMessage.Headers.TryAddWithoutValidation("If-Match", etag);
        requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await http.SendAsync(requestMessage, ct); jar.Read(response); return response;
    }
    var initialProfile = await Profile(targets[0].UserId);
    foreach (var input in new[] { "{}", "null", "[]", "{\"roles\":[]}", "{\"fullName\":null}", "{\"email\":null}", "{\"fullName\":\" \"}", "{\"email\":\"invalid\"}", "{\"phone\":123}", "{\"fullName\":\"A\",\"fullName\":\"B\"}", "{\"FullName\":\"A\"}", "{\"phone\":\"" + new string('x', 21) + "\"}", "{\"fullName\":\"" + new string('x', 201) + "\"}", "{\"email\":\"" + new string('x', 151) + "@example.invalid\"}" })
    {
        using var response = await Patch(admin, initialProfile.ETag, input);
        Check(response.StatusCode == HttpStatusCode.BadRequest, "strict PATCH rejects invalid/unknown/duplicate/presence input");
    }
    foreach (var tag in new[] { "*", "W/" + initialProfile.ETag, "\"00\"", initialProfile.ETag + "," + initialProfile.ETag })
    { using var response = await Patch(admin, tag, "{\"phone\":null}"); Check(response.StatusCode == HttpStatusCode.BadRequest, "PATCH requires one strong version ETag"); }
    using (var response = await Patch(admin, null, "{\"phone\":null}")) Check((int)response.StatusCode == 428, "missing If-Match returns 428");
    using (var response = await Patch(admin, initialProfile.ETag, "{\"phone\":null}", csrf: false)) Check(response.StatusCode == HttpStatusCode.BadRequest, "PATCH cannot bypass CSRF");
    using (var response = await Patch(jars["denied"], initialProfile.ETag, "{\"phone\":null}")) Check(response.StatusCode == HttpStatusCode.Forbidden, "PATCH denies actor without USER_MANAGE");
    var anonymousPatch = new Jar();
    using (var bootstrap = await Send(HttpMethod.Get, "/api/auth/csrf", anonymousPatch))
    {
        using var token = JsonDocument.Parse(await bootstrap.Content.ReadAsStringAsync(ct));
        anonymousPatch.Token = token.RootElement.GetProperty("csrfToken").GetString();
    }
    using (var response = await Patch(anonymousPatch, initialProfile.ETag, "{\"phone\":null}")) Check(response.StatusCode == HttpStatusCode.Unauthorized, "anonymous PATCH with valid anonymous CSRF is still401");
    var credentialBefore = await Fingerprint("user_roles", "user_branch_access");
    var userBefore = await db.Users.AsNoTracking().Where(u => u.UserId == targets[0].UserId)
        .Select(u => new { u.PasswordHash, u.AccountStatus, u.PrimaryBranchId, u.DepartmentId, u.FailedLoginCount, u.LockedUntil, u.TemporaryPasswordExpiresAt }).SingleAsync(ct);
    var revokedBefore = await db.UserSessions.CountAsync(s => s.RevokedAt != null, ct);
    using (var response = await Patch(admin, initialProfile.ETag, "{\"fullName\":\"  Updated Profile  \",\"phone\":null}"))
    {
        Check(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true, "valid PATCH commits with no-store");
        Check(response.Headers.ETag?.Tag != initialProfile.ETag, "PATCH returns a changed rowversion ETag");
    }
    var changedProfile = await Profile(targets[0].UserId);
    Check(changedProfile.Body.GetProperty("fullName").GetString() == "Updated Profile" && changedProfile.Body.GetProperty("phone").ValueKind == JsonValueKind.Null
        && changedProfile.Body.GetProperty("email").GetString() == targets[0].Email, "omitted email retained, name trimmed, explicit null phone cleared");
    using (var response = await Patch(admin, initialProfile.ETag, "{\"phone\":\"new\"}")) Check((int)response.StatusCode == 412, "stale If-Match cannot overwrite profile");
    using (var response = await Patch(admin, changedProfile.ETag, "{\"email\":\"" + targets[1].Email + "\"}")) Check(response.StatusCode == HttpStatusCode.Conflict, "duplicate email translates to 409");
    using (var response = await Patch(admin, changedProfile.ETag, "{\"email\":\"" + targets[26].Email + "\"}")) Check(response.StatusCode == HttpStatusCode.Conflict, "soft-deleted email remains globally unique");
    var audit = await db.AuditLogs.AsNoTracking().Where(a => a.EntityId == targets[0].UserId.ToString() && a.Action == "UPDATE").OrderByDescending(a => a.AuditId).FirstAsync(ct);
    Check(audit.ActorUserId == identities["admin"].UserId && audit.EntityType == "users", "profile audit records real actor and target");
    using (var oldAudit = JsonDocument.Parse(audit.OldValues!))
    using (var newAudit = JsonDocument.Parse(audit.NewValues!))
    { Check(oldAudit.RootElement.GetProperty("fullName").GetString() == targets[0].FullName && newAudit.RootElement.GetProperty("fullName").GetString() == "Updated Profile", "audit captures server before/after");
      Check(newAudit.RootElement.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "fullName", "phone" }), "audit includes only changed approved fields"); }
    Check(await Fingerprint("user_roles", "user_branch_access") == credentialBefore, "PATCH preserves role/scope assignments");
    var userAfter = await db.Users.AsNoTracking().Where(u => u.UserId == targets[0].UserId)
        .Select(u => new { u.PasswordHash, u.AccountStatus, u.PrimaryBranchId, u.DepartmentId, u.FailedLoginCount, u.LockedUntil, u.TemporaryPasswordExpiresAt }).SingleAsync(ct);
    Check(userBefore.Equals(userAfter) && await db.UserSessions.CountAsync(s => s.RevokedAt != null, ct) == revokedBefore, "PATCH preserves credentials/state and does not revoke normal sessions");
    var auditCount = await db.AuditLogs.CountAsync(ct);
    using (var response = await Patch(admin, changedProfile.ETag, "{\"fullName\":\"Updated Profile\"}"))
        Check(response.StatusCode == HttpStatusCode.OK && response.Headers.ETag?.Tag == changedProfile.ETag, "no-op keeps ETag");
    Check(await db.AuditLogs.CountAsync(ct) == auditCount, "no-op does not fabricate update audit");

    // Two real SQL connections, same token: exactly one conditional update commits.
    var realActor = new ProfileIdentity(identities["admin"].UserId);
    UserProfileService Service(HotelOpsDbContext context)
    {
        var data = new EfPermissionDataSource(context);
        var core = new PermissionService(realActor, data, new UnusedResolver(), TimeProvider.System);
        return new(context, new UserProfileAccess(context, realActor, core, data, TimeProvider.System), realActor, TimeProvider.System);
    }
    await using (var one = Open()) await using (var two = Open())
    {
        var version = Convert.FromHexString(changedProfile.ETag.Trim('"'));
        var results = await Task.WhenAll(Service(one).UpdateAsync(targets[0].UserId, version, new(true, "Race One", false, null, false, null), ct),
            Service(two).UpdateAsync(targets[0].UserId, version, new(true, "Race Two", false, null, false, null), ct));
        Check(results.Count(r => r.Status == UserProfileStatus.Success) == 1 && results.Count(r => r.Status == UserProfileStatus.Stale) == 1, "concurrent SQL writers produce one commit and one stale conflict");
    }
    var rollbackProfile = await Profile(targets[0].UserId);
    var rollbackAuditCount = await db.AuditLogs.CountAsync(ct);
    await using (var failing = Open(new RejectAudit()))
    {
        var failed = false;
        try { await Service(failing).UpdateAsync(targets[0].UserId, Convert.FromHexString(rollbackProfile.ETag.Trim('"')), new(true, "Must Roll Back", false, null, false, null), ct); }
        catch (Exception error) when (error is InvalidOperationException or DbUpdateException) { failed = true; }
        Check(failed, "injected audit insertion failure is not swallowed");
    }
    var rolledBack = await Profile(targets[0].UserId);
    Check(rolledBack.ETag == rollbackProfile.ETag && rolledBack.Body.GetProperty("fullName").GetString() == rollbackProfile.Body.GetProperty("fullName").GetString()
        && await db.AuditLogs.CountAsync(ct) == rollbackAuditCount, "audit failure rolls back profile, rowversion and audit atomically");

    // Scope eligibility tests use actual memberships and foundation logic with synthetic grant paths.
    db.AddRange(new UserBranchAccess { UserId = targets[0].UserId, BranchId = A, AccessLevel = "STANDARD", GrantedAt = now.AddMinutes(-1) },
        new UserBranchAccess { UserId = targets[0].UserId, BranchId = B, AccessLevel = "STANDARD", GrantedAt = now.AddMinutes(-1) },
        new UserBranchAccess { UserId = targets[5].UserId, BranchId = A, AccessLevel = "STANDARD", GrantedAt = now.AddMinutes(-1) },
        new UserBranchAccess { UserId = targets[1].UserId, BranchId = B, AccessLevel = "STANDARD", GrantedAt = now.AddMinutes(-1) });
    await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
    var scopedActor = new ProfileIdentity(identities["branch"].UserId);
    var scopedData = new ProfileGrantData(new EfPermissionDataSource(db));
    var scopedCore = new PermissionService(scopedActor, scopedData, new UnusedResolver(), TimeProvider.System);
    var scopeAccess = new UserProfileAccess(db, scopedActor, scopedCore, scopedData, TimeProvider.System);
    scopedData.Path = new("USER_MANAGE", "BRANCH", A, true, true, now.AddMinutes(-1), null, true, true);
    Check(await scopeAccess.CanManageAsync(targets[0].UserId, ct), "BRANCH grant applies only in real common branch");
    Check(await scopeAccess.CanManageAsync(targets[5].UserId, ct), "common membership authorizes even when target PrimaryBranchId differs");
    Check(!await scopeAccess.CanManageAsync(targets[1].UserId, ct), "no common membership denies despite PrimaryBranchId information");
    scopedData.Path = scopedData.Path with { BranchId = B };
    Check(!await scopeAccess.CanManageAsync(targets[0].UserId, ct), "grant at another branch cannot borrow common branch");
    scopedData.Path = scopedData.Path with { BranchId = A, AssignmentActive = false };
    Check(!await scopeAccess.CanManageAsync(targets[0].UserId, ct), "inactive assignment denies scoped management");
    scopedData.Path = scopedData.Path with { AssignmentActive = true, RoleActive = false };
    Check(!await scopeAccess.CanManageAsync(targets[0].UserId, ct), "inactive role denies scoped management");
    scopedData.Path = scopedData.Path with { RoleActive = true, DataScope = "ALL" };
    Check(!await scopeAccess.CanManageAsync(targets[0].UserId, ct), "branch-bound ALL remains denied even with common membership");
    scopedData.Path = scopedData.Path with { DataScope = "OWN" };
    Check(await scopeAccess.CanManageAsync(identities["branch"].UserId, ct), "OWN requires self and valid OWN grant");
    Check(!await scopeAccess.CanManageAsync(targets[0].UserId, ct), "OWN cannot modify another user");
    scopedData.Path = scopedData.Path with { PermissionCode = "USER_VIEW" };
    Check(!await scopeAccess.CanManageAsync(identities["branch"].UserId, ct), "self alone and USER_VIEW cannot grant USER_MANAGE");
    scopedData.Path = scopedData.Path with { PermissionCode = "USER_MANAGE", DataScope = "BRANCH", ExpiresAt = now.AddSeconds(-1) };
    Check(!await scopeAccess.CanManageAsync(targets[0].UserId, ct), "expired scoped assignment cannot grant management");
    scopedData.Path = scopedData.Path with { ExpiresAt = null, AssignedAt = now.AddHours(1) };
    Check(!await scopeAccess.CanManageAsync(targets[0].UserId, ct), "future scoped assignment cannot grant management");
    scopedData.Path = scopedData.Path with { AssignedAt = now.AddMinutes(-1) };
    await db.UserBranchAccesses.Where(a => a.UserId == targets[0].UserId && a.BranchId == A)
        .ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, DateTime.UtcNow), ct);
    Check(!await scopeAccess.CanManageAsync(targets[0].UserId, ct), "revoked target common membership fails closed without borrowing another branch");

    // Task27: real runtime grants on custom TEST-ONLY roles; official seed rows remain unchanged.
    var managePermission = await db.Permissions.SingleAsync(p => p.PermissionCode == "USER_MANAGE", ct);
    var testBranchRole = new Role { RoleCode = "T27_PROFILE_BRANCH", RoleName = "Task27 Branch Fixture", IsActive = true, IsSystemRole = false, CreatedAt = DateTime.UtcNow };
    var testOwnRole = new Role { RoleCode = "T27_PROFILE_OWN", RoleName = "Task27 Own Fixture", IsActive = true, IsSystemRole = false, CreatedAt = DateTime.UtcNow };
    db.AddRange(testBranchRole, testOwnRole); await db.SaveChangesAsync(ct);
    runtimeRoleIds.AddRange([testBranchRole.RoleId, testOwnRole.RoleId]);
    db.AddRange(new RolePermission { RoleId = testBranchRole.RoleId, PermissionId = managePermission.PermissionId, DataScope = "BRANCH", GrantedAt = DateTime.UtcNow },
        new RolePermission { RoleId = testOwnRole.RoleId, PermissionId = managePermission.PermissionId, DataScope = "OWN", GrantedAt = DateTime.UtcNow },
        new UserRole { UserId = identities["branch"].UserId, RoleId = testBranchRole.RoleId, BranchId = A, IsActive = true, AssignedAt = now.AddMinutes(-1) },
        new UserRole { UserId = identities["owner"].UserId, RoleId = testOwnRole.RoleId, BranchId = A, IsActive = true, AssignedAt = now.AddMinutes(-1) },
        new UserBranchAccess { UserId = identities["owner"].UserId, BranchId = A, AccessLevel = "STANDARD", GrantedAt = now.AddMinutes(-1) });
    await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
    var runtimeTarget = targets[5].UserId; // Primary branch B, actual shared membership A.
    var runtimeProfilePath = "/api/users/" + runtimeTarget + "/profile";
    var branchJar = jars["branch"]; var ownJar = jars["owner"];
    await Expect(branchJar, runtimeProfilePath, 200, "RUNTIME BRANCH: real grant + common membership authorizes management");
    await Expect(branchJar, "/api/users/" + targets[1].UserId + "/profile", 403, "RUNTIME BRANCH: no common branch denies");
    await Expect(branchJar, "/api/users", 403, "RUNTIME USER_MANAGE does not grant USER_VIEW list");
    await Expect(branchJar, "/api/users/" + runtimeTarget, 403, "RUNTIME USER_MANAGE does not grant USER_VIEW detail");
    var runtimeVersion = await Profile(runtimeTarget);
    patchPath = "/api/users/" + runtimeTarget;
    using (var response = await Patch(branchJar, runtimeVersion.ETag, "{\"phone\":\"T27 Branch Phone\"}"))
        Check(response.StatusCode == HttpStatusCode.OK, "RUNTIME BRANCH: real-cookie PATCH commits under exact common branch");
    await db.UserRoles.Where(r => r.RoleId == testBranchRole.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.BranchId, B), ct);
    await Expect(branchJar, runtimeProfilePath, 403, "RUNTIME grant at wrong branch cannot borrow membership");
    await db.UserRoles.Where(r => r.RoleId == testBranchRole.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.BranchId, A).SetProperty(r => r.IsActive, false), ct);
    await Expect(branchJar, runtimeProfilePath, 403, "RUNTIME inactive assignment denies management");
    await db.UserRoles.Where(r => r.RoleId == testBranchRole.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct);
    await db.Roles.Where(r => r.RoleId == testBranchRole.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
    await Expect(branchJar, runtimeProfilePath, 403, "RUNTIME inactive role denies management");
    await db.Roles.Where(r => r.RoleId == testBranchRole.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct);
    await db.RolePermissions.Where(p => p.RoleId == testBranchRole.RoleId).ExecuteUpdateAsync(s => s.SetProperty(p => p.DataScope, "ALL"), ct);
    await Expect(branchJar, runtimeProfilePath, 403, "RUNTIME branch-bound ALL cannot become global or scoped management");
    await db.RolePermissions.Where(p => p.RoleId == testBranchRole.RoleId).ExecuteUpdateAsync(s => s.SetProperty(p => p.DataScope, "BRANCH"), ct);
    await Expect(ownJar, "/api/users/" + identities["owner"].UserId + "/profile", 200, "RUNTIME OWN: self requires and receives actual OWN grant");
    await Expect(ownJar, runtimeProfilePath, 403, "RUNTIME OWN: other user denied despite same branch");
    var ownProfile = await Profile(identities["owner"].UserId);
    patchPath = "/api/users/" + identities["owner"].UserId;
    using (var response = await Patch(ownJar, ownProfile.ETag, "{\"fullName\":\"Task27 Own Updated\"}"))
        Check(response.StatusCode == HttpStatusCode.OK, "RUNTIME OWN: valid self-only PATCH with real SQL grant");
    await db.UserBranchAccesses.Where(a => a.UserId == identities["branch"].UserId && a.BranchId == A)
        .ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, DateTime.UtcNow), ct);
    await Expect(branchJar, runtimeProfilePath, 403, "RUNTIME revoked actor membership denies existing cookie");
    await db.UserBranchAccesses.Where(a => a.UserId == identities["owner"].UserId && a.BranchId == A)
        .ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, DateTime.UtcNow), ct);
    await Expect(ownJar, "/api/users/" + identities["owner"].UserId + "/profile", 403, "RUNTIME OWN still requires valid branch membership");
    await db.UserRoles.Where(r => runtimeRoleIds.Contains(r.RoleId)).ExecuteDeleteAsync(ct);
    await db.RolePermissions.Where(p => runtimeRoleIds.Contains(p.RoleId)).ExecuteDeleteAsync(ct);
    await db.Roles.Where(r => runtimeRoleIds.Contains(r.RoleId)).ExecuteDeleteAsync(ct);
    Check(await Fingerprint("roles", "permissions", "role_permissions", "branches", "departments", "reference_values") == catalogBefore, "test-only runtime grants removed; official canonical catalog unchanged");
    await db.UserRoles.Where(r => r.UserId == identities["admin"].UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
    await Expect(admin, "/api/users", 403, "revoked assignment applies on next request");
    await Expect(admin, detailPath, 403, "detail denies assignment revoked after login");
    await db.UserRoles.Where(r => r.UserId == identities["admin"].UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct);
    await db.UserSessions.Where(s => s.UserId == identities["admin"].UserId).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, DateTime.UtcNow), ct);
    await Expect(admin, "/api/users", 401, "revoked SQL session denies the original protected cookie");
    await Expect(admin, detailPath, 401, "detail denies revoked SQL session");
    await Expect(admin, profilePath, 401, "management read-token denies revoked SQL session");
    admin = await Login("admin");
    await db.UserSessions.Where(s => s.UserId == identities["admin"].UserId && s.RevokedAt == null)
        .ExecuteUpdateAsync(s => s.SetProperty(s => s.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)), ct);
    await Expect(admin, detailPath, 401, "detail denies expired SQL session");
    await Expect(admin, profilePath, 401, "management read-token denies expired SQL session");
    Check(await Fingerprint("roles", "permissions", "role_permissions", "branches", "departments", "reference_values") == catalogBefore, "RBAC and canonical catalog rows are unchanged");
    var report = new { database = args[0], success = true, startupGateVerified = true,
        productionRolloutApproved = false,
        checks = passed.Count, cases = passed,
        authorization = "Real API, Cookie + SQL sessions + existing global USER_VIEW policy; BRANCH/OWN paths additionally tested with core doubles",
        safety = "Only new trial fixture users/assignments/sessions/audits written; no main database connection; read phase preserved users/assignments/catalog",
        secretsPersistedInEvidence = false };
    await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), ct);
    Console.WriteLine("PASS: " + passed.Count + " UC141 checks");
}
finally
{
    // Revoke only this new database's owned fixture sessions. Retain fixture records for review.
    try
    {
        var ownedUsers = identities.Values.Select(user => user.UserId).ToArray();
        await db.UserSessions.Where(s => ownedUsers.Contains(s.UserId) && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, DateTime.UtcNow), CancellationToken.None);
        if (runtimeRoleIds.Count != 0)
        {
            await db.UserRoles.Where(r => runtimeRoleIds.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.RolePermissions.Where(p => runtimeRoleIds.Contains(p.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Roles.Where(r => runtimeRoleIds.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
        }
    }
    finally
    {
        try
        {
            if (process != null) { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); process.Dispose(); }
        }
        finally { File.Delete(certFile); } // Exact freshly generated temporary PFX only; no directory cleanup.
    }
}

sealed class Jar
{
    internal Dictionary<string, string> Cookies { get; } = new();
    internal string? Token { get; set; }
    internal void Read(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return;
        foreach (var value in values) { var pair = value.Split(';')[0].Split('=', 2); if (pair.Length == 2) Cookies[pair[0]] = pair[1]; }
    }
}
sealed class QueryCapture : DbCommandInterceptor
{
    internal List<string> Commands { get; } = new();
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { Commands.Add(command.CommandText); return ValueTask.FromResult(result); }
}
sealed class FixtureIdentity : ICurrentUserAccessor
{
    public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(1, 1, false); return true; }
}
sealed class FixturePermissions : IPermissionDataSource
{
    internal PermissionGrantPath Path = null!;
    public Task<bool> IsActiveUserAsync(long id, CancellationToken ct) => Task.FromResult(true);
    public Task<bool> IsActiveBranchAsync(long id, CancellationToken ct) => Task.FromResult(true);
    public Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long id, string code, DateTime now, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PermissionGrantPath>>([Path]);
}
sealed class UnusedResolver : IPermissionResourceResolver
{
    public Task<VerifiedPermissionResource?> ResolveAsync(string code, PermissionResource resource, long id, CancellationToken ct)
        => throw new InvalidOperationException("Global USER_VIEW must not use resource resolver");
}

sealed class ProfileIdentity(long id) : ICurrentUserAccessor
{
    public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(id, 1, false); return true; }
}
sealed class ProfileGrantData(IPermissionDataSource source) : IPermissionDataSource
{
    internal PermissionGrantPath Path = null!;
    public Task<bool> IsActiveUserAsync(long id, CancellationToken ct) => source.IsActiveUserAsync(id, ct);
    public Task<bool> IsActiveBranchAsync(long id, CancellationToken ct) => source.IsActiveBranchAsync(id, ct);
    public Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long id, string code, DateTime now, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PermissionGrantPath>>([Path]);
}
sealed class RejectAudit : DbCommandInterceptor
{
    private static void Guard(DbCommand command)
    {
        if (command.CommandText.Contains("INSERT INTO [audit_logs]", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Injected trial-only audit failure");
    }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { Guard(command); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Guard(command); return ValueTask.FromResult(result); }
}
