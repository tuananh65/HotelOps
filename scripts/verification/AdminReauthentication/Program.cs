using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using HotelOps.Api.Security.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

if (args.Length != 2 || !Regex.IsMatch(args[0], @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$")
    || File.Exists(args[1])) throw new ArgumentException("Owned patched trial and NEW local receipt required");
var connection = new SqlConnectionStringBuilder { DataSource = @"DESKTOP-JI8U50T\SQLEXPRESS", InitialCatalog = args[0],
    IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10 };
using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(8));
var ct = budget.Token;
HotelOpsDbContext Open(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<HotelOpsDbContext>()
    .UseSqlServer(connection.ConnectionString).AddInterceptors(interceptors).Options);
await using var db = Open();
await db.Database.OpenConnectionAsync(ct);
await using (var cmd = db.Database.GetDbConnection().CreateCommand())
{
    cmd.CommandText = "SELECT DB_NAME(),CONVERT(nvarchar(128),@@SERVERNAME),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')";
    await using var rows = await cmd.ExecuteReaderAsync(ct);
    if (!await rows.ReadAsync(ct) || rows.GetString(0) != args[0] || rows.GetString(1) != connection.DataSource
        || rows.IsDBNull(2) || rows.GetString(2) != "Task17") throw new InvalidOperationException("Trial guard rejected");
}
var checks = new List<string>();
void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException("FAIL: " + label); checks.Add(label); Console.WriteLine("PASS: " + label); }
Check(await new StartupSchemaValidator(db).ValidateAsync(ct) == SchemaCompatibility.Compatible, "unchanged startup schema accepts patched trial");
var clock = new TestClock(DateTimeOffset.UtcNow);
var store = new ReauthenticationProofStore(clock);
var hasher = new PasswordCredentialService(Options.Create(new PasswordHasherOptions
    { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
var password = SessionCredential.Generate();
var prefix = "T33_" + Guid.NewGuid().ToString("N")[..10];
var owned = new List<long>();
ISqlSessionService Sessions(HotelOpsDbContext context) => new SqlSessionService(context, new SessionRevocationService(context), clock);
AdminReauthenticationService Reauth(HotelOpsDbContext context, ReauthenticationProofStore? proofStore = null)
    => new(context, Sessions(context), new CredentialLockoutService(context, hasher, clock), proofStore ?? store, clock);
async Task<User> NewUser(string suffix)
{
    var code = prefix + suffix;
    var user = new User { EmployeeCode = code, Username = code, Email = code + "@example.invalid", FullName = "Reauthentication fixture",
        AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = clock.GetUtcNow().UtcDateTime };
    user.PasswordHash = hasher.HashPassword(user, password);
    db.Add(user); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); owned.Add(user.UserId); return user;
}
async Task<(SessionReference Ticket, long Id)> Session(User user)
{
    var raw = SessionCredential.Generate(); var now = clock.GetUtcNow().UtcDateTime;
    var row = new UserSession { UserId = user.UserId, SessionKeyHash = SessionCredential.Hash(raw), CreatedAt = now,
        LastUsedAt = now, ExpiresAt = now.AddMinutes(30) };
    db.Add(row); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
    return (SessionReference.FromProtectedTicket(user.UserId, raw, false, now), row.SessionId);
}
async Task<ReauthenticationStatus> Proof((SessionReference Ticket, long Id) session, ReauthenticationProofStore? custom = null)
{
    await using var context = Open(); await using var tx = await context.Database.BeginTransactionAsync(ct);
    var status = await Reauth(context, custom).ValidateProofAsync(session.Ticket, session.Id, ct);
    await tx.CommitAsync(ct); return status;
}
async Task<ReauthenticationResult> Authenticate((SessionReference Ticket, long Id) session, string? supplied = null)
{
    await using var context = Open(); return await Reauth(context).ReauthenticateAsync(session.Ticket, session.Id, supplied ?? password, ct);
}
try
{
    var actor = await NewUser("core"); var current = await Session(actor); var other = await Session(actor);
    var initialSessions = await db.UserSessions.CountAsync(ct);
    var success = await Authenticate(current);
    Check(success.Status == ReauthenticationStatus.Succeeded && success.ExpiresAtUtc == clock.GetUtcNow().UtcDateTime.AddMinutes(5), "correct current password creates five-minute proof");
    Check(await db.UserSessions.CountAsync(ct) == initialSessions, "reauthentication creates no second login session");
    Check(await Proof(current) == ReauthenticationStatus.Succeeded, "proof valid in exact actor and session");
    Check(await Proof(other) == ReauthenticationStatus.ProofRequired, "same actor other session cannot borrow proof");
    Check(await Proof(current, new ReauthenticationProofStore(clock)) == ReauthenticationStatus.ProofRequired, "process state loss fails closed");
    clock.Advance(TimeSpan.FromMinutes(4));
    await using (var context = Open())
    await using (var tx = await context.Database.BeginTransactionAsync(ct))
    {
        Check((await Sessions(context).TouchSessionAsync(current.Ticket, ct)).IsValid, "session touch succeeds inside proof window");
        await tx.CommitAsync(ct);
    }
    Check(await Proof(current) == ReauthenticationStatus.Succeeded, "proof remains valid before five-minute deadline after touch");
    clock.Advance(TimeSpan.FromMinutes(1));
    Check(await Proof(current) == ReauthenticationStatus.ProofRequired, "proof invalid at exactly five minutes despite session touch");
    var beforeSession = JsonSerializer.Serialize(await db.UserSessions.AsNoTracking().Where(s => s.SessionId == current.Id)
        .Select(s => new { s.SessionId, s.ExpiresAt, s.LastUsedAt, s.RevokedAt }).SingleAsync(ct));
    Check((await Authenticate(current, "incorrect-secret")).Status == ReauthenticationStatus.InvalidCredential, "incorrect password rejects reauthentication");
    Check(JsonSerializer.Serialize(await db.UserSessions.AsNoTracking().Where(s => s.SessionId == current.Id)
        .Select(s => new { s.SessionId, s.ExpiresAt, s.LastUsedAt, s.RevokedAt }).SingleAsync(ct)) == beforeSession,
        "incorrect password does not revoke, replace or mutate active SQL session");
    Check(await Proof(current) == ReauthenticationStatus.ProofRequired, "failed reauthentication grants no proof");
    await Authenticate(current);
    var parallel = await Task.WhenAll(Authenticate(current), Authenticate(current));
    Check(parallel.All(r => r.Status == ReauthenticationStatus.Succeeded) && await Proof(current) == ReauthenticationStatus.Succeeded,
        "concurrent reauthentication serializes under user lock and yields valid bounded proof");
    // Sensitive workflow uses the SAME fake server clock and real SQL transaction; no client time input.
    var globalRole = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "SYS_ADMIN", ct);
    db.UserRoles.Add(new UserRole { UserId = actor.UserId, RoleId = globalRole.RoleId, IsActive = true, AssignedAt = clock.GetUtcNow().UtcDateTime });
    await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
    var target = await NewUser("coretarget");
    async Task<ConditionalCredentialResetResult> Reset((SessionReference Ticket, long Id) session, User person, HotelOpsDbContext context)
    {
        var identity = new FixedIdentity(session.Ticket.UserId, session.Id);
        var data = new EfPermissionDataSource(context); var permissions = new PermissionService(identity, data, new UnresolvedPermissionResourceResolver(), clock);
        var access = new UserCredentialResetAccess(context, identity, permissions, data, clock);
        var credentials = new TemporaryCredentialService(context, hasher, new SessionRevocationService(context), clock);
        return await new AdminCredentialResetWorkflow(credentials, access, identity, Reauth(context))
            .ResetAsync(session.Ticket, session.Id, person.UserId, person.RowVersion, ct);
    }
    clock.Advance(TimeSpan.FromMinutes(5));
    await using (var context = Open())
    using (var expired = await Reset(current, target, context))
        Check(expired.Status == ConditionalCredentialResetStatus.ReauthenticationRequired && expired.Credential == null,
            "expired proof denies integrated SQL reset without credential");
    await Authenticate(current);
    await using (var context = Open())
    using (var foreign = await Reset(other, target, context))
        Check(foreign.Status == ConditionalCredentialResetStatus.ReauthenticationRequired, "different-session proof denies integrated SQL reset");
    // Rollback before commit must neither publish proof nor persist its audit.
    var rollbackUser = await NewUser("rollback"); var rollbackSession = await Session(rollbackUser);
    var beforeAudit = await db.AuditLogs.CountAsync(a => a.ActorUserId == rollbackUser.UserId, ct);
    var failed = false;
    await using (var context = Open(new BeforeCommitFault()))
    {
        try { await Reauth(context).ReauthenticateAsync(rollbackSession.Ticket, rollbackSession.Id, password, ct); }
        catch (InjectedFailure) { failed = true; }
    }
    Check(failed && await Proof(rollbackSession) == ReauthenticationStatus.ProofRequired
        && await db.AuditLogs.CountAsync(a => a.ActorUserId == rollbackUser.UserId, ct) == beforeAudit,
        "reauthentication commit failure rolls back audit and publishes no proof");
    var slowTarget = await NewUser("slowtarget");
    await Authenticate(current);
    await using (var context = Open(new ExpireDuringReset(clock)))
    using (var expiredDuringWrite = await Reset(current, slowTarget, context))
        Check(expiredDuringWrite.Status == ConditionalCredentialResetStatus.ReauthenticationRequired
            && (await db.Users.AsNoTracking().SingleAsync(u => u.UserId == slowTarget.UserId, ct)).RowVersion.SequenceEqual(slowTarget.RowVersion)
            && (await db.Users.AsNoTracking().SingleAsync(u => u.UserId == slowTarget.UserId, ct)).PasswordHash == slowTarget.PasswordHash,
            "proof expiring during reset SQL causes final gate to roll back rotation");
    var logoutUser = await NewUser("logout"); var logoutSession = await Session(logoutUser); await Authenticate(logoutSession);
    await using (var context = Open())
        await new AuthenticationCredentialWorkflow(context, Sessions(context), new SessionRevocationService(context), hasher, clock).LogoutAsync(logoutSession.Ticket, ct);
    Check(await Proof(logoutSession) == ReauthenticationStatus.SessionInvalid, "actual logout invalidates proof through SQL revocation");
    var disabled = await NewUser("disabled"); var disabledSession = await Session(disabled); await Authenticate(disabledSession);
    await db.Users.Where(u => u.UserId == disabled.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "INACTIVE"), ct);
    Check(await Proof(disabledSession) == ReauthenticationStatus.SessionInvalid, "inactive actor invalidates proof");
    await db.Users.Where(u => u.UserId == disabled.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "ACTIVE"), ct);
    Check(await Proof(disabledSession) == ReauthenticationStatus.ProofRequired, "reactivation does not restore prior proof");
    var rotated = await NewUser("rotated"); var rotatedSession = await Session(rotated); await Authenticate(rotatedSession);
    using (var prepared = new TemporaryCredentialService(db, hasher, new SessionRevocationService(db), clock).Prepare(rotated.UserId))
    using (var issued = await new TemporaryCredentialService(db, hasher, new SessionRevocationService(db), clock)
        .ApplyAsync(prepared, actor.UserId, CredentialProvisioningOperation.AdminReset, ct)) { }
    Check(await Proof(rotatedSession) == ReauthenticationStatus.SessionInvalid, "credential reset invalidates old session and proof");
    var expiredUser = await NewUser("expired"); var expiredSession = await Session(expiredUser); await Authenticate(expiredSession);
    await db.UserSessions.Where(s => s.SessionId == expiredSession.Id).ExecuteUpdateAsync(s => s.SetProperty(s => s.ExpiresAt, clock.GetUtcNow().UtcDateTime), ct);
    Check(await Proof(expiredSession) == ReauthenticationStatus.SessionInvalid, "expired SQL session invalidates proof");
    var locked = await NewUser("locked"); var lockedSession = await Session(locked);
    for (var i = 0; i < 5; i++) await Authenticate(lockedSession, "incorrect-secret");
    Check((await Authenticate(lockedSession)).Status == ReauthenticationStatus.Locked, "existing five-attempt lockout blocks correct-password reauthentication");
    Check(await Proof(lockedSession) == ReauthenticationStatus.ProofRequired, "lockout never grants reauthentication proof");
    var audits = await db.AuditLogs.AsNoTracking().Where(a => owned.Contains(a.ActorUserId ?? 0)).Select(a => a.NewValues).ToArrayAsync(ct);
    Check(audits.All(a => a == null || !a.Contains(password)), "reauthentication audit contains no supplied plaintext password");
    await AuthorizationRaceSuite.Run(connection.ConnectionString, clock, Check, ct);
    await HttpSuite.Run(connection.ConnectionString, Check, ct);
    await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new { success = true, checks = checks.Count, cases = checks,
        scope = "Real SQL and real HTTPS API; five-minute expiry uses controlled backend TimeProvider in service tests",
        actualTransportDisconnectVerified = false, secretsPersisted = false }, new JsonSerializerOptions { WriteIndented = true }), ct);
    Console.WriteLine($"PASS: {checks.Count} reauthentication/reset checks");
}
finally
{
    await db.UserSessions.Where(s => owned.Contains(s.UserId) && s.RevokedAt == null)
        .ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, DateTime.UtcNow), CancellationToken.None);
}

sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan amount) => _now += amount;
}
sealed class FixedIdentity(long userId, long sessionId) : ICurrentUserAccessor
{
    public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(userId, sessionId, false); return true; }
}
sealed class InjectedFailure : IOException { }
sealed class BeforeCommitFault : DbTransactionInterceptor
{
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction tx, TransactionEventData data,
        InterceptionResult result, CancellationToken ct = default) => throw new InjectedFailure();
}
sealed class ExpireDuringReset(TestClock clock) : DbCommandInterceptor
{
    private bool _fired;
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData data, int result, CancellationToken ct = default)
    {
        if (!_fired && command.CommandText.Contains("UPDATE [u]")) { _fired = true; clock.Advance(TimeSpan.FromMinutes(5)); }
        return ValueTask.FromResult(result);
    }
}
