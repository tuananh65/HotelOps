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

static class HttpSuite
{
    public static async Task Run(string connection, Action<bool, string> check, CancellationToken ct)
    {
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
        var prefix = "T33H_" + Guid.NewGuid().ToString("N")[..8];
        var password = SessionCredential.Generate();
        var hasher = new PasswordCredentialService(Options.Create(new PasswordHasherOptions
            { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
        var users = new Dictionary<string, User>(); var testRoleIds = new List<long>(); var secrets = new List<string> { password };
        var delivered = new Dictionary<string, string>();
        var now = DateTime.UtcNow;
        var branches = await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId).Take(2).ToArrayAsync(ct);
        var adminRole = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "SYS_ADMIN", ct);
        var manage = await db.Permissions.AsNoTracking().SingleAsync(p => p.PermissionCode == "USER_MANAGE", ct);
        string? certFile = null; Process? process = null; Task<string>? stdout = null, stderr = null;
        try
        {
            foreach (var key in new[] { "admin", "admin2", "branch", "own", "boundall", "denied", "inactive", "expired", "revoked", "logout", "restricted", "target", "otherbranch", "race", "fault", "inactiveTarget", "deletedTarget" })
            {
                var code = prefix + key;
                var user = new User { EmployeeCode = code, Username = code, Email = code + "@example.invalid", FullName = "HTTPS reset fixture",
                    AccountStatus = key == "inactiveTarget" ? "INACTIVE" : "ACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = now,
                    PrimaryBranchId = key == "otherbranch" ? branches[1].BranchId : branches[0].BranchId,
                    DeletedAt = key == "deletedTarget" ? now : null,
                    TemporaryPasswordExpiresAt = key == "restricted" ? now.AddHours(24) : null };
                user.PasswordHash = hasher.HashPassword(user, password); users[key] = user; db.Add(user);
            }
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            secrets.AddRange(users.Values.Select(u => u.PasswordHash!));
            foreach (var user in users.Values)
                db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.UserId, BranchId = user.PrimaryBranchId!.Value,
                    GrantedBy = users["admin"].UserId, GrantedAt = now, IsPrimary = true, AccessLevel = "STANDARD" });
            foreach (var key in new[] { "admin", "admin2" }) db.UserRoles.Add(new UserRole { UserId = users[key].UserId,
                RoleId = adminRole.RoleId, AssignedAt = now, IsActive = true });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            foreach (var (key, scope) in new[] { ("branch", "BRANCH"), ("own", "OWN"), ("boundall", "ALL") })
            {
                var role = new Role { RoleCode = prefix + key, RoleName = "Owned verification role", IsActive = true, IsSystemRole = false };
                db.Add(role); await db.SaveChangesAsync(ct); testRoleIds.Add(role.RoleId); db.ChangeTracker.Clear();
                db.RolePermissions.Add(new RolePermission { RoleId = role.RoleId, PermissionId = manage.PermissionId, DataScope = scope, GrantedAt = now });
                db.UserRoles.Add(new UserRole { UserId = users[key].UserId, RoleId = role.RoleId,
                    BranchId = branches[0].BranchId, AssignedAt = now, IsActive = true });
                await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            }
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
            using var rsa = RSA.Create(2048);
            var certificateRequest = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback); certificateRequest.CertificateExtensions.Add(san.Build());
            using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            certFile = Path.Combine(Path.GetTempPath(), "HotelOps-T33-" + Guid.NewGuid().ToString("N") + ".pfx");
            var certPassword = SessionCredential.Generate(); await File.WriteAllBytesAsync(certFile, certificate.Export(X509ContentType.Pfx, certPassword), ct);
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
            async Task<HttpResponseMessage> Send(string path, Jar jar, object? body = null, string? etag = null, bool csrf = true, string? raw = null, bool get = false)
            {
                using var request = new HttpRequestMessage(get ? HttpMethod.Get : HttpMethod.Post, path);
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
            check(ready, "HTTPS actual API startup and DI resolve");
            async Task<Jar> Login(string key)
            {
                var jar = new Jar();
                using (var boot = await Send("/api/auth/csrf", jar, get: true))
                { using var json = JsonDocument.Parse(await boot.Content.ReadAsStringAsync(ct)); jar.Csrf = json.RootElement.GetProperty("csrfToken").GetString(); }
                using var login = await Send("/api/auth/login", jar, new { username = users[key].Username, password });
                check(login.StatusCode == HttpStatusCode.OK, "HTTPS real login " + key);
                using var result = JsonDocument.Parse(await login.Content.ReadAsStringAsync(ct)); jar.Csrf = result.RootElement.GetProperty("csrfToken").GetString();
                return jar;
            }
            var jars = new Dictionary<string, Jar>();
            foreach (var key in new[] { "admin", "admin2", "branch", "own", "boundall", "denied", "inactive", "expired", "revoked", "logout", "restricted", "target" })
                jars[key] = await Login(key);
            var secondSession = await Login("admin"); // Established before first reauth so this login cannot itself manufacture proof.
            async Task Reauth(string key)
            {
                using var response = await Send("/api/auth/reauthenticate", jars[key], new { currentPassword = password });
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                check(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true
                    && json.RootElement.EnumerateObject().Select(p => p.Name).SequenceEqual(new[] { "expiresAtUtc" }), "HTTPS reauth success/no-store/no bearer or session secret " + key);
            }
            async Task<string> Tag(string key)
                => "\"" + Convert.ToHexString(await db.Users.AsNoTracking().Where(u => u.UserId == users[key].UserId).Select(u => u.RowVersion).SingleAsync(ct)) + "\"";
            string PathFor(string key) => "/api/users/" + users[key].UserId + "/reset-credential";
            var confirmation = new { identityVerificationConfirmed = true };
            async Task Expect(string path, Jar jar, object body, int status, string label, string? tag = null, bool csrf = true, string? raw = null)
            {
                using var response = await Send(path, jar, body, tag, csrf, raw);
                check((int)response.StatusCode == status, label);
                var content = await response.Content.ReadAsStringAsync(ct);
                check(!content.Contains(password) && !content.Contains("temporaryCredential"), label + " has no credential handoff");
            }
            var anonymous = new Jar();
            using (var boot = await Send("/api/auth/csrf", anonymous, get: true))
            { using var json = JsonDocument.Parse(await boot.Content.ReadAsStringAsync(ct)); anonymous.Csrf = json.RootElement.GetProperty("csrfToken").GetString(); }
            await Expect("/api/auth/reauthenticate", anonymous, new { currentPassword = password }, 401, "anonymous reauthentication denied with valid CSRF");
            await Expect("/api/auth/reauthenticate", jars["admin"], new { currentPassword = password }, 400, "reauthentication missing CSRF denied", csrf: false);
            var savedCsrf = jars["admin"].Csrf; jars["admin"].Csrf = "invalid";
            await Expect("/api/auth/reauthenticate", jars["admin"], new { currentPassword = password }, 400, "reauthentication invalid CSRF denied");
            jars["admin"].Csrf = savedCsrf;
            await Expect("/api/auth/reauthenticate", jars["admin"], new { currentPassword = "incorrect-secret" }, 401, "HTTPS incorrect current password denied");
            foreach (var raw in new[] { "{}", "{\"currentPassword\":\"x\",\"currentPassword\":\"x\"}", "{\"currentPassword\":1}", "{\"currentPassword\":\"x\",\"reauthenticated\":true}" })
                await Expect("/api/auth/reauthenticate", jars["admin"], new { }, 400, "strict reauthentication body rejects missing/duplicate/type/client proof", raw: raw);
            var tag = await Tag("target");
            await Expect(PathFor("target"), anonymous, confirmation, 401, "anonymous reset denied with valid CSRF", tag);
            await Expect(PathFor("target"), jars["admin"], confirmation, 403, "reset without proof denied", tag);
            await Reauth("admin");
            await Expect(PathFor("target"), secondSession, confirmation, 403, "same actor different session cannot use reauthentication proof", tag);
            await Expect("/api/auth/reauthenticate", jars["restricted"], new { currentPassword = password }, 403, "restricted temporary session cannot reauthenticate");
            foreach (var key in new[] { "admin2", "branch", "own", "boundall", "denied", "logout" }) await Reauth(key);
            await Expect(PathFor("target"), jars["denied"], confirmation, 403, "no USER_MANAGE denies reset despite reauthentication", tag);
            await Expect(PathFor("target"), jars["own"], confirmation, 403, "OWN grant denies reset", tag);
            await Expect(PathFor("target"), jars["boundall"], confirmation, 403, "branch-bound ALL denies reset", tag);
            await Expect(PathFor("otherbranch"), jars["branch"], confirmation, 403, "BRANCH cross-branch reset denied", await Tag("otherbranch"));
            await Expect(PathFor("admin"), jars["admin"], confirmation, 403, "ALL self-reset denied", await Tag("admin"));
            await Expect(PathFor("target"), jars["admin"], confirmation, 428, "missing If-Match gives428");
            await Expect(PathFor("target"), jars["admin"], confirmation, 400, "malformed If-Match rejected", "*");
            foreach (var raw in new[] { "{}", "{\"identityVerificationConfirmed\":false}", "{\"identityVerificationConfirmed\":\"true\"}",
                "{\"identityVerificationConfirmed\":true,\"identityVerificationConfirmed\":true}", "{\"identityVerificationConfirmed\":true,\"password\":\"chosen\"}" })
                await Expect(PathFor("target"), jars["admin"], new { }, 400, "strict identity confirmation rejects missing/false/type/duplicate/overposting", tag, raw: raw);
            await Expect(PathFor("target"), jars["admin"], confirmation, 400, "reset missing CSRF denied", tag, csrf: false);
            jars["admin"].Csrf = "invalid";
            await Expect(PathFor("target"), jars["admin"], confirmation, 400, "reset invalid CSRF denied", tag);
            jars["admin"].Csrf = savedCsrf;
            await Expect(PathFor("inactiveTarget"), jars["admin"], confirmation, 409, "authorized inactive target reset rejected", await Tag("inactiveTarget"));
            await Expect(PathFor("deletedTarget"), jars["admin"], confirmation, 404, "authorized soft-deleted target not found", await Tag("deletedTarget"));
            await Expect("/api/users/9223372036854775807/reset-credential", jars["admin"], confirmation, 404, "authorized missing target not found", tag);
            await Expect("/api/users/9223372036854775807/reset-credential", jars["denied"], confirmation, 403, "unauthorized missing target conceals existence", tag);
            async Task ReadSuccess(HttpResponseMessage response, string key)
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                check(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true
                    && json.RootElement.EnumerateObject().Select(p => p.Name).SequenceEqual(new[] { "temporaryCredential" }), "reset success-only no-store response whitelist");
                var credential = json.RootElement.GetProperty("temporaryCredential");
                check(credential.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "expiresAtUtc", "password" }), "reset response excludes hash/session/proof internals");
                var secret = credential.GetProperty("password").GetString()!; secrets.Add(secret); delivered[key] = secret;
                var person = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == users[key].UserId, ct);
                secrets.Add(person.PasswordHash!);
                check(hasher.VerifyPassword(person, person.PasswordHash, secret) != PasswordVerificationResult.Failed
                    && response.Headers.ETag?.Tag == await Tag(key), "HTTP handoff matches committed credential and SQL token");
                check(await db.UserSessions.CountAsync(s => s.UserId == person.UserId && s.RevokedAt == null, ct) == 0, "HTTP reset revokes all target SQL sessions");
                var audit = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "users" && a.EntityId == person.UserId.ToString()
                    && a.NewValues != null && a.NewValues.Contains("CREDENTIAL_RESET")).OrderByDescending(a => a.AuditId).FirstAsync(ct);
                using var auditJson = JsonDocument.Parse(audit.NewValues!);
                check(auditJson.RootElement.GetProperty("identity_verification_method").GetString() == "INTERNAL_PROCEDURE_CONFIRMATION"
                    && auditJson.RootElement.GetProperty("identity_verification_source").GetString() == "ADMIN_ATTESTATION"
                    && auditJson.RootElement.GetProperty("reset_outcome").GetString() == "SUCCEEDED"
                    && !audit.NewValues!.Contains(secret) && !audit.NewValues.Contains(person.PasswordHash!), "reset audit records offline admin attestation/outcome without secrets");
            }
            using (var response = await Send(PathFor("target"), jars["branch"], confirmation, tag)) await ReadSuccess(response, "target");
            using (var sessionRead = await Send("/api/auth/session", jars["target"], get: true))
                check(sessionRead.StatusCode == HttpStatusCode.Unauthorized, "GET session rejects revoked target cookie");
            await Expect(PathFor("target"), jars["branch"], confirmation, 412, "same-token retry after commit returns412 no password replay", tag);
            var resetEmployee = new Jar();
            using (var boot = await Send("/api/auth/csrf", resetEmployee, get: true))
            { using var json = JsonDocument.Parse(await boot.Content.ReadAsStringAsync(ct)); resetEmployee.Csrf = json.RootElement.GetProperty("csrfToken").GetString(); }
            using (var login = await Send("/api/auth/login", resetEmployee, new { username = users["target"].Username, password = delivered["target"] }))
            {
                check(login.StatusCode == HttpStatusCode.OK, "reset employee authenticates with delivered temporary password");
                using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync(ct)); resetEmployee.Csrf = json.RootElement.GetProperty("csrfToken").GetString();
            }
            using (var blocked = await Send("/api/users", resetEmployee, get: true))
                check(blocked.StatusCode == HttpStatusCode.Forbidden, "reset employee first-login session is restricted");
            await Expect("/api/auth/reauthenticate", resetEmployee, new { currentPassword = delivered["target"] }, 403,
                "reset temporary password cannot establish admin reauthentication");
            using (var response = await Send(PathFor("otherbranch"), jars["admin"], confirmation, await Tag("otherbranch"))) await ReadSuccess(response, "otherbranch");
            await Reauth("admin"); await Reauth("admin2");
            var raceTag = await Tag("race");
            var race = await Task.WhenAll(Send(PathFor("race"), jars["admin"], confirmation, raceTag), Send(PathFor("race"), jars["admin2"], confirmation, raceTag));
            try
            {
                check(race.Count(r => r.StatusCode == HttpStatusCode.OK) == 1 && race.Count(r => r.StatusCode == HttpStatusCode.PreconditionFailed) == 1,
                    "two administrators concurrent HTTP reset yield one200 and one412");
                await ReadSuccess(race.Single(r => r.StatusCode == HttpStatusCode.OK), "race");
            }
            finally { foreach (var response in race) response.Dispose(); }
            // Trial-only audit CHECK fault, removed in finally. Capture exact per-target state in memory.
            var faultId = users["fault"].UserId; var faultTag = await Tag("fault");
            db.UserSessions.Add(new UserSession { UserId = faultId, SessionKeyHash = SessionCredential.Hash(SessionCredential.Generate()),
                CreatedAt = DateTime.UtcNow, LastUsedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(20) });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            async Task<string> FaultState() => JsonSerializer.Serialize(new { user = await db.Users.AsNoTracking().Where(u => u.UserId == faultId)
                .Select(u => new { u.PasswordHash, u.TemporaryPasswordExpiresAt, u.RowVersion }).SingleAsync(ct),
                sessions = await db.UserSessions.AsNoTracking().Where(s => s.UserId == faultId).Select(s => s.RevokedAt).ToArrayAsync(ct),
                auditCount = await db.AuditLogs.CountAsync(a => a.EntityType == "users" && a.EntityId == faultId.ToString(), ct) });
            var original = await FaultState();
            if (faultId <= 0) throw new InvalidOperationException("Owned positive fixture identity required");
            await db.Database.OpenConnectionAsync(ct);
            await using (var ddl = db.Database.GetDbConnection().CreateCommand())
            {
                // Only a SQL-generated positive Int64, formatted as invariant digits, is embedded in CHECK DDL.
                ddl.CommandText = "ALTER TABLE dbo.audit_logs ADD CONSTRAINT T33_AuditFault CHECK (entity_type<>'users' OR entity_id<>N'"
                    + faultId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "' OR action<>'UPDATE')";
                await ddl.ExecuteNonQueryAsync(ct);
            }
            try
            {
                await Expect(PathFor("fault"), jars["admin"], confirmation, 500, "audit SQL fault returns generic500 without handoff", faultTag);
                check(await FaultState() == original, "HTTP audit fault rolls back rotation/token/session revocation/audit");
            }
            finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.audit_logs DROP CONSTRAINT T33_AuditFault", CancellationToken.None); }
            await db.Users.Where(u => u.UserId == users["inactive"].UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "INACTIVE"), ct);
            await Expect("/api/auth/reauthenticate", jars["inactive"], new { currentPassword = password }, 401, "inactive actor cannot reauthenticate");
            await db.UserSessions.Where(s => s.UserId == users["expired"].UserId).ExecuteUpdateAsync(s => s.SetProperty(s => s.ExpiresAt, now.AddSeconds(1)), ct);
            await Expect("/api/auth/reauthenticate", jars["expired"], new { currentPassword = password }, 401, "expired SQL session cannot reauthenticate");
            await db.UserSessions.Where(s => s.UserId == users["revoked"].UserId).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, DateTime.UtcNow), ct);
            await Expect("/api/auth/reauthenticate", jars["revoked"], new { currentPassword = password }, 401, "revoked SQL session cannot reauthenticate");
            var oldLogout = jars["logout"].Copy();
            using (var response = await Send("/api/auth/logout", jars["logout"])) check(response.StatusCode == HttpStatusCode.NoContent, "actual HTTP logout commits revocation");
            await Expect("/api/auth/reauthenticate", oldLogout, new { currentPassword = password }, 401, "logged-out cookie cannot reuse proof");
            await Expect(PathFor("target"), oldLogout, confirmation, 401, "logged-out session cannot use proof to reset", await Tag("target"));
            var reauthPair = await Task.WhenAll(Send("/api/auth/reauthenticate", jars["admin"], new { currentPassword = password }),
                Send("/api/auth/reauthenticate", jars["admin"], new { currentPassword = password }));
            try { check(reauthPair.All(r => r.StatusCode == HttpStatusCode.OK), "concurrent HTTPS reauthentication succeeds without new login session"); }
            finally { foreach (var response in reauthPair) response.Dispose(); }
            process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None);
            var logs = await stdout + await stderr;
            check(secrets.All(s => !logs.Contains(s)), "actual API Information logs contain no current/temporary plaintext or returned credential hash");
        }
        finally
        {
            if (process != null) { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); process.Dispose(); }
            if (certFile != null) File.Delete(certFile); // Only this run's generated certificate, never existing local artifacts.
            var ids = users.Values.Select(u => u.UserId).Where(id => id > 0).ToArray();
            await db.UserSessions.Where(s => ids.Contains(s.UserId) && s.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, DateTime.UtcNow), CancellationToken.None);
            await db.UserRoles.Where(r => testRoleIds.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.RolePermissions.Where(r => testRoleIds.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Roles.Where(r => testRoleIds.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
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
        public Jar Copy() { var copy = new Jar { Csrf = Csrf }; foreach (var pair in Cookies) copy.Cookies[pair.Key] = pair.Value; return copy; }
    }
}
