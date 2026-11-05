using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class ActivationHttpSuite
{
    internal static async Task Run(string connection, Action<bool, string> check, CancellationToken ct)
    {
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
        var prefix = "T36H_" + Guid.NewGuid().ToString("N")[..8];
        var password = SessionCredential.Generate();
        var hasher = new PasswordCredentialService(Options.Create(new PasswordHasherOptions
            { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
        var users = new Dictionary<string, User>(); var testRoles = new Dictionary<string, long>(); var secrets = new List<string> { password };
        var now = DateTime.UtcNow;
        var branches = await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId).Take(2).ToArrayAsync(ct);
        var department = await db.Departments.AsNoTracking().Where(d => d.IsActive && d.BranchId == branches[0].BranchId).FirstAsync(ct);
        var adminRole = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "SYS_ADMIN", ct);
        var ownerRole = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "OWNER", ct);
        var staffRole = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "HK_STAFF", ct);
        var manage = await db.Permissions.AsNoTracking().SingleAsync(p => p.PermissionCode == "USER_MANAGE", ct);
        string? certFile = null; Process? process = null; Task<string>? stdout = null, stderr = null;
        try
        {
            var actors = new[] { "admin", "admin2", "branch", "own", "boundall", "denied", "owner", "roleoff", "assignmentoff",
                "grantoff", "membershipoff", "futureassignment", "expiredassignment", "inactiveactor", "deletedactor", "revoked", "expired", "restricted" };
            var targets = new[] { "target", "otherbranch", "race", "fault", "locked", "expiredcredential", "deleted", "unknown", "noop" };
            foreach (var key in actors.Concat(targets))
            {
                var code = prefix + key;
                var user = new User { EmployeeCode = code, Username = code, Email = code + "@example.invalid", FullName = "HTTPS activation fixture",
                    AccountStatus = actors.Contains(key) || key == "noop" ? "ACTIVE" : key == "unknown" ? "UNKNOWN" : "INACTIVE",
                    EmploymentStatus = "ACTIVE", CreatedAt = now, PrimaryBranchId = key == "otherbranch" ? branches[1].BranchId : branches[0].BranchId,
                    DepartmentId = key == "otherbranch" ? null : department.DepartmentId, DeletedAt = key == "deleted" ? now : null,
                    LockedUntil = key == "locked" ? now.AddHours(1) : null,
                    TemporaryPasswordExpiresAt = key == "restricted" ? now.AddHours(24) : key == "expiredcredential" ? now.AddMinutes(-1) : null };
                user.PasswordHash = hasher.HashPassword(user, password); users[key] = user; db.Add(user);
            }
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            secrets.AddRange(users.Values.Select(u => u.PasswordHash!));
            foreach (var user in users.Values)
                db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.UserId, BranchId = user.PrimaryBranchId!.Value,
                    GrantedBy = users["admin"].UserId, GrantedAt = now, IsPrimary = true, AccessLevel = "STANDARD" });
            foreach (var key in new[] { "admin", "admin2", "inactiveactor", "deletedactor", "revoked", "expired", "restricted" })
                db.UserRoles.Add(new UserRole { UserId = users[key].UserId, RoleId = adminRole.RoleId, AssignedAt = now, IsActive = true });
            db.UserRoles.Add(new UserRole { UserId = users["owner"].UserId, RoleId = ownerRole.RoleId, AssignedAt = now, IsActive = true });
            db.UserRoles.Add(new UserRole { UserId = users["target"].UserId, RoleId = staffRole.RoleId, BranchId = branches[0].BranchId, AssignedAt = now, IsActive = true });
            var work = new OperationalTask { TaskCode = prefix + "work", BranchId = branches[0].BranchId, DepartmentId = department.DepartmentId,
                Title = "Owned preserved responsibility", Status = "TODO", CreatedBy = users["admin"].UserId, CreatedAt = now };
            db.Add(work); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            db.OperationalTaskAssignments.Add(new OperationalTaskAssignment { TaskId = work.TaskId, AssigneeId = users["target"].UserId,
                AssignedBy = users["admin"].UserId, AssignedAt = now, AssignmentStatus = "ACTIVE" });
            // A previously revoked session must remain revoked after reactivation.
            db.UserSessions.Add(new UserSession { UserId = users["target"].UserId, SessionKeyHash = SessionCredential.Hash(SessionCredential.Generate()),
                CreatedAt = now, LastUsedAt = now, ExpiresAt = now.AddMinutes(20), RevokedAt = now });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            foreach (var key in new[] { "branch", "own", "boundall", "roleoff", "assignmentoff", "grantoff", "membershipoff", "futureassignment", "expiredassignment" })
            {
                var role = new Role { RoleCode = prefix + key, RoleName = "Owned activation test role", IsActive = true, IsSystemRole = false };
                db.Add(role); await db.SaveChangesAsync(ct); testRoles.Add(key, role.RoleId); db.ChangeTracker.Clear();
                db.RolePermissions.Add(new RolePermission { RoleId = role.RoleId, PermissionId = manage.PermissionId,
                    DataScope = key == "own" ? "OWN" : key == "boundall" ? "ALL" : "BRANCH", GrantedAt = now });
                db.UserRoles.Add(new UserRole { UserId = users[key].UserId, RoleId = role.RoleId, BranchId = branches[0].BranchId,
                    AssignedAt = now, IsActive = true });
                await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            }
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
            using var rsa = RSA.Create(2048);
            var certificateRequest = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback); certificateRequest.CertificateExtensions.Add(san.Build());
            using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            certFile = Path.Combine(Path.GetTempPath(), "HotelOps-T36-" + Guid.NewGuid().ToString("N") + ".pfx");
            var certPassword = SessionCredential.Generate(); secrets.Add(certPassword);
            await File.WriteAllBytesAsync(certFile, certificate.Export(X509ContentType.Pfx, certPassword), ct);
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(root, "src/HotelOps.Api"), UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.Combine(root, "src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll"));
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; start.Environment["DOTNET_ENVIRONMENT"] = "Development";
            start.Environment["ASPNETCORE_URLS"] = "https://127.0.0.1:" + port;
            start.Environment["ConnectionStrings__HotelOpsDb"] = connection;
            start.Environment["Kestrel__Certificates__Default__Path"] = certFile;
            start.Environment["Kestrel__Certificates__Default__Password"] = certPassword;
            start.Environment["Logging__LogLevel__Default"] = "Information";
            process = Process.Start(start) ?? throw new InvalidOperationException("API did not launch");
            stdout = process.StandardOutput.ReadToEndAsync(ct); stderr = process.StandardError.ReadToEndAsync(ct);
            using var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false,
                ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.GetCertHashString() == certificate.GetCertHashString() };
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(40) };
            async Task<HttpResponseMessage> Send(string path, Jar jar, object? body = null, string? etag = null, bool csrf = true,
                string? raw = null, HttpMethod? method = null)
            {
                using var request = new HttpRequestMessage(method ?? HttpMethod.Patch, path);
                if (jar.Cookies.Count > 0) request.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(p => p.Key + "=" + p.Value)));
                if (csrf && jar.Csrf != null) request.Headers.Add(HotelOpsAntiforgery.HeaderName, jar.Csrf);
                if (etag != null) request.Headers.TryAddWithoutValidation("If-Match", etag);
                if (raw != null) request.Content = new StringContent(raw, Encoding.UTF8, "application/json");
                else if (body != null) request.Content = JsonContent.Create(body);
                var result = await http.SendAsync(request, ct); jar.Read(result); return result;
            }
            var ready = false;
            for (var attempt = 0; attempt < 45 && !process.HasExited; attempt++)
            {
                try { using var response = await http.GetAsync("/api/auth/csrf", ct); if (response.StatusCode == HttpStatusCode.OK) { ready = true; break; } }
                catch (HttpRequestException) { }
                await Task.Delay(200, ct);
            }
            check(ready, "HTTPS actual API startup resolves activation DI");
            async Task<Jar> Bootstrap()
            {
                var jar = new Jar();
                using var boot = await Send("/api/auth/csrf", jar, method: HttpMethod.Get);
                using var json = JsonDocument.Parse(await boot.Content.ReadAsStringAsync(ct)); jar.Csrf = json.RootElement.GetProperty("csrfToken").GetString();
                secrets.Add(jar.Csrf!); secrets.AddRange(jar.Cookies.Values.Where(v => !string.IsNullOrEmpty(v)));
                return jar;
            }
            async Task<Jar> Login(string key)
            {
                var jar = await Bootstrap();
                using var login = await Send("/api/auth/login", jar, new { username = users[key].Username, password }, method: HttpMethod.Post);
                check(login.StatusCode == HttpStatusCode.OK, "HTTPS real SQL-backed login " + key);
                using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync(ct)); jar.Csrf = json.RootElement.GetProperty("csrfToken").GetString();
                secrets.Add(jar.Csrf!); secrets.AddRange(jar.Cookies.Values.Where(v => !string.IsNullOrEmpty(v)));
                return jar;
            }
            var jars = new Dictionary<string, Jar>();
            foreach (var key in actors) jars[key] = await Login(key);
            string PathFor(string key) => "/api/users/" + users[key].UserId + "/activate";
            async Task<string> Tag(string key) => "\"" + Convert.ToHexString(await db.Users.Where(u => u.UserId == users[key].UserId).Select(u => u.RowVersion).SingleAsync(ct)) + "\"";
            var reason = new { reason = "  Employee returns to work  " };
            async Task Expect(string path, Jar jar, int status, string label, string? etag = null, string? raw = null, bool csrf = true, object? body = null)
            {
                using var response = await Send(path, jar, body ?? reason, etag, csrf, raw);
                var text = await response.Content.ReadAsStringAsync(ct);
                if ((int)response.StatusCode != status)
                    Console.WriteLine("Unexpected response exception types only: " + string.Join(", ",
                        System.Text.RegularExpressions.Regex.Matches(text, @"(?:[A-Za-z_]\w*\.)*[A-Za-z_]\w*Exception")
                            .Select(m => m.Value).Distinct()));
                check((int)response.StatusCode == status, label + " (HTTP " + (int)response.StatusCode + ")");
                check(secrets.All(s => !text.Contains(s)) && !text.Contains("temporaryCredential", StringComparison.OrdinalIgnoreCase), label + " discloses no credential");
            }
            async Task<string> Preserved(string key)
            {
                var id = users[key].UserId;
                var user = await db.Users.AsNoTracking().Where(u => u.UserId == id).Select(u => new { u.EmployeeCode, u.Username, u.Email,
                    u.FullName, u.Phone, u.AvatarUrl, u.DepartmentId, u.PrimaryBranchId, u.ManagerUserId, u.EmploymentStatus, u.PasswordHash,
                    u.TemporaryPasswordExpiresAt, u.LockedUntil, u.FailedLoginCount, u.CreatedAt, u.DeletedAt, u.LastLoginAt }).SingleAsync(ct);
                var roles = await db.UserRoles.AsNoTracking().Where(r => r.UserId == id).OrderBy(r => r.UserRoleId).ToArrayAsync(ct);
                var memberships = await db.UserBranchAccesses.AsNoTracking().Where(r => r.UserId == id).OrderBy(r => r.AccessId).ToArrayAsync(ct);
                var sessions = await db.UserSessions.AsNoTracking().Where(r => r.UserId == id).OrderBy(r => r.SessionId).ToArrayAsync(ct);
                var assignments = await db.OperationalTaskAssignments.AsNoTracking().Where(a => a.AssigneeId == id).OrderBy(a => a.AssignmentId).ToArrayAsync(ct);
                return JsonSerializer.Serialize(new { user, roles, memberships, sessions, assignments }); // Secret-bearing state stays in memory.
            }
            var tag = await Tag("target"); var preserved = await Preserved("target");
            await Expect(PathFor("target"), await Bootstrap(), 401, "anonymous activation denied with valid CSRF", tag);
            await Expect(PathFor("target"), jars["admin"], 400, "missing CSRF denied", tag, csrf: false);
            var csrfToken = jars["admin"].Csrf; jars["admin"].Csrf = "invalid";
            await Expect(PathFor("target"), jars["admin"], 400, "invalid CSRF denied", tag); jars["admin"].Csrf = csrfToken;
            foreach (var key in new[] { "own", "boundall", "denied", "owner", "restricted" })
                await Expect(PathFor("target"), jars[key], 403, key + " cannot activate", tag);
            await Expect(PathFor("otherbranch"), jars["branch"], 403, "cross-branch activation denied", await Tag("otherbranch"));
            await Expect(PathFor("admin"), jars["admin"], 403, "ALL self-activation denied", await Tag("admin"));
            await Expect(PathFor("target"), jars["admin"], 428, "missing If-Match gives428");
            foreach (var invalidTag in new[] { "*", "W/" + tag, "\"0000\"", tag + "," + tag, "\"GGGGGGGGGGGGGGGG\"" })
                await Expect(PathFor("target"), jars["admin"], 400, "malformed If-Match rejected", invalidTag);
            foreach (var raw in new[] { "{}", "[]", "null", "{", "{\"reason\":null}", "{\"reason\":false}", "{\"reason\":123}",
                "{\"reason\":\"  123456789  \"}", "{\"reason\":\"valid reason text\",\"reason\":\"duplicate value\"}",
                "{\"reason\":\"valid reason text\",\"actorId\":1}", "{\"reason\":\"valid reason text\",\"status\":\"INACTIVE\"}" })
                await Expect(PathFor("target"), jars["admin"], 400, "strict activation JSON rejects invalid/duplicate/unknown/status input", tag, raw);
            await Expect(PathFor("target"), jars["admin"], 413, "oversized JSON rejected by request body limit", tag, body: new { reason = new string('x', 20000) });
            await Expect("/api/users/0/activate", jars["admin"], 400, "nonpositive target rejected", tag);
            await Expect(PathFor("deleted"), jars["admin"], 404, "soft-deleted target hidden", await Tag("deleted"));
            await Expect("/api/users/9223372036854775807/activate", jars["admin"], 404, "missing target hidden after ALL authorization", tag);
            await Expect(PathFor("unknown"), jars["admin"], 409, "unknown source status cannot transition", await Tag("unknown"));
            await Expect(PathFor("noop"), jars["admin"], 409, "ACTIVE no-op gives409", await Tag("noop"));
            check(await Preserved("target") == preserved && await Tag("target") == tag, "all denied requests leave target and token unchanged");
            await db.Roles.Where(r => r.RoleId == testRoles["roleoff"]).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
            await db.UserRoles.Where(r => r.UserId == users["assignmentoff"].UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
            await db.RolePermissions.Where(r => r.RoleId == testRoles["grantoff"]).ExecuteUpdateAsync(s => s.SetProperty(r => r.DataScope, "OWN"), ct);
            await db.UserBranchAccesses.Where(a => a.UserId == users["membershipoff"].UserId).ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, now), ct);
            await db.UserRoles.Where(r => r.UserId == users["futureassignment"].UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.AssignedAt, now.AddHours(1)), ct);
            await db.UserRoles.Where(r => r.UserId == users["expiredassignment"].UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiresAt, now.AddSeconds(-1)), ct);
            foreach (var key in new[] { "roleoff", "assignmentoff", "grantoff", "membershipoff", "futureassignment", "expiredassignment" })
                await Expect(PathFor("target"), jars[key], 403, key + " SQL authority denied", tag);
            async Task ReadSuccess(HttpResponseMessage response, string key, string oldTag, string before, long expectedActor)
            {
                check(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true, "activation200 response is no-store after commit");
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                check(json.RootElement.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "accountStatus", "userId" })
                    && json.RootElement.GetProperty("accountStatus").GetString() == "ACTIVE"
                    && json.RootElement.GetProperty("userId").GetInt64() == users[key].UserId, "activation response whitelist has userId and accountStatus only");
                check(response.Headers.ETag?.ToString() == await Tag(key) && await Tag(key) != oldTag, "HTTP ETag equals committed changed SQL rowversion");
                check(await db.Users.Where(u => u.UserId == users[key].UserId).Select(u => u.AccountStatus).SingleAsync(ct) == "ACTIVE"
                    && await Preserved(key) == before, "SQL activation preserves credential roles scope sessions and operational assignment");
                var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityType == "users" && a.EntityId == users[key].UserId.ToString() && a.Action == "UPDATE", ct);
                using var oldValues = JsonDocument.Parse(audit.OldValues!); using var newValues = JsonDocument.Parse(audit.NewValues!);
                check(audit.ActorUserId == expectedActor && oldValues.RootElement.GetProperty("accountStatus").GetString() == "INACTIVE"
                    && newValues.RootElement.GetProperty("accountStatus").GetString() == "ACTIVE"
                    && newValues.RootElement.GetProperty("reason").GetString() == "Employee returns to work"
                    && newValues.RootElement.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "accountStatus", "reason" })
                    && secrets.All(s => !audit.NewValues!.Contains(s)), "audit has server actor target before-after trimmed reason without credentials");
            }
            using (var response = await Send(PathFor("target"), jars["branch"], reason, tag)) await ReadSuccess(response, "target", tag, preserved, users["branch"].UserId);
            await Expect(PathFor("target"), jars["branch"], 412, "retry after activation commit gives412", tag);
            await Expect(PathFor("target"), jars["branch"], 409, "fresh token on ACTIVE gives409", await Tag("target"));
            foreach (var key in new[] { "otherbranch", "locked", "expiredcredential" })
            {
                var originalTag = await Tag(key); var originalState = await Preserved(key);
                using var response = await Send(PathFor(key), jars["admin"], reason, originalTag);
                await ReadSuccess(response, key, originalTag, originalState, users["admin"].UserId);
            }
            foreach (var (key, expected) in new[] { ("locked", 423), ("expiredcredential", 403) })
            {
                var jar = await Bootstrap();
                using var login = await Send("/api/auth/login", jar, new { username = users[key].Username, password }, method: HttpMethod.Post);
                check((int)login.StatusCode == expected, "activation preserves existing login lock/temporary expiry policy for " + key);
            }
            var raceTag = await Tag("race"); var raceState = await Preserved("race");
            var race = await Task.WhenAll(Send(PathFor("race"), jars["admin"], reason, raceTag), Send(PathFor("race"), jars["admin2"], reason, raceTag));
            try
            {
                check(race.Count(r => r.StatusCode == HttpStatusCode.OK) == 1 && race.Count(r => r.StatusCode == HttpStatusCode.PreconditionFailed) == 1,
                    "concurrent HTTP activation by two admins gives one200 one412");
                await ReadSuccess(race.Single(r => r.StatusCode == HttpStatusCode.OK), "race", raceTag, raceState,
                    users[race[0].StatusCode == HttpStatusCode.OK ? "admin" : "admin2"].UserId);
            }
            finally { foreach (var response in race) response.Dispose(); }
            var faultId = users["fault"].UserId; var faultTag = await Tag("fault"); var faultState = await Preserved("fault");
            var faultConstraint = prefix + "AuditFault";
            await db.Database.OpenConnectionAsync(ct);
            await using (var ddl = db.Database.GetDbConnection().CreateCommand())
            {
                // Identifier is generated ASCII prefix; value is an owned positive SQL identity formatted as digits.
                ddl.CommandText = "ALTER TABLE dbo.audit_logs ADD CONSTRAINT " + faultConstraint
                    + " CHECK (entity_type<>'users' OR entity_id<>N'" + faultId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "' OR action<>'UPDATE')";
                await ddl.ExecuteNonQueryAsync(ct);
            }
            try
            {
                await Expect(PathFor("fault"), jars["admin"], 500, "SQL audit failure rejects activation", faultTag);
                check(await Tag("fault") == faultTag && await Preserved("fault") == faultState
                    && await db.Users.Where(u => u.UserId == faultId).Select(u => u.AccountStatus).SingleAsync(ct) == "INACTIVE"
                    && !await db.AuditLogs.AnyAsync(a => a.EntityType == "users" && a.EntityId == faultId.ToString(), ct),
                    "HTTP audit failure rolls back status token and audit without credential/session mutation");
            }
            finally
            {
                await using var cleanup = db.Database.GetDbConnection().CreateCommand();
                cleanup.CommandText = "ALTER TABLE dbo.audit_logs DROP CONSTRAINT " + faultConstraint;
                await cleanup.ExecuteNonQueryAsync(CancellationToken.None);
            }
            await db.Users.Where(u => u.UserId == users["inactiveactor"].UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "INACTIVE"), ct);
            await db.Users.Where(u => u.UserId == users["deletedactor"].UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.DeletedAt, now), ct);
            await db.UserSessions.Where(s => s.UserId == users["revoked"].UserId).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, DateTime.UtcNow), ct);
            await db.UserSessions.Where(s => s.UserId == users["expired"].UserId).ExecuteUpdateAsync(s => s.SetProperty(s => s.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)), ct);
            foreach (var key in new[] { "inactiveactor", "deletedactor", "revoked", "expired" })
                await Expect(PathFor("fault"), jars[key], 401, key + " session cannot activate", faultTag);
            foreach (var path in new[] { "/api/users/" + users["target"].UserId + "/status", "/api/users/" + users["target"].UserId + "/deactivate" })
                await Expect(path, jars["admin"], 404, "no status/deactivate route exists", await Tag("target"), body: new { status = "INACTIVE", reason = "Never deactivate via Task36" });
            check(await db.Users.Where(u => u.UserId == users["target"].UserId).Select(u => u.AccountStatus).SingleAsync(ct) == "ACTIVE", "all deactivation attempts leave target ACTIVE");
            process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None);
            var logs = await stdout + await stderr;
            check(secrets.All(s => !logs.Contains(s)), "actual API Information logs contain no passwords hashes cookies CSRF or certificate secrets");
        }
        finally
        {
            if (process != null) { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); process.Dispose(); }
            if (certFile != null) File.Delete(certFile); // Only this run's ephemeral certificate.
            var ids = users.Values.Select(u => u.UserId).Where(id => id > 0).ToArray(); var roleIds = testRoles.Values.ToArray();
            await db.UserSessions.Where(s => ids.Contains(s.UserId) && s.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, DateTime.UtcNow), CancellationToken.None);
            await db.UserRoles.Where(r => roleIds.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.RolePermissions.Where(r => roleIds.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Roles.Where(r => roleIds.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
        }
    }
    private sealed class Jar
    {
        public Dictionary<string, string> Cookies { get; } = new();
        public string? Csrf { get; set; }
        public void Read(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("Set-Cookie", out var values))
                foreach (var value in values)
                {
                    var pair = value.Split(';')[0]; var equals = pair.IndexOf('=');
                    if (equals > 0) Cookies[pair[..equals]] = pair[(equals + 1)..];
                }
        }
    }
}
