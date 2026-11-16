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
        var checks = new (string Name, string Type, string Method, object[] Args)[]
        {
            ("production-replenishment-notification-query", "HotelOps.Api.Services.MaterialRequests.MaterialApprovalNotificationDelivery", "ReplenishmentReceiptEvents", [db]),
            ("production-material-linked-work-query", "HotelOps.Api.Services.MaterialRequests.MaterialLinkedWorkRead", "Query",
                [db, -1L, -1L, db.InternalRequests.AsNoTracking().Where(r => r.RequesterId == -1L)]),
            ("production-replenishment-waiting-query", "HotelOps.Api.Services.Replenishments.ReplenishmentService", "WaitingQuery",
                [db, -1L, -1L, new long[] { -1L }, db.MaterialRequests.AsNoTracking().Where(r => r.RequesterId == -1L)])
        };
        foreach (var check in checks)
        {
        try
        {
            var type = typeof(HotelOpsDbContext).Assembly.GetType(check.Type, true)!;
            var method = type.GetMethod(check.Method, BindingFlags.NonPublic | BindingFlags.Static)!;
            var query = (IQueryable)method.Invoke(null, check.Args)!;
            var expression = Expression.Call(typeof(Queryable), nameof(Queryable.Take), [query.ElementType], query.Expression, Expression.Constant(0));
            var zeroRows = query.Provider.CreateQuery(expression);
            // Execute through EF so FromSql/SqlQuery parameters retain their actual
            // bindings; ToQueryString is diagnostic text, not an executable command.
            var toList = typeof(EntityFrameworkQueryableExtensions).GetMethods()
                .Single(m => m.Name == "ToListAsync" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2)
                .MakeGenericMethod(query.ElementType);
            var pending = (Task)toList.Invoke(null, [zeroRows, ct])!;
            await pending;
            var rows = (System.Collections.ICollection)pending.GetType().GetProperty("Result")!.GetValue(pending)!;
            if (rows.Count != 0) throw new InvalidOperationException("Zero-row probe returned business data.");
            results.Add(new { check = check.Name, status = "PASS", rows = 0 });
        }
        catch (Exception error)
        {
            failed = true;
            results.Add(new { check = check.Name, status = "FAIL", error = error.GetBaseException().Message });
        }
        }
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { recordedUtc = DateTime.UtcNow, server, database,
            expectedDatabaseGuid = guid.ToString("D"), actualDatabaseGuid = actualGuid,
            readOnly = true, mutations = 0, transport = "Local integrated SQL; not HTTPS/TLS acceptance", results,
            acceptance = "Query translation/schema execution only; no lifecycle, authorization, delivery or UC completion claim" }, new JsonSerializerOptions { WriteIndented = true }), ct);
        Console.WriteLine(failed ? "READ_PROBE_FAIL" : "READ_PROBE_PASS (zero rows; no mutation)");
        return failed ? 1 : 0;
    }
}
