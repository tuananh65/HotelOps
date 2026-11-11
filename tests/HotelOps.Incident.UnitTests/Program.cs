using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Entities;
using HotelOps.Api.Middleware;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Incidents;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

var count = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL " + name); count++; Console.WriteLine("PASS " + name); }
async Task Denied(Func<Task> action, int status, string name)
{
    try { await action(); } catch (IncidentAccessException ex) { Check(ex.StatusCode == status, name); return; }
    throw new Exception("FAIL did not reject " + name);
}
var noSql = new NoSql();
await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
    .UseSqlServer("Server=invalid;Database=NoSql;Integrated Security=true;TrustServerCertificate=true")
    .AddInterceptors(noSql).Options);
var now = DateTime.UtcNow;
var user = new FixtureUser();
var data = new FixtureData();
var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
var permissions = new PermissionService(user, data, new UnresolvedPermissionResourceResolver(), TimeProvider.System);
var authority = new IncidentCreateAuthority(db, user, http, permissions, TimeProvider.System);
void Session(bool restricted = false)
{
    var reference = SessionReference.FromProtectedTicket(7, SessionCredential.Generate(), restricted, now);
    var snapshot = new SessionSnapshot(9, 7, now, now.AddMinutes(10), now.AddMinutes(15), restricted);
    http.HttpContext!.User = HotelOpsCookieTicket.Create(new(reference, snapshot), TimeProvider.System).Principal;
    user.Value = new(7, 9, restricted);
}
await Denied(() => Task.FromResult(authority.Actor(null)), 401, "missing server session denied");
user.Value = new(7, 9, false);
await Denied(() => Task.FromResult(authority.Actor(null)), 401, "actor accessor without valid cookie denied");
Session(true);
await Denied(() => Task.FromResult(authority.Actor(null)), 403, "restricted session denied");
Session();
Check(authority.Actor(null) == 7, "reporter derived from server session when omitted");
Check(authority.Actor(7) == 7, "matching legacy reporter remains compatible");
await Denied(() => Task.FromResult(authority.Actor(99)), 403, "forged legacy reporter rejected");
user.Value = new(8, 9, false);
await Denied(() => Task.FromResult(authority.Actor(null)), 401, "cookie and actor mismatch rejected");
Session();
PermissionGrantPath Grant(string scope, long? branch) => new("INCIDENT_CREATE", scope, branch, true, true, now.AddMinutes(-1), null, true, true);
data.Paths = [Grant("BRANCH", 1)];
Check(await authority.CanCreateAsync(1, default), "create-only actor authorized without INCIDENT_VIEW");
Check(!await authority.CanCreateAsync(2, default), "cross-branch permission rejected");
data.Paths = [Grant("ALL", null)];
Check(await authority.CanCreateAsync(2, default), "existing global ALL permission supported");
foreach (var grant in new[] { Grant("OWN", 1), Grant("DEPARTMENT", 1), Grant("BRANCH", null),
    Grant("BRANCH", 1) with { AssignmentActive = false }, Grant("BRANCH", 1) with { RoleActive = false },
    Grant("BRANCH", 1) with { BranchAccessActive = false }, Grant("BRANCH", 1) with { BranchActive = false },
    Grant("BRANCH", 1) with { ExpiresAt = now }, Grant("BRANCH", 1) with { AssignedAt = now.AddDays(1) },
    Grant("BRANCH", 1) with { PermissionCode = "INCIDENT_VIEW" } })
{
    data.Paths = [grant]; Check(!await authority.CanCreateAsync(1, default), "invalid grant fails closed: " + grant);
}
data.Paths = [Grant("BRANCH", 1)];
Check(await authority.CanCreateAsync(1, default), "eligibility hint initially granted");
data.Paths = [];
Check(!await authority.CanCreateAsync(1, default), "revoked grant re-read after eligibility hint");
data.Paths = [Grant("BRANCH", 1)]; data.Active = false;
Check(!await authority.CanCreateAsync(1, default), "inactive user permission denied"); data.Active = true;
var service = new IncidentService(db, user, http, permissions, TimeProvider.System);
data.Paths = [];
var request = new CreateIncidentRequest { BranchId = 1, RoomId = 1, IncidentTitle = "Fault", Description = "Broken", DiscoveredAt = now };
await Denied(async () => await service.CreateIncidentAsync(request, default), 403, "POST rechecks revoked grant before transaction or persistence");
await Denied(async () => await new IncidentService(db).CreateIncidentAsync(request, default), 401, "missing DI cannot bypass authority");
var location = new IncidentPlacementGuard.Location(1, true, 10, 1, true, 20, 1, true, 10);
IncidentPlacementGuard.Validate(1, location); Check(true, "active consistent placement accepted");
foreach (var invalid in new[] { location with { Active = false }, location with { BranchId = 2 },
    location with { FloorActive = false }, location with { FloorBranchId = 2 },
    location with { AreaActive = false }, location with { AreaBranchId = 2 },
    location with { AreaFloorId = 99 }, location with { FloorId = 0 } })
{
    try { IncidentPlacementGuard.Validate(1, invalid); throw new Exception("FAIL invalid placement"); }
    catch (ValidationException) { Check(true, "inactive/stale/cross-branch parent rejected: " + invalid); }
}
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
foreach (var asset in new Asset?[] { null, new() { BranchId = 2 }, new() { BranchId = 1, LifecycleStatus = "RETIRED" },
    new() { BranchId = 1, CurrentRoomId = 99, CurrentAreaId = 20 }, new() { BranchId = 1, CurrentRoomId = 11, CurrentAreaId = 99 } })
{
    try { IncidentPlacementGuard.ValidateAsset(1, asset, 11, 20); throw new Exception("FAIL invalid asset parentage"); }
    catch (ValidationException) { Check(true, "missing/cross-branch/retired/wrong-room/wrong-area asset rejected"); }
}
IncidentPlacementGuard.ValidateAsset(1, new Asset { BranchId = 1, CurrentRoomId = 11, CurrentAreaId = 20 }, 11, 20);
Check(true, "asset matching authoritative branch and source accepted");
Check(JsonSerializer.Deserialize<CreateIncidentRequest>("{}", json)!.ReportedBy is null, "omitted reporter binds null");
Check(JsonSerializer.Deserialize<CreateIncidentRequest>("{\"reportedBy\":7}", json)!.ReportedBy == 7, "legacy reporter field retained for explicit comparison");
foreach (var payload in new[] { "{}", "{\"branchId\":0}", "{\"branchId\":1,\"roomId\":-1}" })
{
    var dto = JsonSerializer.Deserialize<CreateIncidentRequest>(payload, json)!;
    Check(!Validator.TryValidateObject(dto, new(dto), [], true), "invalid DTO rejected " + payload);
}
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(IncidentsController).Assembly.FullName });
builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddAuthentication(HotelOpsCookieTicket.Scheme).AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>(HotelOpsCookieTicket.Scheme, _ => { });
builder.Services.AddAuthorization(); builder.Services.AddHotelOpsAntiforgery();
builder.Services.AddSingleton<IIncidentService>(service);
builder.Services.AddControllers().AddApplicationPart(typeof(IncidentsController).Assembly);
await using var app = builder.Build();
// The fixture listener is loopback HTTP; production transport is HTTPS. Mark the
// fixture request secure so antiforgery reaches token validation rather than its TLS precondition.
app.Use((context, next) => { context.Request.Scheme = "https"; return next(context); });
app.UseAuthentication(); app.UseMiddleware<ApiCsrfMiddleware>(); app.UseAuthorization(); app.MapControllers();
await app.StartAsync();
try
{
    using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    using (var response = await client.GetAsync("/api/incidents/options?branchId=1")) Check(response.StatusCode == HttpStatusCode.Unauthorized, "real MVC options anonymous 401");
    using (var response = await client.PostAsJsonAsync("/api/incidents", request)) Check(response.StatusCode == HttpStatusCode.BadRequest, "real CSRF middleware rejects POST without token");
    client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "forged");
    using (var response = await client.PostAsJsonAsync("/api/incidents", request)) Check(response.StatusCode == HttpStatusCode.BadRequest, "real antiforgery rejects forged token");
}
finally { await app.StopAsync(); }
Check(noSql.Attempts == 0 && !db.ChangeTracker.HasChanges(), "zero SQL connection attempts and no unauthorized tracked changes");
Console.WriteLine($"PASS assertions={count}; SQL_NOT_RUN; fixture identity/permission and real MVC/CSRF, not SQL integration acceptance.");

sealed class FixtureUser : ICurrentUserAccessor
{
    public CurrentUserInfo? Value;
    public bool TryGetCurrentUser(out CurrentUserInfo user) { user = Value ?? default; return Value.HasValue; }
}
sealed class FixtureData : IPermissionDataSource
{
    public bool Active = true;
    public IReadOnlyList<PermissionGrantPath> Paths = [];
    public Task<bool> IsActiveUserAsync(long id, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(Active); }
    public Task<bool> IsActiveBranchAsync(long id, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(true); }
    public Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long id, string code, DateTime now, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(Paths); }
}
sealed class NoSql : DbConnectionInterceptor
{
    public int Attempts;
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("SQL FORBIDDEN"); }
}
sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
}
