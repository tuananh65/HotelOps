using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

if (args.Length != 2 || !Regex.IsMatch(args[0], @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$")
    || File.Exists(args[1])) throw new ArgumentException("Owned patched trial and NEW local result path required");
var connection = new SqlConnectionStringBuilder { DataSource = @"DESKTOP-JI8U50T\SQLEXPRESS", InitialCatalog = args[0],
    IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10 };
using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(5));
var ct = budget.Token;
HotelOpsDbContext Open(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<HotelOpsDbContext>()
    .UseSqlServer(connection.ConnectionString).AddInterceptors(interceptors).Options);
await using var db = Open();
await db.Database.OpenConnectionAsync(ct);
await using (var command = db.Database.GetDbConnection().CreateCommand())
{
    command.CommandText = "SELECT DB_NAME(),CONVERT(nvarchar(128),@@SERVERNAME),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')";
    await using var rows = await command.ExecuteReaderAsync(ct);
    if (!await rows.ReadAsync(ct) || rows.GetString(0) != args[0] || rows.GetString(1) != connection.DataSource
        || rows.IsDBNull(2) || rows.GetString(2) != "Task17") throw new InvalidOperationException("Trial guard failed");
}
var checks = new List<string>();
void Check(bool value, string label) { if (!value) throw new InvalidOperationException("FAIL: " + label); checks.Add(label); Console.WriteLine("PASS: " + label); }
Check(await new StartupSchemaValidator(db).ValidateAsync(ct) == SchemaCompatibility.Compatible, "trial startup schema is compatible");
var hasher = new PasswordCredentialService(Options.Create(new PasswordHasherOptions
    { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
TemporaryCredentialService Credentials(HotelOpsDbContext context, bool failRevoke = false) => new(context, hasher,
    failRevoke ? new FailRevocation(context) : new SessionRevocationService(context), TimeProvider.System);
var prefix = "T32_" + Guid.NewGuid().ToString("N")[..10];
var ownedIds = new List<long>();
var now = DateTime.UtcNow;
var branches = await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId).Take(2).ToArrayAsync(ct);
if (branches.Length != 2) throw new InvalidOperationException("Two canonical branches required");
async Task<User> NewUser(string suffix, string status = "ACTIVE", bool deleted = false)
{
    var code = prefix + suffix;
    var user = new User { EmployeeCode = code, Username = code, Email = code + "@example.invalid", FullName = "Reset trial fixture",
        AccountStatus = status, EmploymentStatus = "ACTIVE", CreatedAt = now, DeletedAt = deleted ? now : null,
        PrimaryBranchId = branches[0].BranchId };
    user.PasswordHash = hasher.HashPassword(user, SessionCredential.Generate());
    db.Users.Add(user); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); ownedIds.Add(user.UserId);
    return user;
}
async Task<SessionReference> AddSession(User user)
{
    var raw = SessionCredential.Generate();
    db.UserSessions.Add(new UserSession { UserId = user.UserId, SessionKeyHash = SessionCredential.Hash(raw),
        CreatedAt = now, LastUsedAt = now, ExpiresAt = now.AddMinutes(30) });
    await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
    return SessionReference.FromProtectedTicket(user.UserId, raw, false, now);
}
Task<User> Read(long id) => db.Users.AsNoTracking().SingleAsync(u => u.UserId == id, ct);
async Task<string> UnchangedBusiness(long id)
{
    var user = await db.Users.AsNoTracking().Where(u => u.UserId == id).Select(u => new { u.EmployeeCode, u.Username,
        u.FullName, u.Email, u.Phone, u.PrimaryBranchId, u.DepartmentId, u.AccountStatus, u.EmploymentStatus,
        u.AvatarUrl, u.ManagerUserId, u.CreatedAt, u.UpdatedAt, u.LastLoginAt, u.DeletedAt }).SingleAsync(ct);
    var memberships = await db.UserBranchAccesses.AsNoTracking().Where(a => a.UserId == id).OrderBy(a => a.AccessId).ToArrayAsync(ct);
    var roles = await db.UserRoles.AsNoTracking().Where(r => r.UserId == id).OrderBy(r => r.UserRoleId).ToArrayAsync(ct);
    return JsonSerializer.Serialize(new { user, memberships, roles }); // In memory only; no report serialization.
}
async Task<string> AtomicState(long id)
{
    var user = await Read(id);
    var sessions = await db.UserSessions.AsNoTracking().Where(s => s.UserId == id).OrderBy(s => s.SessionId)
        .Select(s => new { s.SessionId, s.RevokedAt }).ToArrayAsync(ct);
    var audits = await db.AuditLogs.CountAsync(a => a.EntityType == "users" && a.EntityId == id.ToString(), ct);
    return JsonSerializer.Serialize(new { user.PasswordHash, user.TemporaryPasswordExpiresAt, user.FailedLoginCount,
        user.LockedUntil, user.RowVersion, sessions, audits }); // Secret-bearing state stays in memory.
}
try
{
    var actor = await NewUser("admin1"); var otherActor = await NewUser("admin2");
    var target = await NewUser("success"); var ticket = await AddSession(target); await AddSession(target);
    await db.UserBranchAccesses.AddAsync(new UserBranchAccess { UserId = target.UserId, BranchId = branches[0].BranchId,
        GrantedBy = actor.UserId, GrantedAt = now, IsPrimary = true, AccessLevel = "STANDARD" }, ct);
    var role = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "HK_STAFF", ct);
    db.UserRoles.Add(new UserRole { UserId = target.UserId, RoleId = role.RoleId, BranchId = branches[0].BranchId,
        IsActive = true, AssignedBy = actor.UserId, AssignedAt = now });
    await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
    var beforeBusiness = await UnchangedBusiness(target.UserId);
    using (var prepared = Credentials(db).Prepare(target.UserId))
    using (var result = await Credentials(db).ResetAsync(prepared, actor.UserId, target.RowVersion, "TRIAL_PROCEDURE", ct))
    {
        Check(result.Status == ConditionalCredentialResetStatus.Success && result.Credential != null, "conditional reset returns success after own commit");
        var secret = result.Credential!.TakePasswordOnce();
        var saved = await Read(target.UserId);
        Check(hasher.VerifyPassword(saved, saved.PasswordHash, secret) != PasswordVerificationResult.Failed
            && hasher.VerifyPassword(saved, saved.PasswordHash, SessionCredential.Generate()) == PasswordVerificationResult.Failed,
            "new password verifies with unchanged Identity V3 hasher");
        Check(!saved.RowVersion.SequenceEqual(target.RowVersion) && saved.RowVersion.SequenceEqual(result.Version!), "reset changes actual SQL rowversion and returns committed version");
        Check(saved.TemporaryPasswordExpiresAt > DateTime.UtcNow.AddHours(23.9)
            && saved.TemporaryPasswordExpiresAt <= DateTime.UtcNow.AddHours(24.01), "temporary validity is 24 hours");
        Check(await db.UserSessions.CountAsync(s => s.UserId == target.UserId && s.RevokedAt == null, ct) == 0, "all existing target sessions revoked");
        await using (var validationTransaction = await db.Database.BeginTransactionAsync(ct))
        {
            Check(!(await new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System).ValidateSessionAsync(ticket, ct)).IsValid,
                "old protected session reference fails actual SQL validation after reset");
            await validationTransaction.RollbackAsync(ct);
        }
        var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityType == "users" && a.EntityId == target.UserId.ToString(), ct);
        using var auditJson = JsonDocument.Parse(audit.NewValues!);
        Check(audit.ActorUserId == actor.UserId && audit.Action == "UPDATE"
            && auditJson.RootElement.GetProperty("revoked_session_count").GetInt32() == 2
            && auditJson.RootElement.GetProperty("identity_verification_method").GetString() == "TRIAL_PROCEDURE"
            && auditJson.RootElement.GetProperty("identity_verified_at_utc").GetDateTime().Kind == DateTimeKind.Utc
            && !audit.NewValues!.Contains(secret) && !audit.NewValues.Contains(saved.PasswordHash!), "existing audit records procedure/time/actor/revoke count without secret or hash");
        Check(await UnchangedBusiness(target.UserId) == beforeBusiness, "profile, status, roles and memberships preserved");
        var twice = false; try { result.Credential.TakePasswordOnce(); } catch (InvalidOperationException) { twice = true; }
        Check(twice, "credential plaintext can be consumed once");
    }
    var afterSuccess = await AtomicState(target.UserId);
    using (var prepared = Credentials(db).Prepare(target.UserId))
    using (var retry = await Credentials(db).ResetAsync(prepared, actor.UserId, target.RowVersion, "TRIAL_PROCEDURE", ct))
        Check(retry.Status == ConditionalCredentialResetStatus.Stale && retry.Credential == null
            && await AtomicState(target.UserId) == afterSuccess, "same-token retry is stale with no rotation, audit or credential replay");
    foreach (var kind in new[] { "inactive", "deleted", "missing", "self" })
    {
        var person = kind == "self" ? actor : await NewUser(kind, kind == "inactive" ? "INACTIVE" : "ACTIVE", kind == "deleted");
        var id = kind == "missing" ? long.MaxValue : person.UserId;
        var expected = kind == "inactive" ? ConditionalCredentialResetStatus.Inactive : kind == "self"
            ? ConditionalCredentialResetStatus.SelfDenied : ConditionalCredentialResetStatus.NotFound;
        using var prepared = Credentials(db).Prepare(id);
        using var rejected = await Credentials(db).ResetAsync(prepared, actor.UserId, person.RowVersion, "TRIAL_PROCEDURE", ct);
        Check(rejected.Status == expected && rejected.Credential == null, "transaction primitive rejects " + kind + " target without handoff");
    }
    foreach (var stage in new[] { "credential", "revocation", "audit", "audit-flush", "before-commit", "cancel" })
    {
        var person = await NewUser("fail" + stage.Replace("-", "")); await AddSession(person);
        var before = await AtomicState(person.UserId);
        await using var failureDb = Open(new ResetCommandFault(stage), new ResetCommitFault(stage));
        var service = Credentials(failureDb, stage == "revocation");
        using var prepared = service.Prepare(person.UserId);
        using var cancelled = new CancellationTokenSource(); if (stage == "cancel") cancelled.Cancel();
        var failed = false;
        try { using var result = await service.ResetAsync(prepared, actor.UserId, person.RowVersion, "TRIAL_PROCEDURE", stage == "cancel" ? cancelled.Token : ct); }
        catch (Exception error) when (error is InjectedResetFailure or DbUpdateException or OperationCanceledException) { failed = true; }
        Check(failed && await AtomicState(person.UserId) == before, stage + " failure rolls back hash, expiry, token, revocation and audit");
    }
    var raceTarget = await NewUser("race"); await AddSession(raceTarget);
    async Task<ConditionalCredentialResetResult> Race(long admin)
    {
        await using var context = Open(); var service = Credentials(context);
        using var prepared = service.Prepare(raceTarget.UserId);
        return await service.ResetAsync(prepared, admin, raceTarget.RowVersion, "TRIAL_PROCEDURE", ct);
    }
    var raceResults = await Task.WhenAll(Race(actor.UserId), Race(otherActor.UserId));
    try
    {
        Check(raceResults.Count(r => r.Status == ConditionalCredentialResetStatus.Success) == 1
            && raceResults.Count(r => r.Status == ConditionalCredentialResetStatus.Stale && r.Credential == null) == 1,
            "two administrators and connections with same token produce one commit and one stale result");
    }
    finally { foreach (var result in raceResults) result.Dispose(); }
    var rotateTarget = await NewUser("rotate");
    using (var firstPrepared = Credentials(db).Prepare(rotateTarget.UserId))
    using (var first = await Credentials(db).ResetAsync(firstPrepared, actor.UserId, rotateTarget.RowVersion, "TRIAL_PROCEDURE", ct))
    using (var nextPrepared = Credentials(db).Prepare(rotateTarget.UserId))
    using (var second = await Credentials(db).ResetAsync(nextPrepared, otherActor.UserId, first.Version!, "TRIAL_PROCEDURE", ct))
    {
        var saved = await Read(rotateTarget.UserId);
        Check(hasher.VerifyPassword(saved, saved.PasswordHash, first.Credential!.TakePasswordOnce()) == PasswordVerificationResult.Failed
            && hasher.VerifyPassword(saved, saved.PasswordHash, second.Credential!.TakePasswordOnce()) != PasswordVerificationResult.Failed,
            "sequential resets with fresh token leave only latest temporary password valid");
    }
    var lostTarget = await NewUser("lost"); await AddSession(lostTarget);
    var lost = false;
    await using (var context = Open(new ResetCommitFault("after-commit")))
    {
        var service = Credentials(context); using var prepared = service.Prepare(lostTarget.UserId);
        try { using var result = await service.ResetAsync(prepared, actor.UserId, lostTarget.RowVersion, "TRIAL_PROCEDURE", ct); }
        catch (InjectedResetFailure) { lost = true; }
    }
    var lostState = await AtomicState(lostTarget.UserId);
    Check(lost && !(await Read(lostTarget.UserId)).RowVersion.SequenceEqual(lostTarget.RowVersion)
        && await db.UserSessions.CountAsync(s => s.UserId == lostTarget.UserId && s.RevokedAt == null, ct) == 0,
        "failure after real SQL commit returns no result but retains complete rotation and revocation");
    using (var prepared = Credentials(db).Prepare(lostTarget.UserId))
    using (var result = await Credentials(db).ResetAsync(prepared, actor.UserId, lostTarget.RowVersion, "TRIAL_PROCEDURE", ct))
        Check(result.Status == ConditionalCredentialResetStatus.Stale && result.Credential == null
            && await AtomicState(lostTarget.UserId) == lostState, "retry after post-commit failure rejects old token without replay or second reset");

    // Permission doubles exercise approved scope logic with real SQL memberships; no catalog grants changed.
    var identity = new ResetIdentity(actor.UserId); var paths = new ResetPaths();
    var permissions = new PermissionService(identity, paths, new UnresolvedPermissionResourceResolver(), TimeProvider.System);
    var access = new UserCredentialResetAccess(db, identity, permissions, paths, TimeProvider.System);
    paths.Path = new("USER_MANAGE", "ALL", null, true, true, now, null, true, true);
    Check(await access.CanResetAsync(target.UserId, ct), "unbound valid USER_MANAGE ALL authorizes other target");
    Check(!await access.CanResetAsync(actor.UserId, ct), "even ALL cannot authorize administrative self-reset");
    paths.Path = paths.Path with { DataScope = "OWN" };
    Check(!await access.CanResetAsync(target.UserId, ct), "OWN cannot authorize admin reset");
    paths.Path = paths.Path with { DataScope = "BRANCH", BranchId = branches[0].BranchId };
    Check(!await access.CanResetAsync(target.UserId, ct), "matching primary branch without actor membership is insufficient");
    db.UserBranchAccesses.Add(new UserBranchAccess { UserId = actor.UserId, BranchId = branches[0].BranchId,
        GrantedAt = now, GrantedBy = otherActor.UserId, AccessLevel = "STANDARD", IsPrimary = true });
    await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
    Check(await access.CanResetAsync(target.UserId, ct), "valid BRANCH grant and effective shared memberships authorize");
    paths.Path = paths.Path with { BranchId = branches[1].BranchId };
    Check(!await access.CanResetAsync(target.UserId, ct), "cross-branch grant cannot borrow shared membership from another branch");
    paths.Path = paths.Path with { DataScope = "ALL", BranchId = branches[0].BranchId };
    Check(!await access.CanResetAsync(target.UserId, ct), "branch-bound ALL is not global or BRANCH reset authority");
    paths.Path = paths.Path with { BranchId = null, AssignmentActive = false };
    Check(!await access.CanResetAsync(target.UserId, ct), "inactive assignment denies reset authority");
    paths.Path = paths.Path with { AssignmentActive = true, PermissionCode = "ROLE_MANAGE" };
    Check(!await access.CanResetAsync(target.UserId, ct), "ROLE_MANAGE does not substitute for USER_MANAGE");
    var validPath = paths.Path with { PermissionCode = "USER_MANAGE", DataScope = "BRANCH", BranchId = branches[0].BranchId };
    foreach (var invalidPath in new[] { validPath with { RoleActive = false }, validPath with { ExpiresAt = now.AddMinutes(-1) },
        validPath with { AssignedAt = now.AddHours(1) }, validPath with { BranchAccessActive = false } })
    {
        paths.Path = invalidPath;
        Check(!await access.CanResetAsync(target.UserId, ct), "ineligible role/time/membership grant path denies reset");
    }
    paths.Path = validPath;
    await db.UserBranchAccesses.Where(a => a.UserId == target.UserId).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, DateTime.UtcNow), ct);
    Check(!await access.CanResetAsync(target.UserId, ct), "revoked target membership denies BRANCH reset");
    identity.Restricted = true;
    paths.Path = validPath with { DataScope = "ALL", BranchId = null };
    Check(!await access.CanResetAsync(target.UserId, ct), "restricted actor session cannot reset even with ALL");
    identity.Restricted = false;
    var guardState = await AtomicState(target.UserId);
    foreach (var guard in new[] { "version", "procedure", "dirty", "transaction" })
    {
        await using var context = Open(); var service = Credentials(context);
        using var prepared = service.Prepare(target.UserId);
        var rejected = false;
        if (guard == "dirty") context.Users.Attach(new User { UserId = actor.UserId });
        await using var outer = guard == "transaction" ? await context.Database.BeginTransactionAsync(ct) : null;
        try { using var result = await service.ResetAsync(prepared, actor.UserId,
            guard == "version" ? new byte[7] : (await Read(target.UserId)).RowVersion,
            guard == "procedure" ? "free form document evidence" : "TRIAL_PROCEDURE", ct); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { rejected = true; }
        Check(rejected && await AtomicState(target.UserId) == guardState, "invalid " + guard + " rejected before mutation");
    }
    // Task 33 supersedes Task 32's endpoint-absence assertion; actual security gates are tested in its HTTP suite.
    Check(typeof(HotelOps.Api.Controllers.AdminCredentialResetController)
        .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true).Length == 1
        && typeof(HotelOps.Api.Controllers.AdminCredentialResetController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true).Length == 0,
        "integrated reset controller requires authentication and has no anonymous bypass");
    await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new { success = true, checks = checks.Count, cases = checks,
        scope = "Technical transaction and scope helpers only; HTTP reset/reauthentication covered by Task 33 suite",
        actualTransportLossVerified = false, secretsPersisted = false }, new JsonSerializerOptions { WriteIndented = true }), ct);
    Console.WriteLine($"PASS: {checks.Count} reset transaction/scope checks");
}
finally
{
    await db.UserSessions.Where(s => ownedIds.Contains(s.UserId) && s.RevokedAt == null)
        .ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, DateTime.UtcNow), CancellationToken.None);
}

sealed class InjectedResetFailure : IOException { }
sealed class FailRevocation(HotelOpsDbContext db) : ISessionRevocationService
{
    public async Task<int> RevokeAllAsync(long id, DateTime now, CancellationToken ct = default)
    {
        await new SessionRevocationService(db).RevokeAllAsync(id, now, ct);
        throw new InjectedResetFailure();
    }
}
sealed class ResetCommandFault(string stage) : DbCommandInterceptor
{
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData data, int result, CancellationToken ct = default)
    {
        if (stage == "credential" && command.CommandText.Contains("UPDATE [u]")) throw new InjectedResetFailure();
        return ValueTask.FromResult(result);
    }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<DbDataReader> result, CancellationToken ct = default)
    {
        if (stage == "audit" && command.CommandText.Contains("INSERT INTO [audit_logs]")) throw new InjectedResetFailure();
        return ValueTask.FromResult(result);
    }
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData data, DbDataReader result, CancellationToken ct = default)
    {
        if (stage == "audit-flush" && command.CommandText.Contains("INSERT INTO [audit_logs]"))
        {
            await result.DisposeAsync();
            throw new InjectedResetFailure();
        }
        return result;
    }
}
sealed class ResetCommitFault(string stage) : DbTransactionInterceptor
{
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction tx, TransactionEventData data,
        InterceptionResult result, CancellationToken ct = default)
    {
        if (stage == "before-commit") throw new InjectedResetFailure();
        return ValueTask.FromResult(result);
    }
    public override Task TransactionCommittedAsync(DbTransaction tx, TransactionEndEventData data, CancellationToken ct = default)
    {
        if (stage == "after-commit") throw new InjectedResetFailure();
        return Task.CompletedTask;
    }
}
sealed class ResetIdentity(long id) : ICurrentUserAccessor
{
    public bool Restricted { get; set; }
    public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(id, 1, Restricted); return true; }
}
sealed class ResetPaths : IPermissionDataSource
{
    public PermissionGrantPath Path { get; set; } = null!;
    public Task<bool> IsActiveUserAsync(long id, CancellationToken ct) => Task.FromResult(true);
    public Task<bool> IsActiveBranchAsync(long id, CancellationToken ct) => Task.FromResult(true);
    public Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long id, string code, DateTime now, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PermissionGrantPath>>([Path]);
}
