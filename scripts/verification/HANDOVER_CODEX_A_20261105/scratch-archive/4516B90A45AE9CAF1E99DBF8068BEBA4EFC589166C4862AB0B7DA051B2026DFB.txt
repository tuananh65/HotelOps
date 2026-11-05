using System.Security.Claims;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var created = DateTime.UtcNow.AddMinutes(-1);
var passed = 0;

DefaultHttpContext Valid(bool restricted = false)
{
    var lifetime = restricted ? SqlSessionPolicy.RestrictedLifetime : TimeSpan.FromHours(2);
    var snapshot = new SessionSnapshot(27, 15, created, created.Add(lifetime), created.Add(lifetime), restricted);
    var reference = SessionReference.FromProtectedTicket(snapshot.UserId, SessionCredential.Generate(), restricted, created);
    var ticket = HotelOpsCookieTicket.Create(new CreatedSqlSession(reference, snapshot), TimeProvider.System);
    var context = new DefaultHttpContext { User = ticket.Principal };
    context.Items[typeof(SessionSnapshot)] = snapshot;
    return context;
}

void Check(string name, HttpContext? context, bool expected, bool restricted = false)
{
    var accessor = new CurrentUserAccessor(new HttpContextAccessor { HttpContext = context });
    var actual = accessor.TryGetCurrentUser(out var user);
    if (actual != expected || (expected
        ? user != new CurrentUserInfo(15, 27, restricted)
        : user != default))
        throw new InvalidOperationException($"FAIL: {name}");
    passed++;
    Console.WriteLine($"PASS: {name}");
}

Check("No HttpContext", null, false);
var context = Valid();
context.User = new ClaimsPrincipal(new ClaimsIdentity(context.User.Claims));
Check("Unauthenticated principal with claims and snapshot", context, false);
context = Valid();
((ClaimsIdentity)context.User.Identity!).RemoveClaim(context.User.FindFirst(HotelOpsCookieTicket.SessionIdClaim)!);
Check("Missing ticket claim", context, false);
context = Valid();
((ClaimsIdentity)context.User.Identity!).AddClaim(new Claim(HotelOpsCookieTicket.RestrictedClaim, "0"));
Check("Duplicate ticket claim", context, false);
context = Valid();
context.Items.Clear();
Check("Missing verified snapshot", context, false);
context = Valid();
context.Items[typeof(SessionSnapshot)] = "invalid";
Check("Wrong snapshot type", context, false);
foreach (var field in new[] { "UserId", "SessionId", "IsRestricted", "CreatedAtUtc" })
{
    context = Valid();
    var snapshot = (SessionSnapshot)context.Items[typeof(SessionSnapshot)]!;
    context.Items[typeof(SessionSnapshot)] = field switch
    {
        "UserId" => snapshot with { UserId = 16 },
        "SessionId" => snapshot with { SessionId = 28 },
        "IsRestricted" => snapshot with { IsRestricted = true },
        _ => snapshot with { CreatedAtUtc = created.AddSeconds(1) }
    };
    Check($"Mismatched {field}", context, false);
}
Check("Valid normal session", Valid(), true);
// Restricted sessions use the existing shorter absolute lifetime.
context = Valid(true);
Check("Valid restricted identity preserves restriction", context, true, true);

var httpAccessor = new HttpContextAccessor { HttpContext = Valid() };
var current = new CurrentUserAccessor(httpAccessor);
if (!current.TryGetCurrentUser(out _)) throw new InvalidOperationException("Initial identity missing");
httpAccessor.HttpContext = null;
if (current.TryGetCurrentUser(out var stale) || stale != default)
    throw new InvalidOperationException("Identity cached across requests");
passed++;
Console.WriteLine("PASS: No identity cached across requests");

var services = new ServiceCollection();
services.AddHttpContextAccessor();
services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
using var scope = provider.CreateScope();
if (scope.ServiceProvider.GetRequiredService<ICurrentUserAccessor>() is not CurrentUserAccessor)
    throw new InvalidOperationException("DI resolution failed");
passed++;
Console.WriteLine($"PASS: DI resolution; {passed} checks passed.");
