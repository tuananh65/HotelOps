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

if (args.Length != 2 || !Regex.IsMatch(args[0], @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$"))
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
try
{
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
    var report = new { database = args[0], success = true, checks = passed.Count, cases = passed,
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
