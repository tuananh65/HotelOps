using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

if ((args.Length != 2 && !(args.Length == 3 && args[2] is "--reset-http" or "--reset-create-order" or "--writers-http")) || !Regex.IsMatch(args[0], @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$") || File.Exists(args[1]))
    throw new ArgumentException("Owned patched trial and NEW receipt required");
var connection = new SqlConnectionStringBuilder { DataSource = @"DESKTOP-JI8U50T\SQLEXPRESS", InitialCatalog = args[0],
    IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10 };
using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(10)); var ct = budget.Token;
HotelOpsDbContext Open() => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection.ConnectionString).Options);
await using var setup = Open(); await setup.Database.OpenConnectionAsync(ct);
await using (var command = setup.Database.GetDbConnection().CreateCommand())
{
    command.CommandText = "SELECT DB_NAME(),CONVERT(nvarchar(128),@@SERVERNAME),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')";
    await using var reader = await command.ExecuteReaderAsync(ct);
    if (!await reader.ReadAsync(ct) || reader.GetString(0) != args[0] || reader.GetString(1) != connection.DataSource
        || reader.IsDBNull(2) || reader.GetString(2) != "Task17") throw new InvalidOperationException("Trial identity rejected");
}
var checks = new List<string>();
void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException("FAIL: " + label); checks.Add(label); Console.WriteLine("PASS: " + label); }
Check(await new StartupSchemaValidator(setup).ValidateAsync(ct) == SchemaCompatibility.Compatible, "guarded trial startup schema compatible");
if (await setup.UserRoles.AnyAsync(ct)) throw new InvalidOperationException("Trial without pre-existing UserRole assignments required for exact global counts");
if (args.Length == 3)
{
    await ResetHttpIntegration.Run(connection.ConnectionString, Check, ct, args[2] == "--reset-create-order", args[2] == "--writers-http");
    await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new { success = true, checks = checks.Count, cases = checks,
        scope = "Task43 actual HTTPS/SQL writer integration", mode = args[2], protectedWorkflows = new[] { "AdminCredentialResetWorkflow.ResetAsync", "TemporaryCredentialService.ResetAsync/ApplyAsync", "UserAccountCreationService.CreateAsync", "UserProfileService.UpdateAsync" },
        allWritersProtected = false, futureExpiryGuaranteed = false, secretsPersisted = false }, new JsonSerializerOptions { WriteIndented = true }), ct);
    Console.WriteLine($"PASS: {checks.Count} actual HTTPS/SQL last-admin reset assertions"); return;
}
var prefix = "T41_" + Guid.NewGuid().ToString("N")[..8]; var now = DateTime.UtcNow;
var users = new Dictionary<string, User>(); var roles = new List<long>();
var passwords = new PasswordCredentialService(Options.Create(new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
    IterationCount = PasswordCredentialService.BaselineIterationCount }));
var rawPassword = SessionCredential.Generate(); // Never output or persist plaintext.
var um = await setup.Permissions.SingleAsync(p => p.PermissionCode == "USER_MANAGE", ct);
var rm = await setup.Permissions.SingleAsync(p => p.PermissionCode == "ROLE_MANAGE", ct); setup.ChangeTracker.Clear();
var branch = await setup.Branches.AsNoTracking().Where(b => b.IsActive).Select(b => b.BranchId).FirstAsync(ct);
GlobalAdminSerialization Guard(HotelOpsDbContext db) => new(db, TimeProvider.System);
async Task<int> Spid(HotelOpsDbContext db) => await db.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
async Task<bool> WaitForLock(int waiter, int owner, Task pending)
{
    for (var i = 0; i < 100 && !pending.IsCompleted; i++)
    {
        if (await setup.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.dm_exec_requests WHERE session_id={waiter} AND blocking_session_id={owner} AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct) > 0) return true;
        await Task.Delay(50, ct);
    }
    return false;
}
async Task<User> User(string name)
{
    var code = prefix + name;
    var user = new User { EmployeeCode = code, Username = code + "@example.invalid", Email = code + ".mail@example.invalid", FullName = "Last admin fixture",
        AccountStatus = name == "inactive" ? "INACTIVE" : "ACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = now,
        DeletedAt = name == "deleted" ? now : null, LockedUntil = name == "locked" ? now.AddMinutes(30) : null,
        TemporaryPasswordExpiresAt = name == "temporary" ? now.AddHours(24) : name == "expiredCredential" ? now.AddHours(-1) : null };
    user.PasswordHash = name == "badHash" ? "unsupported" : passwords.HashPassword(user, rawPassword);
    setup.Add(user); await setup.SaveChangesAsync(ct); setup.ChangeTracker.Clear(); users.Add(name, user); return user;
}
async Task<Role> Role(string suffix, bool manage, bool roleManage, string scope = "ALL", bool active = true)
{
    var role = new Role { RoleCode = prefix + suffix, RoleName = "Capability fixture", IsActive = active };
    setup.Add(role); await setup.SaveChangesAsync(ct); setup.ChangeTracker.Clear(); roles.Add(role.RoleId);
    if (manage) setup.Add(new RolePermission { RoleId = role.RoleId, PermissionId = um.PermissionId, DataScope = scope });
    if (roleManage) setup.Add(new RolePermission { RoleId = role.RoleId, PermissionId = rm.PermissionId, DataScope = scope });
    await setup.SaveChangesAsync(ct); setup.ChangeTracker.Clear(); return role;
}
void Audit(HotelOpsDbContext db, long actor, string suffix) => db.Add(new AuditLog { ActorUserId = actor, EntityType = prefix, EntityId = suffix,
    Action = "UPDATE", OccurredAt = DateTime.UtcNow, OldValues = "{\"fixture\":true}", NewValues = "{\"fixture\":true}" });
try
{
    var full = await Role("full", true, true); var splitU = await Role("splitU", true, false); var splitR = await Role("splitR", false, true);
    var inactiveRole = await Role("inactive", true, true, active: false); var own = await Role("own", true, true, "OWN");
    foreach (var name in new[] { "a", "b", "locked", "temporary", "expiredCredential", "badHash", "ambiguous", "oneIdentifier", "expiring",
        "inactive", "deleted", "bound", "roleOff", "assignmentOff", "future", "expired", "own", "nameOnly" })
    {
        var user = await User(name);
        if (name == "nameOnly") continue; // No grants: role-like name must not confer authority.
        foreach (var roleId in name == "b" ? new[] { splitU.RoleId, splitR.RoleId } : new[] { name == "roleOff" ? inactiveRole.RoleId : name == "own" ? own.RoleId : full.RoleId })
            setup.Add(new UserRole { UserId = user.UserId, RoleId = roleId, BranchId = name == "bound" ? branch : null,
                IsActive = name != "assignmentOff", AssignedAt = name == "future" ? now.AddHours(1) : now,
                ExpiresAt = name == "expired" ? now.AddHours(-1) : name == "expiring" ? now.AddHours(1) : null });
        await setup.SaveChangesAsync(ct); setup.ChangeTracker.Clear();
    }
    var collider = await User("collider");
    await setup.Users.Where(u => u.UserId == collider.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Username, users["ambiguous"].Email)
        .SetProperty(u => u.Email, users["ambiguous"].Username).SetProperty(u => u.AccountStatus, "INACTIVE"), ct);
    var singleCollider = await User("singleCollider");
    await setup.Users.Where(u => u.UserId == singleCollider.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Email, users["oneIdentifier"].Username), ct);
    setup.Add(new UserBranchAccess { UserId = users["bound"].UserId, BranchId = branch, AccessLevel = "STANDARD", GrantedAt = now });
    await setup.SaveChangesAsync(ct); setup.ChangeTracker.Clear();
    await using (var db = Open())
    {
        var missing = false; try { await Guard(db).AcquireAsync(ct: ct); } catch (InvalidOperationException) { missing = true; }
        Check(missing, "global guard refuses missing caller transaction");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadUncommitted, ct);
        var unsupported = false; try { await Guard(db).AcquireAsync(ct: ct); } catch (InvalidOperationException) { unsupported = true; }
        Check(unsupported, "global guard refuses nonlocking isolation");
    }
    await using (var db = Open())
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); db.Attach(new User { UserId = users["a"].UserId });
        var dirty = false; try { await Guard(db).AcquireAsync(ct: ct); } catch (InvalidOperationException) { dirty = true; }
        Check(dirty, "global guard refuses tracked work before acquisition"); db.ChangeTracker.Clear();
        var scope = await Guard(db).AcquireAsync(ct: ct); var snapshot = await scope.ReadSnapshotAsync(ct);
        var expected = new[] { "a", "b", "locked", "oneIdentifier", "expiring" }.Select(k => users[k].UserId).Order().ToArray();
        Check(snapshot.DurableEligibleUserIds.SequenceEqual(expected), "durable eligibility uses effective capabilities normal credential and actual SQL identifier uniqueness");
        Check(snapshot.ImmediatelyLoginCapableUserIds.Count == expected.Length - 1 && !snapshot.ImmediatelyLoginCapableUserIds.Contains(users["locked"].UserId), "finite lockout preserves durable count but denies immediate login");
        Check(snapshot.CapabilityCandidateUserIds.Contains(users["temporary"].UserId) && !snapshot.DurableEligibleUserIds.Contains(users["temporary"].UserId)
            && !snapshot.DurableEligibleUserIds.Contains(users["expiredCredential"].UserId), "valid and expired temporary credentials cannot count as full administrators");
        Check(snapshot.DurableEligibleUserIds.Contains(users["b"].UserId) && !snapshot.CapabilityCandidateUserIds.Contains(users["bound"].UserId), "independent unbound grants intersect per principal and branch-bound ALL does not count");
        Check(snapshot.HasNonExpiringDurableAdministrator && snapshot.NextKnownCapabilityLossUtc.HasValue, "snapshot distinguishes nonexpiring coverage and known role expiry risk");
        var otherScope = await Guard(db).AcquireAsync(ct: ct); var foreign = false;
        try { await otherScope.EnsurePreservedAsync(snapshot, ct); } catch (InvalidOperationException) { foreign = true; }
        Check(foreign, "guard rejects snapshot from another acquisition even in same transaction");
        await tx.CommitAsync(ct);
        var ended = false; try { await scope.ReadSnapshotAsync(ct); } catch (InvalidOperationException) { ended = true; }
        Check(ended, "snapshot refuses completed original transaction");
    }
    // Reduce only owned fixtures to two regular principals. Setup SQL is not a production writer.
    var others = users.Where(p => p.Key is not ("a" or "b")).Select(p => p.Value.UserId).ToArray();
    await setup.Users.Where(u => others.Contains(u.UserId)).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "INACTIVE"), ct);
    var a = users["a"].UserId; var b = users["b"].UserId;
    await using (var first = Open())
    await using (var second = Open())
    {
        await using var tx1 = await first.Database.BeginTransactionAsync(ct); await using var tx2 = await second.Database.BeginTransactionAsync(ct);
        var held = await Guard(first).AcquireAsync(ct: ct); var before = await held.ReadSnapshotAsync(ct); var owner = await Spid(first); var waiter = await Spid(second);
        Check(before.DurableEligibleUserIds.Count == 2, "two regular fixture principals before serialized mutations");
        var waiting = Guard(second).AcquireAsync(15000, ct); Check(await WaitForLock(waiter, owner, waiting), "second global transaction has actual SQL lock wait before reading count");
        await first.Users.Where(u => u.UserId == a).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "INACTIVE"), ct);
        Audit(first, b, "first"); await first.SaveChangesAsync(ct); first.ChangeTracker.Clear(); await held.EnsurePreservedAsync(before, ct); await tx1.CommitAsync(ct);
        var acquired = await waiting; var latest = await acquired.ReadSnapshotAsync(ct);
        Check(latest.DurableEligibleUserIds.SequenceEqual(new[] { b }), "concurrent snapshot observes committed first mutation instead of stale two-admin count");
        await second.UserRoles.Where(r => r.UserId == b && r.RoleId == splitR.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
        Audit(second, b, "rejected"); await second.SaveChangesAsync(ct); second.ChangeTracker.Clear(); var rejected = false;
        try { await acquired.EnsurePreservedAsync(latest, ct); } catch (GlobalAdministrationInvariantException) { rejected = true; }
        await tx2.RollbackAsync(ct);
        Check(rejected && await setup.UserRoles.AnyAsync(r => r.UserId == b && r.RoleId == splitR.RoleId && r.IsActive, ct)
            && !await setup.AuditLogs.AnyAsync(x => x.EntityType == prefix && x.EntityId == "rejected", ct), "last-admin role removal and its audit roll back after one competing mutation commits");
    }
    await using (var db = Open())
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); var guard = await Guard(db).AcquireAsync(ct: ct); var before = await guard.ReadSnapshotAsync(ct);
        await db.Users.Where(u => u.UserId == b).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "INACTIVE"), ct);
        var rejected = false; try { await guard.EnsurePreservedAsync(before, ct); } catch (GlobalAdministrationInvariantException) { rejected = true; }
        await tx.RollbackAsync(ct);
        Check(rejected && await setup.Users.AnyAsync(u => u.UserId == b && u.AccountStatus == "ACTIVE", ct), "single last administrator status removal is refused and rolled back");
    }
    await setup.Users.Where(u => u.UserId == a).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "ACTIVE"), ct);
    await using (var db = Open())
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); var guard = await Guard(db).AcquireAsync(ct: ct); var before = await guard.ReadSnapshotAsync(ct);
        await db.Users.Where(u => u.UserId == a || u.UserId == b).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "INACTIVE"), ct);
        await db.Users.Where(u => u.UserId == users["expiring"].UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "ACTIVE"), ct);
        var after = await guard.EnsurePreservedAsync(before, ct);
        Check(after.DurableEligibleUserIds.Count == 1 && !after.HasNonExpiringDurableAdministrator && after.NextKnownCapabilityLossUtc.HasValue,
            "finite-only survivor is mutation-time eligible but FUTURE EXPIRY NOT GUARANTEED is explicit");
    }
    await using (var db = Open())
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); var guard = await Guard(db).AcquireAsync(ct: ct); var before = await guard.ReadSnapshotAsync(ct);
        await db.Users.Where(u => u.UserId == a).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "INACTIVE"), ct);
        Audit(db, -1, "fault"); var failed = false;
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { failed = true; } db.ChangeTracker.Clear(); await tx.RollbackAsync(ct);
        Check(failed && await setup.Users.AnyAsync(u => u.UserId == a && u.AccountStatus == "ACTIVE", ct), "real SQL audit FK failure rolls back otherwise permissible capability mutation");
    }
    await using (var heldDb = Open())
    await using (var blockedDb = Open())
    {
        await using var heldTx = await heldDb.Database.BeginTransactionAsync(ct); await using var blockedTx = await blockedDb.Database.BeginTransactionAsync(ct);
        await Guard(heldDb).AcquireAsync(ct: ct); var timedOut = false;
        try { await Guard(blockedDb).AcquireAsync(100, ct); } catch (GlobalAdminLockException e) when (e.Result == -1) { timedOut = true; }
        Check(timedOut, "SQL application lock timeout fails closed without snapshot or mutation"); await blockedTx.RollbackAsync(ct);
        await heldTx.RollbackAsync(ct);
    }
    await using (var heldDb = Open())
    await using (var blockedDb = Open())
    {
        await using var heldTx = await heldDb.Database.BeginTransactionAsync(ct); await using var blockedTx = await blockedDb.Database.BeginTransactionAsync(ct);
        await Guard(heldDb).AcquireAsync(ct: ct); using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct); cancel.CancelAfter(200);
        var cancelled = false; try { await Guard(blockedDb).AcquireAsync(5000, cancel.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        catch (GlobalAdminLockException e) when (e.Result == -2 && cancel.IsCancellationRequested) { cancelled = true; }
        await blockedTx.RollbackAsync(CancellationToken.None); await heldTx.CommitAsync(ct);
        Check(cancelled, "waiting SQL lock acquisition honors cancellation and caller rollback");
    }
    await using (var db = Open())
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); var guard = await Guard(db).AcquireAsync(100, ct);
        Check((await guard.ReadSnapshotAsync(ct)).DurableEligibleUserIds.Count == 2, "commit rollback timeout cancellation release lock and permit fresh retry");
    }
    await using (var heldDb = Open())
    await using (var writerDb = Open())
    {
        await using var tx = await heldDb.Database.BeginTransactionAsync(ct); await writerDb.Database.OpenConnectionAsync(ct);
        var guard = await Guard(heldDb).AcquireAsync(ct: ct); await guard.ReadSnapshotAsync(ct);
        var writerSpid = await Spid(writerDb); var heldSpid = await Spid(heldDb);
        var update = writerDb.RolePermissions.Where(r => r.RoleId == full.RoleId && r.PermissionId == um.PermissionId).ExecuteUpdateAsync(s => s.SetProperty(r => r.DataScope, "OWN"), ct);
        Check(await WaitForLock(writerSpid, heldSpid, update), "actual grant revoker waits on retained snapshot SQL locks through commit");
        await tx.CommitAsync(ct); await update;
    }
    await using (var db = Open())
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); var guard = await Guard(db).AcquireAsync(ct: ct);
        Check((await guard.ReadSnapshotAsync(ct)).DurableEligibleUserIds.SequenceEqual(new[] { b }), "fresh snapshot observes committed grant revocation with no permission cache");
    }
    await setup.RolePermissions.Where(r => r.RoleId == full.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.DataScope, "ALL"), ct);
    await Deadlock();
    Check(await setup.Users.AnyAsync(u => u.UserId == a && u.AccountStatus == "ACTIVE", ct), "real deadlock victim caller rollback preserves fixture account state");
    await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new { success = true, checks = checks.Count, cases = checks,
        scope = "Independent SQL primitive and caller-owned fixture mutations only", existingHttpWritersProtected = false,
        deactivationEnabled = false, futureExpiryGuaranteed = false, secretsPersisted = false }, new JsonSerializerOptions { WriteIndented = true }), ct);
    Console.WriteLine($"PASS: {checks.Count} independent SQL assertions; existing mutation integration NOT VERIFIED");

    async Task Deadlock()
    {
        await using var ordered = Open(); await using var inverted = Open();
        await using var goodTx = await ordered.Database.BeginTransactionAsync(ct); await using var badTx = await inverted.Database.BeginTransactionAsync(ct);
        await inverted.Database.ExecuteSqlRawAsync("SET DEADLOCK_PRIORITY LOW", ct);
        await Guard(ordered).AcquireAsync(ct: ct);
        var resource = prefix + ":intentional-inversion";
        async Task LockFeature(HotelOpsDbContext db)
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand(); cmd.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
            cmd.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource,@LockOwner='Transaction',@LockMode='Exclusive',@LockTimeout=15000; SELECT @r";
            cmd.Parameters.Add(new SqlParameter("@resource", resource)); if (Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) < 0) throw new InvalidOperationException("Fixture lock refused");
        }
        await LockFeature(inverted); await inverted.Users.Where(u => u.UserId == a).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "INACTIVE"), ct);
        var orderedSpid = await Spid(ordered); var invertedSpid = await Spid(inverted);
        var waiting = LockFeature(ordered); Check(await WaitForLock(orderedSpid, invertedSpid, waiting), "intentional inverse-order fixture establishes actual SQL wait before deadlock");
        var victim = false;
        try { await Guard(inverted).AcquireAsync(15000, ct); }
        catch (GlobalAdminLockException e) when (e.Result == -3) { victim = true; }
        catch (SqlException e) when (e.Number == 1205) { victim = true; }
        try { await badTx.RollbackAsync(ct); } catch (InvalidOperationException) when (victim) { }
        await waiting; await goodTx.RollbackAsync(ct);
        Check(victim, "real SQL deadlock victim fails closed; no production inverse-order writer is introduced");
    }
}
finally
{
    setup.ChangeTracker.Clear(); var ids = users.Values.Select(u => u.UserId).ToArray();
    await setup.UserRoles.Where(r => ids.Contains(r.UserId)).ExecuteDeleteAsync(CancellationToken.None);
    await setup.RolePermissions.Where(r => roles.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
    await setup.Roles.Where(r => roles.Contains(r.RoleId)).ExecuteDeleteAsync(CancellationToken.None);
    // Users and audits are retained as owned trial fixtures; no sessions or plaintext receipts are created.
}
