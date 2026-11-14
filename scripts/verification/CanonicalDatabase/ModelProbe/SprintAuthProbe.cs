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
    internal static async Task<int> RunAsync(string reportPath)
    {
        if (File.Exists(reportPath)) throw new IOException("Never overwrite prior runtime evidence");
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = budget.Token;
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer($"Server=localhost;Database={Database};Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        await db.Database.OpenConnectionAsync(ct);
        await using (var identity = db.Database.GetDbConnection().CreateCommand())
        {
            identity.CommandText = """
                SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita'
                  AND DB_NAME()='HotelOps_DEV3_142A_20261114_cc78c13f'
                  AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
                    AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
                  AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run'
                    AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
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
            if (user.UserId > 0)
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
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new { database = Database, fixtureUserId = user.UserId,
                checks, failure, cleanup = cleaned, success = failure == null && cleaned,
                scope = "Real HTTPS/SQL login-session-logout; no privileged roles or browser workflow acceptance" },
                new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine($"Sprint HTTPS auth: {(failure == null && cleaned ? "PASS" : "FAIL")}; checks={checks.Count}; cleanup={cleaned}");
        return failure == null && cleaned ? 0 : 1;
    }
}
