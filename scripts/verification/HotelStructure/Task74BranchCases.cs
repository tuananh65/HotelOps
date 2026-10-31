using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class Task74BranchCases
{
    internal static async Task RunAsync(HotelOpsDbContext db, Func<DbCommandInterceptor?, HotelOpsDbContext> open,
        HttpClient http, Func<string, Task<CookieJar>> login, IReadOnlyDictionary<string, User> users,
        long branch, long foreign, Action<bool, string> check, CancellationToken ct)
    {
        var admin = await login("admin"); var other = await login("other"); var scoped = await login("branch");
        var denied = await login("denied"); var anonymous = new CookieJar();
        using (var response = await http.GetAsync("/api/auth/csrf", ct))
        {
            anonymous.Read(response); using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            anonymous.Token = json.RootElement.GetProperty("csrfToken").GetString();
        }
        async Task<(JsonElement Body, string? Tag)> Send(HttpMethod method, string path, CookieJar jar, int expected,
            object? body = null, string? tag = null, bool csrf = true)
        {
            using var request = new HttpRequestMessage(method, path);
            if (jar.Cookies.Count != 0) request.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(x => x.Key + "=" + x.Value)));
            if (csrf && jar.Token != null) request.Headers.Add(HotelOps.Api.Security.HotelOpsAntiforgery.HeaderName, jar.Token);
            if (tag != null) request.Headers.TryAddWithoutValidation("If-Match", tag);
            if (body != null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, ct); var text = await response.Content.ReadAsStringAsync(ct);
            check((int)response.StatusCode == expected, $"UC012 {method} {path}: expected {expected}, actual {(int)response.StatusCode}");
            check(!text.Contains("SqlException") && !text.Contains("stackTrace") && !text.Contains("passwordHash"), "UC012 no sensitive/provider disclosure");
            using var json = JsonDocument.Parse(text); return (json.RootElement.Clone(), response.Headers.ETag?.Tag);
        }
        object Create(string code = "T74_BRANCH") => new { code, name = " Trial branch ", address = " 74 Trial Street ", phone = "+84 123456", email = "trial@example.invalid" };
        object Update(string name = "Updated trial branch") => new { name, address = "Updated trial address", phone = "123456", email = "updated@example.invalid", reason = "Correct branch contact information" };
        const string path = "/api/branches";
        await Send(HttpMethod.Post, path, anonymous, 401, Create());
        await Send(HttpMethod.Post, path, denied, 403, Create());
        await Send(HttpMethod.Post, path, scoped, 403, Create());
        await Send(HttpMethod.Get, path + "/" + foreign + "/edit", scoped, 403);
        await Send(HttpMethod.Post, path, admin, 400, new { code = "bad space", name = "Name", address = "Address" });
        await Send(HttpMethod.Post, path, admin, 400, new { code = "T74_BAD", name = "Name", address = " " });
        await Send(HttpMethod.Post, path, admin, 400, new { code = "T74_BAD", name = "Name", address = "Address", email = "not email" });
        await Send(HttpMethod.Post, path, admin, 400, new { code = "T74_BAD", name = "Name", address = "Address", isActive = false });
        await Send(HttpMethod.Post, path, admin, 400, Create(), csrf: false);
        var created = await Send(HttpMethod.Post, path, admin, 201, Create()); var id = created.Body.GetProperty("id").GetInt64();
        var target = path + "/" + id;
        check(created.Body.GetProperty("timezone").GetString() == "Asia/Ho_Chi_Minh" && created.Body.GetProperty("isActive").GetBoolean()
            && created.Body.GetProperty("name").GetString() == "Trial branch", "UC012 create normalizes fields and fixes timezone/Active");
        check(await db.Branches.AnyAsync(b => b.BranchId == id && b.BranchCode == "T74_BRANCH" && b.Address == "74 Trial Street", ct), "UC012 real SQL persistence");
        await Send(HttpMethod.Post, path, admin, 409, Create("t74_branch"));
        await Send(HttpMethod.Get, path + "/" + long.MaxValue + "/edit", admin, 404);
        await Send(HttpMethod.Put, target, admin, 428, Update());
        await Send(HttpMethod.Put, target, admin, 400, Update(), "*");
        await Send(HttpMethod.Put, target, admin, 400, new { name = "Name", address = "Address", reason = "short" }, created.Tag);
        var read = await Send(HttpMethod.Get, target + "/edit", admin, 200);
        check(read.Tag == created.Tag, "UC012 read retains exact initial revision");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.audit_logs WITH NOCHECK ADD CONSTRAINT T74_BranchAuditFault CHECK(entity_type<>'branches' OR action<>'UPDATE')", ct);
        try { await Send(HttpMethod.Put, target, admin, 500, Update(), read.Tag); }
        finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.audit_logs DROP CONSTRAINT T74_BranchAuditFault", ct); }
        check(await db.Branches.AnyAsync(b => b.BranchId == id && b.BranchName == "Trial branch", ct)
            && await db.AuditLogs.CountAsync(a => a.EntityType == "branches" && a.EntityId == id.ToString(), ct) == 1,
            "UC012 post-write audit fault rolls back entity and audit");
        async Task<int> Race(HttpMethod method, string url, CookieJar jar, object body, string? tag = null)
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(x => x.Key + "=" + x.Value)));
            request.Headers.Add(HotelOps.Api.Security.HotelOpsAntiforgery.HeaderName, jar.Token);
            if (tag != null) request.Headers.TryAddWithoutValidation("If-Match", tag);
            request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, ct); return (int)response.StatusCode;
        }
        var edits = await Task.WhenAll(Race(HttpMethod.Put, target, admin, Update("First concurrent edit"), read.Tag),
            Race(HttpMethod.Put, target, other, Update("Second concurrent edit"), read.Tag));
        check(edits.Order().SequenceEqual(new[] { 200, 412 }), "UC012 real concurrent HTTPS edits: one commit and one stale rejection");
        var current = await Send(HttpMethod.Get, target + "/edit", admin, 200);
        check(current.Tag != read.Tag && current.Body.GetProperty("code").GetString() == "T74_BRANCH", "UC012 update changes revision, preserves code");
        var audit = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "branches" && a.EntityId == id.ToString() && a.Action == "UPDATE").SingleAsync(ct);
        check(audit.ActorUserId == users[edits[0] == 200 ? "admin" : "other"].UserId && audit.BranchId == id && audit.OldValues!.Contains("Trial branch")
            && audit.NewValues!.Contains(edits[0] == 200 ? "First concurrent edit" : "Second concurrent edit"), "UC012 SQL audit binds server actor/branch and before/after");
        var duplicates = await Task.WhenAll(Race(HttpMethod.Post, path, admin, Create("T74_DUP_RACE")),
            Race(HttpMethod.Post, path, other, Create("T74_DUP_RACE")));
        check(duplicates.Order().SequenceEqual(new[] { 201, 409 }), "UC012 real concurrent HTTPS duplicate create: one commit and one conflict");
        check(await db.Branches.CountAsync(b => b.BranchCode == "T74_DUP_RACE", ct) == 1, "UC012 duplicate protection preserves one branch");
        // Targeted shared regression: existing physical-structure read still scopes after branch metadata writes.
        using var regression = new HttpRequestMessage(HttpMethod.Get, $"/api/branches/{branch}/structure");
        regression.Headers.Add("Cookie", string.Join("; ", scoped.Cookies.Select(x => x.Key + "=" + x.Value)));
        using var result = await http.SendAsync(regression, ct);
        check((int)result.StatusCode == 200, "UC013 targeted HTTPS structure regression");
    }
}
