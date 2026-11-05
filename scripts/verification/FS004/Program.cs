using System.Data;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

const string Target = "HotelOps_Canonical_20261105_180000_f040001_Fresh";
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var mode = args.FirstOrDefault() ?? "plan";
if (mode == "credentials-ipc")
{
    if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("FS004_LIVE_ENABLED") != "APPROVED_FS004_LOCAL_TRIAL" || !args.Contains("--private-ipc")) throw new InvalidOperationException("PRIVATE_IPC_REQUIRES_APPROVED_LIVE_HARNESS");
    var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.private"));
    var plaintext = ProtectedData.Unprotect(await File.ReadAllBytesAsync(Path.Combine(directory, "credentials.dpapi")), System.Text.Encoding.UTF8.GetBytes(Target), DataProtectionScope.CurrentUser);
    // Private child-process IPC captured only in memory by the live test harness. Never invoke into terminal/logs.
    await Console.OpenStandardOutput().WriteAsync(plaintext);
    CryptographicOperations.ZeroMemory(plaintext);
    return;
}
if (mode == "gateway")
{
    if (!args.Contains("--approved-local-gateway")) throw new ArgumentException("Gateway requires explicit setup approval.");
    var privateDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.private"));
    ProtectPrivateDirectory(privateDirectory);
    using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
    store.Open(OpenFlags.ReadOnly);
    var certificates = store.Certificates.Cast<X509Certificate2>().Where(cert => cert.HasPrivateKey && cert.NotBefore <= DateTime.Now && cert.NotAfter > DateTime.Now
        && cert.GetNameInfo(X509NameType.DnsName, false) == "localhost" && cert.Extensions.Cast<X509Extension>().Any(e => e.Oid?.Value == "1.3.6.1.4.1.311.84.1.1") && cert.Verify()).OrderByDescending(cert => cert.NotAfter).ToArray();
    if (certificates.Length == 0) throw new InvalidOperationException("NO_EXISTING_TRUSTED_DEVELOPMENT_CERTIFICATE");
    // Export only public certificates. HTTPS signing uses the existing Windows store key handle.
    await File.WriteAllTextAsync(Path.Combine(privateDirectory, "localhost-public.pem"), string.Join(Environment.NewLine, certificates.Select(cert => cert.ExportCertificatePem())));
    var builder = WebApplication.CreateBuilder();
    builder.Logging.ClearProviders();
    builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(5173, listen => listen.UseHttps(certificates[0])));
    await using var gateway = builder.Build();
    using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(60) };
    var excluded = new HashSet<string>(["Host", "Connection", "Keep-Alive", "Transfer-Encoding", "TE", "Trailer", "Upgrade", "Proxy-Authorization", "Proxy-Authenticate"], StringComparer.OrdinalIgnoreCase);
    gateway.Run(async context =>
    {
        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), "http://127.0.0.1:5189" + context.Request.PathBase + context.Request.Path + context.Request.QueryString);
            if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding")) request.Content = new StreamContent(context.Request.Body);
            foreach (var header in context.Request.Headers)
                if (!excluded.Contains(header.Key) && !request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray())) request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            request.Headers.Host = context.Request.Host.Value;
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            context.Response.StatusCode = (int)response.StatusCode;
            foreach (var header in response.Headers.Concat(response.Content.Headers))
                if (!excluded.Contains(header.Key)) context.Response.Headers[header.Key] = header.Value.ToArray();
            await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch when (!context.Response.HasStarted) { context.Response.StatusCode = 502; await context.Response.WriteAsync("Local development proxy unavailable."); }
    });
    Console.WriteLine("FS004_HTTPS_GATEWAY: existing trusted store certificate; public PEM only; no private key export; localhost:5173 -> loopback Vite:5189");
    await gateway.RunAsync();
    return;
}
if (mode == "plan")
{
    Console.WriteLine(JsonSerializer.Serialize(new { mode = "PLAN_ONLY", database = Target, seedRequiresExplicitUserApproval = true, passwords = "Random local-only; never printed", writes = false }, jsonOptions));
    return;
}
if (mode is not ("schema" or "seedstatus" or "seed" or "inspect" or "persistence")) throw new ArgumentException("Use plan/schema/seedstatus/seed/inspect/persistence.");
if (mode == "seed" && !args.Contains("--approved-seed")) throw new ArgumentException("Seed needs explicit user approval plus --approved-seed.");
var privateRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.private"));
var fixturePath = Path.Combine(privateRoot, "fixture.json");
using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
var ct = budget.Token;
var connection = new SqlConnectionStringBuilder { DataSource = @".\SQLEXPRESS", InitialCatalog = Target, IntegratedSecurity = true, Encrypt = true,
    TrustServerCertificate = args.Contains("--local-sql-certificate-exception"), ConnectTimeout = 5, ApplicationName = "HotelOps FS004 explicitly isolated verification" };
await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection.ConnectionString).Options);
var step = "ConnectAndValidate";
try
{
    await db.Database.OpenConnectionAsync(ct);
    await using (var command = db.Database.GetDbConnection().CreateCommand())
    {
        command.CommandText = "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')),CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),DB_NAME(),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS004.Owner')";
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct) || !reader.GetString(0).Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)
            || !reader.GetString(1).Equals("SQLEXPRESS", StringComparison.OrdinalIgnoreCase) || reader.GetString(2) != Target || reader.IsDBNull(3) || reader.GetString(3) != "DEV1_FS004")
            throw new InvalidOperationException("TARGET_IDENTITY_OR_OWNER_MISMATCH");
    }
    var schema = new { startup = await new StartupSchemaValidator(db).ValidateAsync(ct), roomTypes = await RoomTypeSchema.ReadAsync(db, ct), checklists = await ChecklistSchema.StateAsync(db, ct), housekeeping = await HousekeepingSchema.InstalledAsync(db, ct), progress = await ProgressSchema.InstalledAsync(db, ct) };
    if (schema.startup != SchemaCompatibility.Compatible || schema.roomTypes != RoomTypeContract.BranchV1 || schema.checklists != 1 || !schema.housekeeping || !schema.progress) throw new InvalidOperationException("OPTIONAL_SCHEMA_INCOMPATIBLE");
    if (mode == "schema") { Console.WriteLine(JsonSerializer.Serialize(new { status = "SCHEMA_VALIDATORS_PASS", schema }, jsonOptions)); return; }
    if (mode == "seedstatus")
    {
        Console.WriteLine(JsonSerializer.Serialize(new { status = "READ_ONLY_SEED_STATUS", users = await db.Users.AsNoTracking().CountAsync(ct), rooms = await db.Rooms.AsNoTracking().CountAsync(ct), tasks = await db.CleaningTasks.AsNoTracking().CountAsync(ct), credentialFileExists = File.Exists(fixturePath) }, jsonOptions)); return;
    }
    if (mode is "inspect" or "persistence")
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(fixturePath, ct));
        if (fixture.RootElement.GetProperty("database").GetString() != Target) throw new InvalidOperationException("FIXTURE_TARGET_MISMATCH");
        var taskId = fixture.RootElement.GetProperty("taskId").GetInt64();
        var ownerId = fixture.RootElement.GetProperty("accounts").GetProperty("owner").GetProperty("userId").GetInt64();
        var task = await db.CleaningTasks.AsNoTracking().Where(x => x.CleaningTaskId == taskId).Select(x => new { x.Status, x.Room.BranchId }).SingleAsync(ct);
        var logs = await db.CleaningProgressLogs.AsNoTracking().Where(x => x.CleaningTaskId == taskId).OrderByDescending(x => x.ProgressId).Select(x => new { x.ProgressId, x.ProgressPercent, x.LoggedBy }).ToArrayAsync(ct);
        var entityId = taskId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var audits = await db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "cleaning_tasks" && x.EntityId == entityId).Select(x => new { x.ActorUserId, x.BranchId }).ToArrayAsync(ct);
        var sqlPercent = logs.FirstOrDefault()?.ProgressPercent ?? 0;
        var ownerIsAuthor = logs.All(x => x.LoggedBy == ownerId);
        var auditMatches = audits.All(x => x.ActorUserId == ownerId && x.BranchId == task.BranchId);
        var evidenceCount = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.progress_evidence_links e JOIN dbo.cleaning_progress_logs p ON p.progress_id=e.cleaning_progress_id WHERE p.cleaning_task_id={taskId}").SingleAsync(ct);
        var attachments = await db.Attachments.AsNoTracking().Where(x => x.EntityType == "CLEANING_TASK" && x.EntityId == taskId && x.DeletedAt == null).Select(x => x.StorageKey).ToArrayAsync(ct);
        var evidenceFilesPrivate = attachments.All(key => key != null && Path.GetFileName(key) == key && File.Exists(Path.Combine(privateRoot, "evidence", key)));
        var expected = args.FirstOrDefault(x => x.StartsWith("--expected-percent="));
        if (expected != null && sqlPercent != int.Parse(expected.Split('=')[1], System.Globalization.CultureInfo.InvariantCulture)) throw new InvalidOperationException("PERSISTED_PERCENT_MISMATCH");
        var expectedLogs = args.FirstOrDefault(x => x.StartsWith("--expected-log-count="));
        if (expectedLogs != null && logs.Length != int.Parse(expectedLogs.Split('=')[1], System.Globalization.CultureInfo.InvariantCulture)) throw new InvalidOperationException("LOG_COUNT_MISMATCH");
        if (task.Status != "IN_PROGRESS" || !ownerIsAuthor || !auditMatches || (mode == "persistence" && logs.Length == 0) || audits.Length < logs.Length || !evidenceFilesPrivate) throw new InvalidOperationException("PERSISTENCE_OR_AUDIT_MISMATCH");
        Console.WriteLine(JsonSerializer.Serialize(new { result = mode == "inspect" ? "READ_ONLY_OBSERVATION" : "PERSISTENCE_PASS", task.Status, percent = sqlPercent, logCount = logs.Length, auditCount = audits.Length, evidenceCount, evidenceFilesPrivate, ownerIsAuthor, auditMatches }, jsonOptions)); return;
    }
    if (File.Exists(fixturePath) || File.Exists(Path.Combine(privateRoot, "credentials.dpapi"))) throw new InvalidOperationException("PRIVATE_FIXTURE_ALREADY_EXISTS_NO_OVERWRITE");
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    if (await db.Users.AnyAsync(ct) || await db.Rooms.AnyAsync(ct) || await db.CleaningTasks.AnyAsync(ct)) throw new InvalidOperationException("SEED_REQUIRES_EMPTY_NEW_TARGET");
    var branch = await db.Branches.AsNoTracking().Where(x => x.BranchCode == "BR001" && x.IsActive).Select(x => x.BranchId).SingleAsync(ct);
    var foreign = await db.Branches.AsNoTracking().Where(x => x.BranchCode == "BR002" && x.IsActive).Select(x => x.BranchId).SingleAsync(ct);
    var department = await db.Departments.AsNoTracking().Where(x => x.BranchId == branch && x.DepartmentCode == "HK" && x.IsActive).Select(x => x.DepartmentId).SingleAsync(ct);
    var roles = await db.Roles.AsNoTracking().Where(x => x.IsActive).ToDictionaryAsync(x => x.RoleCode, x => x.RoleId, ct);
    var now = DateTime.UtcNow;
    var hasher = new PasswordCredentialService(Options.Create(new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
    var accounts = new Dictionary<string, FixtureAccount>();
    var users = new Dictionary<string, User>();
    foreach (var key in new[] { "owner", "other", "denied", "admin" })
    {
        var password = "Fs4!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var user = new User { EmployeeCode = "FS004_" + key, Username = "fs004." + key, Email = "fs004." + key + "@example.invalid", FullName = "FS004 synthetic " + key,
            AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", PrimaryBranchId = branch, DepartmentId = department, CreatedAt = now };
        user.PasswordHash = hasher.HashPassword(user, password); db.Add(user); users.Add(key, user); accounts.Add(key, new(user.Username, password, 0));
    }
    await db.SaveChangesAsync(ct);
    foreach (var (key, user) in users)
    {
        accounts[key] = accounts[key] with { UserId = user.UserId };
        var roleCode = key is "owner" or "other" ? "HK_STAFF" : key == "denied" ? "OWNER" : "SYS_ADMIN";
        db.Add(new UserRole { UserId = user.UserId, RoleId = roles[roleCode], BranchId = key is "owner" or "other" ? branch : null, AssignedAt = now.AddHours(-1), IsActive = true });
        if (key != "denied") db.Add(new UserBranchAccess { UserId = user.UserId, BranchId = branch, AccessLevel = "STANDARD", GrantedAt = now.AddHours(-1) });
    }
    db.Add(new UserRole { UserId = users["admin"].UserId, RoleId = roles["HK_SUPERVISOR"], BranchId = branch, AssignedAt = now.AddHours(-1), IsActive = true });
    await db.SaveChangesAsync(ct);
    var checklist = new InspectionChecklist { ChecklistCode = "FS004_LEGACY", ChecklistName = "Synthetic legacy-compatible template", IsActive = true, CreatedBy = users["admin"].UserId, CreatedAt = now };
    db.Add(checklist); await db.SaveChangesAsync(ct);
    // Legacy-compatible template is supported by the existing schema, not a UC039/inspection acceptance claim.
    var rooms = new List<Room>();
    foreach (var scope in new[] { branch, foreign })
    {
        var floor = new Floor { BranchId = scope, FloorCode = "FS004_F", FloorName = "FS004 floor", FloorNumber = 1, IsActive = true };
        db.Add(floor); await db.SaveChangesAsync(ct);
        var typeCode = scope == branch ? "FS004_B1" : "FS004_B2";
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_types(type_code,type_name,is_active,branch_id,standard_cleaning_minutes,default_checklist_id) VALUES({typeCode},{"FS004 synthetic type"},{true},{scope},{25},{checklist.ChecklistId})", ct);
        var type = await db.BranchRoomTypes.AsNoTracking().Where(x => x.TypeCode == typeCode && x.BranchId == scope).Select(x => x.RoomTypeId).SingleAsync(ct);
        for (var i = 1; i <= (scope == branch ? 25 : 2); i++)
        {
            var room = new Room { BranchId = scope, FloorId = floor.FloorId, RoomTypeId = type, RoomNumber = $"FS004-{(scope == branch ? "B1" : "B2")}-{i:000}", OperationalStatus = scope == branch && i == 1 ? "CLEANING" : "DIRTY", IsActive = true };
            db.Add(room); rooms.Add(room);
        }
    }
    await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
    var cleaningType = await db.ReferenceValues.AsNoTracking().Where(x => x.GroupCode == "CLEANING_TYPE" && x.ValueCode == "CHECKOUT" && x.IsActive).Select(x => x.ReferenceId).SingleAsync(ct);
    var active = new CleaningTask { TaskNo = "FS004_ACTIVE", RoomId = rooms[0].RoomId, CleaningTypeId = cleaningType, Status = "IN_PROGRESS", ActualStartAt = now.AddMinutes(-10), CreatedBy = users["admin"].UserId, CreatedAt = now.AddHours(-1) };
    var inactive = new CleaningTask { TaskNo = "FS004_ASSIGNED", RoomId = rooms[1].RoomId, CleaningTypeId = cleaningType, Status = "PENDING", CreatedBy = users["admin"].UserId, CreatedAt = now.AddHours(-1) };
    db.AddRange(active, inactive); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status={"IN_PROGRESS"} WHERE cleaning_task_id={active.CleaningTaskId}; UPDATE dbo.cleaning_tasks SET workflow_status={"ASSIGNED"} WHERE cleaning_task_id={inactive.CleaningTaskId}", ct);
    foreach (var task in new[] { active, inactive }) db.Add(new CleaningAssignment { CleaningTaskId = task.CleaningTaskId, AssigneeUserId = users["owner"].UserId, AssignedBy = users["admin"].UserId, AssignedAt = now.AddMinutes(-15), AssignmentStatus = "ACTIVE" });
    await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
    var roomTypeId = rooms[0].RoomTypeId;
    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_item_definitions(room_type_id,label,is_required,sort_order) VALUES({roomTypeId},{"FS004 clean surface"},{true},{0}),({roomTypeId},{"FS004 clean floor"},{true},{1}); INSERT dbo.cleaning_progress_versions(cleaning_task_id) VALUES({active.CleaningTaskId}),({inactive.CleaningTaskId}); INSERT dbo.cleaning_task_items(cleaning_task_id,source_item_id,label,is_required,sort_order) SELECT {active.CleaningTaskId},item_id,label,is_required,sort_order FROM dbo.cleaning_item_definitions WHERE room_type_id={roomTypeId}", ct);
    step = "PrivateDirectoryAcl";
    ProtectPrivateDirectory(privateRoot);
    // Reserve the file before committing. Never overwrite or print this credential material.
    step = "WritePrivateFixture";
    if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows DPAPI required.");
    var credentials = JsonSerializer.SerializeToUtf8Bytes(accounts, jsonOptions);
    var encrypted = ProtectedData.Protect(credentials, System.Text.Encoding.UTF8.GetBytes(Target), DataProtectionScope.CurrentUser);
    CryptographicOperations.ZeroMemory(credentials);
    await using (var secretFile = new FileStream(Path.Combine(privateRoot, "credentials.dpapi"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        await secretFile.WriteAsync(encrypted, ct);
    await using (var file = new FileStream(fixturePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
    {
        await JsonSerializer.SerializeAsync(file, new { database = Target, owner = "DEV1_FS004", sqlCertificateException = connection.TrustServerCertificate, branchId = branch, foreignBranchId = foreign,
            taskId = active.CleaningTaskId, inactiveTaskId = inactive.CleaningTaskId, roomCount = 25, firstRoomNumber = rooms[0].RoomNumber,
            accounts = accounts.ToDictionary(x => x.Key, x => new { x.Value.Username, x.Value.UserId }), credentialsAtRest = "DPAPI CurrentUser encrypted" }, jsonOptions, ct);
    }
    step = "CommitSeed";
    await transaction.CommitAsync(ct);
    Console.WriteLine(JsonSerializer.Serialize(new { status = "SYNTHETIC_FIXTURES_CREATED", accounts = accounts.Count, rooms = rooms.Count, privateCredentialFile = "FS004/.private/fixture.json", database = Target, noProductionImport = true }, jsonOptions));
}
catch (Exception error)
{
    Console.WriteLine(JsonSerializer.Serialize(new { status = "FS004_VERIFICATION_FAILED", stage = mode, step, errorType = error.GetType().Name, hresult = error.HResult, sqlNumber = error is SqlException sql ? (int?)sql.Number : null, details = "Provider/credential payload redacted; partial target retained; no reset/drop." }, jsonOptions));
    Environment.ExitCode = 1;
}
static void ProtectPrivateDirectory(string path)
{
    if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Local Windows fixture only.");
    var directory = Directory.CreateDirectory(path);
    if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException("PRIVATE_DIRECTORY_REPARSE_POINT");
    // Preserve the workspace's restricted-agent access. Credentials are DPAPI CurrentUser ciphertext,
    // not plaintext files protected only by a DACL that can block the restricted development token.
    var acl = directory.GetAccessControl(); acl.SetAccessRuleProtection(false, true);
    directory.SetAccessControl(acl);
}
sealed record FixtureAccount(string Username, string Password, long UserId);
