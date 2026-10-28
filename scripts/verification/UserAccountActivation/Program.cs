using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

if (args.Length != 2 || !Regex.IsMatch(args[0], @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$")
    || File.Exists(args[1])) throw new ArgumentException("Owned patched trial and NEW local receipt required");
var connection = new SqlConnectionStringBuilder { DataSource = @"DESKTOP-JI8U50T\SQLEXPRESS", InitialCatalog = args[0],
    IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10 };
using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(10));
var ct = budget.Token;
await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection.ConnectionString).Options);
await db.Database.OpenConnectionAsync(ct);
await using (var command = db.Database.GetDbConnection().CreateCommand())
{
    command.CommandText = "SELECT DB_NAME(),CONVERT(nvarchar(128),@@SERVERNAME),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')";
    await using var reader = await command.ExecuteReaderAsync(ct);
    if (!await reader.ReadAsync(ct) || reader.GetString(0) != args[0] || reader.GetString(1) != connection.DataSource
        || reader.IsDBNull(2) || reader.GetString(2) != "Task17") throw new InvalidOperationException("Trial guard rejected");
}
var checks = new List<string>();
void Check(bool ok, string label)
{
    if (!ok) throw new InvalidOperationException("FAIL: " + label);
    checks.Add(label); Console.WriteLine("PASS: " + label);
}
Check(await new StartupSchemaValidator(db).ValidateAsync(ct) == SchemaCompatibility.Compatible, "guarded patched trial passes startup schema gate");
await ActivationRaceSuite.Run(connection.ConnectionString, Check, ct);
await ActivationHttpSuite.Run(connection.ConnectionString, Check, ct);
await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new { success = true, checks = checks.Count, cases = checks,
    actualSqlLockWaitsVerified = true, actualHttpsApiVerified = true, actualNetworkResponseLossVerified = false,
    deactivationEnabled = false, secretsPersisted = false }, new JsonSerializerOptions { WriteIndented = true }), ct);
Console.WriteLine($"PASS: {checks.Count} SQL-race/HTTPS activation assertions");
