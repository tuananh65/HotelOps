using System.Data.Common;
using System.Net;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Runs against the actual root Program.cs HTTPS process and a guarded fresh trial.
internal static class Task62AcceptanceCases
{
    internal static List<object> Waits { get; } = [];
    internal static async Task RunAsync(HotelOpsDbContext setup,
        Func<DbCommandInterceptor?, HotelOpsDbContext> open, HttpClient http, Func<string, Task<Jar>> login,
        IReadOnlyDictionary<string, User> identities, IReadOnlyList<User> targets, IReadOnlyDictionary<string, Jar> jars,
        long a, long b, Func<string[], Task<string>> fingerprint, Action<bool, string> check, CancellationToken ct)
    {
        var target = targets[0].UserId;
        var roleIds = await setup.Roles.AsNoTracking().ToDictionaryAsync(x => x.RoleCode, x => x.RoleId, ct);
        var now = DateTime.UtcNow;
        var addedMemberships = new List<UserBranchAccess>();
        foreach (var user in targets.Take(6))
            foreach (var branch in new[] { a, b })
                addedMemberships.Add(new() { UserId = user.UserId, BranchId = branch, AccessLevel = "STANDARD", GrantedAt = now.AddHours(-1) });
        setup.AddRange(addedMemberships);
        var addedRoles = new[]
        {
            new UserRole { UserId = target, RoleId = roleIds["SYS_ADMIN"], IsActive = true, AssignedAt = now.AddHours(-1) },
            new UserRole { UserId = target, RoleId = roleIds["SYS_ADMIN"], BranchId = a, IsActive = true, AssignedAt = now.AddHours(-1) },
            new UserRole { UserId = target, RoleId = roleIds["HK_STAFF"], BranchId = b, IsActive = true, AssignedAt = now.AddHours(-1) }
        };
        setup.AddRange(addedRoles); await setup.SaveChangesAsync(ct); setup.ChangeTracker.Clear();
        var admin = jars["admin"];
        var route = $"/api/users/{target}/effective-access";
        async Task<HttpResponseMessage> Get(string path, Jar jar, CancellationToken token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (jar.Cookies.Count > 0) request.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(x => x.Key + "=" + x.Value)));
            var result = await http.SendAsync(request, token); jar.Read(result); return result;
        }
        async Task<JsonElement> Read(string path, Jar jar)
        {
            using var response = await Get(path, jar, ct);
            check(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true,
                "UC011 authorized production HTTPS response is 200/no-store");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            AssertFields(json.RootElement); return json.RootElement.Clone();
        }
        async Task Denied(string path, Jar jar, int status, string label)
        {
            using var response = await Get(path, jar, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            check((int)response.StatusCode == status && response.Headers.Location == null
                && !text.Contains("effectivePermissions") && !text.Contains("configuredGrants")
                && !text.Contains("SqlException") && !text.Contains("passwordHash"), label);
        }
        JsonElement[] Permissions(JsonElement body) => body.GetProperty("effectivePermissions").EnumerateArray().ToArray();
        JsonElement[] Sources(JsonElement body, string code) => Permissions(body).Single(x => x.GetProperty("code").GetString() == code)
            .GetProperty("sources").EnumerateArray().ToArray();
        try
        {
            var before = await fingerprint(["users", "user_roles", "user_branch_access", "audit_logs"]);
            await Denied(route, new(), 401, "UC011 anonymous denied without access disclosure");
            foreach (var key in identities.Keys.Where(k => k is not "admin" and not "branch"))
                await Denied(route + "?actorId=" + identities["admin"].UserId, jars[key], 403,
                    "UC011 existing SQL authorization rejects " + key + "; client actor cannot grant access");
            var full = await Read(route, admin);
            check(full.GetProperty("reviewerScope").GetProperty("isGlobal").GetBoolean(), "UC011 SQL unbound ALL permits global review");
            check(full.GetProperty("assignedRoles").GetArrayLength() == 5, "UC011 assigned roles retain distinct branch/global assignments");
            check(full.GetProperty("assignedRoles").EnumerateArray().All(x => x.GetProperty("assignedAtUtc").GetString()!.EndsWith('Z'))
                && full.GetProperty("branchMemberships").EnumerateArray().All(x => x.GetProperty("grantedAtUtc").GetString()!.EndsWith('Z')),
                "UC011 SQL datetime values are explicitly serialized as UTC");
            var clean = Sources(full, "CLEANING_EXECUTE");
            check(clean.Any(x => x.GetProperty("scope").GetString() == "OWN" && x.GetProperty("requiresResourceOwnership").GetBoolean()),
                "UC011 OWN target permission remains conditional on verified resource ownership");
            check(Sources(full, "USER_VIEW").Any(x => x.GetProperty("scope").GetString() == "GLOBAL")
                && Sources(full, "USER_VIEW").Any(x => x.GetProperty("scope").GetString() == "BRANCH" && x.GetProperty("branchId").GetInt64() == a),
                "UC011 ALL-unbound and ALL-bound sources preserve distinct effective scope");
            check(Permissions(full).All(x => x.GetProperty("sources").GetArrayLength() > 0), "UC011 every effective permission identifies its granting assignment/role");
            var clipped = await Read(route, jars["branch"]);
            check(!clipped.GetProperty("reviewerScope").GetProperty("isGlobal").GetBoolean()
                && clipped.GetProperty("assignedRoles").EnumerateArray().All(x => x.GetProperty("branchId").GetInt64() == a)
                && clipped.GetProperty("branchMemberships").EnumerateArray().All(x => x.GetProperty("branchId").GetInt64() == a)
                && clipped.GetProperty("configuredGrants").EnumerateArray().All(x => x.GetProperty("branchId").GetInt64() == a),
                "UC011 SQL branch reviewer sees only matching assignments/memberships/grants; global assignments omitted");
            var cross = await Read($"/api/users/{targets[1].UserId}/effective-access", jars["branch"]);
            check(cross.GetProperty("account").GetProperty("primaryBranch").ValueKind == JsonValueKind.Null
                && cross.GetProperty("account").GetProperty("department").ValueKind == JsonValueKind.Null,
                "UC011 cross-branch primary/department redacted independently of visible SQL membership");
            check(Permissions(cross).Length == 0 && cross.GetProperty("assignedRoles").GetArrayLength() == 0,
                "UC011 inactive assignment in other branch cannot grant permissions or leak assignment");
            await Denied($"/api/users/{targets[7].UserId}/effective-access", jars["branch"], 403,
                "UC011 primary branch alone is not target visibility; missing current membership denied");
            await Denied("/api/users/9223372036854775807/effective-access", jars["branch"], 403,
                "UC011 out-of-scope/missing target indistinguishable to branch reviewer");
            await Denied("/api/users/9223372036854775807/effective-access", admin, 404, "UC011 globally authorized missing target returns 404");
            await Denied($"/api/users/{targets[26].UserId}/effective-access", admin, 404, "UC011 soft-deleted target returns 404");
            await Read($"/api/users/{identities["admin"].UserId}/effective-access", admin);
            await Read($"/api/users/{identities["branch"].UserId}/effective-access", jars["branch"]);
            await Denied($"/api/users/{identities["denied"].UserId}/effective-access", jars["denied"], 403,
                "UC011 self review has no permission exemption");
            foreach (var i in new[] { 1, 2, 3, 4, 5 })
            {
                var body = await Read($"/api/users/{targets[i].UserId}/effective-access", admin);
                check(Permissions(body).Length == 0, "UC011 inactive/expired/future/inactive-role/empty assignments not effective: target " + i);
            }
            var filtered = await Read(route + "?feature=%20ADMIN%20&permissionCode=%20USER_VIEW%20", admin);
            check(Permissions(filtered).Length == 1 && Permissions(filtered)[0].GetProperty("code").GetString() == "USER_VIEW",
                "UC011 feature and permission filters compose against stored catalog");
            var none = await Read(route + "?feature=NO_SUCH_FEATURE", admin);
            check(Permissions(none).Length == 0 && none.GetProperty("configuredGrants").GetArrayLength() == 0,
                "UC011 unknown feature produces empty permissions without hiding assigned roles");
            var injected = await Read(route + "?permissionCode=" + Uri.EscapeDataString("' OR 1=1--"), admin);
            check(Permissions(injected).Length == 0, "UC011 permission filter is parameterized literal input");
            foreach (var path in new[] { route + "?feature=" + new string('x', 61), route + "?permissionCode=" + new string('x', 101),
                "/api/users/0/effective-access", "/api/users/x/effective-access", $"/api/users/{target}?includeAccess=invalid" })
                await Denied(path, admin, 400, "UC011/141 invalid route/query rejected by model validation");
            var detail = await Read($"/api/users/{target}?includeAccess=true", admin);
            check(detail.GetProperty("assignedRoles").GetRawText() == full.GetProperty("assignedRoles").GetRawText()
                && detail.GetProperty("effectivePermissions").GetRawText() == full.GetProperty("effectivePermissions").GetRawText(),
                "UC141 step6 opt-in detail uses exactly the UC011 projection");
            check(await fingerprint(["users", "user_roles", "user_branch_access", "audit_logs"]) == before,
                "UC011 read-only HTTPS preserves credentials, roles, memberships, business account fields and audit records");

            await setup.Users.Where(x => x.UserId == target).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountStatus, "INACTIVE"), ct);
            var inactive = await Read(route, admin);
            check(Permissions(inactive).Length == 0 && inactive.GetProperty("assignedRoles").GetArrayLength() == 5
                && inactive.GetProperty("configuredGrants").EnumerateArray().All(x => !x.GetProperty("isEffective").GetBoolean()),
                "UC011 inactive account has configured roles/grants but no effective permission even with active global assignment");
            await setup.Users.Where(x => x.UserId == target).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountStatus, "ACTIVE"), ct);
            await setup.UserBranchAccesses.Where(x => x.UserId == identities["branch"].UserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
            await Denied(route, jars["branch"], 403, "UC011 revoked reviewer branch membership invalidates otherwise active ALL-bound review grant");
            await setup.UserBranchAccesses.Where(x => x.UserId == identities["branch"].UserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, (DateTime?)null), ct);

            // Target membership removal invalidates only its bound grants, never another path.
            await setup.UserBranchAccesses.Where(x => x.UserId == target && x.BranchId == b)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
            var revoked = await Read(route, admin);
            check(revoked.GetProperty("configuredGrants").EnumerateArray().Where(x => x.GetProperty("branchId").ValueKind != JsonValueKind.Null
                    && x.GetProperty("branchId").GetInt64() == b).All(x => !x.GetProperty("isEffective").GetBoolean()),
                "UC011 revoked branch membership invalidates bound target grant without borrowing another assignment");
            await setup.UserBranchAccesses.Where(x => x.UserId == target && x.BranchId == b)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, (DateTime?)null), ct);
            foreach (var expire in new[] { true, false })
            {
                var jar = await login("admin");
                var sid = await setup.UserSessions.Where(x => x.UserId == identities["admin"].UserId)
                    .OrderByDescending(x => x.SessionId).Select(x => x.SessionId).FirstAsync(ct);
                if (expire) await setup.UserSessions.Where(x => x.SessionId == sid).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)), ct);
                else await setup.UserSessions.Where(x => x.SessionId == sid).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
                await Denied(route, jar, 401, "UC011 expired/revoked real SQL session denies cookie");
            }

            foreach (var scenario in new[] { "writer-first", "reviewer-revocation", "session-revocation", "final-expiry", "cancellation", "timeout" })
            {
                admin = await login("admin");
                var sessionId = await setup.UserSessions.Where(x => x.UserId == identities["admin"].UserId)
                    .OrderByDescending(x => x.SessionId).Select(x => x.SessionId).FirstAsync(ct);
                if (scenario == "final-expiry") await setup.UserRoles.Where(x => x.UserId == identities["admin"].UserId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddSeconds(5)), ct);
                await using var holder = open(null); await holder.Database.OpenConnectionAsync(ct);
                var owner = await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                await using var tx = await holder.Database.BeginTransactionAsync(ct);
                var featureWait = scenario is "reviewer-revocation" or "session-revocation" or "final-expiry";
                if (featureWait) await holder.UserRoles.Where(x => x.UserId == target).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
                else await holder.Users.Where(x => x.UserId == target).ExecuteUpdateAsync(s => s.SetProperty(x => x.FullName, "Task62 authoritative committed name"), ct);
                using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var pending = Get(route, admin, stop.Token);
                Task? revokeTask = null;
                await using var revoker = open(null);
                try
                {
                    var api = await Wait(scenario, owner, pending, featureWait ? "UC011:AssignedRoles" : "UC141:UserReadUniverse");
                    if (scenario is "reviewer-revocation" or "session-revocation")
                    {
                        await revoker.Database.OpenConnectionAsync(ct);
                        revokeTask = Revoke();
                        async Task Revoke()
                        {
                            await using var revokeTx = await revoker.Database.BeginTransactionAsync(ct);
                            await new UserEligibilityLock(revoker).LockAsync([identities["admin"].UserId], ct);
                            if (scenario == "reviewer-revocation") await revoker.UserRoles.Where(x => x.UserId == identities["admin"].UserId)
                                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
                            else await revoker.UserSessions.Where(x => x.SessionId == sessionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
                            await revokeTx.CommitAsync(ct);
                        }
                        await Wait(scenario + "-retained-authority", api, revokeTask, null);
                        check(!revokeTask.IsCompleted, "UC011 retains reviewer/session authority while target role SQL is waiting: " + scenario);
                    }
                    if (scenario == "cancellation")
                    {
                        stop.Cancel(); var canceled = false;
                        try { using var response = await pending; } catch (OperationCanceledException) { canceled = true; }
                        check(canceled, "UC011 actual HTTPS cancellation propagates while SQL waits");
                        var released = false;
                        for (var attempt = 0; attempt < 150; attempt++)
                        {
                            if (!await setup.Database.SqlQuery<int>($"SELECT CONVERT(int,session_id) AS [Value] FROM sys.dm_exec_requests WHERE session_id={api} AND blocking_session_id={owner}").AnyAsync(ct)) { released = true; break; }
                            await Task.Delay(40, ct);
                        }
                        check(released, "UC011 RequestAborted releases blocked SQL before blocker transaction ends");
                        await tx.RollbackAsync(ct);
                    }
                    else if (scenario == "timeout")
                    {
                        using var result = await pending;
                        var body = await result.Content.ReadAsStringAsync(ct);
                        check(result.StatusCode == HttpStatusCode.InternalServerError && !body.Contains("effectivePermissions") && !body.Contains("SqlException"),
                            "UC011 real default SQL command timeout returns generic error with no partial access response");
                        await tx.RollbackAsync(ct);
                    }
                    else
                    {
                        if (scenario == "final-expiry") await Task.Delay(TimeSpan.FromSeconds(6), ct);
                        await tx.CommitAsync(ct);
                        using var result = await pending;
                        if (scenario == "final-expiry") check(result.StatusCode == HttpStatusCode.Forbidden
                            && !(await result.Content.ReadAsStringAsync(ct)).Contains("effectivePermissions"),
                            "UC011 final authorization expiry discards materialized report before serialization");
                        else
                        {
                            check(result.StatusCode == HttpStatusCode.OK, "UC011 concurrent read linearizes correctly: " + scenario);
                            using var body = JsonDocument.Parse(await result.Content.ReadAsStringAsync(ct));
                            check(featureWait ? Permissions(body.RootElement).Length == 0
                                : body.RootElement.GetProperty("account").GetProperty("fullName").GetString() == "Task62 authoritative committed name",
                                "UC011 rereads committed target state after real SQL wait: " + scenario);
                        }
                        if (revokeTask != null)
                        {
                            await revokeTask;
                            await Denied(route, admin, scenario == "session-revocation" ? 401 : 403, "UC011 committed revocation applied to next HTTPS request");
                        }
                    }
                }
                finally
                {
                    stop.Cancel();
                    if (holder.Database.CurrentTransaction != null) await tx.RollbackAsync(CancellationToken.None);
                    try { using var response = await pending; } catch { }
                    if (revokeTask != null) try { await revokeTask; } catch { }
                    await setup.UserRoles.Where(x => x.UserId == identities["admin"].UserId)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true).SetProperty(x => x.ExpiresAt, (DateTime?)null), ct);
                    await setup.UserRoles.Where(x => x.UserId == target).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true), ct);
                    await setup.Users.Where(x => x.UserId == target).ExecuteUpdateAsync(s => s.SetProperty(x => x.FullName, targets[0].FullName), ct);
                }
                await Read(route, await login("admin"));
            }
        }
        finally
        {
            // Undo only these extra fixture links; preserve canonical rows, original fixtures and artifacts.
            var ids = addedRoles.Select(x => x.UserRoleId).ToArray();
            await setup.UserRoles.Where(x => ids.Contains(x.UserRoleId)).ExecuteDeleteAsync(ct);
            var membershipIds = addedMemberships.Select(x => x.AccessId).ToArray();
            await setup.UserBranchAccesses.Where(x => membershipIds.Contains(x.AccessId)).ExecuteDeleteAsync(ct);
        }

        async Task<int> Wait(string scenario, int owner, Task pending, string? tag)
        {
            for (var attempt = 0; attempt < 160 && !pending.IsCompleted; attempt++)
            {
                var rows = await setup.Database.SqlQuery<SqlWait>($"SELECT CONVERT(int,r.session_id) AS SessionId, r.wait_type AS Kind, t.text AS Query FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE r.blocking_session_id={owner} AND r.wait_type LIKE 'LCK_M_%'").ToArrayAsync(ct);
                var row = rows.FirstOrDefault(x => tag == null || x.Query.Contains(tag, StringComparison.Ordinal));
                if (row != null) { Waits.Add(new { scenario, owner, waiter = row.SessionId, row.Kind }); return row.SessionId; }
                await Task.Delay(40, ct);
            }
            if (pending.IsFaulted) await pending;
            throw new InvalidOperationException("No observed production SQL wait: " + scenario);
        }
    }
    private static void AssertFields(JsonElement element)
    {
        string[] allowed = ["account", "id", "employeeCode", "username", "fullName", "email", "phone", "accountStatus", "primaryBranch", "department",
            "code", "name", "evaluatedAtUtc", "reviewerScope", "isGlobal", "branchIds", "assignedRoles", "assignmentId", "roleId", "branchId",
            "assignmentActive", "roleActive", "assignedAtUtc", "expiresAtUtc", "branchMemberships", "accessLevel", "branchActive", "grantedAtUtc", "revokedAtUtc",
            "configuredGrants", "permissionCode", "permissionName", "feature", "configuredScope", "roleCode", "isEffective", "effectivePermissions", "sources", "scope", "requiresResourceOwnership"];
        if (element.ValueKind == JsonValueKind.Object) foreach (var property in element.EnumerateObject())
        { if (!allowed.Contains(property.Name)) throw new InvalidOperationException("Unexpected access DTO property: " + property.Name); AssertFields(property.Value); }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) AssertFields(child);
    }
    private sealed class SqlWait
    {
        public int SessionId { get; set; }
        public string Kind { get; set; } = "";
        public string Query { get; set; } = "";
    }
}
