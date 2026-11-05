using System.Net;
using System.Globalization;
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
using Microsoft.Extensions.DependencyInjection.Extensions;

using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
var ct = timeout.Token;
var count = 0;
void Check(bool ok, string name)
{
    if (!ok) throw new InvalidOperationException("FAIL: " + name);
    count++; Console.WriteLine("PASS: " + name);
}
const string permission = CleaningTaskResourceResolver.PermissionCode;
const string resourceType = CleaningTaskResourceResolver.ResourceType;
var now = DateTime.UtcNow;
var reader = new TestReader();
var identity = new TestIdentity();
var data = new TestData();
var resolver = new CleaningTaskResourceResolver(reader, new FixedClock(now));
var service = new PermissionService(identity, data, resolver, new FixedClock(now));
var assignment = new CleaningTaskAssignee(6, "ACTIVE", now.AddHours(-1), null);
CleaningTaskAccess Snapshot(params CleaningTaskAssignee[] assignments) => new(1, true, "IN_PROGRESS", assignments);
void Reset()
{
    reader.Tasks.Clear(); reader.Tasks[2] = Snapshot(assignment); reader.Failure = false;
    identity.Current = new(6, 100, false); data.Active = true; data.RoleActive = true; data.MixedRoles = false;
}
Task<PermissionEvaluation> Evaluate(long id = 2) => service.EvaluateResourceAsync(permission, new(resourceType, id), ct);
async Task Denied(string name, long id = 2) => Check(!(await Evaluate(id)).IsGranted, name);

Reset(); Check((await Evaluate()).IsGranted, "Real adapter and core: current assignee with OWN grant");
Check((await resolver.ResolveAsync(permission, new(resourceType, 2), 6, ct))?.BranchId == 1, "Branch comes from server resource reader");
await Denied("Missing resource denied", 999);
await Denied("Invalid resource identifier denied", 0);
identity.Current = new(7, 101, false); await Denied("Same branch without assignment is not OWN");
Reset(); reader.Tasks[2] = Snapshot(assignment with { Status = "REASSIGNED", EndedAt = now.AddMinutes(-1) }); await Denied("Reassigned former assignee denied");
foreach (var status in new[] { "CANCELLED", "COMPLETED", "UNKNOWN" })
{
    Reset(); reader.Tasks[2] = Snapshot(assignment with { Status = status }); await Denied("Non-active assignment denied: " + status);
}
Reset(); reader.Tasks[2] = Snapshot(assignment with { EndedAt = now.AddMinutes(-1) }); await Denied("Ended assignment cannot establish ownership");
Reset(); reader.Tasks[2] = Snapshot(assignment with { AssignedAt = now.AddMinutes(1) }); await Denied("Future assignment is not current");
Reset(); reader.Tasks[2] = Snapshot(); await Denied("No assignment denied");
Reset(); reader.Tasks[2] = Snapshot(assignment, assignment with { UserId = 7 }); await Denied("Multiple current assignees are ambiguous and denied");
Reset(); reader.Tasks[2] = Snapshot(assignment with { UserId = 7, Status = "REASSIGNED", EndedAt = now.AddMinutes(-1) }, assignment);
Check((await Evaluate()).IsGranted, "Closed history does not replace current assignee");
Reset(); reader.Tasks[2] = Snapshot(assignment) with { BranchId = 2 }; await Denied("Owner at resource branch2 cannot borrow role branch1");
Reset(); reader.Tasks[2] = Snapshot(assignment) with { BranchId = 0 }; await Denied("Unknown resource branch denied");
Reset(); reader.Tasks[2] = Snapshot(assignment) with { RoomActive = false }; await Denied("Inactive room denied");
foreach (var status in new[] { "PENDING", "COMPLETED", "CANCELLED", "UNKNOWN" })
{
    Reset(); reader.Tasks[2] = Snapshot(assignment) with { TaskStatus = status }; await Denied("Unsupported execution state denied: " + status);
}
Reset();
Check(!(await service.EvaluateResourceAsync("TASK_EXECUTE", new(resourceType, 2), ct)).IsGranted, "Permission/resource mismatch denied");
Check(!(await service.EvaluateResourceAsync(permission, new("operational_task", 2), ct)).IsGranted, "Unsupported resource type denied");
identity.Current = new(6, 100, true); await Denied("Restricted session denied");
Reset(); data.Active = false; await Denied("Inactive user denied");
Reset(); data.RoleActive = false; await Denied("Inactive role denied");
Reset(); data.MixedRoles = true; await Denied("Grant cannot combine with unrelated role branch assignment");
Reset(); reader.Failure = true;
Check((await Evaluate()).Decision == PermissionDecision.Unavailable, "Reader database failure fails closed through PermissionService");
Reset();
var unresolved = new PermissionService(identity, data, new UnresolvedPermissionResourceResolver(), new FixedClock(now));
Check(!(await unresolved.EvaluateResourceAsync(permission, new(resourceType, 2), ct)).IsGranted, "Missing resolver denies");
Check(!(await service.EvaluateResourceAsync(permission, new("", 2), ct)).IsGranted, "Incomplete context denied");
Check((await Evaluate()).IsGranted, "Before assignment revocation");
var calls = reader.Reads;
reader.Tasks[2] = Snapshot(assignment with { Status = "CANCELLED" });
await Denied("Assignment changes reflected on next evaluation without caching");
Check(reader.Reads > calls, "Reader queried again after assignment change");
Reset();

// Isolated HTTP integration: production adapter/core/handler/cookie events; persistence doubles only.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", Args = [] });
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddHotelOpsCookieInfrastructure(builder.Environment);
builder.Services.RemoveAll<CookieSessionValidator>(); builder.Services.RemoveAll<ICookieSessionValidator>(); builder.Services.RemoveAll<ICookieSessionToucher>();
builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
builder.Services.AddHotelOpsAntiforgery();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddSingleton(reader); builder.Services.AddScoped<ICleaningTaskAccessReader>(p => p.GetRequiredService<TestReader>());
builder.Services.AddScoped<IPermissionResourceResolver, CleaningTaskResourceResolver>();
builder.Services.AddScoped<IPermissionDataSource, HttpTestData>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IPermissionPolicyCatalog, TestCatalog>();
builder.Services.AddSingleton<TestSessions>();
builder.Services.AddScoped<ICookieSessionValidator>(p => p.GetRequiredService<TestSessions>());
builder.Services.AddScoped<ICookieSessionToucher>(p => p.GetRequiredService<TestSessions>());
builder.Services.AddHotelOpsPermissionAuthorization();
builder.Services.Configure<AuthorizationOptions>(o => o.AddPolicy("Extra", p => p.RequireClaim("extra-confirmation")));
await using var app = builder.Build();
app.UseMiddleware<AuthenticationExceptionMiddleware>();
app.UseRouting(); app.UseAuthentication();
app.UseMiddleware<InvalidCookieMiddleware>(); app.UseMiddleware<ApiCsrfMiddleware>(); app.UseMiddleware<RestrictedSessionMiddleware>();
app.Use(async (http, next) =>
{
    // The route ID is just a lookup key. Branch/ownership are never extracted from client data.
    if (long.TryParse(http.Request.RouteValues["id"]?.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        http.Features.Set(new PermissionResource(resourceType, id));
    await next();
});
app.UseAuthorization(); app.UseMiddleware<SessionTouchMiddleware>();
app.MapGet("/issue/{user:long}", async (long user, HttpContext http, TestSessions sessions) =>
{
    var ticket = sessions.Issue(user);
    await http.SignInAsync(ticket.AuthenticationScheme, ticket.Principal, ticket.Properties);
    return Results.NoContent();
}).AllowAnonymous();
var policy = PermissionPolicy.Name(permission);
app.MapGet("/api/cleaning/{id:long}", () => Results.Ok()).RequireAuthorization(policy);
app.MapGet("/api/multiple/{id:long}", () => Results.Ok()).RequireAuthorization(policy, "Extra");
app.MapGet("/api/no-context", () => Results.Ok()).RequireAuthorization(policy);
reader.Tasks[2] = Snapshot(assignment with { UserId = 7, Status = "REASSIGNED", EndedAt = now.AddMinutes(-1) }, assignment);
reader.Tasks[3] = Snapshot(assignment) with { BranchId = 2 };
reader.Tasks[9] = Snapshot(assignment with { UserId = 3 });
await app.StartAsync(ct);
try
{
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    using var client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
    var cookies = new Dictionary<long, string>();
    async Task Expect(long? user, string path, HttpStatusCode expected, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (user is { } userId)
        {
            if (!cookies.TryGetValue(userId, out var cookie))
            {
                using var issued = await client.GetAsync("/issue/" + userId, ct);
                if (issued.StatusCode != HttpStatusCode.NoContent) throw new InvalidOperationException("Test cookie issuance failed");
                cookie = issued.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith(HotelOpsCookieTicket.CookieName + "=", StringComparison.Ordinal)).Split(';')[0];
                cookies[userId] = cookie;
            }
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }
        using var response = await client.SendAsync(request, ct);
        Check(response.StatusCode == expected && response.Headers.Location is null, name);
    }
    await Expect(null, "/api/cleaning/2", HttpStatusCode.Unauthorized, "HTTP anonymous: 401");
    await Expect(6, "/api/cleaning/2", HttpStatusCode.OK, "HTTP current assignee + real adapter/core/handler: 200");
    await Expect(7, "/api/cleaning/2", HttpStatusCode.Forbidden, "HTTP former reassigned user: 403");
    await Expect(6, "/api/cleaning/999", HttpStatusCode.Forbidden, "HTTP absent resource: 403");
    await Expect(6, "/api/cleaning/3?branchId=1&ownerId=6", HttpStatusCode.Forbidden, "HTTP forged branch cannot move resource into role scope");
    await Expect(6, "/api/cleaning/2?branchId=2&ownerId=999", HttpStatusCode.OK, "HTTP client branch/owner values ignored in favor of actual resource facts");
    await Expect(7, "/api/cleaning/2?branchId=1&ownerId=7", HttpStatusCode.Forbidden, "HTTP forged owner cannot establish assignment");
    await Expect(8, "/api/cleaning/2", HttpStatusCode.Forbidden, "HTTP multiple-role mixing denied");
    await Expect(9, "/api/cleaning/2", HttpStatusCode.Forbidden, "HTTP restricted session denied");
    await Expect(2, "/api/cleaning/2", HttpStatusCode.Forbidden, "HTTP inactive user denied");
    await Expect(3, "/api/cleaning/9", HttpStatusCode.Forbidden, "HTTP inactive role with valid assignment denied");
    await Expect(10, "/api/cleaning/2", HttpStatusCode.Forbidden, "HTTP ALL grant cannot bypass current-assignee rule");
    await Expect(6, "/api/no-context?branchId=1&ownerId=6", HttpStatusCode.Forbidden, "HTTP missing resource context denied");
    await Expect(6, "/api/multiple/2", HttpStatusCode.Forbidden, "HTTP resource permission does not bypass other requirements");
    reader.Failure = true;
    await Expect(6, "/api/cleaning/2", HttpStatusCode.Forbidden, "HTTP reader database error never authorizes");
    reader.Failure = false;
    reader.Tasks[2] = Snapshot(assignment with { Status = "REASSIGNED", EndedAt = now });
    await Expect(6, "/api/cleaning/2", HttpStatusCode.Forbidden, "HTTP assignment revocation reflected at next request");
}
finally { await app.StopAsync(CancellationToken.None); }

if (args.Contains("--live"))
{
    var candidate = await HotelOps.Verification.ReadOnlyVerification.OpenOrReportAsync(ct);
    if (candidate is null) { Environment.ExitCode = 2; return; }
    await using var db = candidate;
    await db.Database.OpenConnectionAsync(ct);
    await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = "SELECT DB_NAME()";
    HotelOps.Verification.ReadOnlyVerification.AssertSelect(command.CommandText);
    Check((string?)await command.ExecuteScalarAsync(ct) == "HotelOpsDB", "Live target verified");
    var sqlReader = new EfCleaningTaskAccessReader(db);
    var sqlResolver = new CleaningTaskResourceResolver(sqlReader, TimeProvider.System);
    var actual = await sqlReader.ReadAsync(2, ct);
    Check(actual is { BranchId: 1, TaskStatus: "IN_PROGRESS" } && actual.Assignments.Any(a => a.UserId == 6 && a.Status == "ACTIVE" && a.EndedAt is null), "Live resource task2 branch1/current assignee6");
    Check(actual!.Assignments.Any(a => a.UserId == 7 && a.Status == "REASSIGNED" && a.EndedAt is not null), "Live previous assignment7 is closed/reassigned");
    Check((await sqlResolver.ResolveAsync(permission, new(resourceType, 2), 6, ct)) is { BranchId: 1, HasOwnership: true }, "Live EF adapter establishes current assignment");
    Check(await sqlResolver.ResolveAsync(permission, new(resourceType, 2), 7, ct) is null, "Live EF adapter denies previous assignee");
    identity.Current = new(6, 100, false);
    var live = new PermissionService(identity, new EfPermissionDataSource(db), sqlResolver, TimeProvider.System);
    Check((await live.EvaluateResourceAsync(permission, new(resourceType, 2), ct)).IsGranted, "Live EF resource + grants integration permits assigned HK staff6");
    identity.Current = new(7, 100, false);
    Check(!(await live.EvaluateResourceAsync(permission, new(resourceType, 2), ct)).IsGranted, "Live valid grant without current assignment is denied");
    Check(await sqlResolver.ResolveAsync("ROOM_VIEW", new(resourceType, 2), 6, ct) is null, "Live incompatible permission denied");
    Check(await sqlReader.ReadAsync(long.MaxValue, ct) is null, "Live absent resource read-only lookup");
    Check(!db.ChangeTracker.Entries().Any(), "Live queries track no entities");
}
Console.WriteLine($"PASS: {count} resource authorization checks; HTTP uses synthetic cookie/session/data with production adapter/core/handler; live SQL is SELECT only.");

sealed class FixedClock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero); }
sealed class TestIdentity : ICurrentUserAccessor
{
    public CurrentUserInfo Current { get; set; }
    public bool TryGetCurrentUser(out CurrentUserInfo user) { user = Current; return true; }
}
sealed class TestReader : ICleaningTaskAccessReader
{
    public Dictionary<long, CleaningTaskAccess> Tasks { get; } = [];
    public bool Failure { get; set; }
    public int Reads { get; private set; }
    public Task<CleaningTaskAccess?> ReadAsync(long cleaningTaskId, CancellationToken cancellationToken)
    {
        Reads++;
        if (Failure) throw new TimeoutException("Synthetic resource read failure");
        return Task.FromResult(Tasks.GetValueOrDefault(cleaningTaskId));
    }
}
sealed class TestData : IPermissionDataSource
{
    public bool Active { get; set; } = true;
    public bool RoleActive { get; set; } = true;
    public bool MixedRoles { get; set; }
    public Task<bool> IsActiveUserAsync(long userId, CancellationToken cancellationToken) => Task.FromResult(Active);
    public Task<bool> IsActiveBranchAsync(long branchId, CancellationToken cancellationToken) => Task.FromResult(branchId is 1 or 2);
    public Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long userId, string permissionCode, DateTime utcNow, CancellationToken cancellationToken)
    {
        var path = new PermissionGrantPath(CleaningTaskResourceResolver.PermissionCode, "OWN", MixedRoles ? 2 : 1, true, RoleActive, utcNow.AddDays(-1), null, true, true);
        return Task.FromResult<IReadOnlyList<PermissionGrantPath>>(MixedRoles
            ? [path, path with { PermissionCode = "ROOM_VIEW", BranchId = 1 }] : [path]);
    }
}
sealed class HttpTestData : IPermissionDataSource
{
    public Task<bool> IsActiveUserAsync(long userId, CancellationToken cancellationToken) => Task.FromResult(userId != 2);
    public Task<bool> IsActiveBranchAsync(long branchId, CancellationToken cancellationToken) => Task.FromResult(branchId is 1 or 2);
    public Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long userId, string permissionCode, DateTime utcNow, CancellationToken cancellationToken)
    {
        var path = new PermissionGrantPath(CleaningTaskResourceResolver.PermissionCode, userId == 10 ? "ALL" : "OWN", userId == 10 ? null : userId == 8 ? 2 : 1, true, userId != 3, utcNow.AddDays(-1), null, true, true);
        return Task.FromResult<IReadOnlyList<PermissionGrantPath>>(userId == 8
            ? [path, path with { PermissionCode = "ROOM_VIEW", BranchId = 1 }] : [path]);
    }
}
sealed class TestCatalog : IPermissionPolicyCatalog
{
    public Task<bool> ContainsAsync(string permissionCode, CancellationToken cancellationToken) => Task.FromResult(permissionCode == CleaningTaskResourceResolver.PermissionCode);
}
sealed class TestSessions : ICookieSessionValidator, ICookieSessionToucher
{
    private readonly Dictionary<long, CreatedSqlSession> sessions = [];
    public AuthenticationTicket Issue(long user)
    {
        var created = DateTime.UtcNow.AddSeconds(-1); var restricted = user == 9;
        var expiry = created.Add(restricted ? SqlSessionPolicy.RestrictedLifetime : TimeSpan.FromHours(1));
        var reference = SessionReference.FromProtectedTicket(user, SessionCredential.Generate(), restricted, created);
        var session = new CreatedSqlSession(reference, new SessionSnapshot(user + 100, user, created, expiry, expiry, restricted));
        sessions[user] = session;
        return HotelOpsCookieTicket.Create(session, TimeProvider.System);
    }
    public Task<SessionValidationResult> ValidateAsync(SessionReference ticket, CancellationToken cancellationToken)
    {
        var ok = sessions.TryGetValue(ticket.UserId, out var session) && session.Ticket.CreatedAtUtc == ticket.CreatedAtUtc
            && session.Ticket.ExportCredentialForProtectedTicket() == ticket.ExportCredentialForProtectedTicket();
        return Task.FromResult(new SessionValidationResult(ok, ok ? session!.Session : null));
    }
    public Task<SessionValidationResult> TouchAsync(SessionReference ticket, CancellationToken cancellationToken) => ValidateAsync(ticket, cancellationToken);
}
