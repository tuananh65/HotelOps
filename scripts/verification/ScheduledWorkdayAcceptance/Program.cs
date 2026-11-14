using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.WorkTime;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ScheduledWorkdayTrial;

// No SQL/HTTP connection without --execute, an approved exact receipt and its supplied SHA.
if (args.Length != 3 || args[0] is not ("--prepare" or "--execute"))
{
    Console.WriteLine("Usage: --prepare|--execute <receipt.json> <receipt-sha256>; default is no execution.");
    return 2;
}
var checks = 0;
void Check(bool value, string name) { if (!value) throw new TrialFailure(name); checks++; Console.WriteLine("PASS " + name); }
string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
try
{
    var bytes = await File.ReadAllBytesAsync(args[1]);
    Check(Hash(bytes).Equals(args[2], StringComparison.OrdinalIgnoreCase), "receipt hash");
    var r = JsonSerializer.Deserialize<Receipt>(bytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new TrialFailure("receipt");
    Check(r.Database.StartsWith("HotelOps_UC139_Trial_", StringComparison.Ordinal) && r.Database != "HotelOpsDB", "disposable database name allowlist");
    Check(r.DatabaseGuid != Guid.Empty && !string.IsNullOrWhiteSpace(r.Owner) && !string.IsNullOrWhiteSpace(r.LeaseId), "named owner and exact database identity");
    Check(r.ActorId > 0 && r.BranchId > 0 && r.DeniedBranchId > 0 && r.DeniedBranchId != r.BranchId && new[] { r.AssignmentId, r.RaceAssignmentId, r.RollbackAssignmentId }.All(x => x > 0)
        && new[] { r.AssignmentId, r.RaceAssignmentId, r.RollbackAssignmentId }.Distinct().Count() == 3, "dedicated fixture IDs");
    Check(Hash(await File.ReadAllBytesAsync(r.SchemaArtifact)).Equals(r.SchemaSha256, StringComparison.OrdinalIgnoreCase)
        && Hash(await File.ReadAllBytesAsync(r.FixtureArtifact)).Equals(r.FixtureSha256, StringComparison.OrdinalIgnoreCase), "prerequisite artifact hashes; approval separately required");
    Check(Uri.TryCreate(r.Origin, UriKind.Absolute, out var origin) && origin.Scheme == "https" && origin.AbsolutePath == "/"
        && origin.Query == "" && origin.Fragment == "" && origin.UserInfo == "", "exact HTTPS origin");
    var trialPackage = await TrialConfiguration.ReadAsync(args[1], args[2]);
    trialPackage.Validate(false, DateTimeOffset.UtcNow);
    TrialArtifacts.ValidatePackage(trialPackage.TrialPackageManifest, trialPackage.TrialPackageSha256, r.SchemaArtifact, r.FixtureArtifact);
    Check(true, "bound WorkTime trial package closure");
    if (args[0] == "--prepare") { Console.WriteLine("PREPARED_ONLY; SQL/HTTPS/WRITES NOT_RUN; lease and runtime approval required for execute."); return 0; }
    Check(r.ApprovalStatus == "APPROVED" && r.ExclusiveLease && r.TrialHostMutationApproval && r.AuditFaultInjectionApproval
        && r.LeaseStartsUtc <= DateTimeOffset.UtcNow && r.LeaseEndsUtc > DateTimeOffset.UtcNow, "explicit active exclusive lease and trial-only mutation/fault approval");
    var connection = Environment.GetEnvironmentVariable("UC139_TRIAL_SQL") ?? throw new TrialFailure("connection env missing");
    var cs = new SqlConnectionStringBuilder(connection);
    Check(cs.DataSource.Equals(r.Server, StringComparison.OrdinalIgnoreCase) && cs.InitialCatalog == r.Database
        && !cs.TrustServerCertificate && cs.Encrypt.ToString() is "True" or "Mandatory" or "Strict"
        && cs.AttachDBFilename == "" && cs.FailoverPartner == "", "exact SQL target; encryption and certificate verification required");
    var remaining = r.LeaseEndsUtc - DateTimeOffset.UtcNow;
    using var timeout = new CancellationTokenSource(remaining < TimeSpan.FromMinutes(4) ? remaining : TimeSpan.FromMinutes(4)); var ct = timeout.Token;
    await trialPackage.VerifyLiveIdentityAsync(connection, ct);
    await using (var sql = new SqlConnection(connection))
    {
        await sql.OpenAsync(ct); // No trust override or certificate callback.
        await using var command = sql.CreateCommand();
        command.CommandText = "SELECT DB_NAME(),CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),(SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()),IS_SRVROLEMEMBER('sysadmin'),IS_MEMBER('db_owner')";
        await using var row = await command.ExecuteReaderAsync(ct); Check(await row.ReadAsync(ct), "identity row");
        Check(row.GetString(0) == r.Database && row.GetString(1).Equals(r.Server, StringComparison.OrdinalIgnoreCase) && row.GetGuid(2) == r.DatabaseGuid, "live SQL identity");
        Check(!row.IsDBNull(3) && row.GetInt32(3) == 0 && !row.IsDBNull(4) && row.GetInt32(4) == 0, "restricted SQL principal, not sysadmin/db_owner");
    }
    await using (var sql = new SqlConnection(connection))
    {
        await sql.OpenAsync(ct); await using var command = sql.CreateCommand();
        command.CommandText = "SELECT (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.workdays') AND name='row_version' AND system_type_id=189 AND max_length=8 AND is_nullable=0),(SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.workdays') AND name='UX_workdays_one_working' AND is_unique=1 AND is_disabled=0),(SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.work_intervals') AND name='UX_work_intervals_one_open' AND is_unique=1 AND is_disabled=0)";
        await using var row = await command.ExecuteReaderAsync(ct); Check(await row.ReadAsync(ct) && row.GetInt32(0) == 1 && row.GetInt32(1) == 1 && row.GetInt32(2) == 1, "rowversion and serialization indexes present; accepted schema artifact still required");
    }
    DbContextOptions<HotelOpsDbContext> Options(bool fault = false)
    {
        var builder = new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection); // no retry strategy
        if (fault) builder.AddInterceptors(new FailAudit()); return builder.Options;
    }
    var fixtureIds = new[] { r.AssignmentId, r.RaceAssignmentId, r.RollbackAssignmentId };
    await using (var db = new HotelOpsDbContext(Options()))
    {
        Check(!await db.Workdays.AnyAsync(d => d.UserId == r.ActorId && d.Status == "WORKING", ct), "actor has no Working day");
        Check(!await db.Workdays.AnyAsync(d => fixtureIds.Contains(d.ShiftAssignmentId ?? 0), ct), "fresh fixture; rerun refuses consumed claims");
        Check(!await db.WorkIntervals.AnyAsync(i => i.UserId == r.ActorId && i.ClosedAt == null, ct), "no open intervals");
        Check(await db.ShiftAssignments.CountAsync(a => fixtureIds.Contains(a.ShiftAssignmentId) && a.UserId == r.ActorId && a.Shift.BranchId == r.BranchId, ct) == 3, "fixture branch and assignment ownership");
    }
    // Interceptor throws on the actual AuditLog SaveChanges, after Workday INSERT.
    long rolledBackId = 0;
    await using (var db = new HotelOpsDbContext(Options(true)))
    {
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            var participant = new SqlWorkTimeTransactionParticipant(db); await participant.LockUsersAsync([r.ActorId], ct);
            try { await participant.StartScheduledWorkdayAsync(r.ActorId, r.BranchId, r.RollbackAssignmentId, ct); throw new TrialFailure("fault not injected"); }
            catch (AuditFault) { rolledBackId = db.ChangeTracker.Entries<Workday>().Single().Entity.WorkdayId; }
            await tx.RollbackAsync(ct);
        }
    }
    await using (var db = new HotelOpsDbContext(Options()))
    {
        Check(rolledBackId > 0 && !await db.Workdays.AnyAsync(d => d.ShiftAssignmentId == r.RollbackAssignmentId, ct), "audit failure rolls back workday INSERT");
        var id = rolledBackId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Check(!await db.AuditLogs.AnyAsync(a => a.ActorUserId == r.ActorId && a.EntityType == "workdays" && a.EntityId == id, ct), "audit failure leaves no audit fragment");
    }
    using var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false };
    using var http = new HttpClient(handler) { BaseAddress = origin, Timeout = TimeSpan.FromSeconds(30) };
    async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body = null, string? csrf = null, string? tag = null)
    {
        Check(DateTimeOffset.UtcNow < r.LeaseEndsUtc, "lease remains active");
        using var request = new HttpRequestMessage(method, path); if (body is not null) request.Content = JsonContent.Create(body);
        if (csrf is not null) request.Headers.Add("X-CSRF-TOKEN", csrf); if (tag is not null) request.Headers.TryAddWithoutValidation("If-Match", tag);
        return await http.SendAsync(request, ct);
    }
    async Task<string> Csrf() { using var response = await Send(HttpMethod.Get, "/api/auth/csrf"); Check(response.StatusCode == HttpStatusCode.OK, "CSRF bootstrap"); var v = await response.Content.ReadFromJsonAsync<JsonElement>(ct); Check(v.GetProperty("headerName").GetString() == "X-CSRF-TOKEN", "CSRF header contract"); return v.GetProperty("csrfToken").GetString() ?? throw new TrialFailure("CSRF"); }
    var token = await Csrf();
    using (var response = await Send(HttpMethod.Post, "/api/auth/login", new { username = Environment.GetEnvironmentVariable("UC139_TRIAL_USERNAME") ?? throw new TrialFailure("username env"), password = Environment.GetEnvironmentVariable("UC139_TRIAL_PASSWORD") ?? throw new TrialFailure("password env") }, token)) Check(response.StatusCode == HttpStatusCode.OK, "real login");
    using (var response = await Send(HttpMethod.Get, "/api/auth/session"))
    {
        Check(response.StatusCode == HttpStatusCode.OK, "real SQL session"); var v = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Check(v.GetProperty("user").GetProperty("id").GetString() == r.ActorId.ToString() && !v.GetProperty("mustChangePassword").GetBoolean(), "exact unrestricted fixture actor");
    }
    token = await Csrf();
    using (var response = await Send(HttpMethod.Get, $"/api/branches/{r.DeniedBranchId}/workdays/current")) Check(response.StatusCode == HttpStatusCode.Forbidden, "branch resource denial 403");
    var path = $"/api/branches/{r.BranchId}/workdays";
    using (var response = await Send(HttpMethod.Get, path + "/current")) Check(response.StatusCode == HttpStatusCode.NoContent, "empty current before Start");
    using (var response = await Send(HttpMethod.Post, path, new { shiftAssignmentId = r.AssignmentId })) Check(response.StatusCode == HttpStatusCode.BadRequest, "CSRF missing blocks Start");
    using var start = await Send(HttpMethod.Post, path, new { shiftAssignmentId = r.AssignmentId }, token); Check(start.StatusCode == HttpStatusCode.Created, "scheduled Start 201");
    var started = await start.Content.ReadFromJsonAsync<JsonElement>(ct); var dayId = started.GetProperty("workdayId").GetInt64(); var tag = start.Headers.ETag?.ToString() ?? throw new TrialFailure("Start ETag");
    Check(tag == '"' + started.GetProperty("rowVersion").GetString() + '"' && started.GetProperty("status").GetString() == "WORKING", "rowversion and Working response");
    using (var response = await Send(HttpMethod.Post, path, new { shiftAssignmentId = r.AssignmentId }, token)) Check(response.StatusCode == HttpStatusCode.Conflict, "duplicate Start not replayed");
    var endPath = path + $"/{dayId}/end"; var version = Convert.FromBase64String(tag[1..^1]); version[0] ^= 0xff; var stale = '"' + Convert.ToBase64String(version) + '"';
    using (var response = await Send(HttpMethod.Post, endPath, new { pauseAll = false }, token, stale)) Check(response.StatusCode == HttpStatusCode.PreconditionFailed, "stale End 412");
    using (var response = await Send(HttpMethod.Post, endPath, new { pauseAll = false }, token, "*")) Check(response.StatusCode == HttpStatusCode.BadRequest, "invalid End If-Match");
    using (var response = await Send(HttpMethod.Post, endPath, new { pauseAll = false }, token, tag)) Check(response.StatusCode == HttpStatusCode.OK, "scheduled End 200");
    using (var response = await Send(HttpMethod.Get, path + "/current")) Check(response.StatusCode == HttpStatusCode.NoContent, "current cleared after End");
    using (var response = await Send(HttpMethod.Post, path, new { shiftAssignmentId = r.AssignmentId }, token)) Check(response.StatusCode == HttpStatusCode.Conflict, "ended assignment cannot restart");
    var race = await Task.WhenAll(Send(HttpMethod.Post, path, new { shiftAssignmentId = r.RaceAssignmentId }, token), Send(HttpMethod.Post, path, new { shiftAssignmentId = r.RaceAssignmentId }, token));
    Check(race.Count(x => x.StatusCode == HttpStatusCode.Created) == 1 && race.Count(x => x.StatusCode == HttpStatusCode.Conflict) == 1, "concurrent Start exactly one claim");
    var winner = race.Single(x => x.StatusCode == HttpStatusCode.Created); var raceDay = await winner.Content.ReadFromJsonAsync<JsonElement>(ct);
    using (var response = await Send(HttpMethod.Post, path + $"/{raceDay.GetProperty("workdayId").GetInt64()}/end", new { pauseAll = false }, token, winner.Headers.ETag!.ToString())) Check(response.StatusCode == HttpStatusCode.OK, "race winner ended");
    foreach (var response in race) response.Dispose();
    await using (var db = new HotelOpsDbContext(Options()))
    {
        var rows = await db.Workdays.AsNoTracking().Where(d => d.ShiftAssignmentId == r.AssignmentId || d.ShiftAssignmentId == r.RaceAssignmentId).ToArrayAsync(ct);
        Check(rows.Length == 2 && rows.All(d => d.Status == "ENDED" && d.EndedAt >= d.StartedAt), "two ended rows, no duplicate claim");
        foreach (var row in rows) { var id = row.WorkdayId.ToString(System.Globalization.CultureInfo.InvariantCulture); Check(await db.AuditLogs.CountAsync(a => a.ActorUserId == r.ActorId && a.EntityType == "workdays" && a.EntityId == id && (a.Action == "CREATE" || a.Action == "UPDATE"), ct) == 2, "atomic CREATE/UPDATE audit pair"); }
    }
    using (var response = await Send(HttpMethod.Post, "/api/auth/logout", new { }, token)) Check(response.StatusCode == HttpStatusCode.NoContent, "logout revoke");
    using (var response = await Send(HttpMethod.Get, path + "/current")) Check(response.StatusCode == HttpStatusCode.Unauthorized, "revoked session 401");
    Console.WriteLine($"SQL_HTTPS_ACCEPTANCE: PASS ({checks} checks); fixture claims consumed; no automatic retry; no production activation."); return 0;
}
catch (Exception error)
{
    // Never print raw SQL/HTTP bodies, exception messages, credentials, identifiers or tokens.
    Console.WriteLine(error is TrialFailure f ? "FAIL " + f.Check : "FAIL trial execution (" + error.GetType().Name + ")");
    Console.WriteLine("SQL_HTTPS_ACCEPTANCE: FAIL; stop, no mutation replay."); return 1;
}
sealed class TrialFailure(string check) : Exception { public string Check { get; } = check; }
sealed class AuditFault : Exception;
sealed class FailAudit : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added)) throw new AuditFault(); return ValueTask.FromResult(result);
    }
}
sealed record Receipt(string Server, string Database, Guid DatabaseGuid, string Owner, string LeaseId, DateTimeOffset LeaseStartsUtc, DateTimeOffset LeaseEndsUtc,
    string ApprovalStatus, bool ExclusiveLease, bool TrialHostMutationApproval, bool AuditFaultInjectionApproval, string SchemaArtifact, string SchemaSha256, string FixtureArtifact, string FixtureSha256,
    string Origin, long ActorId, long BranchId, long DeniedBranchId, long AssignmentId, long RaceAssignmentId, long RollbackAssignmentId);
