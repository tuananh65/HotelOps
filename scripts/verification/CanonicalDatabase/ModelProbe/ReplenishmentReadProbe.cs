using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal static class ReplenishmentReadProbe
{
    internal static async Task<int> RunAsync(string output)
    {
        if (File.Exists(output)) throw new IOException("Evidence already exists.");
        var server = Environment.GetEnvironmentVariable("HOTELOPS_PROBE_SERVER") ?? throw new InvalidOperationException("Probe server required.");
        var database = Environment.GetEnvironmentVariable("HOTELOPS_PROBE_DATABASE") ?? throw new InvalidOperationException("Probe database required.");
        var guid = Guid.Parse(Environment.GetEnvironmentVariable("HOTELOPS_PROBE_GUID") ?? throw new InvalidOperationException("Expected database GUID required."));
        if (!database.StartsWith("HotelOps_DEV3_142A_", StringComparison.Ordinal) || guid == Guid.Empty)
            throw new InvalidOperationException("Explicit disposable identity required.");
        var connection = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database,
            IntegratedSecurity = true, Encrypt = true, ConnectTimeout = 5, ApplicationIntent = ApplicationIntent.ReadOnly };
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var ct = budget.Token;
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection.ConnectionString).Options);
        await db.Database.OpenConnectionAsync(ct);
        await using var identity = db.Database.GetDbConnection().CreateCommand();
        identity.CommandText = "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),DB_NAME(),database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()";
        string actualGuid;
        await using (var row = await identity.ExecuteReaderAsync(ct))
        {
            if (!await row.ReadAsync(ct)) throw new InvalidOperationException("Database identity unavailable.");
            actualGuid = row.GetGuid(2).ToString("D");
            if (row.GetString(0) != server || row.GetString(1) != database || row.GetGuid(2) != guid)
                throw new InvalidOperationException("Database identity mismatch.");
        }
        Console.WriteLine($"READ_ONLY_IDENTITY expected={guid:D} actual={actualGuid}");
        var results = new List<object>();
        var failed = false;
        // Exercise the production EF expression, including its canonical-event anti-join.
        // Take(0) compiles and executes SQL without reading business rows or creating fixtures.
        var delivery = typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.MaterialRequests.MaterialApprovalNotificationDelivery", true)!;
        var method = delivery.GetMethod("ReplenishmentReceiptEvents", BindingFlags.NonPublic | BindingFlags.Static)!;
        try
        {
            var query = (IQueryable)method.Invoke(null, [db])!;
            var expression = Expression.Call(typeof(Queryable), nameof(Queryable.Take), [query.ElementType], query.Expression, Expression.Constant(0));
            var sql = query.Provider.CreateQuery(expression).ToQueryString();
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql; command.CommandTimeout = 15;
            await using var rows = await command.ExecuteReaderAsync(ct);
            if (await rows.ReadAsync(ct)) throw new InvalidOperationException("Zero-row probe returned business data.");
            results.Add(new { check = "production-replenishment-notification-query", status = "PASS", rows = 0 });
        }
        catch (Exception error)
        {
            failed = true;
            results.Add(new { check = "production-replenishment-notification-query", status = "FAIL", error = error.GetBaseException().Message });
        }
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { recordedUtc = DateTime.UtcNow, server, database,
            expectedDatabaseGuid = guid.ToString("D"), actualDatabaseGuid = actualGuid,
            readOnly = true, mutations = 0, transport = "Local integrated SQL; not HTTPS/TLS acceptance", results,
            acceptance = "Query translation/schema execution only; no lifecycle, authorization, delivery or UC completion claim" }, new JsonSerializerOptions { WriteIndented = true }), ct);
        Console.WriteLine(failed ? "READ_PROBE_FAIL" : "READ_PROBE_PASS (zero rows; no mutation)");
        return failed ? 1 : 0;
    }
}
