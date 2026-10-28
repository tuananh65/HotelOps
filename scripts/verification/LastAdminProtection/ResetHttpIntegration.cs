using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

internal static class ResetHttpIntegration
{
    internal static async Task Run(string connection, Action<bool, string> check, CancellationToken ct, bool createOrderOnly = false, bool writersOnly = false)
    {
        HotelOpsDbContext Open() => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
        await using var db = Open(); var prefix = "T42_" + Guid.NewGuid().ToString("N")[..8]; var now = DateTime.UtcNow;
        var users = new Dictionary<string, User>(); var roles = new List<long>(); var secret = SessionCredential.Generate(); var secrets = new List<string> { secret };
        var hash = new PasswordCredentialService(Options.Create(new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = PasswordCredentialService.BaselineIterationCount }));
        Process? process = null; string? certFile = null; Task<string>? stdout = null, stderr = null;
        try
        {
            foreach (var name in new[] { "manager", "manager2", "a", "b", "employee", "collider" })
            {
                var code = prefix + name; var user = new User { EmployeeCode = code, Username = code + "@example.invalid", Email = code + ".mail@example.invalid",
                    FullName = "Reset integration fixture", AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = now };
                user.PasswordHash = hash.HashPassword(user, secret); users.Add(name, user); db.Add(user);
            }
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); secrets.AddRange(users.Values.Select(u => u.PasswordHash!));
            var um = await db.Permissions.AsNoTracking().SingleAsync(p => p.PermissionCode == "USER_MANAGE", ct);
            var rm = await db.Permissions.AsNoTracking().SingleAsync(p => p.PermissionCode == "ROLE_MANAGE", ct);
            var manageRole = new Role { RoleCode = prefix + "manage", RoleName = "SYS_ADMIN", IsActive = true };
            var fullRole = new Role { RoleCode = prefix + "full", RoleName = "Non system capability", IsActive = true };
            db.AddRange(manageRole, fullRole); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); roles.AddRange([manageRole.RoleId, fullRole.RoleId]);
            foreach (var role in new[] { manageRole, fullRole }) db.Add(new RolePermission { RoleId = role.RoleId, PermissionId = um.PermissionId, DataScope = "ALL" });
            db.Add(new RolePermission { RoleId = fullRole.RoleId, PermissionId = rm.PermissionId, DataScope = "ALL" });
            foreach (var name in new[] { "manager", "manager2", "a", "b" }) db.Add(new UserRole { UserId = users[name].UserId,
                RoleId = name.StartsWith("manager", StringComparison.Ordinal) ? manageRole.RoleId : fullRole.RoleId, IsActive = true, AssignedAt = now });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
            using var rsa = RSA.Create(2048); var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback); request.CertificateExtensions.Add(san.Build());
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            var certPassword = SessionCredential.Generate(); secrets.Add(certPassword); certFile = Path.Combine(Path.GetTempPath(), "HotelOps-T42-" + Guid.NewGuid().ToString("N") + ".pfx");
            await File.WriteAllBytesAsync(certFile, certificate.Export(X509ContentType.Pfx, certPassword), ct);
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(root, "src/HotelOps.Api"), UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.Combine(root, "src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll"));
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; start.Environment["DOTNET_ENVIRONMENT"] = "Development";
            start.Environment["ASPNETCORE_URLS"] = "https://127.0.0.1:" + port; start.Environment["ConnectionStrings__HotelOpsDb"] = connection;
            start.Environment["Kestrel__Certificates__Default__Path"] = certFile; start.Environment["Kestrel__Certificates__Default__Password"] = certPassword;
            start.Environment["Logging__LogLevel__Default"] = "Information";
            process = Process.Start(start) ?? throw new InvalidOperationException("API launch failed"); stdout = process.StandardOutput.ReadToEndAsync(ct); stderr = process.StandardError.ReadToEndAsync(ct);
            using var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false,
                ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.GetCertHashString() == certificate.GetCertHashString() };
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(45) };
            async Task<HttpResponseMessage> Send(string path, Jar jar, object? body = null, string? tag = null, bool get = false, CancellationToken? token = null, bool patch = false)
            {
                using var message = new HttpRequestMessage(patch ? HttpMethod.Patch : get ? HttpMethod.Get : HttpMethod.Post, path);
                if (jar.Cookies.Count > 0) message.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(p => p.Key + "=" + p.Value)));
                if (jar.Csrf != null) message.Headers.Add(HotelOpsAntiforgery.HeaderName, jar.Csrf);
                if (tag != null) message.Headers.TryAddWithoutValidation("If-Match", tag);
                if (body != null) message.Content = JsonContent.Create(body);
                var response = await http.SendAsync(message, token ?? ct); jar.Read(response); return response;
            }
            var ready = false;
            for (var attempt = 0; attempt < 60 && !process.HasExited; attempt++)
            {
                try { using var response = await http.GetAsync("/api/auth/csrf", ct); if (response.StatusCode == HttpStatusCode.OK) { ready = true; break; } }
                catch (HttpRequestException) { } await Task.Delay(200, ct);
            }
            check(ready, "actual HTTPS API starts with integrated reset guard");
            async Task<Jar> Login(string name)
            {
                var jar = new Jar(); using (var csrf = await Send("/api/auth/csrf", jar, get: true))
                { using var value = JsonDocument.Parse(await csrf.Content.ReadAsStringAsync(ct)); jar.Csrf = value.RootElement.GetProperty("csrfToken").GetString(); }
                using var login = await Send("/api/auth/login", jar, new { username = users[name].Username, password = secret });
                check(login.StatusCode == HttpStatusCode.OK, "actual SQL-backed login " + name);
                using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync(ct)); jar.Csrf = json.RootElement.GetProperty("csrfToken").GetString();
                secrets.AddRange(jar.Cookies.Values); secrets.Add(jar.Csrf!); return jar;
            }
            var manager = await Login("manager"); var manager2 = await Login("manager2"); var employeeJar = await Login("employee");
            _ = await Login("a"); // An active target session must survive the last-admin refusal.
            async Task Reauth(Jar jar)
            {
                using var response = await Send("/api/auth/reauthenticate", jar, new { currentPassword = secret });
                check(response.StatusCode == HttpStatusCode.OK, "real password reauthentication before reset");
            }
            async Task<string> Tag(string name) => "\"" + Convert.ToHexString(await db.Users.AsNoTracking().Where(u => u.UserId == users[name].UserId).Select(u => u.RowVersion).SingleAsync(ct)) + "\"";
            Task<HttpResponseMessage> Reset(string name, Jar jar, string tag, CancellationToken? token = null)
                => Send("/api/users/" + users[name].UserId + "/reset-credential", jar, new { identityVerificationConfirmed = true }, tag, token: token);
            async Task<string> State(string name)
            {
                var id = users[name].UserId; var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == id, ct);
                var sessions = await db.UserSessions.AsNoTracking().Where(s => s.UserId == id).OrderBy(s => s.SessionId).ToArrayAsync(ct);
                var audit = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "users" && a.EntityId == id.ToString()).OrderBy(a => a.AuditId).ToArrayAsync(ct);
                // Explicit scalars: no EF navigation cycles, no disk/log serialization of secret state.
                return JsonSerializer.Serialize(new { user.PasswordHash, user.TemporaryPasswordExpiresAt, user.FailedLoginCount, user.LockedUntil,
                    user.AccountStatus, user.RowVersion, user.PrimaryBranchId, user.DepartmentId, sessions, audit });
            }
            async Task Baseline(params string[] names)
            {
                var ids = new[] { users["a"].UserId, users["b"].UserId };
                await db.UserRoles.Where(r => ids.Contains(r.UserId)).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
                foreach (var name in names) await db.UserRoles.Where(r => r.UserId == users[name].UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct);
                foreach (var name in new[] { "a", "b", "employee" }) await db.Users.Where(u => u.UserId == users[name].UserId)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, users[name].PasswordHash).SetProperty(u => u.TemporaryPasswordExpiresAt, (DateTime?)null)
                        .SetProperty(u => u.LockedUntil, (DateTime?)null), ct);
                await db.Users.Where(u => u.UserId == users["collider"].UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Username, users["collider"].Username)
                    .SetProperty(u => u.Email, users["collider"].Email), ct);
                await Reauth(manager);
            }
            async Task Denied(string name, Jar jar, string tag, int status, string label)
            {
                var before = await State(name); using var response = await Reset(name, jar, tag); var text = await response.Content.ReadAsStringAsync(ct);
                check((int)response.StatusCode == status && response.Headers.CacheControl?.NoStore == true, label + " HTTP" + (int)response.StatusCode + " no-store");
                check(await State(name) == before && secrets.All(s => !text.Contains(s)), label + " preserves credential token sessions audit and returns no secret");
            }
            async Task Success(string name, HttpResponseMessage response, string oldTag)
            {
                check(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true, "integrated reset returns200/no-store only after commit");
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); var credential = json.RootElement.GetProperty("temporaryCredential");
                var delivered = credential.GetProperty("password").GetString()!; secrets.Add(delivered);
                var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == users[name].UserId, ct);
                check(hash.VerifyPassword(user, user.PasswordHash, delivered) != PasswordVerificationResult.Failed
                    && user.TemporaryPasswordExpiresAt > DateTime.UtcNow.AddHours(23) && await Tag(name) != oldTag && response.Headers.ETag?.ToString() == await Tag(name), "HTTP credential and ETag match committed SQL rotation");
                check(!await db.UserSessions.AnyAsync(s => s.UserId == user.UserId && s.RevokedAt == null, ct), "integrated successful reset revokes every target session");
            }
            async Task Ddl(string sql, CancellationToken? token = null)
            {
                // Only generated ASCII GUID identifiers, fixed SQL and numeric fixture IDs are accepted here.
                if (db.Database.GetDbConnection().State != System.Data.ConnectionState.Open) await db.Database.OpenConnectionAsync(token ?? ct);
                await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = sql;
                await command.ExecuteNonQueryAsync(token ?? ct);
            }
            if (!createOrderOnly && !writersOnly)
            {
            await Baseline("a");
            await Denied("a", manager, await Tag("a"), 409, "last effective admin rejected despite actor role label SYS_ADMIN");
            await Baseline(); var employeeTag = await Tag("employee");
            using (var response = await Reset("employee", manager, employeeTag)) await Success("employee", response, employeeTag);
            using (var session = await Send("/api/auth/session", employeeJar, get: true)) check(session.StatusCode == HttpStatusCode.Unauthorized, "target cookie is revoked after real integrated reset");
            await Baseline("a", "b"); await db.Users.Where(u => u.UserId == users["b"].UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.TemporaryPasswordExpiresAt, now.AddHours(24)), ct);
            await Denied("a", manager, await Tag("a"), 409, "restricted temporary second admin cannot protect last regular admin");
            await Baseline("a", "b");
            await db.Users.Where(u => u.UserId == users["collider"].UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Username, users["b"].Email).SetProperty(u => u.Email, users["b"].Username), ct);
            await Denied("a", manager, await Tag("a"), 409, "ambiguous second admin login identifiers do not count");
            await Baseline("a", "b"); await db.Users.Where(u => u.UserId == users["b"].UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.LockedUntil, now.AddMinutes(30)), ct);
            var aTag = await Tag("a"); using (var response = await Reset("a", manager, aTag)) await Success("a", response, aTag);
            check(await db.Users.AnyAsync(u => u.UserId == users["b"].UserId && u.LockedUntil > now, ct), "finite locked survivor counts durably without clearing its lock");
            await Denied("a", manager, aTag, 412, "old-token retry after committed reset");
            await Baseline("a", "b"); await Reauth(manager2); var ta = await Tag("a"); var tb = await Tag("b");
            var pair = await Task.WhenAll(Reset("a", manager, ta), Reset("b", manager2, tb));
            try
            {
                check(pair.Count(r => r.StatusCode == HttpStatusCode.OK) == 1 && pair.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1, "two actual HTTP admin-removing resets yield one200 one409");
                var winner = pair[0].StatusCode == HttpStatusCode.OK ? "a" : "b";
                await Success(winner, pair.Single(r => r.StatusCode == HttpStatusCode.OK), winner == "a" ? ta : tb);
                check(await db.Users.CountAsync(u => (u.UserId == users["a"].UserId || u.UserId == users["b"].UserId) && u.TemporaryPasswordExpiresAt == null, ct) == 1,
                    "exactly one full-session eligible administrator survives concurrent HTTP resets");
            }
            finally { foreach (var response in pair) response.Dispose(); }
            await Baseline("a", "b");
            var stale = await Tag("a"); await db.Users.Where(u => u.UserId == users["a"].UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.UpdatedAt, DateTime.UtcNow), ct);
            await Denied("a", manager, stale, 412, "stale If-Match denied before mutation");
            // Real audit constraint fault, owned exclusively by this guarded trial run.
            await db.Database.OpenConnectionAsync(ct); var fault = prefix + "AuditFault";
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "ALTER TABLE dbo.audit_logs WITH NOCHECK ADD CONSTRAINT " + fault + " CHECK (entity_id<>N'" + users["a"].UserId + "' OR JSON_VALUE(new_values,'$.authenticationEvent')<>'CREDENTIAL_RESET')";
                await command.ExecuteNonQueryAsync(ct);
            }
            try { await Denied("a", manager, await Tag("a"), 500, "actual HTTP audit failure rolls back protected reset"); }
            finally { await Ddl("ALTER TABLE dbo.audit_logs DROP CONSTRAINT " + fault, CancellationToken.None); }
            // Hold global lock before the actual HTTP orchestration, without blocking middleware User touch.
            await using (var holder = Open())
            {
                await using var tx = await holder.Database.BeginTransactionAsync(ct); await new GlobalAdminSerialization(holder, TimeProvider.System).AcquireAsync(ct: ct);
                var owner = await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                var tag = await Tag("employee"); var state = await State("employee"); var pending = Reset("employee", manager, tag);
                check(await Waiting(owner, pending), "real HTTP reset waits on global lock before any protected mutation");
                using var response = await pending;
                check(response.StatusCode == HttpStatusCode.ServiceUnavailable && await State("employee") == state, "HTTP global lock timeout returns503 with complete unchanged target");
                await tx.RollbackAsync(ct);
                using var retry = await Reset("employee", manager, tag); await Success("employee", retry, tag);
            }
            await Baseline("a", "b");
            await using (var holder = Open())
            {
                await using var tx = await holder.Database.BeginTransactionAsync(ct); await new GlobalAdminSerialization(holder, TimeProvider.System).AcquireAsync(ct: ct);
                var owner = await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct); var state = await State("employee"); var tag = await Tag("employee");
                using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct); var pending = Reset("employee", manager, tag, cancel.Token);
                check(await Waiting(owner, pending), "cancelled HTTP request demonstrably reached blocked SQL guard"); cancel.Cancel();
                var aborted = false; try { using var ignored = await pending; } catch (OperationCanceledException) { aborted = true; }
                var cleared = false;
                for (var i = 0; i < 100; i++)
                {
                    if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.dm_exec_requests WHERE blocking_session_id={owner}").SingleAsync(ct) == 0) { cleared = true; break; }
                    await Task.Delay(50, ct);
                }
                check(cleared, "aborted HTTP SQL waiter disappears while global owner still holds lock");
                await tx.RollbackAsync(ct); await Task.Delay(200, ct);
                check(aborted && await State("employee") == state, "HTTP abort cancels waiting guard without credential/session/token changes");
                using var retry = await Reset("employee", manager, tag); await Success("employee", retry, tag);
            }
            await Baseline("a", "b"); await DeadlockHttp();
            await Baseline("a", "b"); await AuthorizationRaceHttp();
            await WriterCoverage();
            }
            if (writersOnly) await WriterCoverage();
            await Baseline("a", "b"); await CreateOrderProbe();
            process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None);
            var logs = await stdout + await stderr;
            check(secrets.All(value => !logs.Contains(value)), "integrated HTTP logs expose no password hash cookie CSRF or certificate secret");

            async Task<bool> Waiting(int owner, Task pending)
            {
                for (var i = 0; i < 100 && !pending.IsCompleted; i++)
                {
                    if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.dm_exec_requests WHERE blocking_session_id={owner} AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct) > 0) return true;
                    await Task.Delay(50, ct);
                }
                return false;
            }
            async Task WriterCoverage()
            {
                TemporaryCredentialService Credentials(HotelOpsDbContext context)
                    => new(context, hash, new SessionRevocationService(context), TimeProvider.System);
                await Baseline("a"); var before = await State("a");
                await using (var raw = Open())
                {
                    using var prepared = Credentials(raw).Prepare(users["a"].UserId);
                    using var result = await Credentials(raw).ResetAsync(prepared, users["manager"].UserId,
                        users["a"].RowVersion, "INTERNAL_PROCEDURE_CONFIRMATION", ct);
                    // Fixture rowversion advances during Baseline; explicitly exercise stale first.
                    check(result.Status == ConditionalCredentialResetStatus.Stale, "raw reset preserves stale rowversion contract under guard");
                    using var fresh = Credentials(raw).Prepare(users["a"].UserId);
                    using var denied = await Credentials(raw).ResetAsync(fresh, users["manager"].UserId,
                        Convert.FromHexString((await Tag("a"))[1..^1]), "INTERNAL_PROCEDURE_CONFIRMATION", ct);
                    check(denied.Status == ConditionalCredentialResetStatus.LastAdministrator && denied.Credential == null
                        && await State("a") == before, "raw reset refuses last admin and preserves credentials sessions version audit");
                }
                await using (var legacy = Open())
                {
                    using var prepared = Credentials(legacy).Prepare(users["a"].UserId); var refused = false;
                    try { using var ignored = await Credentials(legacy).ApplyAsync(prepared, users["manager"].UserId, CredentialProvisioningOperation.AdminReset, ct); }
                    catch (GlobalAdministrationInvariantException) { refused = true; }
                    check(refused && await State("a") == before, "legacy Apply refuses last admin with complete SQL rollback and no handoff");
                }
                await Baseline("a", "b");
                await using (var raw = Open())
                await using (var legacy = Open())
                {
                    async Task<bool> Raw()
                    {
                        using var prepared = Credentials(raw).Prepare(users["a"].UserId);
                        var version = Convert.FromHexString((await Tag("a"))[1..^1]);
                        using var result = await Credentials(raw).ResetAsync(prepared, users["manager"].UserId, version, "INTERNAL_PROCEDURE_CONFIRMATION", ct);
                        return result.Status == ConditionalCredentialResetStatus.Success;
                    }
                    async Task<bool> Legacy()
                    {
                        using var prepared = Credentials(legacy).Prepare(users["b"].UserId);
                        try { using var issued = await Credentials(legacy).ApplyAsync(prepared, users["manager"].UserId, CredentialProvisioningOperation.AdminReset, ct); return true; }
                        catch (GlobalAdministrationInvariantException) { return false; }
                    }
                    var results = await Task.WhenAll(Raw(), Legacy());
                    check(results.Count(r => r) == 1 && await db.Users.CountAsync(u => (u.UserId == users["a"].UserId || u.UserId == users["b"].UserId)
                        && u.TemporaryPasswordExpiresAt == null, ct) == 1, "concurrent raw and legacy reset preserve exactly one durable administrator");
                }
                await Baseline();
                await using (var raw = Open())
                await using (var legacy = Open())
                {
                    using var p1 = Credentials(raw).Prepare(users["employee"].UserId);
                    using var result = await Credentials(raw).ResetAsync(p1, users["manager"].UserId,
                        Convert.FromHexString((await Tag("employee"))[1..^1]), "INTERNAL_PROCEDURE_CONFIRMATION", ct);
                    using var p2 = Credentials(legacy).Prepare(users["employee"].UserId);
                    using var issued = await Credentials(legacy).ApplyAsync(p2, users["manager"].UserId, CredentialProvisioningOperation.AdminReset, ct);
                    check(result.Status == ConditionalCredentialResetStatus.Success, "raw and legacy allow nonadmin reset with zero baseline admins");
                }

                await Baseline("a"); var admin = await Login("a");
                // Leave only a.Email usable through a cross-column collision.
                await db.Users.Where(u => u.UserId == users["collider"].UserId)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.Email, users["a"].Username), ct);
                var aState = await State("a"); var employeeState = await State("employee");
                async Task<HttpResponseMessage> Profile(string name, string email, string? tag = null)
                    => await Send("/api/users/" + users[name].UserId, manager, new { email }, tag ?? await Tag(name), patch: true);
                using (var response = await Profile("a", users["employee"].Username))
                    check(response.StatusCode == HttpStatusCode.Conflict && (await response.Content.ReadAsStringAsync(ct)).Contains("LAST_DURABLE_ADMIN")
                        && await State("a") == aState, "HTTP profile refuses last-admin identifier loss and rolls back audit token credential sessions");
                var safeEmail = prefix + "safe@example.invalid";
                using (var response = await Profile("employee", safeEmail))
                    check(response.StatusCode == HttpStatusCode.OK, "HTTP nonadmin profile remains allowed with one durable admin");
                using (var response = await Profile("employee", users["a"].Email))
                    check(response.StatusCode == HttpStatusCode.Conflict, "HTTP profile preserves ordinary same-column email uniqueness");
                // Restore own fixtures, then make a.Username ambiguous for Create's remaining-path case.
                await db.Users.Where(u => u.UserId == users["collider"].UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Username, users["collider"].Username)
                    .SetProperty(u => u.Email, users["a"].Username), ct);
                var branch = await db.Branches.AsNoTracking().Where(b => b.IsActive).FirstAsync(ct);
                var department = await db.Departments.AsNoTracking().FirstAsync(d => d.BranchId == branch.BranchId && d.DepartmentCode == "HK", ct);
                object CreateBody(string code) => new { employeeCode = code, username = users["a"].Email, email = code + "@example.invalid",
                    fullName = "Writer coverage fixture", branchId = branch.BranchId, departmentId = department.DepartmentId, roleCode = "HK_STAFF" };
                var code = prefix + "Created"; var count = await db.Users.CountAsync(ct); var audits = await db.AuditLogs.CountAsync(ct);
                using (var response = await Send("/api/users", admin, CreateBody(code)))
                    check(response.StatusCode == HttpStatusCode.Conflict && (await response.Content.ReadAsStringAsync(ct)).Contains("LAST_DURABLE_ADMIN")
                        && await db.Users.CountAsync(ct) == count && await db.AuditLogs.CountAsync(ct) == audits,
                        "HTTP Create refuses last-admin cross-column collision and rolls back user credential grants memberships audit");
                // With another durable admin the existing ambiguity contract remains unchanged.
                await db.UserRoles.Where(r => r.UserId == users["b"].UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct);
                using (var response = await Send("/api/users", admin, CreateBody(code)))
                {
                    check(response.StatusCode == HttpStatusCode.Created, "HTTP Create allows identifier collision when another durable admin survives");
                    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                    secrets.Add(json.RootElement.GetProperty("temporaryCredential").GetProperty("password").GetString()!);
                    users.Add("created", await db.Users.AsNoTracking().SingleAsync(u => u.EmployeeCode == code, ct));
                }
                // Keep retained fixture harmless to later snapshots.
                await db.Users.Where(u => u.EmployeeCode == code).ExecuteUpdateAsync(s => s.SetProperty(u => u.Username, code), ct);
                await Baseline("a", "b");
                // Actual competing HTTP writers remove different eligible principals.
                await db.Users.Where(u => u.UserId == users["collider"].UserId)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.Email, users["a"].Username), ct);
                var raceCode = prefix + "RaceCreated";
                await using (var holder = Open())
                {
                    await using var held = await holder.Database.BeginTransactionAsync(ct);
                    await holder.Database.ExecuteSqlRawAsync("SELECT COUNT_BIG(*) FROM dbo.audit_logs WITH (TABLOCKX,HOLDLOCK)", ct);
                    var owner = await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                    var tag = await Tag("b");
                    var create = Send("/api/users", admin, CreateBody(raceCode));
                    check(await Waiting(owner, create), "actual Create holds protected namespace while its audit INSERT is paused");
                    var createSpid = await db.Database.SqlQuery<int>($"SELECT TOP(1) CONVERT(int,session_id) AS [Value] FROM sys.dm_exec_requests WHERE blocking_session_id={owner} AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct);
                    var reset = Reset("b", manager, tag); bool blocked;
                    try { blocked = await Waiting(createSpid, reset); }
                    finally { await held.RollbackAsync(CancellationToken.None); }
                    using var created = await create; using var rotated = await reset;
                    Console.WriteLine($"Owned mixed-writer race: blocked={blocked}, Create={(int)created.StatusCode}, Reset={(int)rotated.StatusCode}");
                    check(blocked && created.StatusCode == HttpStatusCode.Created && rotated.StatusCode == HttpStatusCode.Conflict,
                        "concurrent actual Create collision commits before waiting reset refuses last remaining admin loss");
                    if (created.StatusCode == HttpStatusCode.Created)
                    {
                        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync(ct));
                        secrets.Add(json.RootElement.GetProperty("temporaryCredential").GetProperty("password").GetString()!);
                        users.Add("createdRace", await db.Users.AsNoTracking().SingleAsync(u => u.EmployeeCode == raceCode, ct));
                        await db.Users.Where(u => u.EmployeeCode == raceCode).ExecuteUpdateAsync(s => s.SetProperty(u => u.Username, raceCode), ct);
                    }
                    else await Success("b", rotated, tag);
                }
                await Baseline("a", "b");
                foreach (var kind in new[] { "Profile", "Create" })
                {
                    var actor = kind == "Profile" ? "manager" : "a";
                    var jar = kind == "Profile" ? manager : await Login("a");
                    var newCode = prefix + kind + "Authority";
                    var profileTag = await Tag("employee");
                    Task<HttpResponseMessage> Mutation() => kind == "Profile"
                        ? Profile("employee", prefix + "authority@example.invalid", profileTag)
                        : Send("/api/users", jar, new { employeeCode = newCode, username = newCode, email = newCode + "@example.invalid",
                            fullName = "Authority fixture", branchId = branch.BranchId, departmentId = department.DepartmentId, roleCode = "HK_STAFF" });
                    var state = await State("employee"); var countBefore = await db.Users.CountAsync(ct);
                    await using (var holder = Open())
                    {
                        await using var held = await holder.Database.BeginTransactionAsync(ct);
                        await new GlobalAdminSerialization(holder, TimeProvider.System).AcquireAsync(ct: ct);
                        var owner = await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                        var pending = Mutation(); check(await Waiting(owner, pending), kind + " HTTP waits before authority evaluation");
                        await db.UserRoles.Where(r => r.UserId == users[actor].UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
                        await held.RollbackAsync(ct); using var response = await pending;
                        check(response.StatusCode == HttpStatusCode.Forbidden && await State("employee") == state
                            && await db.Users.CountAsync(ct) == countBefore, kind + " committed revocation wins before HTTP writer mutation");
                    }
                    await db.UserRoles.Where(r => r.UserId == users[actor].UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct);
                    await using (var holder = Open())
                    await using (var revoker = Open())
                    {
                        await using var held = await holder.Database.BeginTransactionAsync(ct);
                        await holder.Database.ExecuteSqlRawAsync("SELECT COUNT_BIG(*) FROM dbo.audit_logs WITH (TABLOCKX,HOLDLOCK)", ct);
                        var owner = await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                        var pending = Mutation(); check(await Waiting(owner, pending), kind + " actual HTTP reaches audit before final authorization check");
                        await revoker.Database.OpenConnectionAsync(ct);
                        var spid = await revoker.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                        var revoke = revoker.UserRoles.Where(r => r.UserId == users[actor].UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
                        var blocked = false;
                        try
                        {
                            for (var i = 0; i < 100 && !revoke.IsCompleted; i++)
                            {
                                blocked = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.dm_exec_requests WHERE session_id={spid} AND blocking_session_id<>0 AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct) == 1;
                                if (blocked) break;
                                await Task.Delay(50, ct);
                            }
                        }
                        finally { await held.RollbackAsync(CancellationToken.None); }
                        using var response = await pending; await revoke;
                        check(blocked && response.StatusCode == (kind == "Profile" ? HttpStatusCode.OK : HttpStatusCode.Created),
                            kind + " authority revoker waits until protected HTTP commit");
                        if (kind == "Create")
                        {
                            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                            secrets.Add(json.RootElement.GetProperty("temporaryCredential").GetProperty("password").GetString()!);
                            users.Add("createdAuthority", await db.Users.AsNoTracking().SingleAsync(u => u.EmployeeCode == newCode, ct));
                        }
                        using var next = await Mutation(); check(next.StatusCode == HttpStatusCode.Forbidden, kind + " next HTTP request observes revoked authority");
                    }
                    await db.UserRoles.Where(r => r.UserId == users[actor].UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct);
                }
                await Baseline("a", "b");
                // Login's existing U lock must finish before the guard acquires table X.
                await using var authWriter = Open(); await using var tx = await authWriter.Database.BeginTransactionAsync(ct);
                _ = await authWriter.Users.FromSqlInterpolated($"SELECT * FROM dbo.users WITH (UPDLOCK,HOLDLOCK) WHERE user_id={users["a"].UserId}")
                    .AsNoTracking().ToListAsync(ct);
                var authOwner = await authWriter.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                await using var reader = Open(); await using var readerTx = await reader.Database.BeginTransactionAsync(ct);
                var guard = await new GlobalAdminSerialization(reader, TimeProvider.System).AcquireAsync(ct: ct);
                var snapshot = guard.ReadSnapshotAsync(ct);
                check(await Waiting(authOwner, snapshot), "global snapshot waits before Users table X while authentication-style U is held");
                await authWriter.Users.Where(u => u.UserId == users["a"].UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, DateTime.UtcNow), ct);
                await tx.CommitAsync(ct); await snapshot; await readerTx.RollbackAsync(ct);
                check(true, "authentication-style U to X promotion completes without snapshot shared-lock inversion");
            }
            async Task DeadlockHttp()
            {
                await using var writer = Open(); await using var tx = await writer.Database.BeginTransactionAsync(ct);
                await writer.Database.ExecuteSqlRawAsync("SET DEADLOCK_PRIORITY HIGH", ct);
                await writer.Roles.Where(r => r.RoleId == fullRole.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.RoleName, "Owned deadlock fixture"), ct);
                var owner = await writer.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                var tag = await Tag("employee"); var state = await State("employee"); var pending = Reset("employee", manager, tag);
                check(await Waiting(owner, pending), "HTTP reset holds global lock and waits on deliberately inverted role writer");
                await new GlobalAdminSerialization(writer, TimeProvider.System).AcquireAsync(15000, ct);
                using var response = await pending; await tx.RollbackAsync(ct);
                check(response.StatusCode == HttpStatusCode.InternalServerError && await State("employee") == state, "real HTTP SQL deadlock victim returns sanitized500 and rolls back");
                using var retry = await Reset("employee", manager, tag); await Success("employee", retry, tag);
            }
            async Task AuthorizationRaceHttp()
            {
                // Revocation committed while the request waits for the first lock must win.
                await using (var holder = Open())
                {
                    await using var tx = await holder.Database.BeginTransactionAsync(ct);
                    await new GlobalAdminSerialization(holder, TimeProvider.System).AcquireAsync(ct: ct);
                    var owner = await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                    var before = await State("employee"); var tag = await Tag("employee");
                    var pending = Reset("employee", manager, tag);
                    check(await Waiting(owner, pending), "HTTP authorization race reaches global lock before authority recheck");
                    await db.UserRoles.Where(r => r.UserId == users["manager"].UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
                    await tx.RollbackAsync(ct);
                    using var response = await pending;
                    check(response.StatusCode == HttpStatusCode.Forbidden && await State("employee") == before,
                        "committed authority revocation wins over waiting HTTP reset without mutation");
                }
                await db.UserRoles.Where(r => r.UserId == users["manager"].UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct);
                await Reauth(manager);
                await using (var holder = Open())
                await using (var revoker = Open())
                {
                    await using var tx = await holder.Database.BeginTransactionAsync(ct);
                    await holder.Database.ExecuteSqlRawAsync("SELECT COUNT_BIG(*) FROM dbo.audit_logs WITH (TABLOCKX,HOLDLOCK)", ct);
                    var owner = await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                    var tag = await Tag("employee"); var pending = Reset("employee", manager, tag);
                    check(await Waiting(owner, pending), "HTTP protected reset reaches real audit INSERT before revocation");
                    await revoker.Database.OpenConnectionAsync(ct);
                    var revokeSpid = await revoker.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                    var revoke = revoker.UserRoles.Where(r => r.UserId == users["manager"].UserId)
                        .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
                    var blocked = false;
                    try
                    {
                        for (var i = 0; i < 100 && !revoke.IsCompleted; i++)
                        {
                            blocked = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.dm_exec_requests WHERE session_id={revokeSpid} AND blocking_session_id<>0 AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct) == 1;
                            if (blocked) break;
                            await Task.Delay(50, ct);
                        }
                    }
                    finally { await tx.RollbackAsync(CancellationToken.None); }
                    using var response = await pending; await revoke;
                    check(blocked, "concurrent authority revoker blocks on protected HTTP reset retained SQL locks");
                    await Success("employee", response, tag);
                    await Denied("employee", manager, await Tag("employee"), 403, "subsequent HTTP reset observes committed authority revocation");
                }
                await db.UserRoles.Where(r => r.UserId == users["manager"].UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct);
            }
            async Task CreateOrderProbe()
            {
                var branch = await db.Branches.AsNoTracking().Where(b => b.IsActive).FirstAsync(ct);
                var department = await db.Departments.AsNoTracking().FirstAsync(d => d.BranchId == branch.BranchId && d.DepartmentCode == "HK", ct);
                var role = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "HK_STAFF", ct);
                await using var creator = Open(); await using var createTx = await creator.Database.BeginTransactionAsync(ct);
                _ = await creator.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH (UPDLOCK,HOLDLOCK) WHERE branch_id={branch.BranchId}").AsNoTracking().ToListAsync(ct);
                _ = await creator.Departments.FromSqlInterpolated($"SELECT * FROM dbo.departments WITH (UPDLOCK,HOLDLOCK) WHERE department_id={department.DepartmentId}").AsNoTracking().ToListAsync(ct);
                _ = await creator.Roles.FromSqlInterpolated($"SELECT * FROM dbo.roles WITH (UPDLOCK,HOLDLOCK) WHERE role_id={role.RoleId}").AsNoTracking().ToListAsync(ct);
                var code = prefix + "CreateProbe";
                await using var holder = Open(); await using var holdTx = await holder.Database.BeginTransactionAsync(ct);
                await using (var command = holder.Database.GetDbConnection().CreateCommand())
                {
                    // Pause the real audit INSERT without adding a trigger: EF's OUTPUT INSERTED
                    // is incompatible with enabled triggers on the target table (SQL error 334).
                    command.Transaction = holdTx.GetDbTransaction();
                    command.CommandText = "SELECT COUNT_BIG(*) FROM dbo.audit_logs WITH (TABLOCKX,HOLDLOCK)";
                    await command.ExecuteScalarAsync(ct);
                }
                try
                {
                    var owner = await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                    var tag = await Tag("employee"); var pending = Reset("employee", manager, tag);
                    var paused = await Waiting(owner, pending);
                    if (!paused)
                    {
                        var waits = await db.Database.SqlQueryRaw<WaitRow>("SELECT CONVERT(int,session_id) AS SessionId,CONVERT(int,blocking_session_id) AS BlockingId,wait_type AS WaitType FROM sys.dm_exec_requests WHERE blocking_session_id<>0").ToArrayAsync(ct);
                        Console.WriteLine("Owned Create-order diagnostic waits: " + JsonSerializer.Serialize(waits));
                        if (pending.IsCompleted) { using var failed = await pending; Console.WriteLine("Owned Create-order diagnostic HTTP status: " + (int)failed.StatusCode); }
                    }
                    check(paused, "HTTP snapshot passes Create-style Branch Department Role UPDLOCK without inversion");
                    creator.Add(new User { EmployeeCode = code, Username = code, Email = code + "@example.invalid", FullName = "Owned Create order probe", AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = now });
                    var creatorId = await creator.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                    var insert = creator.SaveChangesAsync(ct); await Task.Delay(100, ct);
                    check(!insert.IsCompleted && await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.dm_exec_requests WHERE session_id={creatorId} AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct) == 1,
                        "Create-style new User INSERT waits while protected HTTP reset retains User namespace");
                    await holdTx.RollbackAsync(ct); using var response = await pending; await insert; await createTx.RollbackAsync(ct);
                    await Success("employee", response, tag);
                }
                finally
                {
                    if (holder.Database.CurrentTransaction != null) await holdTx.RollbackAsync(CancellationToken.None);
                }
            }
        }
        finally
        {
            if (process != null) { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); process.Dispose(); }
            if (certFile != null) File.Delete(certFile);
            db.ChangeTracker.Clear(); var ids = users.Values.Select(u => u.UserId).ToArray();
            await db.UserSessions.Where(s => ids.Contains(s.UserId) && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, DateTime.UtcNow), CancellationToken.None);
            await db.UserRoles.Where(r => ids.Contains(r.UserId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.RolePermissions.Where(r => roles.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Roles.Where(r => roles.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
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
    private sealed class WaitRow
    {
        public int SessionId { get; set; }
        public int BlockingId { get; set; }
        public string? WaitType { get; set; }
    }
}
