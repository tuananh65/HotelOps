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
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
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
// Reuse the guarded Task26 prerequisite on this new empty trial only; no new schema contract.
await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(Path.Combine(root,
    "scripts/verification/UserProfileUpdate/rowversion-forward.sql"), ct), ct);
if (await db.Users.AnyAsync(ct) || await db.UserSessions.AnyAsync(ct) || await db.CleaningTasks.AnyAsync(ct))
    throw new InvalidOperationException("Unused empty personnel/business trial required; no reset/reuse.");
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
foreach (var pair in users.Where(x => x.Key != "denied"))
{
    var branch = pair.Value.PrimaryBranchId!.Value;
    var code = pair.Key == "admin" ? "SYS_ADMIN" : pair.Key == "own" ? "HK_STAFF" : "HK_SUPERVISOR";
    db.Add(new UserRole { UserId = pair.Value.UserId, RoleId = roles[code].RoleId,
        BranchId = code == "SYS_ADMIN" ? null : branch, IsActive = true, AssignedAt = now.AddMinutes(-1) });
    if (code != "SYS_ADMIN") db.Add(new UserBranchAccess { UserId = pair.Value.UserId, BranchId = branch,
        AccessLevel = "STANDARD", IsPrimary = true, GrantedAt = now.AddMinutes(-1) });
}
var floorA = new Floor { BranchId = a, FloorCode = "CM_A", FloorName = "Monitor A", IsActive = true };
var floorB = new Floor { BranchId = b, FloorCode = "CM_B", FloorName = "Monitor B", IsActive = true };
var type = new RoomType { TypeCode = "CM", TypeName = "Monitor fixture", IsActive = true };
db.AddRange(floorA, floorB, type); await db.SaveChangesAsync(ct);
var cleaningType = await db.ReferenceValues.FirstAsync(x => x.GroupCode == "CLEANING_TYPE" && x.IsActive, ct);
var priority = await db.ReferenceValues.SingleAsync(x => x.GroupCode == "PRIORITY" && x.ValueCode == "NORMAL", ct);
var blockerType = await db.ReferenceValues.FirstAsync(x => x.GroupCode == "BLOCKER" && x.IsActive, ct);
var tasks = new List<CleaningTask>();
var states = new[] { "IN_PROGRESS", "PENDING", "COMPLETED", "CANCELLED", "PENDING" };
for (var i = 0; i < states.Length; i++)
{
    var branch = i == 4 ? b : a;
    var room = new Room { BranchId = branch, FloorId = i == 4 ? floorB.FloorId : floorA.FloorId,
        RoomTypeId = type.RoomTypeId, RoomNumber = "CM" + i, OperationalStatus = "DIRTY", IsActive = true, CreatedAt = now };
    db.Add(room); await db.SaveChangesAsync(ct);
    var task = new CleaningTask { TaskNo = "CM_TASK_" + i, RoomId = room.RoomId, CleaningTypeId = cleaningType.ReferenceId,
        PriorityId = priority.ReferenceId, Status = states[i], ScheduledStartAt = now, DueAt = i == 1 ? now.AddHours(1) : now.AddHours(-1),
        CreatedBy = users["supervisor"].UserId, CreatedAt = now, Instruction = "Fixture cleaning" };
    db.Add(task); await db.SaveChangesAsync(ct); tasks.Add(task);
}
db.Add(new CleaningAssignment { CleaningTaskId = tasks[0].CleaningTaskId, AssigneeUserId = users["own"].UserId,
    AssignedBy = users["supervisor"].UserId, AssignedAt = now.AddMinutes(-1), AssignmentStatus = "ACTIVE" });
db.Add(new CleaningProgressLog { CleaningTaskId = tasks[0].CleaningTaskId, ProgressPercent = 35,
    Status = "IN_PROGRESS", BlockerTypeId = blockerType.ReferenceId, Note = "Waiting supplies",
    LoggedBy = users["own"].UserId, LoggedAt = now });
var checklist = new InspectionChecklist { ChecklistCode = "CM", ChecklistName = "Monitor", RoomTypeId = type.RoomTypeId,
    IsActive = true, CreatedBy = users["supervisor"].UserId, CreatedAt = now };
db.Add(checklist); await db.SaveChangesAsync(ct);
var version = new InspectionChecklistVersion { ChecklistId = checklist.ChecklistId, VersionNo = 1,
    Status = "PUBLISHED", PublishedBy = users["supervisor"].UserId, EffectiveAt = now };
db.Add(version); await db.SaveChangesAsync(ct);
var inspection = new RoomInspection { InspectionNo = "CM_INSPECT", RoomId = tasks[0].RoomId,
    CleaningTaskId = tasks[0].CleaningTaskId, VersionId = version.VersionId, InspectorUserId = users["supervisor"].UserId,
    Status = "COMPLETED", Result = "FAIL", CreatedAt = now };
db.Add(inspection); await db.SaveChangesAsync(ct);
var rework = new CleaningRework { CleaningTaskId = tasks[0].CleaningTaskId, InspectionId = inspection.InspectionId,
    AssigneeUserId = users["own"].UserId, Status = "PENDING", RequestedAt = now, Reason = "Rework fixture" };
db.Add(rework);
db.Add(new CleaningRework { CleaningTaskId = tasks[0].CleaningTaskId, InspectionId = inspection.InspectionId,
    AssigneeUserId = users["own"].UserId, Status = "COMPLETED", RequestedAt = now, CompletedAt = now, Reason = "Historical completed rework" });
db.Add(new AuditLog { BranchId = a, ActorUserId = users["supervisor"].UserId, EntityType = "cleaning_tasks",
    EntityId = tasks[0].CleaningTaskId.ToString(), Action = "CREATE", OccurredAt = now });
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
start.ArgumentList.Add(Path.Combine(root, "src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll"));
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
    var anonymous = new Jar(); var route = $"/api/branches/{a}/cleaning-monitor";
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
    var businessBefore = await Fingerprint("cleaning_tasks", "cleaning_assignments", "cleaning_progress_logs", "cleaning_reworks", "room_inspections", "rooms", "audit_logs");
    await Expect(anonymous, route, 401, "anonymous monitor denied");
    await Expect(denied, route, 403, "missing CLEANING_VIEW denied");
    await Expect(other, route, 403, "cross-branch role cannot read A");
    await Expect(admin, route, 403, "SYS_ADMIN is not operational permission bypass");
    await Expect(own, route, 403, "OWN grant cannot become supervisor branch monitoring");
    await Expect(restricted, route, 403, "restricted session remains blocked");
    var page = (await Expect(supervisor, route, 200, "production DI and scoped list SQL execute"))!.Value;
    Check(page.GetProperty("total").GetInt32() == 6 && page.GetProperty("items").GetArrayLength() == 6, "four cleaning and two reworks, no branch B leakage");
    Check(page.GetProperty("items").EnumerateArray().All(x => x.GetProperty("roomNumber").GetString() != "CM4"), "foreign room absent");
    var reworkRow = page.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("kind").GetString() == "rework"
        && x.GetProperty("id").GetInt64() == rework.ReworkId);
    Check(reworkRow.GetProperty("dueAtUtc").ValueKind == JsonValueKind.Null && reworkRow.GetProperty("isOverdue").ValueKind == JsonValueKind.Null,
        "legacy rework without due column is unknown, never borrows parent deadline");
    Check(page.GetProperty("items").EnumerateArray().All(x => x.GetProperty("workflowState").ValueKind == JsonValueKind.Null), "legacy status not relabeled as SRS workflow state");
    foreach (var state in states.Distinct())
    {
        var filtered = (await Expect(supervisor, route + "?status=" + state, 200, "recorded state filter " + state))!.Value;
        Check(filtered.GetProperty("items").EnumerateArray().All(x => x.GetProperty("recordedStatus").GetString() == state), "state rows match " + state);
    }
    var overdue = (await Expect(supervisor, route + "?overdue=true", 200, "overdue filter"))!.Value;
    Check(overdue.GetProperty("total").GetInt32() == 1, "completed/cancelled and unknown-due rework excluded from overdue");
    var notOverdue = (await Expect(supervisor, route + "?overdue=false", 200, "not-overdue filter"))!.Value;
    Check(notOverdue.GetProperty("total").GetInt32() == 4, "terminal rework is not overdue even when legacy due date is absent");
    Check(notOverdue.GetProperty("items").EnumerateArray().Where(x => x.GetProperty("kind").GetString() == "rework")
        .All(x => x.GetProperty("isOverdue").ValueKind == JsonValueKind.False), "completed status takes precedence over unknown deadline per GBR055");
    var current = (await Expect(supervisor, route + "?kind=cleaning&assigneeId=" + users["own"].UserId, 200, "assignee and kind filters"))!.Value;
    Check(current.GetProperty("total").GetInt32() == 1, "current assignment selected");
    var empty = (await Expect(supervisor, route + "?floorId=" + floorB.FloorId, 200, "foreign floor filter remains scoped"))!.Value;
    Check(empty.GetProperty("total").GetInt32() == 0, "foreign filter returns no data/count");
    var search = (await Expect(supervisor, route + "?search=not-found", 200, "search empty page"))!.Value;
    Check(search.GetProperty("total").GetInt32() == 0, "empty result is 200 with zero count");
    var p1 = (await Expect(supervisor, route + "?pageSize=1", 200, "page one"))!.Value;
    var p2 = (await Expect(supervisor, route + "?pageSize=1&page=2", 200, "page two"))!.Value;
    Check(p1.GetProperty("items")[0].GetProperty("id").GetInt64() != p2.GetProperty("items")[0].GetProperty("id").GetInt64(), "stable pagination does not repeat first row");
    foreach (var query in new[] { "page=0", "pageSize=101", "floorId=-1", "kind=unknown", "status=%27--", "fromDate=0001-01-01", "fromDate=2026-11-04&toDate=2026-11-03", "fromDate=bad" })
        await Expect(supervisor, route + "?" + query, 400, "invalid filters rejected " + query);
    var detailRoute = route + "/cleaning/" + tasks[0].CleaningTaskId;
    var detail = (await Expect(supervisor, detailRoute, 200, "cleaning detail and bounded timeline"))!.Value;
    Check(detail.GetProperty("assignmentCount").GetInt32() == 1 && detail.GetProperty("progressCount").GetInt32() == 1
        && detail.GetProperty("auditCount").GetInt32() == 1, "actual assignment/progress/audit projections");
    Check(detail.GetProperty("item").GetProperty("progressPercent").GetInt32() == 35
        && detail.GetProperty("item").GetProperty("blockerCode").GetString() == blockerType.ValueCode, "latest progress and blocker persisted SQL data");
    await Expect(supervisor, route + "/rework/" + rework.ReworkId, 200, "rework detail resolves source lineage");
    await Expect(supervisor, route + "/cleaning/" + tasks[4].CleaningTaskId, 404, "foreign task ID masked inside authorized branch");
    await Expect(supervisor, route + "/rework/9223372036854775807", 404, "missing rework returns 404");
    await Expect(supervisor, route + "/cleaning/0", 400, "invalid detail ID");
    await Expect(supervisor, $"/api/branches/{b}/cleaning-monitor", 403, "forged route branch denied");
    using (var mutation = await Send(HttpMethod.Post, detailRoute + "/cancel", supervisor, new { reason = "No mutation" }))
        Check((int)mutation.StatusCode == 404, "no Cancel endpoint exposed or falsely implemented");
    Check(businessBefore == await Fingerprint("cleaning_tasks", "cleaning_assignments", "cleaning_progress_logs", "cleaning_reworks", "room_inspections", "rooms", "audit_logs"),
        "all read/validation requests preserve exact business and audit rows");

    // Actual SQL lock wait: no fake pause, no mocked transaction. Rollback remains fixture-owned.
    await using (var blocker = new SqlConnection(connection.ConnectionString))
    {
        await blocker.OpenAsync(ct); await using var tx = (SqlTransaction)await blocker.BeginTransactionAsync(ct);
        await using var command = blocker.CreateCommand(); command.Transaction = tx;
        command.CommandText = "UPDATE dbo.cleaning_tasks SET status='CANCELLED' WHERE cleaning_task_id=@id; SELECT CONVERT(int,@@SPID)";
        command.Parameters.AddWithValue("@id", tasks[0].CleaningTaskId);
        var spid = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        var pending = Send(HttpMethod.Get, detailRoute, supervisor);
        var observed = false;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            await using var observer = db.Database.GetDbConnection().CreateCommand();
            observer.CommandText = "SELECT COUNT(*) FROM sys.dm_exec_requests WHERE database_id=DB_ID() AND blocking_session_id=@spid";
            var parameter = observer.CreateParameter(); parameter.ParameterName = "@spid"; parameter.Value = spid; observer.Parameters.Add(parameter);
            if (Convert.ToInt32(await observer.ExecuteScalarAsync(ct)) > 0) { observed = true; break; }
            await Task.Delay(100, ct);
        }
        Check(observed && !pending.IsCompleted, "DMV proves actual HTTPS reader waits for SQL writer");
        await tx.RollbackAsync(ct);
        using var response = await pending; Check(response.IsSuccessStatusCode, "read completes after SQL rollback");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Check(json.RootElement.GetProperty("item").GetProperty("recordedStatus").GetString() == "IN_PROGRESS", "uncommitted cancellation never leaks");
    }
    // Revocation is performed only on fixture grants/sessions in the explicitly guarded trial.
    await db.UserBranchAccesses.Where(x => x.UserId == users["supervisor"].UserId && x.BranchId == a).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
    await Expect(supervisor, route, 403, "membership revocation reflected at next real SQL request");
    await db.UserBranchAccesses.Where(x => x.UserId == users["supervisor"].UserId && x.BranchId == a).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, (DateTime?)null), ct);
    await db.UserRoles.Where(x => x.UserId == users["supervisor"].UserId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
    await Expect(supervisor, route, 403, "role assignment revocation reflected without cache");
    await db.UserRoles.Where(x => x.UserId == users["supervisor"].UserId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true), ct);
    await db.UserSessions.Where(x => x.UserId == users["supervisor"].UserId).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
    await Expect(supervisor, route, 401, "revoked SQL session cannot read monitor");
    Check(catalogBefore == await Fingerprint("roles", "permissions", "role_permissions", "branches", "departments", "reference_values"), "shared catalog/master rows unchanged");
    await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new { database = args[0], success = true,
        checks = cases.Count, cases, scope = "UC037_READ_ONLY_PARTIAL", optionalTask73Schema = "NOT VERIFIED", cancellation = "NOT VERIFIED",
        completedUCs = 26, verifiedPoints = 60 }, new JsonSerializerOptions { WriteIndented = true }), ct);
    Console.WriteLine($"PASS: {cases.Count} real HTTPS/SQL checks; UC037 remains PARTIAL, no completion points.");
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
