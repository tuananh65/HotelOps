using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using HotelOps.Api.Data;
using System.Globalization;

internal sealed class TestDatabase : DbCommandInterceptor
{
    internal const string Name = "HotelOps_Authorization_Test";
    internal string Server { get; }
    internal const string SecretsId = "HotelOps.Authorization.Integration.Task06";
    internal string ConnectionString { get; }
    internal static AsyncLocal<bool> InjectSqlFailure { get; } = new();
    private static int injectedFailures;
    internal static int InjectedFailures => Volatile.Read(ref injectedFailures);
    private readonly string guard;
    internal TestDatabase(string receiptPath)
    {
        using var receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        var identity = receipt.RootElement.GetProperty("identity");
        var configuration = Configuration();
        ConnectionString = configuration["ConnectionStrings:Integration"] ?? throw new InvalidOperationException("Separate Integration User Secret required.");
        var cs = new SqlConnectionStringBuilder(ConnectionString);
        Server = configuration["HotelOpsIntegration:ExpectedServer"] ?? configuration["Integration:ExpectedServer"] ?? cs.DataSource;
        if (cs.InitialCatalog != Name || cs.AttachDBFilename.Length != 0
            || !System.Text.RegularExpressions.Regex.IsMatch(Server, @"^[A-Za-z0-9_.-]+(\\[A-Za-z0-9_.-]+)?$"))
            throw new InvalidOperationException("Refusing non-test SQL connection.");
        if (identity.GetProperty("database").GetString() != Name || identity.GetProperty("server").GetString() != Server)
            throw new InvalidOperationException("Wrong test database receipt.");
        var id = identity.GetProperty("databaseId").GetInt32();
        if (id <= 0) throw new InvalidOperationException("Invalid database receipt ID.");
        var stamp = DateTime.ParseExact(identity.GetProperty("createStamp").GetString()!, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
            .ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        guard = $"IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) <> N'{Server}' OR DB_NAME() <> N'{Name}' OR DB_ID() <> {id} OR NOT EXISTS (SELECT 1 FROM sys.databases WHERE database_id=DB_ID() AND create_date=CONVERT(datetime,N'{stamp}',121)) OR NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.Authorization.Integration' AND CONVERT(nvarchar(128),value)=N'Task06/v1') THROW 51906, 'Integration target guard rejected', 1;\n";
    }
    internal static IConfigurationRoot Configuration() => new ConfigurationBuilder().AddUserSecrets(SecretsId).AddEnvironmentVariables().Build();
    internal HotelOpsDbContext Open() => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(ConnectionString).AddInterceptors(this).Options);
    private void Protect(DbCommand command)
    {
        if (!command.CommandText.StartsWith(guard, StringComparison.Ordinal))
        {
            var fault=InjectSqlFailure.Value && command.CommandText.Contains("[cleaning_tasks]",StringComparison.OrdinalIgnoreCase)
                ? "THROW 51907, 'Controlled integration SQL read failure', 1;\n" : "";
            if(fault.Length>0)Interlocked.Increment(ref injectedFailures);
            command.CommandText = guard + fault + command.CommandText;
        }
    }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r) { Protect(c); return r; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default) { Protect(c); return ValueTask.FromResult(r); }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand c, CommandEventData e, InterceptionResult<int> r) { Protect(c); return r; }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<int> r, CancellationToken ct = default) { Protect(c); return ValueTask.FromResult(r); }
    public override InterceptionResult<object> ScalarExecuting(DbCommand c, CommandEventData e, InterceptionResult<object> r) { Protect(c); return r; }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<object> r, CancellationToken ct = default) { Protect(c); return ValueTask.FromResult(r); }
}
