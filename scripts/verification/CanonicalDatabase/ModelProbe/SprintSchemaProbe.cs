using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

internal static class SprintSchemaProbe
{
    internal static async Task<int> RunAsync(string path)
    {
        if (File.Exists(path)) throw new IOException("Evidence already exists");
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = budget.Token;
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        await db.Database.OpenConnectionAsync(ct);
        await using var identity = db.Database.GetDbConnection().CreateCommand();
        identity.CommandText = """
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita'
              AND DB_NAME()='HotelOps_DEV3_142A_20261114_cc78c13f'
              AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
                AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run'
                AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c') THEN 1 ELSE 0 END
            """;
        if (!Equals(await identity.ExecuteScalarAsync(ct), 1)) throw new InvalidOperationException("Disposable identity rejected");
        var rows = new List<object>();
        var failures = 0;
        var method = typeof(SprintSchemaProbe).GetMethod(nameof(ReadZeroAsync))!;
        foreach (var entity in db.Model.GetEntityTypes().Where(e => !e.IsOwned() && e.BaseType == null).OrderBy(e => e.Name))
        {
            string? error = null;
            try { await (Task)method.MakeGenericMethod(entity.ClrType).Invoke(null, [db, ct])!; }
            catch (Exception e) { error = e.GetBaseException().Message; failures++; }
            rows.Add(new { entity = entity.ClrType.Name, success = error == null, error });
        }
        var triggers = new List<object>();
        var definitions = new Dictionary<string, string>();
        foreach (var package in new[] { ("checklists", "01_Checklists.sql"), ("housekeeping", "01_Housekeeping.sql"), ("progress", "01_Progress.sql") })
        {
            var source = await File.ReadAllTextAsync(Path.Combine("scripts", "database", "release", package.Item1, package.Item2), ct);
            foreach (var batch in System.Text.RegularExpressions.Regex.Split(source, @"(?im)^\s*GO\s*\r?$"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(batch.Trim(), @"^CREATE TRIGGER dbo\.([A-Za-z0-9_]+)");
                if (!match.Success) continue;
                definitions[match.Groups[1].Value] = batch;
            }
        }
        await using (var marker = db.Database.GetDbConnection().CreateCommand())
        {
            marker.CommandText = "SELECT COUNT(*) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.Housekeeping.EvidenceRevision.V1'";
            if (Equals(await marker.ExecuteScalarAsync(ct), 1))
            {
                var source = await File.ReadAllTextAsync("scripts/database/release/housekeeping/04_EvidenceInvalidation.sql", ct);
                foreach (var batch in System.Text.RegularExpressions.Regex.Split(source, @"(?im)^\s*GO\s*\r?$"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(batch.Trim(), @"^CREATE OR ALTER TRIGGER dbo\.([A-Za-z0-9_]+)");
                    if (match.Success) definitions[match.Groups[1].Value] = batch;
                }
            }
        }
        foreach (var (name, expected) in definitions)
        {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT OBJECT_DEFINITION(OBJECT_ID(@name))";
                var parameter = command.CreateParameter(); parameter.ParameterName = "@name";
                parameter.Value = "dbo." + name; command.Parameters.Add(parameter);
                var actual = await command.ExecuteScalarAsync(ct) as string;
                static string Normalize(string value) => System.Text.RegularExpressions.Regex.Replace(value.Replace("\r\n", "\n").Trim(),
                    @"^(?:CREATE OR ALTER|ALTER|CREATE)\s+TRIGGER\b", "CREATE TRIGGER");
                var equal = actual != null && Normalize(actual) == Normalize(expected);
                triggers.Add(new { name = parameter.Value, sourceMatches = equal });
                if (!equal) failures++;
        }
        var modules = new { checklist = await ChecklistSchema.StateAsync(db, ct),
            housekeeping = await HousekeepingSchema.InstalledAsync(db, ct), progress = await ProgressSchema.InstalledAsync(db, ct) };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { success = failures == 0,
            entityCount = rows.Count, failures, projections = rows, triggers, modules,
            scope = "Real SQL zero-row EF SELECT per mapped entity; not full type/constraint/trigger or workflow compatibility" },
            new JsonSerializerOptions { WriteIndented = true }), ct);
        Console.WriteLine($"SQL compatibility: entities={rows.Count}; failures={failures}; triggers={triggers.Count}; modules={JsonSerializer.Serialize(modules)}");
        return failures == 0 ? 0 : 1;
    }
    public static async Task ReadZeroAsync<T>(HotelOpsDbContext db, CancellationToken ct) where T : class
        => _ = await db.Set<T>().AsNoTracking().Take(0).ToListAsync(ct);
}
