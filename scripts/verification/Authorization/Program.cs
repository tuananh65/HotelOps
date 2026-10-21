using System.Net;
using System.Security.Claims;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Middleware;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;

using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
var ct = deadline.Token;
var checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + label);
    checks++;
    Console.WriteLine("PASS: " + label);
}
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", Args = [] });
builder.Logging.ClearProviders(); // Never emit tickets, cookies, secret-bearing headers or provider error payloads.
builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
builder.Services.AddHotelOpsCookieInfrastructure(builder.Environment);
builder.Services.RemoveAll<CookieSessionValidator>();
builder.Services.RemoveAll<ICookieSessionValidator>();
builder.Services.RemoveAll<ICookieSessionToucher>();
// Isolated test host: no persistent key files and no production SQL session store.
builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
builder.Services.AddHotelOpsAntiforgery();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddSingleton<TestStore>();
builder.Services.AddScoped<IPermissionDataSource>(p => p.GetRequiredService<TestStore>());
builder.Services.AddScoped<IPermissionResourceResolver, TestResourceResolver>();
builder.Services.AddSingleton<TestCatalog>();
builder.Services.AddScoped<IPermissionPolicyCatalog>(p => p.GetRequiredService<TestCatalog>());
builder.Services.AddSingleton<TestSessions>();
builder.Services.AddScoped<ICookieSessionValidator>(p => p.GetRequiredService<TestSessions>());
builder.Services.AddScoped<ICookieSessionToucher>(p => p.GetRequiredService<TestSessions>());
builder.Services.AddHotelOpsPermissionAuthorization();
builder.Services.Configure<AuthorizationOptions>(o =>
{
    o.AddPolicy("ExternalClaim", p => p.RequireAuthenticatedUser().RequireClaim("external-confirmed"));
    o.FallbackPolicy = new AuthorizationPolicyBuilder(HotelOpsCookieTicket.Scheme)
        .RequireAuthenticatedUser().RequireClaim("fallback-confirmed").Build();
});
await using var app = builder.Build();
app.UseMiddleware<AuthenticationExceptionMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<InvalidCookieMiddleware>();
app.UseMiddleware<ApiCsrfMiddleware>();
app.UseMiddleware<RestrictedSessionMiddleware>();
// Only test backend constants create contexts. No route/query/header branch or ownership is consumed.
app.Use(async (http, next) =>
{
    if (http.Request.Path is var p && (p == "/api/branch" || p == "/api/multiple" || p == "/api/write"))
        http.Features.Set(new PermissionBranchContext(1));
    if (http.Request.Path == "/api/other-branch") http.Features.Set(new PermissionBranchContext(2));
    if (http.Request.Path == "/api/resource") http.Features.Set(new PermissionResource("test-assignment", 1));
    if (http.Request.Path == "/api/not-owned") http.Features.Set(new PermissionResource("test-assignment", 2));
    if (http.Request.Path == "/api/unresolved") http.Features.Set(new PermissionResource("unknown", 1));
    await next();
});
app.UseAuthorization();
app.UseMiddleware<SessionTouchMiddleware>();
var branchPolicy = PermissionPolicy.Name("ROOM_VIEW", PermissionCheck.Branch);
var resourcePolicy = PermissionPolicy.Name("TASK_EXECUTE");
var globalPolicy = PermissionPolicy.Name("USER_VIEW", PermissionCheck.Global);
app.MapGet("/issue/{userId:long}", async (long userId, HttpContext http, TestSessions sessions) =>
{
    var ticket = sessions.Issue(userId);
    // Untrusted role claims never authorize. Cookie events replace the principal with canonical fields.
    ((ClaimsIdentity)ticket.Principal.Identity!).AddClaim(new Claim(ClaimTypes.Role, "SYS_ADMIN"));
    await http.SignInAsync(ticket.AuthenticationScheme, ticket.Principal, ticket.Properties);
    return Results.NoContent();
}).AllowAnonymous();
app.MapGet("/api/branch", () => Results.Ok()).RequireAuthorization(branchPolicy);
app.MapGet("/api/other-branch", () => Results.Ok()).RequireAuthorization(branchPolicy);
app.MapGet("/api/no-context", () => Results.Ok()).RequireAuthorization(branchPolicy);
app.MapGet("/api/global", () => Results.Ok()).RequireAuthorization(globalPolicy);
app.MapGet("/api/resource", () => Results.Ok()).RequireAuthorization(resourcePolicy);
app.MapGet("/api/not-owned", () => Results.Ok()).RequireAuthorization(resourcePolicy);
app.MapGet("/api/unresolved", () => Results.Ok()).RequireAuthorization(resourcePolicy);
app.MapGet("/api/multiple", () => Results.Ok()).RequireAuthorization(branchPolicy, "ExternalClaim");
app.MapGet("/api/external", () => Results.Ok()).RequireAuthorization("ExternalClaim");
app.MapGet("/api/default", () => Results.Ok()).RequireAuthorization();
app.MapGet("/api/fallback", () => Results.Ok());
app.MapGet("/api/missing-policy", () => Results.Ok()).RequireAuthorization("HotelOps.Permission:Global:DOES_NOT_EXIST");
app.MapPost("/api/write", () => Results.Ok()).RequireAuthorization(branchPolicy);
await app.StartAsync(ct);
try
{
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    using var client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
    var catalog = app.Services.GetRequiredService<TestCatalog>();
    var sessions = app.Services.GetRequiredService<TestSessions>();
    var provider = app.Services.GetRequiredService<IAuthorizationPolicyProvider>();
    var options = app.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
    Check(!provider.AllowsCachingPolicies, "Dynamic permission policies do not cache catalog decisions");
    Check(ReferenceEquals(await provider.GetDefaultPolicyAsync(), options.DefaultPolicy), "Default policy preserved");
    Check(ReferenceEquals(await provider.GetFallbackPolicyAsync(), options.FallbackPolicy), "Fallback policy preserved");
    Check(await provider.GetPolicyAsync("ExternalClaim") is not null, "External named policy preserved");
    foreach (var name in new[] { "missing", "HotelOps.Permission:Global:DOES_NOT_EXIST", "HotelOps.Permission:0:USER_VIEW", "HotelOps.Permission:global:USER_VIEW", "HotelOps.Permission:Global:user_view", "HotelOps.Permission:Global:USER_VIEW:extra" })
        Check(await provider.GetPolicyAsync(name) is null, "Invalid/unknown policy is not constructed: " + name);
    var generated = await provider.GetPolicyAsync(globalPolicy);
    Check(generated is not null && generated.AuthenticationSchemes.SequenceEqual([HotelOpsCookieTicket.Scheme])
        && generated.Requirements.OfType<PermissionRequirement>().Count() == 1, "Generated policy retains cookie authentication and permission requirement");
    catalog.Codes.Remove("USER_VIEW");
    Check(await provider.GetPolicyAsync(globalPolicy) is null, "Catalog removal reflected at next policy lookup");
    catalog.Codes.Add("USER_VIEW");

    var cookies = new Dictionary<long, string>();
    async Task<string> Cookie(long user)
    {
        if (cookies.TryGetValue(user, out var existing)) return existing;
        using var response = await client.GetAsync("/issue/" + user, ct);
        Check(response.StatusCode == HttpStatusCode.NoContent, "Issue synthetic cookie for test identity " + user);
        var value = response.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith(HotelOpsCookieTicket.CookieName + "=", StringComparison.Ordinal)).Split(';')[0];
        cookies[user] = value;
        return value;
    }
    async Task Expect(string path, long? user, HttpStatusCode status, string label, string? code = null, HttpMethod? method = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
        if (user is { } id) request.Headers.TryAddWithoutValidation("Cookie", await Cookie(id));
        using var response = await client.SendAsync(request, ct);
        Check(response.StatusCode == status && response.Headers.Location is null, label);
        if (code is not null)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            Check(json.RootElement.GetProperty("code").GetString() == code
                && response.Content.Headers.ContentType?.MediaType == "application/problem+json", label + " problem contract");
        }
    }
    await Expect("/api/branch", null, HttpStatusCode.Unauthorized, "Anonymous protected request: 401", "AUTHENTICATION_REQUIRED");
    await Expect("/api/branch", 1, HttpStatusCode.OK, "Authenticated valid branch permission: 200");
    await Expect("/api/branch", 2, HttpStatusCode.Forbidden, "Inactive account: 403", "FORBIDDEN");
    await Expect("/api/branch", 3, HttpStatusCode.Forbidden, "Inactive role: 403");
    await Expect("/api/branch", 4, HttpStatusCode.Forbidden, "Missing role permission despite SYS_ADMIN claim: 403");
    await Expect("/api/other-branch", 1, HttpStatusCode.Forbidden, "Cross-branch: 403");
    await Expect("/api/no-context?branchId=1&ownerId=1", 1, HttpStatusCode.Forbidden, "Client context cannot replace backend context: 403");
    await Expect("/api/branch", 6, HttpStatusCode.Forbidden, "Grant from another role branch cannot be combined: 403");
    await Expect("/api/branch", 7, HttpStatusCode.Forbidden, "SYS_ADMIN explicit administrative grant is not an operational bypass");
    await Expect("/api/global", 7, HttpStatusCode.OK, "Explicit global ALL grant: 200");
    await Expect("/api/branch", 8, HttpStatusCode.Forbidden, "OWNER reporting grant cannot authorize operation");
    await Expect("/api/resource", 5, HttpStatusCode.Forbidden, "OWN without verified ownership: 403");
    await Expect("/api/resource", 11, HttpStatusCode.OK, "OWN with authoritative test resolver and branch: 200");
    await Expect("/api/not-owned", 11, HttpStatusCode.Forbidden, "Resource not assigned to user: 403");
    await Expect("/api/unresolved", 11, HttpStatusCode.Forbidden, "Missing ownership adapter/context: 403");
    await Expect("/api/branch", 9, HttpStatusCode.Forbidden, "Database timeout result is never Authorized");
    await Expect("/api/branch", 13, HttpStatusCode.InternalServerError, "Unexpected service exception is generic failure", "INTERNAL_SERVER_ERROR");
    await Expect("/api/branch", 10, HttpStatusCode.Forbidden, "Restricted session blocked by existing middleware", "PASSWORD_CHANGE_REQUIRED");
    await Expect("/api/multiple", 1, HttpStatusCode.Forbidden, "Permission success does not bypass second authorization requirement");
    await Expect("/api/external", 1, HttpStatusCode.Forbidden, "External named policy still enforced");
    await Expect("/api/default", 1, HttpStatusCode.OK, "Existing default authorization policy preserved");
    await Expect("/api/fallback", 1, HttpStatusCode.Forbidden, "Existing fallback authorization policy preserved");
    await Expect("/api/missing-policy", 1, HttpStatusCode.InternalServerError, "Unknown policy fails as endpoint configuration error, never permit-all", "INTERNAL_SERVER_ERROR");
    await Expect("/api/write", 1, HttpStatusCode.BadRequest, "CSRF middleware remains before authorization", "CSRF_VALIDATION_FAILED", HttpMethod.Post);
    catalog.Fail = true;
    await Expect("/api/global", 7, HttpStatusCode.InternalServerError, "Catalog query exception fails closed", "INTERNAL_SERVER_ERROR");
    catalog.Fail = false;
    using (var invalid = new HttpRequestMessage(HttpMethod.Get, "/api/branch"))
    {
        invalid.Headers.TryAddWithoutValidation("Cookie", HotelOpsCookieTicket.CookieName + "=invalid-test-cookie");
        using var response = await client.SendAsync(invalid, ct);
        Check(response.StatusCode == HttpStatusCode.Unauthorized, "Invalid protected cookie rejected: 401");
    }
    Check(sessions.Validations > 0 && sessions.Touches > 0, "Real cookie events and session-touch middleware use test session store");

    // Imperative authorization uses the exact request principal and typed backend target.
    using (var scope = app.Services.CreateScope())
    {
        var http = new DefaultHttpContext();
        var ticket = sessions.Issue(1);
        http.User = ticket.Principal;
        http.Items[typeof(SessionSnapshot)] = sessions.Snapshots[1];
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = http;
        try
        {
            var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
            Check((await authorization.AuthorizeAsync(http.User, new PermissionBranchContext(1), branchPolicy)).Succeeded, "Imperative authorization with typed backend branch context");
            Check(!(await authorization.AuthorizeAsync(new ClaimsPrincipal(http.User), new PermissionBranchContext(1), branchPolicy)).Succeeded, "Different supplied principal cannot borrow validated request identity");
            http.Items.Clear();
            Check(!(await authorization.AuthorizeAsync(http.User, new PermissionBranchContext(1), branchPolicy)).Succeeded, "Missing validated snapshot cannot authorize");
        }
        finally { accessor.HttpContext = null; }
    }
    if (args.Contains("--live"))
    {
        var candidate = await HotelOps.Verification.ReadOnlyVerification.OpenOrReportAsync(ct);
        if (candidate is null) { Environment.ExitCode = 2; return; }
        await using var db = candidate;
        await db.Database.OpenConnectionAsync(ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT DB_NAME()";
        HotelOps.Verification.ReadOnlyVerification.AssertSelect(command.CommandText);
        Check((string?)await command.ExecuteScalarAsync(ct) == "HotelOpsDB", "Live policy catalog target");
        var sqlCatalog = new EfPermissionPolicyCatalog(db);
        Check(await sqlCatalog.ContainsAsync("ROOM_VIEW", ct), "Live valid permission code");
        Check(!await sqlCatalog.ContainsAsync("DOES_NOT_EXIST", ct), "Live nonexistent code rejected");
        Check(!await sqlCatalog.ContainsAsync("room_view", ct), "Live ordinal code validation under SQL collation");
    }
    Console.WriteLine($"PASS: {checks} authorization checks; loopback HTTP uses real cookie/middleware + doubles, not production SQL sessions or business ownership adapters.");
}
finally { await app.StopAsync(CancellationToken.None); }

sealed class TestCatalog : IPermissionPolicyCatalog
{
    public HashSet<string> Codes { get; } = ["ROOM_VIEW", "TASK_EXECUTE", "USER_VIEW", "REPORT_VIEW"];
    public bool Fail { get; set; }
    public Task<bool> ContainsAsync(string permissionCode, CancellationToken cancellationToken) =>
        Fail ? throw new TimeoutException("Synthetic catalog failure") : Task.FromResult(Codes.Contains(permissionCode));
}
sealed class TestStore : IPermissionDataSource
{
    public Task<bool> IsActiveUserAsync(long userId, CancellationToken cancellationToken) => userId switch
    {
        9 => throw new TimeoutException("Synthetic SQL timeout"),
        13 => throw new InvalidOperationException("Synthetic programming error"),
        _ => Task.FromResult(userId != 2)
    };
    public Task<bool> IsActiveBranchAsync(long branchId, CancellationToken cancellationToken) => Task.FromResult(branchId is 1 or 2);
    public Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long userId, string permissionCode, DateTime utcNow, CancellationToken cancellationToken)
    {
        PermissionGrantPath Path(string code, string scope = "BRANCH", long? branch = 1, bool active = true) =>
            new(code, scope, branch, true, active, utcNow.AddDays(-1), null, true, true);
        PermissionGrantPath[] paths = userId switch
        {
            3 => [Path("ROOM_VIEW", active: false)],
            4 => [],
            5 or 11 => [Path("TASK_EXECUTE", "OWN")],
            6 => [Path("ROOM_VIEW", branch: 2), Path("TASK_EXECUTE")],
            7 => [Path("USER_VIEW", "ALL", null)],
            8 => [Path("REPORT_VIEW", "ALL", null)],
            _ => [Path("ROOM_VIEW")]
        };
        return Task.FromResult<IReadOnlyList<PermissionGrantPath>>(paths.Where(p => p.PermissionCode == permissionCode).ToArray());
    }
}
sealed class TestResourceResolver : IPermissionResourceResolver
{
    public Task<VerifiedPermissionResource?> ResolveAsync(string permissionCode, PermissionResource resource, long currentUserId, CancellationToken cancellationToken) =>
        Task.FromResult(resource.ResourceType == "test-assignment" && permissionCode == "TASK_EXECUTE"
            ? new VerifiedPermissionResource(1, currentUserId == 11 && resource.ResourceId == 1) : null);
}
sealed class TestSessions : ICookieSessionValidator, ICookieSessionToucher
{
    public Dictionary<long, SessionSnapshot> Snapshots { get; } = [];
    private readonly Dictionary<long, SessionReference> references = [];
    public int Validations { get; private set; }
    public int Touches { get; private set; }
    public AuthenticationTicket Issue(long userId)
    {
        var created = DateTime.UtcNow.AddSeconds(-1);
        var restricted = userId == 10;
        var expiry = created.Add(restricted ? SqlSessionPolicy.RestrictedLifetime : TimeSpan.FromHours(1));
        var snapshot = new SessionSnapshot(userId + 100, userId, created, expiry, expiry, restricted);
        var reference = SessionReference.FromProtectedTicket(userId, SessionCredential.Generate(), restricted, created);
        Snapshots[userId] = snapshot; references[userId] = reference;
        return HotelOpsCookieTicket.Create(new CreatedSqlSession(reference, snapshot), TimeProvider.System);
    }
    public Task<SessionValidationResult> ValidateAsync(SessionReference ticket, CancellationToken cancellationToken)
    {
        Validations++;
        var valid = references.TryGetValue(ticket.UserId, out var expected)
            && expected.CreatedAtUtc == ticket.CreatedAtUtc && expected.IsRestricted == ticket.IsRestricted
            && expected.ExportCredentialForProtectedTicket() == ticket.ExportCredentialForProtectedTicket();
        return Task.FromResult(new SessionValidationResult(valid, valid ? Snapshots[ticket.UserId] : null));
    }
    public Task<SessionValidationResult> TouchAsync(SessionReference ticket, CancellationToken cancellationToken)
    {
        Touches++;
        return ValidateAsync(ticket, cancellationToken);
    }
}
