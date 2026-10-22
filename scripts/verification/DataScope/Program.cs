using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using HotelOps.Verification;

using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
var ct = timeout.Token;
var modelOnly = args.Contains("--model-only");
var candidate = modelOnly
    ? new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer("Data Source=verification.invalid;Initial Catalog=ModelOnly;Integrated Security=True").Options)
    : await ReadOnlyVerification.OpenOrReportAsync(ct);
if (candidate is null) { Environment.ExitCode = 2; return; }
await using var db = candidate;
var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    checks++;
    Console.WriteLine("PASS: " + name);
}

var entity = db.Model.FindEntityType(typeof(RolePermission))!;
var property = entity.FindProperty(nameof(RolePermission.DataScope));
Check(property is not null, "EF recognizes DataScope");
var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
Check(entity.GetTableName() == "role_permissions" && property!.GetColumnName(table) == "data_scope", "Table/column mapping");
Check(property!.ClrType == typeof(string) && property.GetColumnType() == "varchar(20)"
    && property.GetMaxLength() == 20 && property.IsUnicode() == false, "CLR and varchar(20) facets");
Check(!property.IsNullable && property.GetDefaultValueSql() == "('BRANCH')", "Required column and database default");
Check(entity.FindPrimaryKey()!.Properties.Single().Name == nameof(RolePermission.RolePermissionId), "Existing primary key");
Check(entity.GetIndexes().Any(i => i.IsUnique && i.Properties.Select(p => p.Name)
    .SequenceEqual(new[] { nameof(RolePermission.RoleId), nameof(RolePermission.PermissionId) })), "Existing unique role/permission index");
Check(entity.GetForeignKeys().Count() == 3
    && entity.GetForeignKeys().Any(f => f.PrincipalEntityType.ClrType == typeof(Role) && f.Properties.Single().Name == nameof(RolePermission.RoleId))
    && entity.GetForeignKeys().Any(f => f.PrincipalEntityType.ClrType == typeof(Permission) && f.Properties.Single().Name == nameof(RolePermission.PermissionId)), "Existing relationships");

if (modelOnly) { Console.WriteLine($"PASS: {checks} model-only checks; no connection opened. SQL checks not run."); return; }
// No SaveChanges, migrations, schema creation or fixtures. All SQL below is SELECT.
await db.Database.OpenConnectionAsync(ct);
var connection = db.Database.GetDbConnection();
await using (var identity = connection.CreateCommand())
{
    identity.CommandText = "SELECT DB_NAME()";
    ReadOnlyVerification.AssertSelect(identity.CommandText);
    Check((string?)await identity.ExecuteScalarAsync(ct) == "HotelOpsDB", "Actual database is HotelOpsDB");
}
await using (var schema = connection.CreateCommand())
{
    schema.CommandText = """
        SELECT TYPE_NAME(c.user_type_id),c.max_length,c.is_nullable,d.definition
        FROM sys.columns c LEFT JOIN sys.default_constraints d ON d.object_id=c.default_object_id
        WHERE c.object_id=OBJECT_ID('dbo.role_permissions') AND c.name='data_scope'
        """;
    ReadOnlyVerification.AssertSelect(schema.CommandText);
    await using var reader = await schema.ExecuteReaderAsync(ct);
    Check(await reader.ReadAsync(ct) && reader.GetString(0) == "varchar" && reader.GetInt16(1) == 20
        && !reader.GetBoolean(2) && reader.GetString(3) == "('BRANCH')", "Live SQL schema matches EF facets/default");
}
var grants = await db.RolePermissions.AsNoTracking()
    .OrderBy(g => g.RolePermissionId)
    .Select(g => new { g.RolePermissionId, g.DataScope, g.Role.RoleCode, g.Permission.PermissionCode })
    .ToListAsync(ct);
Check(grants.Count == 140 && await db.Roles.CountAsync(ct) == 12
    && await db.Roles.CountAsync(r => r.IsActive, ct) == 10
    && await db.Permissions.CountAsync(ct) == 62, "Live RBAC catalog counts");
Check(grants.Count(g => g.DataScope == "ALL") == 8
    && grants.Count(g => g.DataScope == "BRANCH") == 81
    && grants.Count(g => g.DataScope == "OWN") == 51, "ALL=8, BRANCH=81, OWN=51");
Check(grants.All(g => g.DataScope is "ALL" or "BRANCH" or "OWN"), "No DEPARTMENT, null or unknown scope");
await using (var query = connection.CreateCommand())
{
    query.CommandText = "SELECT role_permission_id,data_scope FROM dbo.role_permissions ORDER BY role_permission_id";
    ReadOnlyVerification.AssertSelect(query.CommandText);
    await using var reader = await query.ExecuteReaderAsync(ct);
    var index = 0;
    var matches = true;
    while (await reader.ReadAsync(ct))
    {
        if (index >= grants.Count || grants[index].RolePermissionId != reader.GetInt64(0)
            || grants[index].DataScope != reader.GetString(1)) matches = false;
        index++;
    }
    Check(matches && index == grants.Count, "Every EF scope matches raw SQL by primary key");
}
var materialized = await db.RolePermissions.AsNoTracking().FirstAsync(ct);
Check(grants.Any(g => g.RolePermissionId == materialized.RolePermissionId && g.DataScope == materialized.DataScope), "Full entity materialization remains compatible");
Check(!db.ChangeTracker.Entries().Any(), "Read-only verification tracks no entities");
Console.WriteLine($"PASS: {checks} checks; no database writes performed. HTTP authentication integration not covered.");
