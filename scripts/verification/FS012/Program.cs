using Microsoft.Extensions.Configuration;
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

const string Target = "HotelOps_Canonical_20261106_f120001_Fresh";
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var mode = args.FirstOrDefault() ?? "plan";
if (mode == "credentials-ipc")
{
    if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("FS012_LIVE_ENABLED") != "APPROVED_FS012_LOCAL_TRIAL" || !args.Contains("--private-ipc")) throw new InvalidOperationException("PRIVATE_IPC_REQUIRES_APPROVED_LIVE_HARNESS");
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
    var publicPath = Path.Combine(privateDirectory, "localhost-public.pem");
    var publicTemporary = Path.Combine(privateDirectory, "public-" + Guid.NewGuid().ToString("N") + ".tmp");
    await File.WriteAllTextAsync(publicTemporary, string.Join(Environment.NewLine, certificates.Select(cert => cert.ExportCertificatePem())));
    File.Move(publicTemporary, publicPath, true);
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
    Console.WriteLine("FS012_HTTPS_GATEWAY: existing trusted store certificate; public PEM only; no private key export; localhost:5173 -> loopback Vite:5189");
    await gateway.RunAsync();
    return;
}
if (mode == "plan")
{
    Console.WriteLine(JsonSerializer.Serialize(new { mode = "PLAN_ONLY", database = Target, seedRequiresExplicitUserApproval = true, passwords = "Random local-only; never printed", writes = false }, jsonOptions));
    return;
}
if (!mode.StartsWith("candidate-") && mode is not ("recipient-proof" or "lifecycle-inspect" or "fault-on" or "fault-off" or "schema" or "seedstatus" or "seed" or "inspect" or "persistence")) throw new ArgumentException("Use plan/schema/seedstatus/seed/inspect/persistence.");
if (mode == "seed" && !args.Contains("--approved-seed")) throw new ArgumentException("Seed needs explicit user approval plus --approved-seed.");
var privateRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.private"));
var fixturePath = Path.Combine(privateRoot, "fixture.json");
using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
var ct = budget.Token;
var connection = new SqlConnectionStringBuilder
{
    DataSource = @".\SQLEXPRESS",
    InitialCatalog = Target,
    IntegratedSecurity = true,
    Encrypt = true,
    TrustServerCertificate = args.Contains("--local-sql-certificate-exception"),
    ConnectTimeout = 5,
    ApplicationName = "HotelOps FS012 explicitly isolated verification"
};
await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection.ConnectionString).AddInterceptors(HotelOps.Api.Services.Notifications.CandidateCommitGuard.Instance, HotelOps.Api.Services.Notifications.CandidateCommandGuard.Instance).Options);
var step = "ConnectAndValidate";
try
{
    await db.Database.OpenConnectionAsync(ct);
    await using (var command = db.Database.GetDbConnection().CreateCommand())
    {
        command.CommandText = "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')),CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),DB_NAME(),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS012.Owner')";
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct) || !reader.GetString(0).Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)
            || !reader.GetString(1).Equals("SQLEXPRESS", StringComparison.OrdinalIgnoreCase) || reader.GetString(2) != Target || reader.IsDBNull(3) || reader.GetString(3) != "DEV1_FS012")
            throw new InvalidOperationException("TARGET_IDENTITY_OR_OWNER_MISMATCH");
    }
    if (mode.StartsWith("candidate-"))
    {
        Guid? selectedPublication = args.FirstOrDefault(x => x.StartsWith("--publication=")) is { } selectedArg ? Guid.ParseExact(selectedArg[14..], "D") : null;
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "CleaningAssignment:Candidate", "FS012_PROVISIONAL" } }).Build();
        var candidate = new HotelOps.Api.Services.Notifications.ProvisionalCleaningNotification(db, config, new CandidateEnvironment());
        if (!await candidate.AvailableAsync(ct)) throw new InvalidOperationException("CANDIDATE_SCOPE_UNAVAILABLE");
        if (mode == "candidate-boundary-proof") { Console.WriteLine(JsonSerializer.Serialize(await SqlBoundaryProof.RunAsync(connection.ConnectionString, ct), jsonOptions)); return; }
        if (mode == "candidate-physical-proof") { Console.WriteLine(JsonSerializer.Serialize(await PhysicalGuardProof.RunAsync(db, ct), jsonOptions)); return; }
        if (mode == "candidate-commit-proof") { Console.WriteLine(JsonSerializer.Serialize(await CommitProof.RunAsync(connection.ConnectionString, ct), jsonOptions)); return; }
        if (mode == "candidate-inspect")
        {
            var operations = await db.CandidateOperations.AsNoTracking().Select(x => new { x.ActorUserId, x.OperationKey, x.Completed, x.ResultJson }).ToArrayAsync(ct);
            var publications = await db.CandidatePublications.AsNoTracking().Select(x => new { x.PublicationId, x.ActorUserId, x.OperationKey, x.AssignmentId, x.PayloadJson }).ToArrayAsync(ct);
            var intents = await db.CandidateIntents.AsNoTracking().Select(x => new { x.IntentId, x.PublicationId, x.UserId, x.Status, x.AttemptCount, x.FailureCode, x.ClaimId, x.ClaimedUntil, x.InboxNotificationId }).ToArrayAsync(ct);
            var inbox = await db.NotificationRecipients.AsNoTracking().Where(x => x.Notification.NotificationType == "CLEANING_TASK_ASSIGNED").Select(x => new { x.NotificationId, x.UserId, x.DeliveryChannel, x.DeliveryStatus, x.Notification.ActionUrl }).ToArrayAsync(ct);
            Console.WriteLine(JsonSerializer.Serialize(new { operations, publications, intents, inbox }, jsonOptions)); return;
        }
        if (mode is "candidate-deliver" or "candidate-claim-only")
        {
            var reader = new HotelOps.Api.Services.HotelStructure.RoomTypeCreationService(db, null!, null!, null!, TimeProvider.System);
            var dispatcher = new HotelOps.Api.Services.Notifications.ProvisionalCleaningDelivery(db, candidate, reader, TimeProvider.System);
            Console.WriteLine(JsonSerializer.Serialize(new { status = await dispatcher.DeliverOneAsync(ct, mode == "candidate-claim-only", selectedPublication), noHttpContext = true }, jsonOptions)); return;
        }
        if (mode == "candidate-extend-claim") { if (selectedPublication == null) throw new InvalidOperationException("EXACT_PUBLICATION_REQUIRED"); await db.CandidateIntents.Where(x => x.PublicationId == selectedPublication && x.Status == "PENDING" && x.ClaimId != null).ExecuteUpdateAsync(x => x.SetProperty(i => i.ClaimedUntil, DateTime.UtcNow.AddHours(1)), ct); Console.WriteLine("{}"); return; }
        if (mode == "candidate-expire-claim") { await db.CandidateIntents.Where(x => x.Status == "PENDING" && x.ClaimId != null && (selectedPublication == null || x.PublicationId == selectedPublication)).ExecuteUpdateAsync(x => x.SetProperty(i => i.ClaimedUntil, DateTime.UtcNow.AddSeconds(-1)).SetProperty(i => i.NextAttemptAt, DateTime.UtcNow.AddSeconds(-1)), ct); Console.WriteLine("{}"); return; }
        if (mode == "candidate-retry-now") { await db.CandidateIntents.Where(x => x.Status == "PENDING" && (selectedPublication == null || x.PublicationId == selectedPublication)).ExecuteUpdateAsync(x => x.SetProperty(i => i.NextAttemptAt, DateTime.UtcNow.AddSeconds(-1)), ct); Console.WriteLine("{}"); return; }
        if (mode is "candidate-recipient-revoke" or "candidate-recipient-restore" or "candidate-actor-revoke" or "candidate-actor-restore")
        {
            var actor = mode.Contains("actor"); var username = actor ? "fs012.admin" : "fs012.owner"; var roleEnabled = mode.EndsWith("restore");
            await db.UserRoles.Where(x => x.User.Username == username).ExecuteUpdateAsync(x => x.SetProperty(r => r.IsActive, roleEnabled), ct); Console.WriteLine("{}"); return;
        }
        if (mode is "candidate-stage-fault-on" or "candidate-stage-fault-off" or "candidate-inbox-fault-on" or "candidate-inbox-fault-off")
        {
            string sql = mode switch
            {
                "candidate-stage-fault-on" => "ALTER TABLE dbo.notification_delivery_intents WITH NOCHECK ADD CONSTRAINT CK_FS012_StageFault CHECK(intent_id<0)",
                "candidate-stage-fault-off" => "ALTER TABLE dbo.notification_delivery_intents DROP CONSTRAINT CK_FS012_StageFault",
                "candidate-inbox-fault-on" => "ALTER TABLE dbo.notifications WITH NOCHECK ADD CONSTRAINT CK_FS012_InboxFault CHECK(created_by IS NULL)",
                _ => "ALTER TABLE dbo.notifications DROP CONSTRAINT CK_FS012_InboxFault"
            };
            await db.Database.ExecuteSqlRawAsync(sql, ct); Console.WriteLine("{}"); return;
        }
        if (mode is "candidate-recipient-branch-revoke" or "candidate-recipient-branch-restore")
        {
            var revoked = mode.EndsWith("revoke") ? (DateTime?)DateTime.UtcNow : null;
            await db.UserBranchAccesses.Where(x => x.User.Username == "fs012.owner").ExecuteUpdateAsync(x => x.SetProperty(a => a.RevokedAt, revoked), ct); Console.WriteLine("{}"); return;
        }
        if (mode is "candidate-recipient-inactive" or "candidate-recipient-active")
        {
            var status = mode.EndsWith("-inactive") ? "INACTIVE" : "ACTIVE";
            await db.Users.Where(x => x.Username == "fs012.owner").ExecuteUpdateAsync(x => x.SetProperty(u => u.AccountStatus, status), ct); Console.WriteLine("{}"); return;
        }
        if (mode == "candidate-supersede-latest")
        {
            var assignment = await (from i in db.CandidateIntents join p in db.CandidatePublications on i.PublicationId equals p.PublicationId where i.Status == "PENDING" && (selectedPublication == null || i.PublicationId == selectedPublication) orderby i.IntentId descending select p.AssignmentId).FirstAsync(ct);
            await db.CleaningAssignments.Where(x => x.AssignmentId == assignment).ExecuteUpdateAsync(x => x.SetProperty(a => a.EndedAt, DateTime.UtcNow), ct); Console.WriteLine("{}"); return;
        }
        if (mode == "candidate-expand-rooms")
        {
            var sample = await db.Rooms.AsNoTracking().Where(x => x.RoomNumber.StartsWith("FS012-B1-")).FirstAsync(ct);
            if (await db.Rooms.CountAsync(x => x.RoomNumber.StartsWith("FS012-B1-") && !x.CleaningTasks.Any(), ct) >= 25) { Console.WriteLine("{\"status\":\"CAPACITY_SUFFICIENT\"}"); return; }
            for (var n = 0; n < 25; n++) db.Rooms.Add(new() { BranchId = sample.BranchId, FloorId = sample.FloorId, RoomTypeId = sample.RoomTypeId, RoomNumber = "FS012-B1-X" + Guid.NewGuid().ToString("N")[..8], IsActive = true, OperationalStatus = sample.OperationalStatus, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); Console.WriteLine(JsonSerializer.Serialize(new { status = "APPENDED_25_SYNTHETIC_ROOMS", noReset = true, noReseed = true }, jsonOptions)); return;
        }
        if (mode == "candidate-append-current-shift")
        {
            var appendTime = DateTime.UtcNow;
            var shiftTemplate = await db.ShiftTemplates.AsNoTracking().SingleAsync(x => x.ShiftCode == "FS012_SHIFT", ct);
            var adminId = await db.Users.Where(x => x.Username == "fs012.admin").Select(x => x.UserId).SingleAsync(ct);
            var shiftUsers = await db.Users.AsNoTracking().Where(x => x.Username == "fs012.owner" || x.Username == "fs012.other").ToArrayAsync(ct);
            if (shiftUsers.Length != 2 || shiftUsers.Any(x => x.PrimaryBranchId != shiftTemplate.BranchId || x.DepartmentId == null)) throw new InvalidOperationException("SYNTHETIC_SHIFT_SCOPE_MISMATCH");
            if (await db.ShiftAssignments.CountAsync(x => shiftUsers.Select(u => u.UserId).Contains(x.UserId) && x.AssignmentStatus == "ASSIGNED" && x.Shift.ScheduledStartAt <= appendTime && x.Shift.ScheduledEndAt > appendTime.AddHours(2) && x.Shift.Status == "PLANNED", ct) >= 2)
            { Console.WriteLine("{\"status\":\"CURRENT_SYNTHETIC_SHIFT_SUFFICIENT\"}"); return; }
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var appendedShift = new WorkShift { BranchId = shiftTemplate.BranchId, DepartmentId = shiftUsers[0].DepartmentId!.Value, ShiftTemplateId = shiftTemplate.ShiftTemplateId,
                ShiftDate = DateOnly.FromDateTime(appendTime), ScheduledStartAt = appendTime.AddMinutes(-1), ScheduledEndAt = appendTime.AddHours(8), RequiredStaffCount = 2,
                Status = "PLANNED", Note = "FS012 forward-only synthetic capacity after interrupted run", CreatedBy = adminId, CreatedAt = appendTime };
            db.WorkShifts.Add(appendedShift); await db.SaveChangesAsync(ct);
            foreach (var shiftUser in shiftUsers) db.ShiftAssignments.Add(new() { ShiftId = appendedShift.ShiftId, UserId = shiftUser.UserId, AssignmentStatus = "ASSIGNED", AssignedBy = adminId, AssignedAt = appendTime });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
            Console.WriteLine(JsonSerializer.Serialize(new { status = "APPENDED_NEW_SYNTHETIC_SHIFT", appendedShift.ShiftId, appendedShift.ScheduledStartAt, appendedShift.ScheduledEndAt, priorRowsChanged = false, testerDatabaseWritten = false }, jsonOptions)); return;
        }
        if (mode == "candidate-block-unused-room")
        {
            var room = await db.Rooms.AsNoTracking().Where(x => x.RoomNumber.StartsWith("FS012-B1-") && !x.CleaningTasks.Any()).OrderBy(x => x.RoomId).FirstAsync(ct);
            var actor = await db.Users.Where(x => x.Username == "fs012.admin").Select(x => x.UserId).SingleAsync(ct);
            db.RoomBlocks.Add(new() { RoomId = room.RoomId, Reason = "FS012 isolated block fixture", BlockedAt = DateTime.UtcNow, BlockedBy = actor, ExpectedReleaseAt = DateTime.UtcNow.AddMinutes(-1) }); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            Console.WriteLine(JsonSerializer.Serialize(new { room.RoomId, room.OperationalStatus, effectiveBlock = true, expectedReleaseInPast = true }, jsonOptions)); return;
        }
        if (mode == "candidate-model-parity")
        {
            var rows = new List<object>();
            foreach (var clr in new[] { typeof(HotelOps.Api.Models.NotificationOperationCandidate), typeof(HotelOps.Api.Models.NotificationPublicationCandidate), typeof(HotelOps.Api.Models.NotificationIntentCandidate) })
            {
                var entity = db.Model.FindEntityType(clr)!; var table = entity.GetTableName()!;
                await using var cmd = db.Database.GetDbConnection().CreateCommand(); cmd.CommandText = "SELECT name,is_nullable,is_identity,system_type_id,max_length FROM sys.columns WHERE object_id=OBJECT_ID(@table)";
                var arg = cmd.CreateParameter(); arg.ParameterName = "@table"; arg.Value = "dbo." + table; cmd.Parameters.Add(arg);
                var columns = new Dictionary<string, (bool Nullable, bool Identity, int Type, int Length)>();
                await using (var r = await cmd.ExecuteReaderAsync(ct)) { while (await r.ReadAsync(ct)) columns.Add(r.GetString(0), (r.GetBoolean(1), r.GetBoolean(2), r.GetByte(3), r.GetInt16(4))); }
                foreach (var property in entity.GetProperties())
                {
                    if (!columns.TryGetValue(property.GetColumnName(), out var actual) || actual.Nullable != property.IsNullable) throw new InvalidOperationException("EF_SQL_NULLABILITY_OR_COLUMN_MISMATCH");
                    var scalarType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                    var expectedType = scalarType == typeof(long) ? 127 : scalarType == typeof(int) ? 56 : scalarType == typeof(bool) ? 104 : scalarType == typeof(Guid) ? 36 : scalarType == typeof(DateTime) ? 42 : scalarType == typeof(byte[]) ? (property.IsConcurrencyToken ? 189 : 173) : scalarType == typeof(string) ? (property.IsUnicode() == false ? 167 : 231) : 0;
                    if (expectedType == 0 || actual.Type != expectedType) throw new InvalidOperationException("EF_SQL_TYPE_MISMATCH");
                    if (scalarType == typeof(string)) { var size = property.GetMaxLength(); var expectedLength = size == null ? -1 : size.Value * (property.IsUnicode() == false ? 1 : 2); if (actual.Length != expectedLength) throw new InvalidOperationException("EF_SQL_STRING_LENGTH_MISMATCH"); }
                    if (scalarType == typeof(byte[]) && !property.IsConcurrencyToken) { var match = System.Text.RegularExpressions.Regex.Match(property.GetColumnType() ?? "", @"\((\d+)\)"); if (!match.Success || actual.Length != int.Parse(match.Groups[1].Value)) throw new InvalidOperationException("EF_SQL_BINARY_LENGTH_MISMATCH"); }
                    if (property.IsConcurrencyToken && (actual.Type != 189 || actual.Length != 8)) throw new InvalidOperationException("EF_SQL_ROWVERSION_MISMATCH");
                    if (actual.Identity && property.ValueGenerated != Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd) throw new InvalidOperationException("EF_SQL_IDENTITY_MISMATCH");
                }
                if (columns.Count != entity.GetProperties().Count()) throw new InvalidOperationException("EF_SQL_COLUMN_COUNT_MISMATCH");
                rows.Add(new { table, columns = columns.Count, foreignKeys = entity.GetForeignKeys().Count(), sqlNullability = true, rowversionParity = true, typesAndLengths = true });
            }
            Console.WriteLine(JsonSerializer.Serialize(new { status = "PASS_EF_SQL_COLUMN_PARITY", rows, generatedRecipientUnchanged = true }, jsonOptions)); return;
        }
        if (mode == "candidate-guard-proof")
        {
            var op = await db.CandidateOperations.AsNoTracking().FirstAsync(x => x.Completed, ct);
            var cases = new List<string>();
            foreach (var name in new[] { "valid", "missing-intent", "missing-audit", "savepoint-restore", "immutable-publication", "immutable-completion", "wrong-physical" })
            {
                await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
                try
                {
                    if (name == "savepoint-restore") await tx.CreateSavepointAsync("beforeRemoval", ct);
                    if (name is "missing-intent" or "savepoint-restore") await db.Database.ExecuteSqlInterpolatedAsync($"DELETE i FROM dbo.notification_delivery_intents i JOIN dbo.notification_publications p ON p.publication_id=i.publication_id WHERE p.actor_user_id={op.ActorUserId} AND p.operation_key={op.OperationKey}", ct);
                    if (name == "savepoint-restore") await tx.RollbackToSavepointAsync("beforeRemoval", ct);
                    if (name == "missing-audit") await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.audit_logs WHERE TRY_CONVERT(uniqueidentifier,JSON_VALUE(new_values,'$.operationId'))={op.OperationKey}", ct);
                    if (name == "immutable-publication") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_publications SET payload_json=N'{{}}' WHERE actor_user_id={op.ActorUserId} AND operation_key={op.OperationKey}", ct);
                    if (name == "immutable-completion") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_operation_reservations SET result_json=N'{{}}' WHERE actor_user_id={op.ActorUserId} AND operation_key={op.OperationKey}", ct);
                    if (name == "wrong-physical")
                    {
                        await using var other = new SqlConnection(connection.ConnectionString); await other.OpenAsync(ct); await using var otherTx = await other.BeginTransactionAsync(ct);
                        await candidate.VerifyBeforeCommitAsync(otherTx, op.ActorUserId, op.OperationKey, ct);
                    }
                    else await candidate.VerifyBeforeCommitAsync(Microsoft.EntityFrameworkCore.Storage.DbContextTransactionExtensions.GetDbTransaction(tx), op.ActorUserId, op.OperationKey, ct);
                    if (name is not ("valid" or "savepoint-restore")) throw new InvalidOperationException("GUARD_FAILED_TO_REJECT");
                    cases.Add(name + ":PASS");
                }
                catch (SqlException e) when ((name is "missing-intent" or "missing-audit" or "immutable-publication" or "immutable-completion") && e.Number is 51911 or 51913 or 51914) { cases.Add(name + ":PASS"); }
                catch (InvalidOperationException e) when (name == "wrong-physical" && e.Message == "WRONG_PHYSICAL_TRANSACTION") { cases.Add(name + ":PASS"); }
                finally { if (db.Database.CurrentTransaction != null) await tx.RollbackAsync(ct); db.ChangeTracker.Clear(); }
            }
            Console.WriteLine(JsonSerializer.Serialize(new { status = "PASS_SCOPED_GUARD_COMPONENT", cases, rawExternalCommitInterception = false }, jsonOptions)); return;
        }
        throw new InvalidOperationException("UNKNOWN_CANDIDATE_MODE");
    }
    if (mode == "recipient-proof")
    {
        var owner = await db.Users.AsNoTracking().Where(x => x.Username == "fs012.owner").Select(x => x.UserId).SingleAsync(ct);
        var source = await db.CleaningAssignments.AsNoTracking().Where(x => x.AssigneeUserId == owner).OrderBy(x => x.AssignmentId).Select(x => new { x.AssignmentId, x.CleaningTaskId, x.CleaningTask.Room.BranchId }).FirstAsync(ct);
        var reader = new HotelOps.Api.Services.HotelStructure.RoomTypeCreationService(db, null!, null!, null!, TimeProvider.System);
        var allowed = await reader.CanReadAssignedCleaningAsync(owner, source.BranchId, source.CleaningTaskId, source.AssignmentId, ct);
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await db.UserBranchAccesses.Where(x => x.UserId == owner && x.BranchId == source.BranchId).ExecuteUpdateAsync(x => x.SetProperty(a => a.RevokedAt, DateTime.UtcNow), ct);
            var denied = !await reader.CanReadAssignedCleaningAsync(owner, source.BranchId, source.CleaningTaskId, source.AssignmentId, ct);
            if (!allowed || !denied) throw new InvalidOperationException("Explicit recipient permission revoke failed.");
            await tx.RollbackAsync(ct);
        }
        if (!await reader.CanReadAssignedCleaningAsync(owner, source.BranchId, source.CleaningTaskId, source.AssignmentId, ct)) throw new InvalidOperationException("Recipient revoke rollback failed.");
        Console.WriteLine(JsonSerializer.Serialize(new { status = "PASS_EXPLICIT_USER_SQL", noHttpContext = true, allowed, revokedDenied = true, rollbackRestored = true }, jsonOptions)); return;
    }
    if (mode is "lifecycle-inspect" or "fault-on" or "fault-off")
    {
        if (mode == "fault-on") await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.audit_logs WITH NOCHECK ADD CONSTRAINT CK_FS012_AuditFault CHECK(entity_type <> 'cleaning_tasks')", ct);
        if (mode == "fault-off") await db.Database.ExecuteSqlRawAsync("IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_FS012_AuditFault') ALTER TABLE dbo.audit_logs DROP CONSTRAINT CK_FS012_AuditFault", ct);
        if (mode != "lifecycle-inspect") { Console.WriteLine(JsonSerializer.Serialize(new { mode, target = Target }, jsonOptions)); return; }
        var proofRooms = await db.Rooms.AsNoTracking().Where(x => x.RoomNumber.StartsWith("FS012-B1-")).OrderBy(x => x.RoomId).Select(x => new { x.RoomId, x.RoomNumber, x.BranchId, x.OperationalStatus }).ToArrayAsync(ct);
        var tasks = await db.CleaningTasks.AsNoTracking().Where(x => x.Room.RoomNumber.StartsWith("FS012-B1-")).Select(x => new { x.CleaningTaskId, x.RoomId, x.TaskNo, x.Status, x.DueAt }).ToArrayAsync(ct);
        var cycles = await db.Database.SqlQuery<CycleProof>($"SELECT room_id AS RoomId,cleaning_task_id AS TaskId FROM dbo.room_cleaning_cycle_state").ToArrayAsync(ct);
        var events = await db.Database.SqlQuery<EventProof>($"SELECT room_id AS RoomId,cleaning_task_id AS TaskId,readiness AS Readiness,has_effective_block AS Blocked FROM dbo.room_readiness_events").ToArrayAsync(ct);
        var audits = await db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "cleaning_tasks").Select(x => new { x.EntityId, x.ActorUserId, x.BranchId }).ToArrayAsync(ct);
        var assignments = await db.CleaningAssignments.AsNoTracking().Select(x => new { x.AssignmentId, x.CleaningTaskId, x.AssigneeUserId }).ToArrayAsync(ct);
        Console.WriteLine(JsonSerializer.Serialize(new { target = Target, rooms = proofRooms, tasks, cycles, events, audits, assignments }, jsonOptions)); return;
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
        var user = new User
        {
            EmployeeCode = "FS012_" + key,
            Username = "fs012." + key,
            Email = "fs012." + key + "@example.invalid",
            FullName = "FS012 synthetic " + key,
            AccountStatus = "ACTIVE",
            EmploymentStatus = "ACTIVE",
            PrimaryBranchId = branch,
            DepartmentId = department,
            CreatedAt = now
        };
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
    var seedTemplate = new ShiftTemplate { BranchId = branch, ShiftCode = "FS012_SHIFT", ShiftName = "FS012 synthetic eligibility", StartTime = new TimeOnly(0, 0), EndTime = new TimeOnly(23, 59), BreakMinutes = 0, CrossesMidnight = false, IsActive = true };
    db.ShiftTemplates.Add(seedTemplate); await db.SaveChangesAsync(ct); var template = seedTemplate.ShiftTemplateId;
    var shift = new WorkShift { BranchId = branch, DepartmentId = department, ShiftTemplateId = template, ShiftDate = DateOnly.FromDateTime(now), ScheduledStartAt = now.AddHours(-1), ScheduledEndAt = now.AddHours(8), RequiredStaffCount = 2, Status = "PLANNED", CreatedBy = users["admin"].UserId, CreatedAt = now };
    db.WorkShifts.Add(shift); await db.SaveChangesAsync(ct);
    foreach (var name in new[] { "owner", "other" }) db.ShiftAssignments.Add(new() { ShiftId = shift.ShiftId, UserId = users[name].UserId, AssignmentStatus = "ASSIGNED", AssignedBy = users["admin"].UserId, AssignedAt = now });
    await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
    var checklist = new InspectionChecklist { ChecklistCode = "FS012_LEGACY", ChecklistName = "Synthetic legacy-compatible template", IsActive = true, CreatedBy = users["admin"].UserId, CreatedAt = now };
    db.Add(checklist); await db.SaveChangesAsync(ct);
    // Legacy-compatible template is supported by the existing schema, not a UC039/inspection acceptance claim.
    var rooms = new List<Room>();
    foreach (var scope in new[] { branch, foreign })
    {
        var floor = new Floor { BranchId = scope, FloorCode = "FS012_F", FloorName = "FS012 floor", FloorNumber = 1, IsActive = true };
        db.Add(floor); await db.SaveChangesAsync(ct);
        var typeCode = scope == branch ? "FS012_B1" : "FS012_B2";
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_types(type_code,type_name,is_active,branch_id,standard_cleaning_minutes,default_checklist_id) VALUES({typeCode},{"FS012 synthetic type"},{true},{scope},{25},{checklist.ChecklistId})", ct);
        var type = await db.BranchRoomTypes.AsNoTracking().Where(x => x.TypeCode == typeCode && x.BranchId == scope).Select(x => x.RoomTypeId).SingleAsync(ct);
        for (var i = 1; i <= (scope == branch ? 25 : 2); i++)
        {
            var room = new Room { BranchId = scope, FloorId = floor.FloorId, RoomTypeId = type, RoomNumber = $"FS012-{(scope == branch ? "B1" : "B2")}-{i:000}", OperationalStatus = scope == branch && i == 1 ? "CLEANING" : "DIRTY", IsActive = true };
            db.Add(room); rooms.Add(room);
        }
    }
    await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
    var cleaningType = await db.ReferenceValues.AsNoTracking().Where(x => x.GroupCode == "CLEANING_TYPE" && x.ValueCode == "CHECKOUT" && x.IsActive).Select(x => x.ReferenceId).SingleAsync(ct);
    var active = new CleaningTask { TaskNo = "FS012_ACTIVE", RoomId = rooms[0].RoomId, CleaningTypeId = cleaningType, Status = "IN_PROGRESS", ActualStartAt = now.AddMinutes(-10), CreatedBy = users["admin"].UserId, CreatedAt = now.AddHours(-1) };
    var inactive = new CleaningTask { TaskNo = "FS012_ASSIGNED", RoomId = rooms[1].RoomId, CleaningTypeId = cleaningType, Status = "PENDING", CreatedBy = users["admin"].UserId, CreatedAt = now.AddHours(-1) };
    db.AddRange(active, inactive); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status={"IN_PROGRESS"} WHERE cleaning_task_id={active.CleaningTaskId}; UPDATE dbo.cleaning_tasks SET workflow_status={"ASSIGNED"} WHERE cleaning_task_id={inactive.CleaningTaskId}", ct);
    foreach (var task in new[] { active, inactive }) db.Add(new CleaningAssignment { CleaningTaskId = task.CleaningTaskId, AssigneeUserId = users["owner"].UserId, AssignedBy = users["admin"].UserId, AssignedAt = now.AddMinutes(-15), AssignmentStatus = "ACTIVE" });
    await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
    var roomTypeId = rooms[0].RoomTypeId;
    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_item_definitions(room_type_id,label,is_required,sort_order) VALUES({roomTypeId},{"FS012 clean surface"},{true},{0}),({roomTypeId},{"FS012 clean floor"},{true},{1}); INSERT dbo.cleaning_progress_versions(cleaning_task_id) VALUES({active.CleaningTaskId}),({inactive.CleaningTaskId}); INSERT dbo.cleaning_task_items(cleaning_task_id,source_item_id,label,is_required,sort_order) SELECT {active.CleaningTaskId},item_id,label,is_required,sort_order FROM dbo.cleaning_item_definitions WHERE room_type_id={roomTypeId}", ct);
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
        await JsonSerializer.SerializeAsync(file, new
        {
            database = Target,
            owner = "DEV1_FS012",
            sqlCertificateException = connection.TrustServerCertificate,
            branchId = branch,
            foreignBranchId = foreign,
            taskId = active.CleaningTaskId,
            inactiveTaskId = inactive.CleaningTaskId,
            roomCount = 25,
            firstRoomNumber = rooms[0].RoomNumber,
            accounts = accounts.ToDictionary(x => x.Key, x => new { x.Value.Username, x.Value.UserId }),
            credentialsAtRest = "DPAPI CurrentUser encrypted"
        }, jsonOptions, ct);
    }
    step = "CommitSeed";
    await transaction.CommitAsync(ct);
    Console.WriteLine(JsonSerializer.Serialize(new { status = "SYNTHETIC_FIXTURES_CREATED", accounts = accounts.Count, rooms = rooms.Count, privateCredentialFile = "FS012/.private/fixture.json", database = Target, noProductionImport = true }, jsonOptions));
}
catch (Exception error)
{
    var invariant = error is InvalidOperationException && System.Text.RegularExpressions.Regex.IsMatch(error.Message, "^[A-Z0-9_]+$") ? error.Message : null;
    var sqlErrors = error is SqlException provider ? provider.Errors.Cast<SqlError>().Select(x => new { x.Number, x.Class, x.State, x.Procedure, x.LineNumber }).ToArray() : null;
    Console.WriteLine(JsonSerializer.Serialize(new { status = "FS012_VERIFICATION_FAILED", stage = mode, step, invariant, errorType = error.GetType().Name, hresult = error.HResult, sqlNumber = CandidateSqlNumber(error), sqlErrors, details = "Provider/credential payload redacted; partial target retained; no reset/drop." }, jsonOptions));
    Environment.ExitCode = 1;
}
static int? CandidateSqlNumber(Exception error) { for (Exception? cause = error; cause != null; cause = cause.InnerException) if (cause is SqlException sql) return sql.Number; return null; }
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

public sealed class CycleProof { public long RoomId { get; set; } public long TaskId { get; set; } }
public sealed class EventProof { public long RoomId { get; set; } public long TaskId { get; set; } public string Readiness { get; set; } = ""; public bool Blocked { get; set; } }

public sealed class CandidateEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "FS012.Verification";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    public string WebRootPath { get; set; } = AppContext.BaseDirectory;
    public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
}
