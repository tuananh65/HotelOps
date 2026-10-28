using System.Data;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

if (args.Length is < 2 or > 3 || (args.Length == 3 && args[2] != "--persistence")
    || !Regex.IsMatch(args[0], @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_(Fresh|Upgrade|AuthUpgrade)$"))
    throw new ArgumentException("Usage: ModelProbe <isolated database> <new report path> [--persistence]");
if (File.Exists(args[1])) throw new IOException("Evidence already exists");
var connection = new SqlConnectionStringBuilder
{
    DataSource = @"DESKTOP-JI8U50T\SQLEXPRESS", InitialCatalog = args[0],
    IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10,
    ApplicationName = "HotelOps Canonical EF ReadOnly Probe"
};
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
var ct = timeout.Token;
await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
    .UseSqlServer(connection.ConnectionString).Options);
await db.Database.OpenConnectionAsync(ct);
await using (var identity = db.Database.GetDbConnection().CreateCommand())
{
    identity.CommandText = "SELECT CONVERT(nvarchar(128),@@SERVERNAME),DB_NAME()";
    await using var r = await identity.ExecuteReaderAsync(ct);
    if (!await r.ReadAsync(ct) || r.GetString(0) != connection.DataSource || r.GetString(1) != args[0])
        throw new InvalidOperationException("SQL identity mismatch");
}
var errors = new List<string>();
var advisories = new List<string>();
var observations = new List<object>();
_ = db.Model; // Forces runtime model validation, including relationship validation.
var entities = db.GetService<IDesignTimeModel>().Model.GetEntityTypes().ToArray();
var rowversions = new List<string>();
var relationshipChecks = 0;
var indexChecks = 0;
foreach (var entity in entities)
{
    var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
    var actual = new Dictionary<string, (string Type, bool Nullable)>();
    await using var command = db.Database.GetDbConnection().CreateCommand();
    command.CommandText = """
        SELECT c.name,ty.name,c.max_length,c.precision,c.scale,c.is_nullable
        FROM sys.columns c JOIN sys.types ty ON ty.user_type_id=c.user_type_id
        WHERE c.object_id=OBJECT_ID(@table)
        """;
    var parameter = command.CreateParameter(); parameter.ParameterName = "@table";
    parameter.Value = $"[{table.Schema ?? "dbo"}].[{table.Name}]"; command.Parameters.Add(parameter);
    await using (var reader = await command.ExecuteReaderAsync(ct))
    {
        while (await reader.ReadAsync(ct))
        {
            var type = reader.GetString(1); var length = reader.GetInt16(2);
            var precision = reader.GetByte(3); var scale = reader.GetByte(4);
            var sqlType = type switch
            {
                "varchar" or "char" or "varbinary" or "binary" => $"{type}({(length == -1 ? "max" : length.ToString())})",
                "nvarchar" or "nchar" => $"{type}({(length == -1 ? "max" : (length / 2).ToString())})",
                "decimal" or "numeric" => $"decimal({precision},{scale})",
                "datetime2" or "datetimeoffset" or "time" => scale == 7 ? type : $"{type}({scale})",
                "timestamp" => "rowversion",
                _ => type
            };
            actual.Add(reader.GetString(0), (sqlType, reader.GetBoolean(5)));
        }
    }
    foreach (var property in entity.GetProperties())
    {
        var column = property.GetColumnName(table)!;
        var expected = property.GetRelationalTypeMapping().StoreType.ToLowerInvariant()
            .Replace("datetime2(7)", "datetime2").Replace("timestamp", "rowversion");
        if (!actual.TryGetValue(column, out var sql)) errors.Add($"Missing {table.Name}.{column}");
        else if (sql.Type != expected || sql.Nullable != property.IsColumnNullable(table))
            errors.Add($"{table.Name}.{column}: SQL={sql}, EF=({expected}, {property.IsColumnNullable(table)})");
        if (expected == "rowversion")
        {
            rowversions.Add($"{table.Name}.{column}");
            if (!property.IsConcurrencyToken || property.ValueGenerated != ValueGenerated.OnAddOrUpdate)
                errors.Add($"Invalid concurrency mapping {table.Name}.{column}");
        }
        if (property.IsShadowProperty()) errors.Add($"Shadow property {entity.Name}.{property.Name}");
    }
    foreach (var key in entity.GetKeys())
    {
        var columns = string.Join(",", key.Properties.Select(p => p.GetColumnName(table)));
        await using var keyCommand = db.Database.GetDbConnection().CreateCommand();
        keyCommand.CommandText = """
            SELECT STRING_AGG(CONVERT(nvarchar(max),c.name),',') WITHIN GROUP(ORDER BY ic.key_ordinal)
            FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE i.object_id=OBJECT_ID(@table) AND i.is_unique=1 AND ic.key_ordinal>0
            GROUP BY i.index_id
            """;
        var p = keyCommand.CreateParameter(); p.ParameterName = "@table"; p.Value = $"[{table.Schema ?? "dbo"}].[{table.Name}]";
        keyCommand.Parameters.Add(p);
        await using var r = await keyCommand.ExecuteReaderAsync(ct);
        var found = false; while (await r.ReadAsync(ct)) found |= r.GetString(0) == columns;
        if (!found) errors.Add($"Missing unique key {table.Name}({columns})");
    }
    foreach (var fk in entity.GetForeignKeys())
    {
        var principal = StoreObjectIdentifier.Table(fk.PrincipalEntityType.GetTableName()!, fk.PrincipalEntityType.GetSchema());
        var from = string.Join(",", fk.Properties.Select(p => p.GetColumnName(table)));
        var to = string.Join(",", fk.PrincipalKey.Properties.Select(p => p.GetColumnName(principal)));
        await using var fkCommand = db.Database.GetDbConnection().CreateCommand();
        fkCommand.CommandText = """
            SELECT OBJECT_NAME(f.referenced_object_id),
              STRING_AGG(CONVERT(nvarchar(max),pc.name),',') WITHIN GROUP(ORDER BY fc.constraint_column_id),
              STRING_AGG(CONVERT(nvarchar(max),rc.name),',') WITHIN GROUP(ORDER BY fc.constraint_column_id)
            FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id
            JOIN sys.columns pc ON pc.object_id=f.parent_object_id AND pc.column_id=fc.parent_column_id
            JOIN sys.columns rc ON rc.object_id=f.referenced_object_id AND rc.column_id=fc.referenced_column_id
            WHERE f.parent_object_id=OBJECT_ID(@table) AND f.is_disabled=0 AND f.is_not_trusted=0
            GROUP BY f.object_id,f.referenced_object_id
            """;
        var p = fkCommand.CreateParameter(); p.ParameterName = "@table"; p.Value = $"[{table.Schema ?? "dbo"}].[{table.Name}]";
        fkCommand.Parameters.Add(p);
        await using var r = await fkCommand.ExecuteReaderAsync(ct);
        var found = false;
        while (await r.ReadAsync(ct)) found |= r.GetString(0) == principal.Name && r.GetString(1) == from && r.GetString(2) == to;
        if (!found) errors.Add($"Missing trusted FK {table.Name}({from})->{principal.Name}({to})");
        relationshipChecks++;
    }
    foreach (var index in entity.GetIndexes())
    {
        var columns = string.Join(",", index.Properties.Select(p => p.GetColumnName(table)));
        await using var indexCommand = db.Database.GetDbConnection().CreateCommand();
        indexCommand.CommandText = """
            SELECT i.is_unique,
              STRING_AGG(CONVERT(nvarchar(max),c.name),',') WITHIN GROUP(ORDER BY ic.key_ordinal)
            FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE i.object_id=OBJECT_ID(@table) AND i.is_disabled=0 AND ic.key_ordinal>0
            GROUP BY i.index_id,i.is_unique
            """;
        var p = indexCommand.CreateParameter(); p.ParameterName = "@table"; p.Value = $"[{table.Schema ?? "dbo"}].[{table.Name}]";
        indexCommand.Parameters.Add(p);
        await using var r = await indexCommand.ExecuteReaderAsync(ct);
        var found = false; while (await r.ReadAsync(ct)) found |= r.GetBoolean(0) == index.IsUnique && r.GetString(1) == columns;
        if (!found)
        {
            var message = $"Missing index {table.Name}({columns}), unique={index.IsUnique}";
            // EF conventions propose FK lookup indexes even in a Database First model.
            // Their absence is a performance advisory, not a relational correctness failure.
            if (!index.IsUnique && index is IConventionIndex convention && convention.GetConfigurationSource() == ConfigurationSource.Convention)
                advisories.Add(message);
            else errors.Add(message);
        }
        indexChecks++;
    }
    // Execute each entity's real EF SELECT; this also exercises provider materialization.
    try
    {
        var method = typeof(Probe).GetMethod(nameof(Probe.ReadAsync))!.MakeGenericMethod(entity.ClrType);
        await (Task)method.Invoke(null, [db, ct])!;
    }
    catch (Exception e) { errors.Add($"Read {table.Name}: {e.GetBaseException().Message}"); }
    observations.Add(new { table = table.Name, efColumns = entity.GetProperties().Count(), sqlColumns = actual.Count });
}
var branches = await db.Branches.AsNoTracking().OrderBy(b => b.BranchCode)
    .Select(b => new { b.BranchCode, departments = b.Departments.Count(d => d.IsActive) }).ToListAsync(ct);
var grants = await db.RolePermissions.AsNoTracking()
    .GroupBy(g => g.DataScope).Select(g => new { scope = g.Key, count = g.Count() }).ToListAsync(ct);
if (entities.Length != 82) errors.Add($"Expected 82 mapped entities, got {entities.Length}");
if (rowversions.Count != 9) errors.Add($"Expected 9 concurrency columns, got {rowversions.Count}");
var persistence = "Not Run";
if (args.Length == 3)
{
    var uomCount = await db.UnitsOfMeasures.AsNoTracking().CountAsync(ct);
    await using (var transaction = await db.Database.BeginTransactionAsync(ct))
    {
        var unit = new UnitsOfMeasure { UomCode = "CANON_" + Guid.NewGuid().ToString("N")[..12], UomName = "Trial only", IsActive = true };
        db.Add(unit);
        await db.SaveChangesAsync(ct);
        if (unit.RowVersion.Length != 8) errors.Add("INSERT did not return an 8-byte rowversion");
        var original = unit.RowVersion.ToArray();
        await using var stale = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(db.Database.GetDbConnection()).Options);
        await stale.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);
        var staleUnit = await stale.UnitsOfMeasures.SingleAsync(u => u.UomId == unit.UomId, ct);
        unit.UomName = "First update";
        await db.SaveChangesAsync(ct);
        if (unit.RowVersion.SequenceEqual(original)) errors.Add("UPDATE did not advance rowversion");
        staleUnit.UomName = "Stale update must fail";
        var rejected = false;
        try { await stale.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { rejected = true; }
        if (!rejected) errors.Add("Stale rowversion was not rejected");
        await transaction.RollbackAsync(ct);
    }
    db.ChangeTracker.Clear();
    if (await db.UnitsOfMeasures.AsNoTracking().CountAsync(ct) != uomCount) errors.Add("Trial fixture rollback did not restore UOM count");
    persistence = errors.Count == 0 ? "PASS: EF insert/update, stale rowversion rejection, fixture rollback" : "FAILED";
}
var report = new { database = args[0], success = errors.Count == 0, entityCount = entities.Length,
    rowversions, relationshipChecks, indexChecks, branches, grants, observations, errors, advisories, persistence,
    checks = new[] { "EF model validation", "All mapped column SQL types/nullability", "No shadow properties",
        "Rowversion concurrency/value generation", "Unique keys", "Trusted FK column mappings", "Index keys and uniqueness",
        "Read-only EF query per entity", "Branch and grant projections" } };
await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), ct);
Console.WriteLine(JsonSerializer.Serialize(new { report.database, report.success, report.entityCount, concurrencyColumns = rowversions.Count, relationshipChecks, indexChecks, advisoryCount = advisories.Count, errors }));
return report.success ? 0 : 1;

public static class Probe
{
    public static async Task ReadAsync<T>(HotelOpsDbContext db, CancellationToken ct) where T : class =>
        _ = await db.Set<T>().AsNoTracking().Take(1).ToListAsync(ct);
}
