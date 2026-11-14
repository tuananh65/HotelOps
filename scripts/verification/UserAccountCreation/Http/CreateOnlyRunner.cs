using System.Net;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace UC004Trial;

// Values come from the hashed, owner-approved fixture. Credentials remain in local environment only.
public sealed record HttpFixture(string Origin, string CertificateSha256, string DeploymentApproval,
    string DeployedApiSha256, Guid DatabaseGuid, string LeaseId, string Prefix,
    long BranchId, long DepartmentId, long OtherBranchId, long OtherDepartmentId,
    HttpActor Allowed, HttpActor Denied, HttpActor BranchBound, string[] AllowedOperations);
public sealed record HttpActor(long UserId, string Username, string PasswordEnvironment);

public static class CreateOnlyRunner
{
    public static readonly string[] Operations = ["AUTH_LOGIN", "UC004_CREATE", "UC004_DUPLICATE_CONCURRENCY", "SQL_READ_BACK"];

    public static Uri Validate(HttpFixture f, Receipt r)
    {
        TrialAdmission.Require(Uri.TryCreate(f.Origin, UriKind.Absolute, out var origin)
            && origin.Scheme == "https" && origin.UserInfo == "" && origin.AbsolutePath == "/"
            && origin.Query == "" && origin.Fragment == "", "APPROVED_HTTPS_ORIGIN_REQUIRED");
        TrialAdmission.Require(Regex.IsMatch(f.CertificateSha256, @"\A[0-9A-Fa-f]{64}\z")
            && !string.IsNullOrWhiteSpace(f.DeploymentApproval) && f.DeployedApiSha256 == r.ApiSha256
            && f.DatabaseGuid == r.DatabaseGuid && f.LeaseId == r.LeaseId, "HTTP_DEPLOYMENT_BINDING_REQUIRED");
        TrialAdmission.Require(Regex.IsMatch(f.Prefix, @"\AUC004_[a-f0-9]{12}_\z")
            && f.BranchId > 0 && f.DepartmentId > 0 && f.OtherBranchId > 0 && f.OtherDepartmentId > 0
            && f.BranchId != f.OtherBranchId && f.DepartmentId != f.OtherDepartmentId, "HTTP_FIXTURE_SCOPE_REQUIRED");
        var actors = new[] { f.Allowed, f.Denied, f.BranchBound };
        TrialAdmission.Require(actors.All(a => a is not null && a.UserId > 0 && !string.IsNullOrWhiteSpace(a.Username)
            && Regex.IsMatch(a.PasswordEnvironment, @"\AUC004_SECRET_[A-Z0-9_]+\z"))
            && actors.Select(a => a.UserId).Distinct().Count() == 3, "SYNTHETIC_ACTORS_REQUIRED");
        TrialAdmission.Require(f.AllowedOperations is not null && f.AllowedOperations.Order().SequenceEqual(Operations.Order()), "HTTP_MUTATION_SCOPE_REQUIRED");
        return origin!;
    }

    public static bool CertificateAccepted(SslPolicyErrors errors, string? digest, string expected)
        => errors == SslPolicyErrors.None && digest is not null && digest.Equals(expected, StringComparison.OrdinalIgnoreCase);

    // No redirects, retry policy, automatic authentication, or certificate-validation fallback.
    public static HttpClient Client(Uri origin, string pin, CookieContainer? cookies = null) => new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = true,
        CookieContainer = cookies ?? new CookieContainer(),
        ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
            CertificateAccepted(errors, certificate?.GetCertHashString(HashAlgorithmName.SHA256), pin)
    })
    { BaseAddress = origin, Timeout = TimeSpan.FromSeconds(30) };

    public static async Task<HttpResponseMessage> SendAsync(HttpClient http, HttpMethod method, string path,
        object? body, string? csrf, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        TrialAdmission.Require(path is "/api/auth/csrf" or "/api/auth/login" or "/api/users", "HTTP_PATH_OUTSIDE_SCOPE");
        TrialAdmission.Require(path == "/api/auth/csrf" ? method == HttpMethod.Get : method == HttpMethod.Post,
            "HTTP_METHOD_OUTSIDE_SCOPE");
        using var request = new HttpRequestMessage(method, path);
        if (csrf is not null) request.Headers.Add(HotelOpsAntiforgery.HeaderName, csrf);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await http.SendAsync(request, ct); // Exactly one attempt; unknown outcome is not replayed.
    }

    public static async Task RunAsync(string[] args)
    {
        var admission = TrialAdmission.Load(args, "UC004_HTTP");
        var receipt = admission.Receipt;
        using var document = JsonDocument.Parse(File.ReadAllBytes(receipt.FixturePath));
        var fixture = document.RootElement.GetProperty("http").Deserialize<HttpFixture>()
            ?? throw new AdmissionFailure("HTTP_FIXTURE_REQUIRED");
        var origin = Validate(fixture, receipt);
        var actors = new[] { fixture.Allowed, fixture.Denied, fixture.BranchBound };
        var passwords = actors.Select(a => Environment.GetEnvironmentVariable(a.PasswordEnvironment)).ToArray();
        TrialAdmission.Require(passwords.All(p => !string.IsNullOrEmpty(p)), "SYNTHETIC_CREDENTIALS_REQUIRED");
        using var budget = admission.Budget(TimeSpan.FromMinutes(8));
        var ct = budget.Token;
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(admission.Connection.ConnectionString).Options);
        await TrialAdmission.VerifyAsync(db.Database.GetDbConnection(), admission, ct);
        TrialAdmission.Require(await new StartupSchemaValidator(db).ValidateAsync(ct) == SchemaCompatibility.Compatible, "SCHEMA_COMPATIBILITY_REQUIRED");
        foreach (var actor in actors)
            TrialAdmission.Require(await db.Users.AsNoTracking().AnyAsync(u => u.UserId == actor.UserId
                && u.Username == actor.Username && u.AccountStatus == "ACTIVE" && u.DeletedAt == null, ct), "LIVE_SYNTHETIC_ACTOR_MISMATCH");
        TrialAdmission.Require(!await db.Users.AnyAsync(u => u.EmployeeCode.StartsWith(fixture.Prefix), ct), "UNUSED_FIXTURE_NAMESPACE_REQUIRED");
        var checks = new List<string>();
        void Check(bool value, string name)
        {
            if (!value) throw new AdmissionFailure("CHECK_FAILED:" + name);
            checks.Add(name);
        }
        using var evidence = new FileStream(args[1], FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        var completed = false;
        try
        {
            using var allowed = Client(origin, fixture.CertificateSha256);
            using var denied = Client(origin, fixture.CertificateSha256);
            using var branch = Client(origin, fixture.CertificateSha256);
            using var anonymous = Client(origin, fixture.CertificateSha256);
            var invalidCookies = new CookieContainer();
            invalidCookies.Add(origin, new Cookie(HotelOpsCookieTicket.CookieName, "invalid-synthetic-ticket", "/") { Secure = true, HttpOnly = true });
            using var invalidSession = Client(origin, fixture.CertificateSha256, invalidCookies);
            async Task<string> Bootstrap(HttpClient client)
            {
                using var response = await SendAsync(client, HttpMethod.Get, "/api/auth/csrf", null, null, ct);
                Check(response.StatusCode == HttpStatusCode.OK, "csrf-bootstrap");
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                Check(json.RootElement.GetProperty("headerName").GetString() == HotelOpsAntiforgery.HeaderName, "csrf-header-contract");
                return json.RootElement.GetProperty("csrfToken").GetString()!;
            }
            async Task<string> Login(HttpClient client, int index)
            {
                var token = await Bootstrap(client);
                using var response = await SendAsync(client, HttpMethod.Post, "/api/auth/login",
                    new { username = actors[index].Username, password = passwords[index] }, token, ct);
                Check(response.StatusCode == HttpStatusCode.OK, "fixture-login");
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                return json.RootElement.GetProperty("csrfToken").GetString()!;
            }
            var allowedToken = await Login(allowed, 0);
            var deniedToken = await Login(denied, 1);
            var branchToken = await Login(branch, 2);
            var anonymousToken = await Bootstrap(anonymous);
            var invalidSessionToken = await Bootstrap(invalidSession);
            Dictionary<string, object?> Body(string suffix, bool other = false) => new()
            {
                ["employeeCode"] = fixture.Prefix + suffix,
                ["username"] = fixture.Prefix + suffix,
                ["email"] = fixture.Prefix + suffix + "@example.invalid",
                ["fullName"] = "Synthetic UC004 Trial",
                ["phone"] = null,
                ["branchId"] = other ? fixture.OtherBranchId : fixture.BranchId,
                ["departmentId"] = other ? fixture.OtherDepartmentId : fixture.DepartmentId,
                ["roleCode"] = "HK_STAFF"
            };
            async Task<string> Snapshot()
            {
                // An exclusive disposable fixture is required; unrelated concurrent changes invalidate this evidence.
                var values = new List<string>();
                foreach (var (table, key) in new[] { ("users", "user_id"), ("user_roles", "user_role_id"), ("user_branch_access", "access_id"), ("audit_logs", "audit_id") })
                {
                    await using var command = db.Database.GetDbConnection().CreateCommand();
                    // Fixed internal allowlist, never fixture-controlled identifiers. Hash only, never persist row data.
                    command.CommandText = $"SELECT * FROM dbo.[{table}] ORDER BY [{key}] FOR JSON PATH, INCLUDE_NULL_VALUES";
                    using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    await using var rows = await command.ExecuteReaderAsync(ct);
                    while (await rows.ReadAsync(ct)) digest.AppendData(System.Text.Encoding.UTF8.GetBytes(rows.GetString(0)));
                    values.Add(Convert.ToHexString(digest.GetHashAndReset()));
                }
                return string.Join(":", values);
            }
            async Task Negative(HttpClient client, string? token, Dictionary<string, object?> body, int status, string name)
            {
                TrialAdmission.Validate(receipt, receipt.Database, receipt.Mode, DateTimeOffset.UtcNow);
                var before = await Snapshot();
                using var response = await SendAsync(client, HttpMethod.Post, "/api/users", body, token, ct);
                Check((int)response.StatusCode == status, name);
                Check(!(await response.Content.ReadAsStringAsync(ct)).Contains("temporaryCredential", StringComparison.Ordinal), name + "-no-handoff");
                Check(await Snapshot() == before, name + "-domain-audit-unchanged");
            }
            await Negative(anonymous, anonymousToken, Body("anonymous"), 401, "missing-session");
            await Negative(invalidSession, invalidSessionToken, Body("invalidSession"), 401, "invalid-session");
            await Negative(denied, deniedToken, Body("denied"), 403, "permission-denial");
            await Negative(branch, branchToken, Body("scope", true), 403, "branch-scope-denial");
            await Negative(allowed, null, Body("csrf"), 400, "missing-csrf");
            var invalid = Body("invalid"); invalid["email"] = "invalid";
            await Negative(allowed, allowedToken, invalid, 400, "invalid-input");
            async Task VerifyCreated(HttpResponseMessage response, string code)
            {
                Check(response.StatusCode == HttpStatusCode.Created && response.Headers.CacheControl?.NoStore == true, "create-201-no-store");
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var root = json.RootElement;
                Check(root.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "temporaryCredential", "user" }), "handoff-envelope");
                var id = root.GetProperty("user").GetProperty("id").GetInt64();
                var credential = root.GetProperty("temporaryCredential");
                Check(credential.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "expiresAtUtc", "password" }), "credential-whitelist");
                var secret = credential.GetProperty("password").GetString()!;
                var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == id, ct);
                var hasher = new PasswordCredentialService(Options.Create(new PasswordHasherOptions()));
                Check(!string.IsNullOrEmpty(secret) && hasher.VerifyPassword(user, user.PasswordHash, secret) != PasswordVerificationResult.Failed,
                    "credential-matches-persisted-hash");
                Check(user.EmployeeCode == code && user.AccountStatus == "ACTIVE" && user.PrimaryBranchId == fixture.BranchId
                    && user.DepartmentId == fixture.DepartmentId && !string.IsNullOrEmpty(user.PasswordHash), "persisted-user");
                var audits = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "users" && a.EntityId == id.ToString()).ToArrayAsync(ct);
                Check(audits.Length == 2 && audits.All(a => a.ActorUserId == fixture.Allowed.UserId
                    && !(a.NewValues ?? "").Contains(secret) && !(a.NewValues ?? "").Contains(user.PasswordHash!)), "persisted-atomic-audits-redacted");
                Check(await db.UserRoles.CountAsync(r => r.UserId == id && r.IsActive && r.BranchId == fixture.BranchId, ct) == 1
                    && await db.UserBranchAccesses.CountAsync(a => a.UserId == id && a.IsPrimary && a.RevokedAt == null, ct) == 1, "persisted-initial-scope");
            }
            var success = Body("success");
            TrialAdmission.Validate(receipt, receipt.Database, receipt.Mode, DateTimeOffset.UtcNow);
            using (var response = await SendAsync(allowed, HttpMethod.Post, "/api/users", success, allowedToken, ct))
                await VerifyCreated(response, fixture.Prefix + "success");
            // Deliberate separate duplicate case, never transport retry after an unknown result.
            await Negative(allowed, allowedToken, success, 409, "duplicate-identity");
            TrialAdmission.Validate(receipt, receipt.Database, receipt.Mode, DateTimeOffset.UtcNow);
            var concurrent = Body("concurrent");
            var first = SendAsync(allowed, HttpMethod.Post, "/api/users", concurrent, allowedToken, ct);
            var second = SendAsync(allowed, HttpMethod.Post, "/api/users", concurrent, allowedToken, ct);
            var responses = await Task.WhenAll(first, second);
            try
            {
                Check(responses.Count(r => r.StatusCode == HttpStatusCode.Created) == 1
                    && responses.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1, "concurrent-one-winner");
                await VerifyCreated(responses.Single(r => r.StatusCode == HttpStatusCode.Created), fixture.Prefix + "concurrent");
                Check(await db.Users.CountAsync(u => u.EmployeeCode == fixture.Prefix + "concurrent", ct) == 1, "concurrent-persisted-uniqueness");
            }
            finally { foreach (var response in responses) response.Dispose(); }
            completed = true;
        }
        finally
        {
            // No response bodies, credentials, cookie/session tokens, SQL exceptions or connection strings in evidence.
            await JsonSerializer.SerializeAsync(evidence, new
            {
                completed,
                receipt.DatabaseGuid,
                receipt.LeaseId,
                receipt.ApiSha256,
                receipt.RunnerSha256,
                receipt.FixtureSha256,
                checks,
                storageFaultRollback = "NOT_RUN_REQUIRES_APPROVED_FAULT_PROFILE",
                expiredSession = "NOT_RUN_REQUIRES_PREEXPIRED_SESSION_FIXTURE",
                completeVerified = false
            }, cancellationToken: CancellationToken.None);
        }
    }
}
