using System.Reflection;
using HotelOps.Api.Controllers.Filters;
using HotelOps.Api.Data;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Dev2Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

// Test-only capability fixture matching the current compiled demo action contract.
// The caller supplies a command-capturing DbConnection, never a SqlConnection.
// This tests entry/first-command order, not permission evaluation or SQL acceptance.
internal sealed class WriterAuthorityFixture : IDisposable
{
    private readonly ServiceProvider services;
    private readonly string? previousFlag;
    private readonly HttpContext context;
    private readonly MethodInfo remove;
    internal IHttpContextAccessor Http { get; }

    internal WriterAuthorityFixture(HotelOpsDbContext db, string actionName, object input)
    {
        if (db.Database.GetDbConnection() is Microsoft.Data.SqlClient.SqlConnection)
            throw new InvalidOperationException("Writer order fixtures must never use a real SQL connection.");
        var action = Dev2ActionAdmissionRegistry.KnownActions.Single(x => x.ActionName == actionName);
        var now = DateTime.UtcNow.AddMinutes(-1);
        var reference = SessionReference.FromProtectedTicket(9, SessionCredential.Generate(), false, now);
        var snapshot = new SessionSnapshot(1, 9, now, now.AddMinutes(30), now.AddHours(12), false);
        services = new ServiceCollection().AddSingleton(db)
            .AddSingleton<ICurrentUserAccessor>(new Identity()).BuildServiceProvider();
        context = new DefaultHttpContext
        {
            RequestServices = services,
            User = HotelOpsCookieTicket.Create(new(reference, snapshot), TimeProvider.System).Principal
        };
        Http = new HttpContextAccessor { HttpContext = context };
        previousFlag = Environment.GetEnvironmentVariable("DEV2_DEMO_AUTHORITY_V1");
        var assembly = typeof(Dev2ServiceAuthority).Assembly;
        remove = typeof(Dev2ServiceAuthority).GetMethod("Remove", BindingFlags.Static | BindingFlags.NonPublic)!;
        try
        {
            Environment.SetEnvironmentVariable("DEV2_DEMO_AUTHORITY_V1", Dev2ExpandedPolicyIdentity.Sha256);
            var contract = typeof(Dev2DemoBindings).GetMethod("FindExpanded", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [action, context]) ?? throw new InvalidOperationException("Compiled action contract unavailable.");
            var arguments = new Dictionary<string, object?>
            {
                ["request"] = input, ["id"] = 1L, ["assetId"] = 1L, ["handoverId"] = 1L
            };
            var bound = Activator.CreateInstance(assembly.GetType("HotelOps.Api.Services.Dev2Authorization.Dev2BoundAction")!,
                [action, context, new CurrentUserInfo(9, 1, false), arguments])!;
            var grant = Activator.CreateInstance(assembly.GetType("HotelOps.Api.Services.Dev2Authorization.Dev2ActionGrant")!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                [bound, contract, new Func<Dev2AuthorizationPhase, CancellationToken, Task>((_, _) =>
                    throw new InvalidOperationException("Order probe must stop at first User query, before later authorization/persistence."))], null)!;
            typeof(Dev2ServiceAuthority).GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [context, grant]);
        }
        catch
        {
            Environment.SetEnvironmentVariable("DEV2_DEMO_AUTHORITY_V1", previousFlag);
            services.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        remove.Invoke(null, [context]);
        Environment.SetEnvironmentVariable("DEV2_DEMO_AUTHORITY_V1", previousFlag);
        services.Dispose();
    }

    private sealed class Identity : ICurrentUserAccessor
    {
        public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(9, 1, false); return true; }
    }
}
