using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace UC012Acceptance;

public sealed record CaseReceipt(string Id, bool Passed, long BranchId = 0, string? ETag = null);
public static class Cases
{
    public static bool StrongTag(string? text) { try { return text is { Length: 14 } && text[0] == '"' && text[^1] == '"' && Convert.FromBase64String(text[1..^1]).Length == 8 && Convert.ToBase64String(Convert.FromBase64String(text[1..^1])) == text[1..^1]; } catch { return false; } }
    public static async Task RunAsync(TrialReceipt r, string sql, List<CaseReceipt> receipts, CancellationToken ct)
    {
        var fault = new FaultSwitch(); await using var app = await Host.StartAsync(r, sql, fault, ct);
        using var admin = new Session(r.Origin); using var denied = new Session(r.Origin); using var anon = new Session(r.Origin);
        await admin.Login(r.ActorUsername, Environment.GetEnvironmentVariable("UC012_ACTOR_PASSWORD") ?? throw new GuardFailure("ACTOR_PASSWORD_ENV_REQUIRED"), ct);
        await denied.Login(r.DeniedUsername, Environment.GetEnvironmentVariable("UC012_DENIED_PASSWORD") ?? throw new GuardFailure("DENIED_PASSWORD_ENV_REQUIRED"), ct);
        DbContextOptions<HotelOpsDbContext> options = new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(sql).Options;
        await using var db = new HotelOpsDbContext(options);
        var code = r.CodePrefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        object Create() => new { code, name = "UC012 leased fixture", address = "Disposable trial", phone = (string?)null, email = (string?)null };
        object Update(string name = "UC012 changed") => new { name, address = "Disposable trial", phone = (string?)null, email = (string?)null, reason = "Approved disposable acceptance" };
        long id = 0; string tag = "";
        async Task Pass(int index, Func<Task> test) { Guards.Validate(r, DateTimeOffset.UtcNow); await test(); receipts.Add(new(Guards.Cases[index], true, id, tag)); }
        await Pass(0, async () =>
        {
            using var response = await admin.Send(HttpMethod.Post, "/api/branches", Create(), null, true, ct); Expect(response, 201);
            using var value = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); id = value.RootElement.GetProperty("id").GetInt64(); tag = Tag(response);
            Guards.Require(id > 0 && StrongTag(tag), "CREATE_DTO_OR_ETAG_INVALID");
            Guards.Require(await db.Branches.AsNoTracking().AnyAsync(b => b.BranchId == id && b.BranchCode == code, ct), "CREATE_NOT_PERSISTED");
        });
        var path = $"/api/branches/{id}";
        await Pass(1, async () =>
        {
            using var read = await admin.Send(HttpMethod.Get, path + "/edit", null, null, true, ct); Expect(read, 200);
            Guards.Require(Tag(read) == tag && await Version() == tag, "READ_SQL_VERSION_MISMATCH");
        });
        var original = tag;
        await Pass(2, async () =>
        {
            using var update = await admin.Send(HttpMethod.Put, path, Update(), tag, true, ct); Expect(update, 200); tag = Tag(update);
            Guards.Require(StrongTag(tag) && tag != original && tag == await Version(), "UPDATE_VERSION_MISMATCH");
            Guards.Require(await db.Branches.AsNoTracking().AnyAsync(b => b.BranchId == id && b.Timezone == "Asia/Ho_Chi_Minh" && b.IsActive && b.BranchCode == code, ct), "FIXED_FIELDS_CHANGED");
        });
        await Pass(3, async () => { using var duplicate = await admin.Send(HttpMethod.Post, "/api/branches", Create(), null, true, ct); Expect(duplicate, 409); Guards.Require(await db.Branches.CountAsync(b => b.BranchCode == code, ct) == 1, "DUPLICATE_WRITE"); });
        await Pass(4, async () =>
        {
            var before = await AuditCount();
            foreach (var pair in new (string? Tag, int Status)[] { (null, 428), ("*", 400), (original, 412) })
            { using var response = await admin.Send(HttpMethod.Put, path, Update("must not persist"), pair.Tag, true, ct); Expect(response, pair.Status); }
            Guards.Require(await Version() == tag && await AuditCount() == before, "FAILED_PRECONDITION_WROTE");
        });
        await Pass(5, async () =>
        {
            using var response = await denied.Send(HttpMethod.Get, path + "/edit", null, null, true, ct); Expect(response, 403);
            var now = DateTime.UtcNow;
            Guards.Require(await db.UserRoles.AsNoTracking().AnyAsync(a => a.UserId == r.DeniedActorId && a.IsActive && a.Role.IsActive && a.BranchId != null && a.AssignedAt <= now && (a.ExpiresAt == null || a.ExpiresAt > now)
                && a.Role.RolePermissions.Any(g => g.Permission.PermissionCode == "MASTER_DATA_MANAGE"), ct), "BRANCH_BOUND_DENIED_FIXTURE_REQUIRED");
        });
        await Pass(6, async () =>
        {
            var before = await AuditCount(); using var response = await admin.Send(HttpMethod.Put, path, Update("CSRF denied"), tag, false, ct); Expect(response, 400);
            Guards.Require(await Version() == tag && await AuditCount() == before, "CSRF_FAILURE_WROTE");
        });
        // Expire only sessions created after this runner's login, matching exact dedicated actor.
        await Pass(7, async () =>
        {
            using var unauth = await anon.Send(HttpMethod.Get, path + "/edit", null, null, false, ct); Expect(unauth, 401);
            var expired = await db.UserSessions.Where(s => s.UserId == r.DeniedActorId && s.CreatedAt >= denied.LoginStartedUtc && s.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)), ct);
            Guards.Require(expired == 1, "DEDICATED_SESSION_EXPIRY_TARGET_REQUIRED");
            using var invalid = await denied.Send(HttpMethod.Get, path + "/edit", null, null, false, ct); Expect(invalid, 401);
        });
        await Pass(8, async () =>
        {
            using var second = new Session(r.Origin); await second.Login(r.ActorUsername, Environment.GetEnvironmentVariable("UC012_ACTOR_PASSWORD")!, ct);
            var before = await AuditCount();
            var tasks = new[] { admin.Send(HttpMethod.Put, path, Update("Race A"), tag, true, ct), second.Send(HttpMethod.Put, path, Update("Race B"), tag, true, ct) };
            var responses = await Task.WhenAll(tasks);
            try { Guards.Require(responses.Count(x => (int)x.StatusCode == 200) == 1 && responses.Count(x => (int)x.StatusCode == 412) == 1 && await AuditCount() == before + 1, "RACE_NOT_ONE_WINNER"); }
            finally { foreach (var x in responses) x.Dispose(); }
            tag = await Version();
        });
        await Pass(9, async () =>
        {
            var row = await db.Branches.AsNoTracking().SingleAsync(b => b.BranchId == id, ct); var before = await AuditCount();
            using var noop = await admin.Send(HttpMethod.Put, path, Update(row.BranchName), tag, true, ct); Expect(noop, 200);
            Guards.Require(await AuditCount() == before && await Version() == tag, "NOOP_ADVANCED_AUDIT_OR_VERSION");
            Guards.Require(before == 3 && await db.AuditLogs.AnyAsync(a => a.EntityType == "branches" && a.EntityId == id.ToString() && a.ActorUserId == r.ActorId && a.Action == "CREATE", ct), "ATOMIC_AUDIT_COUNT_INVALID");
        });
        await Pass(10, async () =>
        {
            var before = await AuditCount(); var old = await db.Branches.AsNoTracking().SingleAsync(b => b.BranchId == id, ct);
            fault.Armed = true;
            try { using var response = await admin.Send(HttpMethod.Put, path, Update("FAULT must rollback"), tag, true, ct); Expect(response, r.AuditFaultStatus!.Value); }
            finally { fault.Armed = false; }
            Guards.Require(fault.Hits == 1 && await Version() == tag && await AuditCount() == before && await db.Branches.AnyAsync(b => b.BranchId == id && b.BranchName == old.BranchName, ct), "AUDIT_ROLLBACK_FAILED");
        });
        await Pass(11, async () =>
        {
            var before = await AuditCount();
            using var legacy = await admin.Send(HttpMethod.Put, path, Update("legacy"), '"' + new string('A', 64) + '"', true, ct); Expect(legacy, 400);
            Guards.Require(await Version() == tag && await AuditCount() == before, "ETAG_CUTOVER_REPLAY");
        });
        async Task<int> AuditCount() => await db.AuditLogs.CountAsync(a => a.EntityType == "branches" && a.EntityId == id.ToString(), ct);
        async Task<string> Version()
        {
            var bytes = await db.Database.SqlQuery<byte[]>($"SELECT CONVERT(varbinary(8),row_version) AS [Value] FROM dbo.branches WHERE branch_id={id}").SingleAsync(ct);
            return '"' + Convert.ToBase64String(bytes) + '"';
        }
    }
    static void Expect(HttpResponseMessage r, int status) => Guards.Require((int)r.StatusCode == status, $"HTTP_STATUS_CONTRACT_MISMATCH_EXPECTED_{status}_ACTUAL_{(int)r.StatusCode}");
    static string Tag(HttpResponseMessage r) => r.Headers.ETag?.ToString() ?? "";
    sealed class Session : IDisposable
    {
        readonly HttpClient client; string token = "";
        public DateTime LoginStartedUtc { get; private set; }
        public Session(string origin) { client = new(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false, UseProxy = false }) { BaseAddress = new(origin), Timeout = TimeSpan.FromSeconds(30) }; }
        public async Task Login(string user, string password, CancellationToken ct)
        {
            LoginStartedUtc = DateTime.UtcNow;
            using var csrf = await client.GetAsync("/api/auth/csrf", ct); Expect(csrf, 200); token = await Token(csrf, ct);
            using var result = await Send(HttpMethod.Post, "/api/auth/login", new { username = user, password }, null, true, ct); Expect(result, 200);
            using var fresh = await client.GetAsync("/api/auth/csrf", ct); Expect(fresh, 200); token = await Token(fresh, ct);
        }
        static async Task<string> Token(HttpResponseMessage r, CancellationToken ct)
        {
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct)); var root = doc.RootElement;
            Guards.Require(root.GetProperty("headerName").GetString() == "X-CSRF-TOKEN", "CSRF_HEADER_CONTRACT");
            return root.GetProperty("csrfToken").GetString() ?? throw new GuardFailure("CSRF_TOKEN_MISSING");
        }
        public async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, string? tag, bool csrf, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body);
            if (tag is not null) request.Headers.TryAddWithoutValidation("If-Match", tag);
            if (csrf) request.Headers.Add("X-CSRF-TOKEN", token);
            return await client.SendAsync(request, ct); // No retries, redirects, queues or certificate callbacks.
        }
        public void Dispose() => client.Dispose();
    }
}
