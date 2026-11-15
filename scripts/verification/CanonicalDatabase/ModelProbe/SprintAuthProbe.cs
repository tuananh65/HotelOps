using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal static class SprintAuthProbe
{
    // Exact target from this sprint's independently verified provision receipt; no arbitrary connection input.
    private const string Database = "HotelOps_DEV3_142A_20261114_cc78c13f";
    internal static async Task<int> RunAsync(string reportPath, bool audit = false, bool roleAssignment = false, bool dashboard = false, bool notifications = false, bool browser = false, bool activation = false, bool taskRead = false, bool taskCriteria = false, bool taskResume = false, bool reports = false, bool taskEntry = false,string? taskSubmissionReceipt=null,bool taskVerification=false,bool taskDecline=false)
    {
        if (File.Exists(reportPath)) throw new IOException("Never overwrite prior runtime evidence");
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(reports || audit ? 8 : taskSubmissionReceipt==null?2:6));
        var ct = budget.Token;
        var database=taskCriteria?"HotelOps_DEV3_142A_CriteriaTrial_037b659a":Database;
        var guid=taskCriteria?"57748c4c-9803-49bf-bc30-508e6ea8bd3e":new Guid("1D5A9B53"+"-7D19"+"-435A"+"-96AB"+"-9FDFF447B6E0").ToString();
        var run=taskCriteria?"037b659ad5d348e783301e5d3e1ed3d5":"cc78c13ffc124efcad67b3d804baa84c";
        if(taskSubmissionReceipt!=null)
        {
            using var receipt=JsonDocument.Parse(await File.ReadAllTextAsync(taskSubmissionReceipt,ct));
            var r=receipt.RootElement; database=r.GetProperty("database").GetString()!;run=r.GetProperty("run").GetString()!;guid=r.GetProperty("databaseGuid").GetString()!;
            if(!System.Text.RegularExpressions.Regex.IsMatch(run,"^[a-f0-9]{32}$") || database!="HotelOps_DEV3_142A_CriteriaTrial_"+run[..8]
                || !Guid.TryParse(guid,out var identityGuid)||identityGuid==Guid.Empty||r.GetProperty("status").GetString()!="CLONE_READY"
                ||r.GetProperty("source").GetString()!=Database)throw new InvalidOperationException("Verified disposable clone receipt required");
        }
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer($"Server=localhost;Database={database};Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        await db.Database.OpenConnectionAsync(ct);
        await using (var identity = db.Database.GetDbConnection().CreateCommand())
        {
            identity.CommandText = $"""
                SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita'
                  AND DB_NAME()='{database}'
                  AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
                    AND database_guid='{guid}')
                  AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run'
                    AND CONVERT(nvarchar(128),value)='{run}')
                  THEN 1 ELSE 0 END
                """;
            if (!Equals(await identity.ExecuteScalarAsync(ct), 1)) throw new InvalidOperationException("Disposable identity rejected");
        }
        if (await db.Users.AnyAsync(ct) || await db.UserSessions.AnyAsync(ct) || await db.AuditLogs.AnyAsync(ct))
            throw new InvalidOperationException("Empty synthetic personnel/session/audit fixture required");
        var checks = new List<object>();
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) + "aA1!";
        var name = "sprint142_" + Guid.NewGuid().ToString("N")[..12];
        var user = new User { EmployeeCode = name, Username = name, Email = name + "@example.invalid",
            FullName = "Synthetic sprint auth", EmploymentStatus = "ACTIVE", AccountStatus = "ACTIVE", CreatedAt = DateTime.UtcNow };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        var cleaned = false;
        string? failure = null;
        try
        {
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            using var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false };
            // Default TLS validation: no certificate callback or insecure HTTP fallback.
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7142") };
            async Task Check(HttpMethod method, string path, int expected, object? body = null, string? csrf = null)
            {
                using var request = new HttpRequestMessage(method, path);
                if (body != null) request.Content = JsonContent.Create(body);
                if (csrf != null) request.Headers.Add("X-CSRF-TOKEN", csrf);
                using var response = await http.SendAsync(request, ct);
                checks.Add(new { method = method.Method, path, expected, actual = (int)response.StatusCode });
                if ((int)response.StatusCode != expected) throw new InvalidOperationException($"{method} {path}: expected {expected}, actual {(int)response.StatusCode}");
            }
            await Check(HttpMethod.Get, "/api/auth/session", 401);
            await Check(HttpMethod.Get, "/api/users", 401);
            if (notifications) await Check(HttpMethod.Get, "/api/notifications", 401);
            if (taskRead) await Check(HttpMethod.Get, "/api/branches/1/operational-tasks", 401);
            await Check(HttpMethod.Post, "/api/auth/login", 400, new { username = name, password });
            using var bootstrap = await http.GetAsync("/api/auth/csrf", ct);
            bootstrap.EnsureSuccessStatusCode();
            using var bootstrapJson = JsonDocument.Parse(await bootstrap.Content.ReadAsStringAsync(ct));
            var csrf = bootstrapJson.RootElement.GetProperty("csrfToken").GetString()!;
            using var login = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            { Content = JsonContent.Create(new { username = name, password }) };
            login.Headers.Add("X-CSRF-TOKEN", csrf);
            using var response = await http.SendAsync(login, ct);
            checks.Add(new { method = "POST", path = "/api/auth/login", expected = 200, actual = (int)response.StatusCode });
            response.EnsureSuccessStatusCode();
            using var loginJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var sessionCsrf = loginJson.RootElement.GetProperty("csrfToken").GetString()!;
            if (await db.UserSessions.CountAsync(s => s.UserId == user.UserId && s.RevokedAt == null, ct) != 1)
                throw new InvalidOperationException("Login session was not persisted to the verified trial");
            checks.Add(new { persistedSession = true });
            await Check(HttpMethod.Get, "/api/auth/session", 200);
            await Check(HttpMethod.Get, "/api/users", 403);
            if (audit) await SprintAuditScenario.RunAsync(db, http, user.UserId, checks, ct, name, password);
            if (roleAssignment) await SprintRoleAssignmentScenario.RunAsync(db, http, user.UserId, sessionCsrf, checks, ct);
            if (activation) await SprintActivationScenario.RunAsync(db, http, user.UserId, sessionCsrf, checks, ct, browser ? name : null, browser ? password : null);
            if (notifications) await SprintNotificationScenario.RunAsync(db, http, user.UserId, sessionCsrf, checks, ct, browser ? name : null, browser ? password : null);
            if (dashboard) await SprintDashboardScenario.RunAsync(db, http, user.UserId, checks, ct);
            if (reports && Environment.GetEnvironmentVariable("DEV3_VERIFY_IR_READ")=="1")
                await SprintInternalRequestReadScenario.RunAsync(db,http,user.UserId,checks,ct,name,password);
            if (reports) await SprintReportApiScenario.RunAsync(db,http,user.UserId,checks,ct,name,password);
            if (taskRead) await SprintTaskReadScenario.RunAsync(db,http,user.UserId,checks,ct,name,password,taskCriteria,taskResume,taskEntry,taskSubmissionReceipt!=null,taskVerification,taskDecline);
            await Check(HttpMethod.Post, "/api/auth/logout", 400);
            await Check(HttpMethod.Post, "/api/auth/logout", 204, csrf: sessionCsrf);
            await Check(HttpMethod.Get, "/api/auth/session", 401);
            if (await db.UserSessions.AnyAsync(s => s.UserId == user.UserId && s.RevokedAt == null, ct))
                throw new InvalidOperationException("Logout session remains active in SQL");
            if (!await db.AuditLogs.AnyAsync(a => a.ActorUserId == user.UserId, ct))
                throw new InvalidOperationException("Authentication audit missing");
            checks.Add(new { persistedRevocation = true, auditPresent = true });
        }
        catch (Exception error) { failure = error.GetType().Name + ": " + error.Message; }
        finally
        {
            // HTTP commits cannot be rolled back by a test transaction. Compensate only owned fixture rows.
            if (user.UserId > 0 && !taskCriteria)
            {
                await using var cleanup = await db.Database.BeginTransactionAsync(CancellationToken.None);
                await db.UserSessions.Where(s => s.UserId == user.UserId).ExecuteDeleteAsync(CancellationToken.None);
                var subjectId = user.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                await db.AuditLogs.Where(a => a.ActorUserId == user.UserId
                    || (a.EntityType == "users" && a.EntityId == subjectId)).ExecuteDeleteAsync(CancellationToken.None);
                await db.Users.Where(u => u.UserId == user.UserId && u.Username == name).ExecuteDeleteAsync(CancellationToken.None);
                await cleanup.CommitAsync(CancellationToken.None);
                cleaned = !await db.Users.AnyAsync() && !await db.UserSessions.AnyAsync() && !await db.AuditLogs.AnyAsync();
            }
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new { database, fixtureUserId = user.UserId,
                checks, failure, cleanup = cleaned, cleanupRequiresDatabaseDrop=taskCriteria, success = failure == null && (cleaned || taskCriteria),
                scope = taskDecline ? "Real UC104 Assign/Reassign plus UC107 Accept/Decline, workload, current ownership, atomic interval closure, rollback, notification and canonical browser; isolated clone cleanup required" : taskVerification ? "Real task submission, Return/restart/resubmit, verification/Closed, time, audit and private assignee handoff; Internal Request requester enforced, other legacy sources gated" : taskSubmissionReceipt!=null ? "Real UC112 submission/results/evidence/interval/audit/notification and canonical browser handoff; whole-clone drop required; UC105 verification not implemented"
                    : taskCriteria ? "Real criteria PASS/FAIL/note and evidence browser/API/SQL workflow; immutable criteria retained pending verified whole-clone drop; no full lifecycle acceptance"
                    : browser && activation ? "Real browser HTTPS/SQL login, canonical activation, credential preservation and logout; no mocks; deactivation not accepted"
                    : browser ? "Real browser HTTPS/SQL login, inbox, read markers, reload and logout with production frontend; no mocks"
                    : taskEntry ? "Real HTTPS/SQL Accept/Start with browser continuation into progress/pause/resume, first start and interval/audit atomicity; task creation, decline and completion excluded"
                    : taskResume ? "Real HTTPS/SQL task pause/resume with approved reference reasons, interval closure, audit/history, concurrency, injected rollback and canonical browser; start/create not accepted"
                    : taskRead ? "Real HTTPS/SQL and canonical browser task progress, evidence upload/private download, paused execution, audit, stale/concurrent writes and scope denials; criteria mutation and create/lifecycle acceptance excluded"
                    : notifications ? "Real HTTPS/SQL recipient inbox and read markers; no source-opening or acknowledgment acceptance"
                    : activation ? "Real HTTPS/SQL account activation, concurrency preconditions and credential preservation; deactivation remains unavailable"
                    : reports ? "Real HTTPS/SQL and canonical browser report actor restriction, preview snapshot, CSV admission/audit and revocation; detailed RPT indicators remain partial"
                    : dashboard ? "Real HTTPS/SQL auth and scoped dashboard reads; no browser or all-KPI acceptance"
                    : roleAssignment ? "Real HTTPS/SQL auth and supported role assignment with temporary synthetic admin grant; revocation not tested"
                    : audit ? "Real canonical browser/HTTPS/SQL audit filters, pagination, record chronology and redacted field comparison; branch/global Administrator admission and revocation; no audit mutation endpoint"
                    : "Real HTTPS/SQL login-session-logout; no privileged roles or browser workflow acceptance" },
                new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine($"Sprint HTTPS auth checks: {(failure == null && (cleaned || taskCriteria) ? "PASS" : "FAIL")}; checks={checks.Count}; cleanup={cleaned}; wholeCloneDropRequired={taskCriteria}");
        return failure == null && (cleaned || taskCriteria) ? 0 : 1;
    }
}
