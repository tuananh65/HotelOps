using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

if (args.Length != 2 || !Regex.IsMatch(args[0], @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$") || File.Exists(args[1]))
    throw new ArgumentException("Owned disposable trial and NEW receipt required");
var connection = new SqlConnectionStringBuilder { DataSource = @"DESKTOP-JI8U50T\SQLEXPRESS", InitialCatalog = args[0],
    IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10 };
using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(12)); var ct = budget.Token;
await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection.ConnectionString).Options);
await db.Database.OpenConnectionAsync(ct);
await using (var command = db.Database.GetDbConnection().CreateCommand())
{
    command.CommandText = "SELECT DB_NAME(),CONVERT(nvarchar(128),@@SERVERNAME),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')";
    await using var reader = await command.ExecuteReaderAsync(ct);
    if (!await reader.ReadAsync(ct) || reader.GetString(0) != args[0] || reader.GetString(1) != connection.DataSource
        || reader.IsDBNull(2) || reader.GetString(2) != "Task17") throw new InvalidOperationException("Trial identity rejected");
}
var checks = new List<string>();
void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException("FAIL: " + label); checks.Add(label); Console.WriteLine("PASS: " + label); }
Check(await new StartupSchemaValidator(db).ValidateAsync(ct) == SchemaCompatibility.Compatible, "owned disposable SQL startup schema compatible");
async Task<string> Catalog() => JsonSerializer.Serialize(new {
    roles = await db.Roles.AsNoTracking().OrderBy(r => r.RoleId).Select(r => new { r.RoleId, r.RoleCode, r.RoleName, r.IsActive, r.IsSystemRole }).ToArrayAsync(ct),
    grants = await db.RolePermissions.AsNoTracking().OrderBy(g => g.RolePermissionId).Select(g => new { g.RolePermissionId, g.RoleId, g.PermissionId, g.DataScope }).ToArrayAsync(ct),
    permissions = await db.Permissions.AsNoTracking().OrderBy(p => p.PermissionId).Select(p => new { p.PermissionId, p.PermissionCode }).ToArrayAsync(ct),
    branches = await db.Branches.AsNoTracking().OrderBy(b => b.BranchId).Select(b => new { b.BranchId, b.IsActive }).ToArrayAsync(ct),
    departments = await db.Departments.AsNoTracking().OrderBy(d => d.DepartmentId).Select(d => new { d.DepartmentId, d.DepartmentCode, d.BranchId, d.IsActive }).ToArrayAsync(ct) });
var before = await Catalog();
await AssignmentHttpSuite.Run(connection.ConnectionString, Check, ct);
Check(await Catalog() == before, "canonical roles permissions and grants unchanged after all owned trial fault/race cleanup");
await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new { success = true, checks = checks.Count, cases = checks,
    scope = "Task44 approved v1 actual HTTPS/SQL assignment and concurrency", formalSrsApprovalClaimed = false,
    roleRevokeEnabled = false, regrantEnabled = false, secretsPersisted = false }, new JsonSerializerOptions { WriteIndented = true }), ct);
Console.WriteLine($"PASS: {checks.Count} UC010 HTTPS/SQL assertions");
