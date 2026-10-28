using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

if (args.Length != 2 || !Regex.IsMatch(args[0], @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$"))
    throw new ArgumentException("Verified trial and new local evidence path required");
if (File.Exists(args[1])) throw new InvalidOperationException("Do not overwrite evidence");
var connection = new SqlConnectionStringBuilder { DataSource = @"DESKTOP-JI8U50T\SQLEXPRESS",
    InitialCatalog = args[0], IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10 };
using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(5));
var ct = budget.Token;
HotelOpsDbContext Open(IInterceptor? interceptor = null)
{
    var options = new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection.ConnectionString);
    if (interceptor != null) options.AddInterceptors(interceptor);
    return new(options.Options);
}
await using var db = Open();
await db.Database.OpenConnectionAsync(ct);
await using (var command = db.Database.GetDbConnection().CreateCommand())
{
    command.CommandText = "SELECT DB_NAME(),CONVERT(nvarchar(128),@@SERVERNAME),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')";
    await using var rows = await command.ExecuteReaderAsync(ct);
    if (!await rows.ReadAsync(ct) || rows.GetString(0) != args[0] || rows.GetString(1) != connection.DataSource
        || rows.IsDBNull(2) || rows.GetString(2) != "Task17") throw new InvalidOperationException("Trial identity rejected");
}
if (await new StartupSchemaValidator(db).ValidateAsync(ct) != SchemaCompatibility.Compatible)
    throw new InvalidOperationException("Patched trial required");
var checks = new List<string>();
void Check(bool valid, string label) { if (!valid) throw new InvalidOperationException("FAIL: " + label); checks.Add(label); Console.WriteLine("PASS: " + label); }
var prefix = "T30_" + Guid.NewGuid().ToString("N")[..10];
var hasher = new PasswordCredentialService(Options.Create(new PasswordHasherOptions
    { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
TemporaryCredentialService Credentials(HotelOpsDbContext context, bool fail = false) => new(context, hasher,
    fail ? new FailedRevoker() : new SessionRevocationService(context), TimeProvider.System);
User NewUser(string suffix) => new() { EmployeeCode = prefix + suffix, Username = prefix + suffix,
    Email = prefix + suffix + "@example.invalid", FullName = "Non-sensitive transaction fixture",
    AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = DateTime.UtcNow };
var actor = NewUser("actor"); actor.AccountStatus = "INACTIVE";
db.Users.Add(actor); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
var branch = await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId).FirstAsync(ct);
var department = await db.Departments.AsNoTracking().FirstAsync(d => d.BranchId == branch.BranchId && d.IsActive && d.DepartmentCode == "HK", ct);
var initialRoleId = await db.Roles.AsNoTracking().Where(r => r.RoleCode == "HK_STAFF" && r.IsActive).Select(r => r.RoleId).SingleAsync(ct);
async Task<long> Insert(HotelOpsDbContext context, string suffix)
{
    var user = NewUser(suffix); user.PrimaryBranchId = branch.BranchId; user.DepartmentId = department.DepartmentId;
    context.Users.Add(user); await context.SaveChangesAsync(ct); context.ChangeTracker.Clear();
    context.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.UserId, BranchId = branch.BranchId,
        IsPrimary = true, AccessLevel = "STANDARD", GrantedBy = actor.UserId, GrantedAt = DateTime.UtcNow });
    context.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = initialRoleId, BranchId = branch.BranchId,
        IsActive = true, AssignedAt = DateTime.UtcNow, AssignedBy = actor.UserId });
    context.AuditLogs.Add(new AuditLog { ActorUserId = actor.UserId, EntityType = "users", EntityId = user.UserId.ToString(),
        Action = "CREATE", OccurredAt = DateTime.UtcNow, NewValues = "{\"fixture\":true}" });
    await context.SaveChangesAsync(ct); context.ChangeTracker.Clear();
    return user.UserId;
}
IssuedTemporaryCredential Release(PendingInitialCredential pending) => (IssuedTemporaryCredential)typeof(PendingInitialCredential)
    .GetMethod("ReleaseAfterCommit", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(pending, null)!;
Check(!typeof(PendingInitialCredential).GetMethods().Any(m => m.Name.Contains("Password") || m.Name == "ReleaseAfterCommit"),
    "pending credential has no public secret/release accessor");

// Successful caller-controlled commit with the approved HK_STAFF/HK fixture.
long committed;
PendingInitialCredential pendingSuccess;
await using (var tx = await db.Database.BeginTransactionAsync(ct))
{
    committed = await Insert(db, "success");
    using var prepared = Credentials(db).Prepare(committed);
    pendingSuccess = await Credentials(db).StageInitialAsync(prepared, actor.UserId, ct);
    Check(db.Database.CurrentTransaction?.TransactionId == tx.TransactionId, "participant preserves caller transaction and does not commit");
    Check(pendingSuccess.ToString() == "[Pending initial credential: redacted]", "pending ToString is redacted");
    await tx.CommitAsync(ct);
}
using (pendingSuccess)
using (var issued = Release(pendingSuccess))
{
    var plaintext = issued.TakePasswordOnce();
    var saved = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == committed, ct);
    Check(hasher.VerifyPassword(saved, saved.PasswordHash, plaintext) != PasswordVerificationResult.Failed, "committed credential uses unchanged Identity V3 hashing");
    Check(saved.TemporaryPasswordExpiresAt > DateTime.UtcNow.AddHours(23.9) && saved.TemporaryPasswordExpiresAt <= DateTime.UtcNow.AddHours(24.01), "temporary policy remains 24 hours");
    var once = false; try { issued.TakePasswordOnce(); } catch (InvalidOperationException) { once = true; }
    Check(once, "committed handoff can be taken only once");
    var audit = await db.AuditLogs.AsNoTracking().Where(a => a.EntityId == committed.ToString()).Select(a => a.NewValues).ToArrayAsync(ct);
    Check(audit.All(a => a == null || (!a.Contains(plaintext) && !a.Contains(saved.PasswordHash!))), "audits contain neither plaintext nor hash values");
    Check(audit.Length == 2, "account CREATE and credential UPDATE audits both persist");
}

// Stage succeeds, then caller rolls everything back.
long rolledBack;
await using (var tx = await db.Database.BeginTransactionAsync(ct))
{
    rolledBack = await Insert(db, "rollback");
    using var prepared = Credentials(db).Prepare(rolledBack);
    using var pending = await Credentials(db).StageInitialAsync(prepared, actor.UserId, ct);
    await tx.RollbackAsync(ct);
}
Check(!await db.Users.AnyAsync(u => u.UserId == rolledBack, ct)
    && !await db.UserRoles.AnyAsync(a => a.UserId == rolledBack, ct)
    && !await db.UserBranchAccesses.AnyAsync(a => a.UserId == rolledBack, ct)
    && !await db.AuditLogs.AnyAsync(a => a.EntityId == rolledBack.ToString(), ct), "caller rollback removes user, credential, scope, role and both audits");

foreach (var failure in new[] { "credential", "audit" })
{
    var interceptor = new FailCredentialAudit();
    await using var failureDb = Open(interceptor);
    long id = 0; var failed = false;
    await using (var tx = await failureDb.Database.BeginTransactionAsync(ct))
    {
        id = await Insert(failureDb, failure);
        using var prepared = Credentials(failureDb).Prepare(id);
        interceptor.Enabled = failure == "audit";
        try { using var pending = await Credentials(failureDb, failure == "credential").StageInitialAsync(prepared, actor.UserId, ct); }
        catch (Exception e) when (e is InvalidOperationException or DbUpdateException) { failed = true; }
        await tx.RollbackAsync(ct);
    }
    Check(failed && !await db.Users.AnyAsync(u => u.UserId == id, ct)
        && !await db.UserRoles.AnyAsync(a => a.UserId == id, ct)
        && !await db.UserBranchAccesses.AnyAsync(a => a.UserId == id, ct)
        && !await db.AuditLogs.AnyAsync(a => a.EntityId == id.ToString(), ct), failure + " failure rolls back complete caller workflow");
}

await using (var tx = await db.Database.BeginTransactionAsync(ct))
{
    var id = await Insert(db, "cancelled");
    using var prepared = Credentials(db).Prepare(id);
    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
    var cancelledObserved = false;
    try { using var pending = await Credentials(db).StageInitialAsync(prepared, actor.UserId, cancelled.Token); }
    catch (OperationCanceledException) { cancelledObserved = true; }
    await tx.RollbackAsync(ct);
    Check(cancelledObserved && !await db.Users.AnyAsync(u => u.UserId == id, ct), "cancelled provisioning allows complete caller rollback without handoff");
}
await using (var tx = await db.Database.BeginTransactionAsync(ct))
{
    var id = await Insert(db, "dirty");
    using var prepared = Credentials(db).Prepare(id);
    var unsaved = NewUser("unsaved"); db.Users.Add(unsaved);
    var rejected = false;
    try { using var pending = await Credentials(db).StageInitialAsync(prepared, actor.UserId, ct); }
    catch (InvalidOperationException) { rejected = true; }
    db.Entry(unsaved).State = EntityState.Detached;
    using var pendingClean = await Credentials(db).StageInitialAsync(prepared, actor.UserId, ct);
    Check(rejected, "dirty tracker rejected before consuming preparation; clean retry stages safely");
    await tx.RollbackAsync(ct);
}
var wrapperFailureId = await Insert(db, "wrapperFailure");
var wrapperInterceptor = new FailCredentialAudit { Enabled = true };
await using (var wrapperDb = Open(wrapperInterceptor))
using (var prepared = Credentials(wrapperDb).Prepare(wrapperFailureId))
{
    var failed = false;
    try { using var issued = await Credentials(wrapperDb).ApplyAsync(prepared, actor.UserId, CredentialProvisioningOperation.InitialProvisioning, ct); }
    catch (Exception e) when (e is InvalidOperationException or DbUpdateException) { failed = true; }
    var unchanged = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == wrapperFailureId, ct);
    Check(failed && unchanged.PasswordHash == null && unchanged.TemporaryPasswordExpiresAt == null, "legacy wrapper audit failure rolls back its own credential transaction");
}

var standaloneId = await Insert(db, "standalone");
using (var prepared = Credentials(db).Prepare(standaloneId))
{
    var noTransaction = false;
    try { using var pending = await Credentials(db).StageInitialAsync(prepared, actor.UserId, ct); }
    catch (InvalidOperationException) { noTransaction = true; }
    Check(noTransaction, "participant refuses missing caller transaction without consuming preparation");
    using var issued = await Credentials(db).ApplyAsync(prepared, actor.UserId, CredentialProvisioningOperation.InitialProvisioning, ct);
    var secret = issued.TakePasswordOnce();
    var saved = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == standaloneId, ct);
    Check(hasher.VerifyPassword(saved, saved.PasswordHash, secret) != PasswordVerificationResult.Failed && db.Database.CurrentTransaction == null, "legacy ApplyAsync still owns commit and delivers valid credential");
}
using (var prepared = Credentials(db).Prepare(standaloneId))
{
    var refused = false;
    try { using var issued = await Credentials(db).ApplyAsync(prepared, actor.UserId, CredentialProvisioningOperation.InitialProvisioning, ct); }
    catch (InvalidOperationException) { refused = true; }
    Check(refused, "initial provisioning cannot overwrite existing credentials");
}
using (var prepared = Credentials(db).Prepare(standaloneId))
using (var issued = await Credentials(db).ApplyAsync(prepared, actor.UserId, CredentialProvisioningOperation.AdminReset, ct))
{
    var saved = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == standaloneId, ct);
    Check(hasher.VerifyPassword(saved, saved.PasswordHash, issued.TakePasswordOnce()) != PasswordVerificationResult.Failed, "legacy explicit reset remains functional");
}
await using (var tx = await db.Database.BeginTransactionAsync(ct))
using (var prepared = Credentials(db).Prepare(committed))
{
    var rejected = false;
    try { using var issued = await Credentials(db).ApplyAsync(prepared, actor.UserId, CredentialProvisioningOperation.AdminReset, ct); }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected, "legacy wrapper still rejects caller transaction");
    await tx.RollbackAsync(ct);
}
// Uniqueness backstop under two independent connections, never precheck-only.
foreach (var field in new[] { "Username", "Email", "EmployeeCode" })
{
    var a = NewUser("a" + field); var b = NewUser("b" + field);
    typeof(User).GetProperty(field)!.SetValue(b, typeof(User).GetProperty(field)!.GetValue(a));
    async Task<bool> Attempt(User user)
    {
        await using var context = Open(); context.Add(user);
        try { await context.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 }) { return false; }
    }
    var outcomes = await Task.WhenAll(Attempt(a), Attempt(b));
    Check(outcomes.Count(x => x) == 1, "concurrent duplicate " + field + " permits exactly one insert");
}
// Fault at the real SQL commit boundary, before the service can release plaintext.
// This is not a network-disconnect test and does not certify HTTP delivery.
var commitFault = new FailAfterCommit();
var lostCode = prefix + "lost";
var lostRequest = new CreateEmployeeAccountRequest(lostCode, lostCode, "Commit boundary fixture",
    lostCode + "@example.invalid", null, branch.BranchId, department.DepartmentId, "HK_STAFF");
UserAccountCreationService Creation(HotelOpsDbContext context)
{
    var credentialService = Credentials(context);
    return new(context, new CreationIdentity(actor.UserId), new CreationGrants(), credentialService,
        credentialService, TimeProvider.System);
}
var commitFailureObserved = false;
await using (var faultDb = Open(commitFault))
{
    try { using var unexpected = await Creation(faultDb).CreateAsync(lostRequest, ct); }
    catch (IOException error) when (error.Message == FailAfterCommit.Message) { commitFailureObserved = true; }
}
var committedAfterFault = await db.Users.AsNoTracking().SingleAsync(u => u.EmployeeCode == lostCode, ct);
Check(commitFailureObserved && commitFault.Fired && committedAfterFault.PasswordHash != null,
    "post-commit injected failure leaves committed account but returns no credential result");
var faultId = committedAfterFault.UserId;
var auditsBeforeRetry = await db.AuditLogs.CountAsync(a => a.EntityType == "users" && a.EntityId == faultId.ToString(), ct);
Check(auditsBeforeRetry == 2 && await db.UserRoles.CountAsync(r => r.UserId == faultId, ct) == 1
    && await db.UserBranchAccesses.CountAsync(a => a.UserId == faultId, ct) == 1,
    "post-commit failure preserves complete account, membership, role and both audits");
await using (var retryDb = Open())
using (var retry = await Creation(retryDb).CreateAsync(lostRequest, ct))
    Check(retry.Status == AccountCreationStatus.Duplicate && retry.User == null && retry.Credential == null
        && retry.DuplicateFields!.OrderBy(x => x).SequenceEqual(new[] { "email", "employeeCode", "username" }),
        "retry after committed failure conflicts without replaying credential or account response");
var afterRetry = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == faultId, ct);
Check(await db.Users.CountAsync(u => u.EmployeeCode == lostCode, ct) == 1
    && afterRetry.PasswordHash == committedAfterFault.PasswordHash
    && afterRetry.TemporaryPasswordExpiresAt == committedAfterFault.TemporaryPasswordExpiresAt
    && await db.AuditLogs.CountAsync(a => a.EntityType == "users" && a.EntityId == faultId.ToString(), ct) == auditsBeforeRetry,
    "retry preserves single account, original credential hash/expiry and audit count without reset");

await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new { success = true, checks = checks.Count, cases = checks,
    scope = "Technical participant only, not UC004 endpoint certification", secretsPersisted = false }, new JsonSerializerOptions { WriteIndented = true }), ct);
Console.WriteLine($"PASS: {checks.Count} credential transaction checks");

sealed class FailedRevoker : ISessionRevocationService
{
    public Task<int> RevokeAllAsync(long id, DateTime now, CancellationToken ct = default) => throw new InvalidOperationException("Injected credential step failure");
}

sealed class FailAfterCommit : DbTransactionInterceptor
{
    public const string Message = "Injected failure after actual SQL commit";
    public bool Fired { get; private set; }
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Fired = true;
        throw new IOException(Message);
    }
}
// Authorization doubles isolate the commit boundary; actual permissions are covered by the HTTP suite.
sealed class CreationIdentity(long actorId) : ICurrentUserAccessor
{
    public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(actorId, 1, false); return true; }
}
sealed class CreationGrants : IPermissionService
{
    public Task<PermissionEvaluation> EvaluateGlobalAsync(string code, CancellationToken ct)
        => Task.FromResult(new PermissionEvaluation(code is "USER_MANAGE" or "ROLE_MANAGE"
            ? PermissionDecision.Granted : PermissionDecision.Denied));
    public Task<PermissionEvaluation> EvaluateBranchAsync(string code, long branchId, CancellationToken ct)
        => throw new InvalidOperationException("Unexpected branch check");
    public Task<PermissionEvaluation> EvaluateResourceAsync(string code, PermissionResource resource, CancellationToken ct)
        => throw new InvalidOperationException("Unexpected resource check");
}
sealed class FailCredentialAudit : DbCommandInterceptor
{
    public bool Enabled { get; set; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<DbDataReader> result, CancellationToken ct = default)
    {
        if (Enabled && command.CommandText.Contains("INSERT INTO [audit_logs]")) throw new InvalidOperationException("Injected audit failure");
        return ValueTask.FromResult(result);
    }
}
