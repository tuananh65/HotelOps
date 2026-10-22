using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
var ct = timeout.Token;
var now = DateTime.UtcNow;
var identity = new TestIdentity();
var data = new TestData();
var resolver = new TestResolver();
var service = new PermissionService(identity, data, resolver, new FixedClock(now));
var checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + label);
    checks++;
    Console.WriteLine("PASS: " + label);
}
PermissionGrantPath Path(string scope = "BRANCH", long? branch = 1) =>
    new("ROOM_VIEW", scope, branch, true, true, now.AddDays(-1), null, true, true);
void Reset()
{
    identity.Current = new(1, 10, false); data.Active = true; data.BranchActive = true;
    data.Paths = [Path()]; data.Failure = false; data.PathFailure = null; resolver.Context = null; resolver.Failure = false;
}
async Task Expect(PermissionDecision decision, string label) =>
    Check((await service.EvaluateBranchAsync("ROOM_VIEW", 1, ct)).Decision == decision, label);

Check(!default(PermissionEvaluation).IsGranted, "Default/uninitialized result fails closed");
Reset(); identity.Current = null; await Expect(PermissionDecision.Unauthenticated, "No valid current user");
Reset(); identity.Current = new(1, 10, true); await Expect(PermissionDecision.Restricted, "Restricted session cannot authorize business action");
Reset(); data.Active = false; await Expect(PermissionDecision.Denied, "Inactive/deleted account denied");
Reset(); data.Paths = [Path() with { RoleActive = false }]; await Expect(PermissionDecision.Denied, "Inactive role denied");
Reset(); data.Paths = []; await Expect(PermissionDecision.Denied, "No permission grant denied");
Reset(); Check(!(await service.EvaluateBranchAsync("MISSING", 1, ct)).IsGranted, "Nonexistent permission denied");
Reset(); await Expect(PermissionDecision.Granted, "Valid branch grant");
Reset(); data.Paths = [Path() with { RoleActive = false }, Path()]; await Expect(PermissionDecision.Granted, "Multiple roles: independently valid path grants");
Reset(); data.Paths = [Path() with { ExpiresAt = now }]; await Expect(PermissionDecision.Denied, "Assignment expiry boundary denied");
Reset(); data.Paths = [Path() with { AssignmentActive = false }]; await Expect(PermissionDecision.Denied, "Revoked assignment denied");
Reset(); data.Paths = [Path() with { AssignedAt = now.AddSeconds(1) }]; await Expect(PermissionDecision.Denied, "Future assignment denied");
Reset(); Check(!(await service.EvaluateBranchAsync("ROOM_VIEW", 2, ct)).IsGranted, "Cross-branch denied");
Reset(); data.Paths = [Path() with { BranchAccessActive = false }]; await Expect(PermissionDecision.Denied, "Revoked/missing branch access denied");
Reset(); data.Paths = [Path() with { BranchActive = false }]; await Expect(PermissionDecision.Denied, "Inactive assignment branch denied");
Reset(); data.BranchActive = false; await Expect(PermissionDecision.Denied, "Inactive target branch denied");
Reset(); data.Paths = [Path("OWN")]; resolver.Context = new(1, true);
Check((await service.EvaluateResourceAsync("ROOM_VIEW", new("test-assignment", 9), ct)).IsGranted, "OWN with trusted test resolver evidence");
resolver.Context = new(1, false);
Check(!(await service.EvaluateResourceAsync("ROOM_VIEW", new("test-assignment", 9), ct)).IsGranted, "OWN without ownership denied");
resolver.Context = new(2, true);
Check(!(await service.EvaluateResourceAsync("ROOM_VIEW", new("test-assignment", 9), ct)).IsGranted, "OWN cannot cross assignment branch");
resolver.Context = null;
Check((await service.EvaluateResourceAsync("ROOM_VIEW", new("test-assignment", 9), ct)).Decision == PermissionDecision.ResourceContextRequired, "Missing/unverified resource context denied");
Check(!(await service.EvaluateBranchAsync("ROOM_VIEW", 1, ct)).IsGranted, "Branch membership is not OWN evidence");
Reset(); Check(!(await service.EvaluateGlobalAsync("ROOM_VIEW", ct)).IsGranted, "Grant-only global check cannot authorize BRANCH");
Reset(); data.Paths = [Path("ALL", null)];
Check((await service.EvaluateGlobalAsync("ROOM_VIEW", ct)).IsGranted, "Valid global ALL grant");
Check((await service.EvaluateResourceAsync("ROOM_VIEW", new("unknown", 9), ct)).Decision == PermissionDecision.ResourceContextRequired, "ALL still requires authoritative resource context");
Reset(); data.Paths = [Path("ALL", 1)];
Check(!(await service.EvaluateBranchAsync("ROOM_VIEW", 2, ct)).IsGranted, "Branch-bound ALL does not widen assignment");
Reset(); data.Paths = [Path("BRANCH", null)]; await Expect(PermissionDecision.Denied, "NULL branch assignment does not borrow branch access");
Reset(); data.Paths = [Path("BRANCH", 2), Path() with { PermissionCode = "OTHER" }];
await Expect(PermissionDecision.Denied, "Grant of role at branch2 cannot borrow role at branch1");
Reset(); data.Paths = [Path() with { PermissionCode = "USER_MANAGE" }]; await Expect(PermissionDecision.Denied, "Administrative grant gives no automatic operational permission (SYS_ADMIN)");
Reset(); data.Paths = [Path("ALL", null) with { PermissionCode = "REPORT_VIEW" }]; await Expect(PermissionDecision.Denied, "Reporting grant gives no operational permission (OWNER)");
Reset(); foreach (var scope in new[] { "DEPARTMENT", "all", "UNKNOWN" })
{
    data.Paths = [Path(scope)]; await Expect(PermissionDecision.Denied, "Unsupported scope denied: " + scope);
}
Reset(); await Expect(PermissionDecision.Granted, "Before revocation"); data.Paths = [];
await Expect(PermissionDecision.Denied, "Changed store grant reflected on next evaluation without cache");
Reset(); data.Failure = true; await Expect(PermissionDecision.Unavailable, "Query timeout fails closed");
Reset(); data.PathFailure = new TestDbException(); await Expect(PermissionDecision.Unavailable, "Permission grant query database failure denies");
Reset(); data.PathFailure = new InvalidOperationException("Provider wrapper", new TestDbException());
await Expect(PermissionDecision.Unavailable, "Wrapped provider database failure denies");
Reset(); data.PathFailure = new InvalidOperationException("Programming failure");
try { await service.EvaluateBranchAsync("ROOM_VIEW", 1, ct); throw new Exception("Failure swallowed"); }
catch (InvalidOperationException) { Check(true, "Unexpected programming failure propagates without authorization"); }
Reset(); resolver.Failure = true;
Check((await service.EvaluateResourceAsync("ROOM_VIEW", new("test", 9), ct)).Decision == PermissionDecision.Unavailable, "Resource query timeout fails closed");
Reset();
using (var canceled = new CancellationTokenSource())
{
    canceled.Cancel();
    try { await service.EvaluateGlobalAsync("ROOM_VIEW", canceled.Token); throw new InvalidOperationException("Cancellation ignored"); }
    catch (OperationCanceledException) { Check(true, "Cancellation propagated"); }
}
Check(!(await service.EvaluateBranchAsync("ROOM_VIEW", 0, ct)).IsGranted, "Invalid branch denied");
var unresolved = new UnresolvedPermissionResourceResolver();
Check(await unresolved.ResolveAsync("ROOM_VIEW", new("test", 9), 1, ct) is null, "Production default resolver has no invented ownership rule");
var registrations = new ServiceCollection();
registrations.AddSingleton<TimeProvider>(TimeProvider.System);
registrations.AddDbContext<HotelOpsDbContext>(o => o.UseSqlServer("Server=unused;Database=unused;Integrated Security=true"));
registrations.AddScoped<ICurrentUserAccessor>(_ => identity);
registrations.AddScoped<IPermissionDataSource, EfPermissionDataSource>();
registrations.AddScoped<IPermissionResourceResolver, UnresolvedPermissionResourceResolver>();
registrations.AddScoped<IPermissionService, PermissionService>();
using (var provider = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }))
using (var scope = provider.CreateScope())
{
    Check(ReferenceEquals(scope.ServiceProvider.GetRequiredService<IPermissionService>(), scope.ServiceProvider.GetRequiredService<IPermissionService>()), "Scoped DI resolution without SQL connection");
}

if (args.Contains("--live"))
{
    var candidate = await HotelOps.Verification.ReadOnlyVerification.OpenOrReportAsync(ct);
    if (candidate is null) { Environment.ExitCode = 2; return; }
    await using var db = candidate;
    await db.Database.OpenConnectionAsync(ct);
    await using var cmd = db.Database.GetDbConnection().CreateCommand();
    cmd.CommandText = "SELECT DB_NAME()";
    HotelOps.Verification.ReadOnlyVerification.AssertSelect(cmd.CommandText);
    Check((string?)await cmd.ExecuteScalarAsync(ct) == "HotelOpsDB", "Live target identity");
    var sql = new EfPermissionDataSource(db);
    var live = new PermissionService(identity, sql, unresolved, TimeProvider.System);
    // Test accessor supplies known identity for read-only core integration, not a real authenticated HTTP session.
    identity.Current = new(1, 10, false);
    Check((await live.EvaluateBranchAsync("ROOM_VIEW", 1, ct)).IsGranted, "Live OM branch1 permission resolution");
    Check(!(await live.EvaluateBranchAsync("ROOM_VIEW", 2, ct)).IsGranted, "Live OM branch2 denied");
    identity.Current = new(16, 10, false);
    Check((await live.EvaluateGlobalAsync("USER_VIEW", ct)).IsGranted, "Live SYS_ADMIN explicit administrative grant");
    Check(!(await live.EvaluateBranchAsync("ROOM_VIEW", 1, ct)).IsGranted, "Live SYS_ADMIN has no operational bypass");
    Check(!(await live.EvaluateGlobalAsync("MISSING_PERMISSION", ct)).IsGranted, "Live unknown permission denied");
    Check(!db.ChangeTracker.Entries().Any(), "Live EF queries are not tracked");
}
Console.WriteLine($"PASS: {checks} checks. Test doubles exercise core decisions; --live adds SQL reads. No HTTP authorization or production ownership adapter is certified.");

sealed class FixedClock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero); }
sealed class TestIdentity : ICurrentUserAccessor
{
    public CurrentUserInfo? Current { get; set; }
    public bool TryGetCurrentUser(out CurrentUserInfo user) { user = Current.GetValueOrDefault(); return Current.HasValue; }
}
sealed class TestResolver : IPermissionResourceResolver
{
    public VerifiedPermissionResource? Context { get; set; }
    public bool Failure { get; set; }
    public Task<VerifiedPermissionResource?> ResolveAsync(string permissionCode, PermissionResource resource, long currentUserId, CancellationToken cancellationToken) =>
        Failure ? throw new TimeoutException() : Task.FromResult(Context);
}
sealed class TestData : IPermissionDataSource
{
    public bool Active { get; set; } = true;
    public bool BranchActive { get; set; } = true;
    public bool Failure { get; set; }
    public Exception? PathFailure { get; set; }
    public IReadOnlyList<PermissionGrantPath> Paths { get; set; } = [];
    public Task<bool> IsActiveUserAsync(long userId, CancellationToken cancellationToken) => Failure ? throw new TimeoutException() : Task.FromResult(Active);
    public Task<bool> IsActiveBranchAsync(long branchId, CancellationToken cancellationToken) => Task.FromResult(BranchActive);
    public Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long userId, string permissionCode, DateTime utcNow, CancellationToken cancellationToken) =>
        PathFailure is not null ? throw PathFailure : Failure ? throw new TimeoutException() : Task.FromResult(Paths);
}
sealed class TestDbException : System.Data.Common.DbException;
