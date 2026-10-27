using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// The schema subsection uses direct fixture SQL; the subsequent suites exercise the approved UC007 workflow.
if (args.Length != 2 || !Regex.IsMatch(args[0], @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$")
    || File.Exists(args[1])) throw new ArgumentException("Owned patched trial and NEW local receipt required");
var connection = new SqlConnectionStringBuilder { DataSource = @"DESKTOP-JI8U50T\SQLEXPRESS", InitialCatalog = args[0],
    IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10 };
using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(10));
var ct = budget.Token;
HotelOpsDbContext Open() => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection.ConnectionString).Options);
await using var db = Open();
await db.Database.OpenConnectionAsync(ct);
await using (var command = db.Database.GetDbConnection().CreateCommand())
{
    command.CommandText = "SELECT DB_NAME(),CONVERT(nvarchar(128),@@SERVERNAME),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')";
    await using var reader = await command.ExecuteReaderAsync(ct);
    if (!await reader.ReadAsync(ct) || reader.GetString(0) != args[0] || reader.GetString(1) != connection.DataSource
        || reader.IsDBNull(2) || reader.GetString(2) != "Task17") throw new InvalidOperationException("Trial guard failed");
}
var checks = new List<string>();
void Check(bool ok, string label)
{
    if (!ok) throw new InvalidOperationException("FAIL: " + label);
    checks.Add(label); Console.WriteLine("PASS: " + label);
}
Check(await new StartupSchemaValidator(db).ValidateAsync(ct) == SchemaCompatibility.Compatible, "patched trial startup compatible");
var prefix = "T38_" + Guid.NewGuid().ToString("N")[..10];
var branches = await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId).Take(2).ToArrayAsync(ct);
if (branches.Length != 2) throw new InvalidOperationException("Two active canonical branches required");
var now = DateTime.UtcNow;
async Task<User> User(string suffix)
{
    var code = prefix + suffix;
    var row = new User { EmployeeCode = code, Username = code, Email = code + "@example.invalid", FullName = "Assignment schema fixture",
        AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", PrimaryBranchId = branches[0].BranchId, CreatedAt = now };
    db.Add(row); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); return row;
}
UserBranchAccess Membership(long userId) => new() { UserId = userId, BranchId = branches[1].BranchId,
    AccessLevel = "STANDARD", IsPrimary = false, GrantedAt = now };
async Task<bool> InsertDuplicate(HotelOpsDbContext context, long userId)
{
    var row = Membership(userId); context.Add(row);
    try { await context.SaveChangesAsync(ct); return true; }
    catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 }) { return false; }
    finally { context.ChangeTracker.Clear(); }
}
var target = await User("latent");
var role = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "DEPT_MANAGER", ct);
var assignment = new UserRole { UserId = target.UserId, RoleId = role.RoleId, BranchId = branches[1].BranchId,
    AssignedAt = now, IsActive = true };
db.Add(assignment); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
try
{
    var identity = new Identity(target.UserId);
    var permissions = new PermissionService(identity, new EfPermissionDataSource(db), new UnresolvedPermissionResourceResolver(), TimeProvider.System);
    Check(!(await permissions.EvaluateBranchAsync("USER_VIEW", branches[1].BranchId, ct)).IsGranted,
        "existing valid branch role cannot grant without membership");
    var before = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == target.UserId, ct);
    Check(await InsertDuplicate(db, target.UserId), "actual schema permits new nonprimary branch membership");
    var after = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == target.UserId, ct);
    Check(after.RowVersion.SequenceEqual(before.RowVersion), "child membership INSERT does not automatically advance User.RowVersion");
    Check(after.PrimaryBranchId == before.PrimaryBranchId && after.DepartmentId == before.DepartmentId,
        "child membership does not automatically move primary branch or department");
    Check((await permissions.EvaluateBranchAsync("USER_VIEW", branches[1].BranchId, ct)).IsGranted,
        "next real SQL grant evaluation activates existing branch role after membership add");
    Check(await db.UserRoles.CountAsync(r => r.UserId == target.UserId, ct) == 1
        && (await db.UserRoles.AsNoTracking().SingleAsync(r => r.UserRoleId == assignment.UserRoleId, ct)).IsActive,
        "authority changed without editing existing UserRole");
    Check(!await InsertDuplicate(db, target.UserId), "actual unique constraint rejects duplicate effective membership");
    await db.UserBranchAccesses.Where(a => a.UserId == target.UserId && a.BranchId == branches[1].BranchId)
        .ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, now), ct);
    Check(!(await permissions.EvaluateBranchAsync("USER_VIEW", branches[1].BranchId, ct)).IsGranted,
        "next evaluation denies after membership revocation without permission cache");
    Check(!await InsertDuplicate(db, target.UserId), "unique constraint also rejects second row for revoked membership");
    var revoked = await db.UserBranchAccesses.AsNoTracking().SingleAsync(a => a.UserId == target.UserId, ct);
    await using (var transaction = await db.Database.BeginTransactionAsync(ct))
    {
        await db.UserBranchAccesses.Where(a => a.AccessId == revoked.AccessId).ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, (DateTime?)null), ct);
        Check((await permissions.EvaluateBranchAsync("USER_VIEW", branches[1].BranchId, ct)).IsGranted,
            "trial-only regrant simulation also restores pre-existing role authority");
        await transaction.RollbackAsync(ct);
    }
    Check(!(await permissions.EvaluateBranchAsync("USER_VIEW", branches[1].BranchId, ct)).IsGranted,
        "rollback restores revoked membership and denied authority atomically");
    var concurrent = await User("race");
    await using (var first = Open())
    await using (var second = Open())
    {
        var results = await Task.WhenAll(InsertDuplicate(first, concurrent.UserId), InsertDuplicate(second, concurrent.UserId));
        Check(results.Count(x => x) == 1, "two actual SQL inserts same tuple yield one success and one uniqueness conflict");
    }
    Check(await db.UserBranchAccesses.CountAsync(a => a.UserId == concurrent.UserId, ct) == 1,
        "SQL concurrency retains exactly one membership row");
    var tokenBefore = concurrent.RowVersion.ToArray();
    await using (var transaction = await db.Database.BeginTransactionAsync(ct))
    {
        var changed = await db.Users.Where(u => u.UserId == concurrent.UserId && u.RowVersion == tokenBefore)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.UpdatedAt, now), ct);
        Check(changed == 1, "aggregate token can be explicitly advanced under caller transaction without new column");
        var stale = await db.Users.Where(u => u.UserId == concurrent.UserId && u.RowVersion == tokenBefore)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.UpdatedAt, now), ct);
        Check(stale == 0, "actual SQL rowversion rejects stale second compare-and-swap");
        await transaction.RollbackAsync(ct);
    }
    Check((await db.Users.Where(u => u.UserId == concurrent.UserId).Select(u => u.RowVersion).SingleAsync(ct)).SequenceEqual(tokenBefore),
        "rollback restores original aggregate token value");
}
finally
{
    await db.UserRoles.Where(r => r.UserRoleId == assignment.UserRoleId).ExecuteDeleteAsync(CancellationToken.None);
}

await WorkflowSqlSuite.Run(connection.ConnectionString, Check, ct);
await AssignmentRaceSuite.Run(connection.ConnectionString, Check, ct);
await BranchHttpSuite.Run(connection.ConnectionString, Check, ct);
await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new { success = true, checks = checks.Count, cases = checks,
    scope = "Approved UC007 Add-only SQL schema/workflow/races and real HTTPS", uc007EndpointVerified = true,
    secretsPersisted = false }, new JsonSerializerOptions { WriteIndented = true }), ct);
Console.WriteLine($"PASS: {checks.Count} schema/workflow/SQL-race/HTTPS assertions");

sealed class Identity(long userId) : ICurrentUserAccessor
{
    public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(userId, 1, false); return true; }
}
