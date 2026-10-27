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

internal static class BranchHttpSuite
{
    internal static async Task Run(string connection, Action<bool, string> check, CancellationToken ct)
    {
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
        var prefix = "T38H_" + Guid.NewGuid().ToString("N")[..8]; var now = DateTime.UtcNow;
        var users = new Dictionary<string, User>(); var testRoles = new List<long>(); var roleByActor = new Dictionary<string, long>();
        var password = SessionCredential.Generate(); var secrets = new List<string> { password };
        var hasher = new PasswordCredentialService(Options.Create(new PasswordHasherOptions
            { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
        var branches = await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId).Take(2).ToArrayAsync(ct);
        var adminRole = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "SYS_ADMIN", ct);
        var deptRole = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "DEPT_MANAGER", ct);
        var omRole = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "OPERATIONS_MANAGER", ct);
        var manage = await db.Permissions.AsNoTracking().SingleAsync(p => p.PermissionCode == "USER_MANAGE", ct);
        string? certFile = null; Process? process = null; Task<string>? stdout = null, stderr = null;
        try
        {
            var actors = new[] { "admin", "admin2", "branch", "sourceonly", "destonly", "own", "boundall", "denied", "roleoff", "memberoff", "revoked", "restricted" };
            var targets = new[] { "target", "allTarget", "inactive", "deleted", "latent", "om", "duplicate", "revokedMember", "race", "fault" };
            foreach (var key in actors.Concat(targets))
            {
                var code = prefix + key;
                var user = new User { EmployeeCode = code, Username = code, Email = code + "@example.invalid", FullName = "HTTPS membership fixture",
                    AccountStatus = key == "inactive" ? "INACTIVE" : "ACTIVE", EmploymentStatus = "ACTIVE", PrimaryBranchId = branches[0].BranchId,
                    CreatedAt = now, DeletedAt = key == "deleted" ? now : null, TemporaryPasswordExpiresAt = key == "restricted" ? now.AddHours(24) : null };
                user.PasswordHash = hasher.HashPassword(user, password); users.Add(key, user); db.Add(user);
            }
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); secrets.AddRange(users.Values.Select(u => u.PasswordHash!));
            foreach (var user in users.Values) db.Add(new UserBranchAccess { UserId = user.UserId, BranchId = branches[0].BranchId,
                IsPrimary = true, AccessLevel = "STANDARD", GrantedBy = users["admin"].UserId, GrantedAt = now });
            foreach (var key in new[] { "admin", "admin2", "revoked", "restricted" })
                db.Add(new UserRole { UserId = users[key].UserId, RoleId = adminRole.RoleId, IsActive = true, AssignedAt = now });
            foreach (var key in new[] { "target", "latent", "om" }) db.Add(new UserRole { UserId = users[key].UserId,
                RoleId = key == "om" ? omRole.RoleId : deptRole.RoleId, BranchId = key == "latent" ? branches[1].BranchId : branches[0].BranchId,
                IsActive = true, AssignedAt = now });
            foreach (var key in new[] { "duplicate", "revokedMember" }) db.Add(new UserBranchAccess { UserId = users[key].UserId,
                BranchId = branches[1].BranchId, IsPrimary = false, AccessLevel = "STANDARD", GrantedAt = now, RevokedAt = key == "revokedMember" ? now : null });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            foreach (var key in new[] { "branch", "sourceonly", "destonly", "own", "boundall", "roleoff", "memberoff" })
            {
                var role = new Role { RoleCode = prefix + key, RoleName = "Owned membership authority", IsActive = true };
                db.Add(role); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); testRoles.Add(role.RoleId); roleByActor.Add(key, role.RoleId);
                db.Add(new RolePermission { RoleId = role.RoleId, PermissionId = manage.PermissionId, DataScope = key == "own" ? "OWN" : key == "boundall" ? "ALL" : "BRANCH" });
                foreach (var branch in branches)
                {
                    if (key == "sourceonly" && branch.BranchId == branches[1].BranchId || key == "destonly" && branch.BranchId == branches[0].BranchId) continue;
                    db.Add(new UserRole { UserId = users[key].UserId, RoleId = role.RoleId, BranchId = branch.BranchId, IsActive = true, AssignedAt = now });
                }
                if (key != "sourceonly") db.Add(new UserBranchAccess { UserId = users[key].UserId, BranchId = branches[1].BranchId, AccessLevel = "STANDARD", GrantedAt = now });
                await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            }
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
            using var rsa = RSA.Create(2048);
            var certificateRequest = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback); certificateRequest.CertificateExtensions.Add(san.Build());
            using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            certFile = Path.Combine(Path.GetTempPath(), "HotelOps-T38-" + Guid.NewGuid().ToString("N") + ".pfx"); var certPassword = SessionCredential.Generate(); secrets.Add(certPassword);
            await File.WriteAllBytesAsync(certFile, certificate.Export(X509ContentType.Pfx, certPassword), ct);
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(root, "src/HotelOps.Api"), UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.Combine(root, "src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll"));
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; start.Environment["DOTNET_ENVIRONMENT"] = "Development";
            start.Environment["ASPNETCORE_URLS"] = "https://127.0.0.1:" + port; start.Environment["ConnectionStrings__HotelOpsDb"] = connection;
            start.Environment["Kestrel__Certificates__Default__Path"] = certFile; start.Environment["Kestrel__Certificates__Default__Password"] = certPassword;
            start.Environment["Logging__LogLevel__Default"] = "Information";
            process = Process.Start(start) ?? throw new InvalidOperationException("API launch failed"); stdout = process.StandardOutput.ReadToEndAsync(ct); stderr = process.StandardError.ReadToEndAsync(ct);
            using var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false,
                ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.GetCertHashString() == certificate.GetCertHashString() };
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(40) };
            async Task<HttpResponseMessage> Send(string path, Jar jar, object? body = null, string? tag = null, bool csrf = true, string? raw = null, HttpMethod? method = null)
            {
                using var request = new HttpRequestMessage(method ?? HttpMethod.Post, path);
                if (jar.Cookies.Count > 0) request.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(p => p.Key + "=" + p.Value)));
                if (csrf && jar.Csrf != null) request.Headers.Add(HotelOpsAntiforgery.HeaderName, jar.Csrf);
                if (tag != null) request.Headers.TryAddWithoutValidation("If-Match", tag);
                if (raw != null) request.Content = new StringContent(raw, Encoding.UTF8, "application/json"); else if (body != null) request.Content = JsonContent.Create(body);
                var response = await http.SendAsync(request, ct); jar.Read(response); return response;
            }
            var ready = false;
            for (var attempt = 0; attempt < 60 && !process.HasExited; attempt++)
            {
                try { using var response = await http.GetAsync("/api/auth/csrf", ct); if (response.StatusCode == HttpStatusCode.OK) { ready = true; break; } }
                catch (HttpRequestException) { } await Task.Delay(200, ct);
            }
            check(ready, "actual HTTPS API resolves Add membership DI");
            async Task<Jar> Bootstrap()
            {
                var jar = new Jar(); using var response = await Send("/api/auth/csrf", jar, method: HttpMethod.Get);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); jar.Csrf = json.RootElement.GetProperty("csrfToken").GetString();
                secrets.Add(jar.Csrf!); secrets.AddRange(jar.Cookies.Values); return jar;
            }
            async Task<Jar> Login(string key)
            {
                var jar = await Bootstrap(); using var response = await Send("/api/auth/login", jar, new { username = users[key].Username, password });
                check(response.StatusCode == HttpStatusCode.OK, "real SQL-backed HTTPS login " + key);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); jar.Csrf = json.RootElement.GetProperty("csrfToken").GetString();
                secrets.Add(jar.Csrf!); secrets.AddRange(jar.Cookies.Values); return jar;
            }
            var jars = new Dictionary<string, Jar>(); foreach (var actor in actors) jars.Add(actor, await Login(actor));
            var targetJar = await Login("target");
            string PathFor(string key) => "/api/users/" + users[key].UserId + "/branches";
            async Task<string> Tag(string key) => "\"" + Convert.ToHexString(await db.Users.Where(u => u.UserId == users[key].UserId).Select(u => u.RowVersion).SingleAsync(ct)) + "\"";
            var body = new { branchId = branches[1].BranchId };
            async Task Expect(string path, Jar jar, int status, string label, string? tag = null, object? input = null, string? raw = null, bool csrf = true)
            {
                using var response = await Send(path, jar, input ?? body, tag, csrf, raw); var text = await response.Content.ReadAsStringAsync(ct);
                check((int)response.StatusCode == status, label + " HTTP" + (int)response.StatusCode);
                check(secrets.Where(s => !string.IsNullOrEmpty(s)).All(s => !text.Contains(s)), label + " discloses no credential/session secret");
            }
            async Task<string> Preserved(string key)
            {
                var id = users[key].UserId;
                var user = await db.Users.AsNoTracking().Where(u => u.UserId == id).Select(u => new { u.PrimaryBranchId, u.DepartmentId, u.AccountStatus, u.EmploymentStatus,
                    u.FullName, u.Email, u.Phone, u.AvatarUrl, u.ManagerUserId, u.PasswordHash, u.TemporaryPasswordExpiresAt, u.FailedLoginCount, u.LockedUntil, u.LastLoginAt }).SingleAsync(ct);
                var roles = await db.UserRoles.AsNoTracking().Where(r => r.UserId == id).OrderBy(r => r.UserRoleId).ToArrayAsync(ct);
                var sessions = await db.UserSessions.AsNoTracking().Where(s => s.UserId == id).OrderBy(s => s.SessionId).ToArrayAsync(ct);
                return JsonSerializer.Serialize(new { user, roles, sessions }); // Memory only.
            }
            var tag = await Tag("target"); var before = await Preserved("target");
            await Expect(PathFor("target"), await Bootstrap(), 401, "anonymous denied", tag);
            await Expect(PathFor("target"), jars["admin"], 400, "missing CSRF denied", tag, csrf: false);
            var goodCsrf = jars["admin"].Csrf; jars["admin"].Csrf = "invalid";
            await Expect(PathFor("target"), jars["admin"], 400, "invalid CSRF denied", tag); jars["admin"].Csrf = goodCsrf;
            foreach (var actor in new[] { "sourceonly", "destonly", "own", "boundall", "denied", "restricted" }) await Expect(PathFor("target"), jars[actor], 403, actor + " denied", tag);
            await Expect(PathFor("admin"), jars["admin"], 403, "ALL self assignment denied", await Tag("admin"));
            await Expect(PathFor("target"), jars["admin"], 428, "missing If-Match");
            foreach (var invalid in new[] { "*", "W/" + tag, tag + "," + tag, "\"0000\"" }) await Expect(PathFor("target"), jars["admin"], 400, "invalid If-Match", invalid);
            foreach (var raw in new[] { "{}", "[]", "null", "{", "{\"branchId\":0}", "{\"branchId\":-1}", "{\"branchId\":1.5}", "{\"branchId\":\"1\"}",
                "{\"branchId\":9223372036854775808}", "{\"branchId\":1,\"branchId\":2}", "{\"branchId\":1,\"isPrimary\":true}", "{\"branchId\":1,\"departmentId\":2}", "{\"branchId\":1,\"actorId\":1}" })
                await Expect(PathFor("target"), jars["admin"], 400, "strict Add body rejects invalid/duplicate/mass assignment", tag, raw: raw);
            await Expect(PathFor("inactive"), jars["admin"], 409, "inactive target denied", await Tag("inactive"));
            await Expect(PathFor("deleted"), jars["admin"], 404, "soft-deleted target hidden", await Tag("deleted"));
            await Expect(PathFor("latent"), jars["admin"], 409, "even ALL plus ROLE_MANAGE cannot activate dormant destination role", await Tag("latent"));
            await Expect(PathFor("om"), jars["admin"], 409, "effective OPERATIONS_MANAGER cannot acquire second branch", await Tag("om"));
            foreach (var key in new[] { "duplicate", "revokedMember" }) await Expect(PathFor(key), jars["admin"], 409, key + " conflicts without regrant", await Tag(key));
            await Expect(PathFor("target"), jars["admin"], 400, "nonexistent branch denied", tag, input: new { branchId = long.MaxValue });
            await db.Roles.Where(r => r.RoleId == roleByActor["roleoff"]).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
            await db.UserBranchAccesses.Where(a => a.UserId == users["memberoff"].UserId && a.BranchId == branches[1].BranchId).ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, now), ct);
            await db.UserSessions.Where(s => s.UserId == users["revoked"].UserId).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, now), ct);
            await Expect(PathFor("target"), jars["roleoff"], 403, "inactive actor role denied", tag);
            await Expect(PathFor("target"), jars["memberoff"], 403, "revoked destination actor membership denied", tag);
            await Expect(PathFor("target"), jars["revoked"], 401, "revoked SQL actor session denied", tag);
            check(await Preserved("target") == before && await Tag("target") == tag, "all rejected HTTP requests preserve target/token");
            async Task Success(HttpResponseMessage response, string key, string oldTag, string prior, long expectedActor)
            {
                check(response.StatusCode == HttpStatusCode.Created && response.Headers.CacheControl?.NoStore == true, "Add returns201/no-store only after commit");
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); var value = json.RootElement;
                check(value.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "accessId", "accessLevel", "branchId", "isPrimary", "userId" })
                    && value.GetProperty("userId").GetInt64() == users[key].UserId && value.GetProperty("branchId").GetInt64() == branches[1].BranchId
                    && value.GetProperty("accessLevel").GetString() == "STANDARD" && !value.GetProperty("isPrimary").GetBoolean(), "201 membership response whitelist matches add-only contract");
                check(response.Headers.ETag?.ToString() == await Tag(key) && await Tag(key) != oldTag, "response ETag matches changed committed aggregate token");
                check(await Preserved(key) == prior, "HTTP Add does not change credentials status roles primary department or sessions");
                var id = value.GetProperty("accessId").GetInt64();
                var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityType == "user_branch_access" && a.EntityId == id.ToString(), ct);
                using var details = JsonDocument.Parse(audit.NewValues!);
                check(audit.ActorUserId == expectedActor && audit.BranchId == branches[1].BranchId && audit.Action == "CREATE"
                    && details.RootElement.GetProperty("userId").GetInt64() == users[key].UserId
                    && secrets.Where(s => !string.IsNullOrEmpty(s)).All(s => !audit.NewValues!.Contains(s)), "atomic audit has backend actor target branch before-after and no secret values");
            }
            using (var response = await Send(PathFor("target"), jars["branch"], body, tag)) await Success(response, "target", tag, before, users["branch"].UserId);
            using (var sessionResponse = await Send("/api/auth/session", targetJar, method: HttpMethod.Get)) check(sessionResponse.StatusCode == HttpStatusCode.OK, "target existing SQL session remains valid at next HTTP request");
            await Expect(PathFor("target"), jars["branch"], 412, "old-token retry after commit", tag);
            await Expect(PathFor("target"), jars["branch"], 409, "fresh-token duplicate", await Tag("target"));
            var allTag = await Tag("allTarget"); var allBefore = await Preserved("allTarget");
            using (var response = await Send(PathFor("allTarget"), jars["admin"], body, allTag)) await Success(response, "allTarget", allTag, allBefore, users["admin"].UserId);
            var raceTag = await Tag("race"); var raceBefore = await Preserved("race");
            var pair = await Task.WhenAll(Send(PathFor("race"), jars["admin"], body, raceTag), Send(PathFor("race"), jars["admin2"], body, raceTag));
            try
            {
                check(pair.Count(r => r.StatusCode == HttpStatusCode.Created) == 1 && pair.Count(r => r.StatusCode == HttpStatusCode.PreconditionFailed) == 1, "concurrent HTTPS Add yields one201 one412");
                await Success(pair.Single(r => r.StatusCode == HttpStatusCode.Created), "race", raceTag, raceBefore, users[pair[0].StatusCode == HttpStatusCode.Created ? "admin" : "admin2"].UserId);
            }
            finally { foreach (var response in pair) response.Dispose(); }
            var faultId = users["fault"].UserId; var faultTag = await Tag("fault"); var faultBefore = await Preserved("fault"); var faultName = prefix + "AuditFault";
            await db.Database.OpenConnectionAsync(ct);
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "ALTER TABLE dbo.audit_logs ADD CONSTRAINT " + faultName + " CHECK (entity_type<>'user_branch_access' OR JSON_VALUE(new_values,'$.userId')<>N'"
                    + faultId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "')";
                await command.ExecuteNonQueryAsync(ct);
            }
            try
            {
                await Expect(PathFor("fault"), jars["admin"], 500, "SQL audit fault denies Add", faultTag);
                check(await Tag("fault") == faultTag && await Preserved("fault") == faultBefore
                    && !await db.UserBranchAccesses.AnyAsync(a => a.UserId == faultId && a.BranchId == branches[1].BranchId, ct), "HTTP audit error rolls back membership and aggregate token atomically");
            }
            finally { await using var cleanup = db.Database.GetDbConnection().CreateCommand(); cleanup.CommandText = "ALTER TABLE dbo.audit_logs DROP CONSTRAINT " + faultName; await cleanup.ExecuteNonQueryAsync(CancellationToken.None); }
            process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None);
            var logs = await stdout + await stderr; check(secrets.Where(s => !string.IsNullOrEmpty(s)).All(s => !logs.Contains(s)), "actual Add API logs contain no password/hash/cookie/CSRF/certificate secret");
        }
        finally
        {
            if (process != null) { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); process.Dispose(); }
            if (certFile != null) File.Delete(certFile);
            var ids = users.Values.Select(u => u.UserId).Where(id => id > 0).ToArray();
            await db.UserSessions.Where(s => ids.Contains(s.UserId) && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, DateTime.UtcNow), CancellationToken.None);
            await db.UserRoles.Where(r => ids.Contains(r.UserId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.RolePermissions.Where(r => testRoles.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Roles.Where(r => testRoles.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
        }
    }
    private sealed class Jar
    {
        public Dictionary<string, string> Cookies { get; } = new(); public string? Csrf { get; set; }
        public void Read(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("Set-Cookie", out var values)) foreach (var value in values)
            { var pair = value.Split(';')[0]; var equals = pair.IndexOf('='); if (equals > 0) Cookies[pair[..equals]] = pair[(equals + 1)..]; }
        }
    }
}
