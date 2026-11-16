using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal static class RecoveryAuthProbe
{
    internal static async Task<int> RunAsync(string output, bool replenishment = false)
    {
        if (File.Exists(output)) throw new IOException("Evidence already exists.");
        const string database = "HotelOps_DEV3_142A_Recovery_2b221f40";
        var connection = new SqlConnectionStringBuilder { DataSource = @"meita\MSSQLSERVER01", InitialCatalog = database,
            IntegratedSecurity = true, Encrypt = true, TrustServerCertificate = false };
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = budget.Token;
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection.ConnectionString).Options);
        await db.Database.OpenConnectionAsync(ct);
        await using (var identity = db.Database.GetDbConnection().CreateCommand())
        {
            identity.CommandText = """
                SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita\MSSQLSERVER01'
                AND DB_NAME()='HotelOps_DEV3_142A_Recovery_2b221f40'
                AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
                 AND database_guid='5270D07C-7C27-477B-A60E-86485ADD7FAD') THEN 1 ELSE 0 END
                """;
            if (!Equals(await identity.ExecuteScalarAsync(ct), 1)) throw new InvalidOperationException("Exact disposable identity rejected.");
        }
        var name = "D3HTTPS_" + Guid.NewGuid().ToString("N")[..16];
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) + "aA1!";
        var user = new User { EmployeeCode = name, Username = name, Email = name + "@example.invalid",
            FullName = "Synthetic recovery auth", EmploymentStatus = "ACTIVE", AccountStatus = "ACTIVE", CreatedAt = DateTime.UtcNow };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        var checks = new List<object>(); string? failure = null; bool cleaned = false;
        try
        {
            db.Users.Add(user); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            using var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false };
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7148") };
            async Task<HttpResponseMessage> Send(HttpMethod method, string path, int expected, object? body = null, string? token = null)
            {
                using var request = new HttpRequestMessage(method, path);
                if (body != null) request.Content = JsonContent.Create(body);
                if (token != null) request.Headers.Add("X-CSRF-TOKEN", token);
                var response = await http.SendAsync(request, ct);
                checks.Add(new { path, method = method.Method, expected, actual = (int)response.StatusCode });
                if ((int)response.StatusCode != expected) { response.Dispose(); throw new InvalidOperationException($"Unexpected status for {path}"); }
                return response;
            }
            using (await Send(HttpMethod.Post, "/api/auth/login", 400, new { username = name, password })) { }
            using var bootstrap = await Send(HttpMethod.Get, "/api/auth/csrf", 200);
            using var bootstrapJson = JsonDocument.Parse(await bootstrap.Content.ReadAsStringAsync(ct));
            using var login = await Send(HttpMethod.Post, "/api/auth/login", 200, new { username = name, password }, bootstrapJson.RootElement.GetProperty("csrfToken").GetString());
            using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync(ct));
            var token = loginJson.RootElement.GetProperty("csrfToken").GetString();
            if (await db.UserSessions.CountAsync(s => s.UserId == user.UserId && s.RevokedAt == null, ct) != 1)
                throw new InvalidOperationException("API session not persisted in the verified disposable database.");
            checks.Add(new { persistedSession = true });
            using (await Send(HttpMethod.Get, "/api/auth/session", 200)) { }
            foreach (var path in new[] { "/api/material-requests?branchId=1", "/api/replenishments?branchId=1" })
                using (await Send(HttpMethod.Get, path, 403)) { }
            if (replenishment) await RecoveryReplenishmentScenario.RunAsync(db, http, user.UserId, token!, checks, ct);
            using (await Send(HttpMethod.Post, "/api/auth/logout", 400)) { }
            using (await Send(HttpMethod.Get, "/api/auth/session", 200)) { }
            using (await Send(HttpMethod.Post, "/api/auth/logout", 204, token: token)) { }
            using (await Send(HttpMethod.Get, "/api/auth/session", 401)) { }
            if (await db.UserSessions.AnyAsync(s => s.UserId == user.UserId && s.RevokedAt == null, ct)
                || !await db.AuditLogs.AnyAsync(a => a.ActorUserId == user.UserId && a.Action == "LOGIN", ct))
                throw new InvalidOperationException("Session revocation or login audit missing.");
            checks.Add(new { persistedRevocation = true, loginAudit = true });
        }
        catch (Exception e) { failure = e.GetBaseException().Message; }
        finally
        {
            using var cleanupBudget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var cleanupToken = cleanupBudget.Token;
            try
            {
                if (user.UserId > 0)
                {
                    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cleanupToken);
                    if (!await db.Users.AnyAsync(u => u.UserId == user.UserId && u.Username == name && u.Email == name + "@example.invalid", cleanupToken))
                        throw new InvalidOperationException("Synthetic ownership check failed.");
                    await db.UserSessions.Where(s => s.UserId == user.UserId).ExecuteDeleteAsync(cleanupToken);
                    var id = user.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    await db.AuditLogs.Where(a => a.ActorUserId == user.UserId && a.EntityType == "users" && a.EntityId == id).ExecuteDeleteAsync(cleanupToken);
                    await db.Users.Where(u => u.UserId == user.UserId && u.Username == name).ExecuteDeleteAsync(cleanupToken);
                    await tx.CommitAsync(cleanupToken);
                }
                cleaned = !await db.Users.AnyAsync(u => u.Username == name, cleanupToken);
            }
            catch (Exception e) { failure = (failure ?? "") + " Cleanup: " + e.GetBaseException().Message; }
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { database, fixtureUserId = user.UserId,
                checks, failure, cleanup = cleaned, success = failure == null && cleaned,
                scope = replenishment ? "Synthetic HTTPS Create/read/Close replenishment with isolated grants and fixture cleanup; not complete UC acceptance"
                    : "Synthetic HTTPS login/session/CSRF/logout and no-grant denial; no positive business workflow acceptance" }, new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine($"RECOVERY_AUTH {(failure == null && cleaned ? "PASS" : "FAIL")}; cleanup={cleaned}");
        return failure == null && cleaned ? 0 : 1;
    }
}
