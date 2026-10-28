using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class AssignmentHttpSuite
{
    internal static async Task Run(string connection, Action<bool, string> check, CancellationToken ct)
    {
        HotelOpsDbContext Open() => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
        await using var db = Open();
        var users = new List<long>(); var customRoles = new List<long>();
        var prefix = "T44_" + Guid.NewGuid().ToString("N")[..8]; var sequence = 0;
        var secret = SessionCredential.Generate(); var secrets = new List<string> { secret };
        var passwords = new PasswordCredentialService(Options.Create(new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = PasswordCredentialService.BaselineIterationCount }));
        Process? process = null; string? certFile = null; Task<string>? stdout = null, stderr = null;
        var branches = await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId).Take(2).ToArrayAsync(ct);
        if (branches.Length != 2) throw new InvalidOperationException("Two canonical branches required");
        var roles = await db.Roles.AsNoTracking().ToDictionaryAsync(r => r.RoleCode, ct);
        var permissions = await db.Permissions.AsNoTracking().ToDictionaryAsync(p => p.PermissionCode, ct);
        async Task<User> Person(string dept = "HK", bool membership = true, int branchIndex = 0)
        {
            var code = prefix + (++sequence); var branch = branches[branchIndex];
            var department = await db.Departments.AsNoTracking().SingleAsync(d => d.BranchId == branch.BranchId && d.DepartmentCode == dept && d.IsActive, ct);
            var user = new User { EmployeeCode = code, Username = code + "@example.invalid", Email = code + ".mail@example.invalid",
                FullName = "Owned role assignment fixture", AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = DateTime.UtcNow,
                PrimaryBranchId = branch.BranchId, DepartmentId = department.DepartmentId };
            user.PasswordHash = passwords.HashPassword(user, secret); secrets.Add(user.PasswordHash);
            db.Add(user); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); users.Add(user.UserId);
            if (membership) { db.Add(new UserBranchAccess { UserId = user.UserId, BranchId = branch.BranchId, IsPrimary = true, AccessLevel = "STANDARD", GrantedAt = DateTime.UtcNow });
                await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); }
            return user;
        }
        async Task<Role> Authority(string scope, bool manage = true, bool roleManage = true)
        {
            var role = new Role { RoleCode = prefix + "R" + (++sequence), RoleName = "Owned non-system authority", IsActive = true };
            db.Add(role); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); customRoles.Add(role.RoleId);
            if (manage) db.Add(new RolePermission { RoleId = role.RoleId, PermissionId = permissions["USER_MANAGE"].PermissionId, DataScope = scope });
            if (roleManage) db.Add(new RolePermission { RoleId = role.RoleId, PermissionId = permissions["ROLE_MANAGE"].PermissionId, DataScope = scope });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); return role;
        }
        async Task Bind(User user, long role, long? branch = null, bool active = true, DateTime? expiry = null, DateTime? assigned = null)
        {
            db.Add(new UserRole { UserId = user.UserId, RoleId = role, BranchId = branch, IsActive = active,
                AssignedAt = assigned ?? DateTime.UtcNow, ExpiresAt = expiry }); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        }
        string Json(object value) => JsonSerializer.Serialize(value);
        async Task<string> Tag(User user) => "\"" + Convert.ToHexString(await db.Users.AsNoTracking().Where(u => u.UserId == user.UserId).Select(u => u.RowVersion).SingleAsync(ct)) + "\"";
        async Task<string> State(User user, bool includeMutation = true)
        {
            var u = await db.Users.AsNoTracking().Where(u => u.UserId == user.UserId).Select(u => new { u.PasswordHash, u.FailedLoginCount, u.LockedUntil,
                u.TemporaryPasswordExpiresAt, u.AccountStatus, u.EmploymentStatus, u.PrimaryBranchId, u.DepartmentId, u.FullName, u.Email, u.Username,
                u.Phone, u.CreatedAt, u.LastLoginAt, u.DeletedAt }).SingleAsync(ct);
            return Json(new { user = u, sessions = await db.UserSessions.AsNoTracking().Where(s => s.UserId == user.UserId).OrderBy(s => s.SessionId).ToArrayAsync(ct),
                mutation = includeMutation ? Json(new {
                    token = await Tag(user), updated = await db.Users.Where(u => u.UserId == user.UserId).Select(u => u.UpdatedAt).SingleAsync(ct),
                    assignments = await db.UserRoles.AsNoTracking().Where(r => r.UserId == user.UserId).OrderBy(r => r.UserRoleId).ToArrayAsync(ct),
                    audit = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "user_roles" && a.NewValues != null
                        && a.NewValues.Contains("\"UserId\":" + user.UserId + ",")).OrderBy(a => a.AuditId).ToArrayAsync(ct) }) : null });
        }
        async Task<int> Spid(HotelOpsDbContext context) => await context.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
        async Task<bool> Wait(int owner, Task pending)
        {
            for (var i = 0; i < 100 && !pending.IsCompleted; i++)
            {
                if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.dm_exec_requests WHERE blocking_session_id={owner} AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct) > 0) return true;
                await Task.Delay(50, ct);
            }
            return false;
        }
        async Task Ddl(string sql)
        {
            await db.Database.OpenConnectionAsync(CancellationToken.None);
            await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = sql;
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
        try
        {
            var actor = await Person(); await Bind(actor, roles["SYS_ADMIN"].RoleId);
            var delegated = await Person(); var authority = await Authority("ALL"); await Bind(delegated, authority.RoleId);
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
            using var rsa = RSA.Create(2048); var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback); request.CertificateExtensions.Add(san.Build());
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            var certPassword = SessionCredential.Generate(); secrets.Add(certPassword);
            certFile = Path.Combine(Path.GetTempPath(), "HotelOps-T44-" + Guid.NewGuid().ToString("N") + ".pfx");
            await File.WriteAllBytesAsync(certFile, certificate.Export(X509ContentType.Pfx, certPassword), ct);
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(root, "src/HotelOps.Api"), UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.Combine(root, "src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll"));
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; start.Environment["DOTNET_ENVIRONMENT"] = "Development";
            start.Environment["ASPNETCORE_URLS"] = "https://127.0.0.1:" + port; start.Environment["ConnectionStrings__HotelOpsDb"] = connection;
            start.Environment["Kestrel__Certificates__Default__Path"] = certFile; start.Environment["Kestrel__Certificates__Default__Password"] = certPassword;
            start.Environment["Logging__LogLevel__Default"] = "Information";
            process = Process.Start(start) ?? throw new InvalidOperationException("API launch failed");
            stdout = process.StandardOutput.ReadToEndAsync(ct); stderr = process.StandardError.ReadToEndAsync(ct);
            using var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false,
                ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.GetCertHashString() == certificate.GetCertHashString() };
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(45) };
            async Task<HttpResponseMessage> Send(string path, Jar jar, string? json = null, string? tag = null, bool csrf = true, string method = "POST", CancellationToken? token = null)
            {
                using var message = new HttpRequestMessage(new HttpMethod(method), path);
                if (jar.Cookies.Count > 0) message.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(p => p.Key + "=" + p.Value)));
                if (csrf && jar.Csrf != null) message.Headers.Add(HotelOpsAntiforgery.HeaderName, jar.Csrf);
                if (tag != null) message.Headers.TryAddWithoutValidation("If-Match", tag);
                if (json != null) message.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await http.SendAsync(message, token ?? ct); jar.Read(response); return response;
            }
            var ready = false;
            for (var i = 0; i < 60 && !process.HasExited; i++)
            {
                try { using var r = await http.GetAsync("/api/auth/csrf", ct); if (r.StatusCode == HttpStatusCode.OK) { ready = true; break; } }
                catch (HttpRequestException) { } await Task.Delay(200, ct);
            }
            check(ready, "actual HTTPS API starts with Assign Role DI");
            async Task<Jar> Bootstrap()
            {
                var jar = new Jar(); using var r = await Send("/api/auth/csrf", jar, method: "GET");
                using var json = JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct)); jar.Csrf = json.RootElement.GetProperty("csrfToken").GetString(); return jar;
            }
            async Task<Jar> Login(User user)
            {
                var jar = await Bootstrap(); using var r = await Send("/api/auth/login", jar, Json(new { username = user.Username, password = secret }));
                check(r.StatusCode == HttpStatusCode.OK, "real SQL-backed login for owned actor/target");
                using var json = JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct)); jar.Csrf = json.RootElement.GetProperty("csrfToken").GetString();
                secrets.AddRange(jar.Cookies.Values); secrets.Add(jar.Csrf!); return jar;
            }
            var admin = await Login(actor); var other = await Login(delegated);
            string Body(string role = "HK_STAFF", long? branch = null) => Json(new { roleCode = role, branchId = branch ?? branches[0].BranchId });
            Task<HttpResponseMessage> Assign(User target, Jar jar, string tag, string? body = null, CancellationToken? token = null)
                => Send("/api/users/" + target.UserId + "/roles", jar, body ?? Body(), tag, token: token);
            async Task Denied(User target, Jar jar, int status, string label, string? body = null, string? tag = null)
            {
                var before = await State(target); var etag = tag ?? await Tag(target);
                using var response = await Assign(target, jar, etag, body);
                var text = await response.Content.ReadAsStringAsync(ct);
                check((int)response.StatusCode == status && response.Headers.CacheControl?.NoStore == true, label + " HTTP" + status + " no-store");
                check(await State(target) == before && secrets.All(s => !text.Contains(s)), label + " preserves role version credentials sessions and audit without secrets");
            }
            async Task Success(User target, HttpResponseMessage response, string oldTag, string roleCode = "HK_STAFF", long? expectedActor = null, string? preserved = null)
            {
                check(response.StatusCode == HttpStatusCode.Created && response.Headers.CacheControl?.NoStore == true, "Assign returns201/no-store after commit");
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); var value = json.RootElement;
                check(value.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "id", "userId", "roleCode", "branchId", "assignedBy", "assignedAtUtc", "isActive", "expiresAtUtc" }.Order()), "assignment response DTO exact whitelist");
                var row = await db.UserRoles.AsNoTracking().SingleAsync(r => r.UserId == target.UserId, ct);
                check(row.UserRoleId == value.GetProperty("id").GetInt64() && row.RoleId == roles[roleCode].RoleId
                    && row.AssignedBy == (expectedActor ?? actor.UserId) && row.BranchId == target.PrimaryBranchId && row.IsActive && row.ExpiresAt == null
                    && row.AssignedAt <= DateTime.UtcNow && row.AssignedAt > DateTime.UtcNow.AddMinutes(-2), "committed SQL assignment has approved role binding server actor time and flags");
                check(await Tag(target) != oldTag && response.Headers.ETag?.ToString() == await Tag(target), "assignment advances aggregate rowversion and returns committed ETag");
                var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityType == "user_roles" && a.EntityId == row.UserRoleId.ToString(), ct);
                check(audit.Action == "CREATE" && audit.ActorUserId == (expectedActor ?? actor.UserId) && audit.BranchId == target.PrimaryBranchId
                    && audit.NewValues != null && audit.NewValues.Contains(roleCode) && secrets.All(s => !audit.NewValues.Contains(s)), "CREATE audit is atomic server-attributed and contains no credential/session material");
                if (preserved != null) check(await State(target, false) == preserved, "Assign preserves credential status primary Department and every target session");
            }

            // All approved envelopes must work through the actual API, with live SQL sessions.
            foreach (var pair in new[] { ("HK_STAFF", "HK"), ("HK_SUPERVISOR", "HK"), ("MAINTENANCE_STAFF", "MT"), ("MAINTENANCE_LEAD", "MT"), ("STOREKEEPER", "WH") })
            {
                var target = await Person(pair.Item2); var ticket = await Login(target); var tag = await Tag(target); var state = await State(target, false);
                using var response = await Assign(target, admin, tag, Body(pair.Item1)); await Success(target, response, tag, pair.Item1, preserved: state);
                using var session = await Send("/api/auth/session", ticket, method: "GET"); check(session.StatusCode == HttpStatusCode.OK, "target pre-existing session remains valid after assignment");
                await Denied(target, admin, 409, "exact tuple cannot regrant", Body(pair.Item1));
                await Denied(target, admin, 412, "old If-Match rejected after committed assignment", Body(pair.Item1), tag);
            }
            var customTarget = await Person(); var customTag = await Tag(customTarget);
            using (var response = await Assign(customTarget, other, customTag)) await Success(customTarget, response, customTag, expectedActor: delegated.UserId);
            var deniedTarget = await Person();
            foreach (var code in new[] { "SYS_ADMIN", "OWNER", "OPERATIONS_MANAGER", "DEPT_MANAGER", "RECEPTION", "HK_INSPECTOR", "STAFF", authority.RoleCode, "UNKNOWN", "hk_staff" })
                await Denied(deniedTarget, admin, 403, "nondelegable role " + code, Body(code));
            foreach (var item in new[] { ("ALL", true, false, false), ("ALL", false, true, false), ("ALL", true, true, true), ("BRANCH", true, true, true), ("OWN", true, true, true), ("DEPARTMENT", true, true, true) })
            {
                var limited = await Person(); var role = await Authority(item.Item1, item.Item2, item.Item3);
                await Bind(limited, role.RoleId, item.Item4 ? branches[0].BranchId : null); var jar = await Login(limited);
                await Denied(deniedTarget, jar, 403, "insufficient/scoped actor authority " + item.Item1 + item.Item2 + item.Item3 + item.Item4);
            }
            using (var self = await Assign(actor, admin, await Tag(actor))) check(self.StatusCode == HttpStatusCode.Forbidden, "self-role assignment forbidden");
            var restricted = await Person(); await Bind(restricted, roles["SYS_ADMIN"].RoleId);
            await db.Users.Where(u => u.UserId == restricted.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.TemporaryPasswordExpiresAt, DateTime.UtcNow.AddHours(1)), ct);
            await Denied(deniedTarget, await Login(restricted), 403, "restricted session cannot delegate");
            await Denied(deniedTarget, await Bootstrap(), 401, "anonymous with valid CSRF cannot assign");
            using (var noCsrf = await Send("/api/users/" + deniedTarget.UserId + "/roles", admin, Body(), await Tag(deniedTarget), csrf: false))
                check(noCsrf.StatusCode == HttpStatusCode.BadRequest, "existing CSRF middleware protects assignment");
            using (var missingTag = await Send("/api/users/" + deniedTarget.UserId + "/roles", admin, Body())) check((int)missingTag.StatusCode == 428, "missing If-Match returns428");
            foreach (var tag in new[] { "*", "W/\"0000000000000000\"", "\"not-a-version\"", "\"0000000000000000\",\"0000000000000001\"" })
                await Denied(deniedTarget, admin, 400, "invalid strong If-Match", tag: tag);
            foreach (var body in new[] { "{}", "[]", "null", "{\"roleCode\":\"HK_STAFF\",\"branchId\":null}",
                "{\"roleCode\":\"HK_STAFF\",\"branchId\":0}", "{\"roleCode\":\"HK_STAFF\",\"branchId\":1.5}",
                "{\"roleCode\":\"HK_STAFF\",\"roleCode\":\"HK_STAFF\",\"branchId\":1}",
                Json(new { roleCode = "HK_STAFF", branchId = branches[0].BranchId, assignedBy = actor.UserId }),
                Json(new { roleCode = "HK_STAFF", branchId = branches[0].BranchId, expiresAt = DateTime.UtcNow.AddYears(1) }) })
                await Denied(deniedTarget, admin, 400, "strict JSON denies invalid duplicate or extra field", body);
            await Denied(deniedTarget, admin, 409, "secondary Branch cannot be selected", Body(branch: branches[1].BranchId));
            await Denied(await Person("MT"), admin, 409, "Department/role mismatch");
            var missingMember = await Person(membership: false); await Denied(missingMember, admin, 409, "missing primary membership");
            foreach (var future in new[] { false, true })
            {
                var target = await Person();
                if (future) await db.UserBranchAccesses.Where(a => a.UserId == target.UserId).ExecuteUpdateAsync(s => s.SetProperty(a => a.GrantedAt, DateTime.UtcNow.AddHours(1)), ct);
                else await db.UserBranchAccesses.Where(a => a.UserId == target.UserId).ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, DateTime.UtcNow), ct);
                await Denied(target, admin, 409, "revoked or future membership");
            }
            foreach (var kind in new[] { "inactive", "deleted" })
            {
                var target = await Person();
                if (kind == "inactive") await db.Users.Where(u => u.UserId == target.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "INACTIVE"), ct);
                else await db.Users.Where(u => u.UserId == target.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.DeletedAt, DateTime.UtcNow), ct);
                await Denied(target, admin, kind == "inactive" ? 409 : 404, "target " + kind);
            }
            foreach (var kind in new[] { "branch", "department", "role", "extra-grant" })
            {
                var target = await Person();
                try
                {
                    if (kind == "branch") await db.Branches.Where(b => b.BranchId == branches[0].BranchId).ExecuteUpdateAsync(s => s.SetProperty(b => b.IsActive, false), ct);
                    else if (kind == "department") await db.Departments.Where(d => d.DepartmentId == target.DepartmentId).ExecuteUpdateAsync(s => s.SetProperty(d => d.IsActive, false), ct);
                    else if (kind == "role") await db.Roles.Where(r => r.RoleId == roles["HK_STAFF"].RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
                    else { db.Add(new RolePermission { RoleId = roles["HK_STAFF"].RoleId, PermissionId = permissions["USER_MANAGE"].PermissionId, DataScope = "ALL" });
                        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); }
                    await Denied(target, admin, kind is "branch" or "department" ? 409 : 403, "inactive master or extra administrative envelope " + kind);
                }
                finally
                {
                    db.ChangeTracker.Clear();
                    if (kind == "branch") await db.Branches.Where(b => b.BranchId == branches[0].BranchId).ExecuteUpdateAsync(s => s.SetProperty(b => b.IsActive, true), CancellationToken.None);
                    else if (kind == "department") await db.Departments.Where(d => d.DepartmentId == target.DepartmentId).ExecuteUpdateAsync(s => s.SetProperty(d => d.IsActive, true), CancellationToken.None);
                    else if (kind == "role") await db.Roles.Where(r => r.RoleId == roles["HK_STAFF"].RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), CancellationToken.None);
                    else await db.RolePermissions.Where(g => g.RoleId == roles["HK_STAFF"].RoleId && g.PermissionId == permissions["USER_MANAGE"].PermissionId).ExecuteDeleteAsync(CancellationToken.None);
                }
            }
            foreach (var kind in new[] { "active", "inactive", "expired", "future", "unbound", "OM" })
            {
                var target = await Person(); await Bind(target, roles[kind == "OM" ? "OPERATIONS_MANAGER" : "HK_STAFF"].RoleId,
                    kind == "unbound" ? null : branches[0].BranchId, kind != "inactive", kind == "expired" ? DateTime.UtcNow.AddHours(-1) : null,
                    kind == "future" ? DateTime.UtcNow.AddHours(1) : null);
                await Denied(target, admin, 409, "existing role path " + kind);
                if (kind == "active") await Denied(target, admin, 409, "distinct second role combination forbidden", Body("HK_SUPERVISOR"));
            }
            var concurrent = await Person(); var concurrentTag = await Tag(concurrent);
            var pairResults = await Task.WhenAll(Assign(concurrent, admin, concurrentTag), Assign(concurrent, other, concurrentTag));
            try { check(pairResults.Count(r => r.StatusCode == HttpStatusCode.Created) == 1 && pairResults.Count(r => r.StatusCode == HttpStatusCode.PreconditionFailed) == 1,
                "concurrent actual Assign requests produce exactly one201 and one412"); }
            finally { foreach (var r in pairResults) r.Dispose(); }

            await AddRace(assignFirst: true); await AddRace(assignFirst: false);
            await WriterFirst("authority"); await WriterFirst("permission"); await WriterFirst("role"); await WriterFirst("grant");
            await AssignFirst("authority"); await AssignFirst("permission"); await AssignFirst("role"); await AssignFirst("grant");
            await ExpiringAuthority(); await ExpiringAuthority(lastAdministrator: true);
            await Failures();
            process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None);
            var logs = await stdout + await stderr;
            check(secrets.All(value => !logs.Contains(value)), "actual assignment API logs contain no password hash cookie CSRF or certificate secret");

            async Task AddRace(bool assignFirst)
            {
                var target = await Person(); var tag = await Tag(target);
                await using var holder = Open(); await using var tx = await holder.Database.BeginTransactionAsync(ct);
                await holder.Database.ExecuteSqlRawAsync("SELECT COUNT_BIG(*) FROM dbo.audit_logs WITH (TABLOCKX,HOLDLOCK)", ct);
                var owner = await Spid(holder);
                Task<HttpResponseMessage> Add() => Send("/api/users/" + target.UserId + "/branches", admin, Json(new { branchId = branches[1].BranchId }), tag);
                var first = assignFirst ? Assign(target, other, tag) : Add();
                check(await Wait(owner, first), "first actual Assign/Add request pauses at SQL audit");
                var firstSpid = await db.Database.SqlQuery<int>($"SELECT TOP(1) CONVERT(int,session_id) AS [Value] FROM sys.dm_exec_requests WHERE blocking_session_id={owner} AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct);
                var second = assignFirst ? Add() : Assign(target, other, tag);
                bool blocked; try { blocked = await Wait(firstSpid, second); } finally { await tx.RollbackAsync(CancellationToken.None); }
                using var a = await first; using var b = await second;
                check(blocked && a.StatusCode == HttpStatusCode.Created && b.StatusCode == HttpStatusCode.PreconditionFailed,
                    (assignFirst ? "Assign-first" : "Add-first") + " actual SQL blocking preserves aggregate token with one201 one412");
            }
            async Task WriterFirst(string kind)
            {
                var target = await Person(); var state = await State(target); var tag = await Tag(target);
                await using var holder = Open(); await using var tx = await holder.Database.BeginTransactionAsync(ct);
                await new GlobalAdminSerialization(holder, TimeProvider.System).AcquireAsync(ct: ct); var owner = await Spid(holder);
                var pending = Assign(target, other, tag);
                try
                {
                    check(await Wait(owner, pending), kind + " writer-first request reaches SQL global lock");
                    if (kind == "authority") await db.UserRoles.Where(r => r.UserId == delegated.UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
                    else if (kind == "permission") await db.RolePermissions.Where(g => g.RoleId == authority.RoleId && g.PermissionId == permissions["ROLE_MANAGE"].PermissionId)
                        .ExecuteUpdateAsync(s => s.SetProperty(g => g.DataScope, "OWN"), ct);
                    else if (kind == "role") await db.Roles.Where(r => r.RoleId == roles["HK_STAFF"].RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsSystemRole, true), ct);
                    else await db.RolePermissions.Where(g => g.RoleId == roles["HK_STAFF"].RoleId && g.PermissionId == permissions["ROOM_VIEW"].PermissionId)
                        .ExecuteUpdateAsync(s => s.SetProperty(g => g.DataScope, "OWN"), ct);
                    await tx.RollbackAsync(ct); using var response = await pending;
                    check(response.StatusCode == HttpStatusCode.Forbidden && await State(target) == state, kind + " committed drift/revocation wins before mutation with unchanged target");
                }
                finally { await Restore(kind); }
            }
            async Task Restore(string kind)
            {
                if (kind == "authority") await db.UserRoles.Where(r => r.UserId == delegated.UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), CancellationToken.None);
                else if (kind == "permission") await db.RolePermissions.Where(g => g.RoleId == authority.RoleId && g.PermissionId == permissions["ROLE_MANAGE"].PermissionId)
                    .ExecuteUpdateAsync(s => s.SetProperty(g => g.DataScope, "ALL"), CancellationToken.None);
                else if (kind == "role") await db.Roles.Where(r => r.RoleId == roles["HK_STAFF"].RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsSystemRole, false), CancellationToken.None);
                else await db.RolePermissions.Where(g => g.RoleId == roles["HK_STAFF"].RoleId && g.PermissionId == permissions["ROOM_VIEW"].PermissionId)
                    .ExecuteUpdateAsync(s => s.SetProperty(g => g.DataScope, "BRANCH"), CancellationToken.None);
            }
            async Task AssignFirst(string kind)
            {
                var target = await Person(); var tag = await Tag(target);
                await using var holder = Open(); await using var tx = await holder.Database.BeginTransactionAsync(ct);
                await holder.Database.ExecuteSqlRawAsync("SELECT COUNT_BIG(*) FROM dbo.audit_logs WITH (TABLOCKX,HOLDLOCK)", ct);
                var owner = await Spid(holder); var pending = Assign(target, other, tag);
                check(await Wait(owner, pending), kind + " assign-first HTTP reaches audit with retained grant locks");
                var assignSpid = await db.Database.SqlQuery<int>($"SELECT TOP(1) CONVERT(int,session_id) AS [Value] FROM sys.dm_exec_requests WHERE blocking_session_id={owner} AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct);
                await using var writer = Open(); await writer.Database.OpenConnectionAsync(ct); var writerSpid = await Spid(writer);
                Task<int> mutation = kind switch {
                    "authority" => writer.UserRoles.Where(r => r.UserId == delegated.UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct),
                    "permission" => writer.RolePermissions.Where(g => g.RoleId == authority.RoleId && g.PermissionId == permissions["ROLE_MANAGE"].PermissionId)
                        .ExecuteUpdateAsync(s => s.SetProperty(g => g.DataScope, "OWN"), ct),
                    "role" => writer.Roles.Where(r => r.RoleId == roles["HK_STAFF"].RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsSystemRole, true), ct),
                    _ => writer.RolePermissions.Where(g => g.RoleId == roles["HK_STAFF"].RoleId && g.PermissionId == permissions["ROOM_VIEW"].PermissionId)
                        .ExecuteUpdateAsync(s => s.SetProperty(g => g.DataScope, "OWN"), ct) };
                try
                {
                    var blocked = false;
                    try
                    {
                        for (var i = 0; i < 100 && !mutation.IsCompleted; i++)
                        {
                            blocked = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.dm_exec_requests WHERE session_id={writerSpid} AND blocking_session_id={assignSpid} AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct) > 0;
                            if (blocked) break; await Task.Delay(50, ct);
                        }
                    }
                    finally { await tx.RollbackAsync(CancellationToken.None); }
                    using var response = await pending; await mutation;
                    check(blocked && response.StatusCode == HttpStatusCode.Created, kind + " SQL writer waits on actual Assign SPID through authorized commit");
                    await Denied(await Person(), other, 403, kind + " changed SQL state denies next assignment");
                }
                finally { await Restore(kind); }
            }
            async Task Failures()
            {
                var target = await Person(); var state = await State(target); var tag = await Tag(target); var fault = prefix + "AuditFault";
                await Ddl("ALTER TABLE dbo.audit_logs WITH NOCHECK ADD CONSTRAINT " + fault + " CHECK (entity_type<>'user_roles' OR JSON_VALUE(new_values,'$.UserId')<>N'" + target.UserId + "')");
                try { await Denied(target, admin, 500, "actual SQL audit failure rollback"); }
                finally { await Ddl("ALTER TABLE dbo.audit_logs DROP CONSTRAINT " + fault); }
                await using (var holder = Open())
                {
                    await using var tx = await holder.Database.BeginTransactionAsync(ct); await new GlobalAdminSerialization(holder, TimeProvider.System).AcquireAsync(ct: ct);
                    var owner = await Spid(holder); var pending = Assign(target, admin, tag);
                    check(await Wait(owner, pending), "actual assignment SQL global lock wait observed");
                    using var response = await pending;
                    check(response.StatusCode == HttpStatusCode.ServiceUnavailable && await State(target) == state, "global timeout503 leaves complete target unchanged");
                    await tx.RollbackAsync(ct);
                }
                await using (var holder = Open())
                {
                    await using var tx = await holder.Database.BeginTransactionAsync(ct); await new GlobalAdminSerialization(holder, TimeProvider.System).AcquireAsync(ct: ct);
                    var owner = await Spid(holder); using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    var pending = Assign(target, admin, tag, token: cancel.Token); check(await Wait(owner, pending), "cancellable actual Assign reaches blocked SQL guard");
                    cancel.Cancel(); var cancelled = false; try { using var r = await pending; } catch (OperationCanceledException) { cancelled = true; }
                    var cleared = false;
                    for (var i = 0; i < 100; i++) { if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.dm_exec_requests WHERE blocking_session_id={owner}").SingleAsync(ct) == 0) { cleared = true; break; } await Task.Delay(50, ct); }
                    check(cancelled && cleared && await State(target) == state, "HTTP cancellation removes SQL waiter and preserves target while owner still holds lock");
                    await tx.RollbackAsync(ct);
                }
                await using (var writer = Open())
                {
                    await using var tx = await writer.Database.BeginTransactionAsync(ct); await writer.Database.ExecuteSqlRawAsync("SET DEADLOCK_PRIORITY HIGH", ct);
                    await writer.Roles.Where(r => r.RoleId == roles["HK_STAFF"].RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.RoleName, "Owned deadlock probe"), ct);
                    var owner = await Spid(writer); var pending = Assign(target, admin, tag);
                    check(await Wait(owner, pending), "actual Assign holds global lock and waits on intentionally inverted role writer");
                    await new GlobalAdminSerialization(writer, TimeProvider.System).AcquireAsync(15000, ct);
                    using var response = await pending; await tx.RollbackAsync(ct);
                    check(response.StatusCode == HttpStatusCode.InternalServerError && await State(target) == state, "SQL deadlock victim returns sanitized500 and rolls back token role audit");
                }
                using (var retry = await Assign(target, admin, tag)) await Success(target, retry, tag);
            }
            async Task ExpiringAuthority(bool lastAdministrator = false)
            {
                var target = await Person(); var state = await State(target); var tag = await Tag(target);
                if (lastAdministrator)
                {
                    // Owned fixture only: exclude the other full actor temporarily so this
                    // test proves the preservation result rather than only authority denial.
                    await db.Users.Where(u => u.UserId == actor.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.TemporaryPasswordExpiresAt, DateTime.UtcNow.AddHours(1)), ct);
                    await using var inspect = Open(); await using var inspectTx = await inspect.Database.BeginTransactionAsync(ct);
                    var guarded = await new GlobalAdminSerialization(inspect, TimeProvider.System).AcquireAsync(ct: ct);
                    check((await guarded.ReadSnapshotAsync(ct)).DurableEligibleUserIds.SequenceEqual(new[] { delegated.UserId }),
                        "isolated trial has exactly the expiring actor as last durable administrator");
                    await inspectTx.RollbackAsync(ct);
                }
                await using var holder = Open(); await using var tx = await holder.Database.BeginTransactionAsync(ct);
                await holder.Database.ExecuteSqlRawAsync("SELECT COUNT_BIG(*) FROM dbo.audit_logs WITH (TABLOCKX,HOLDLOCK)", ct);
                var owner = await Spid(holder); var deadline = DateTime.UtcNow.AddSeconds(5);
                await db.UserRoles.Where(r => r.UserId == delegated.UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiresAt, deadline), ct);
                try
                {
                    var pending = Assign(target, other, tag);
                    check(await Wait(owner, pending), "expiring authority request passes initial gate and reaches actual SQL audit INSERT");
                    var remaining = deadline.AddMilliseconds(500) - DateTime.UtcNow;
                    if (remaining > TimeSpan.Zero) await Task.Delay(remaining, ct);
                    await tx.RollbackAsync(ct); using var response = await pending;
                    check(response.StatusCode == (lastAdministrator ? HttpStatusCode.Conflict : HttpStatusCode.Forbidden) && await State(target) == state,
                        lastAdministrator ? "last durable admin expires during audit wait: HTTP409 and whole assignment rollback" : "authority expiring while locked fails final gate and rolls back complete assignment");
                }
                finally
                {
                    if (holder.Database.CurrentTransaction != null) await tx.RollbackAsync(CancellationToken.None);
                    await db.UserRoles.Where(r => r.UserId == delegated.UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiresAt, (DateTime?)null), CancellationToken.None);
                    if (lastAdministrator) await db.Users.Where(u => u.UserId == actor.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.TemporaryPasswordExpiresAt, (DateTime?)null), CancellationToken.None);
                }
            }
        }
        finally
        {
            if (process != null) { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); process.Dispose(); }
            if (certFile != null) File.Delete(certFile);
            db.ChangeTracker.Clear();
            await db.UserSessions.Where(s => users.Contains(s.UserId) && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, DateTime.UtcNow), CancellationToken.None);
            await db.UserRoles.Where(r => users.Contains(r.UserId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.RolePermissions.Where(g => customRoles.Contains(g.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Roles.Where(r => customRoles.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
        }
    }
    private sealed class Jar
    {
        public Dictionary<string, string> Cookies { get; } = new(); public string? Csrf { get; set; }
        public void Read(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("Set-Cookie", out var values)) foreach (var value in values)
            { var pair = value.Split(';')[0]; var split = pair.IndexOf('='); if (split > 0) Cookies[pair[..split]] = pair[(split + 1)..]; }
        }
    }
}
