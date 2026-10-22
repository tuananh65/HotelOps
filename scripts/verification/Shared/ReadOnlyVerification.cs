using System.Data.Common;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace HotelOps.Verification;

internal static class ReadOnlyVerification
{
    internal const string SecretsId = "HotelOps.Authorization.Verification";
    internal static async Task<HotelOpsDbContext?> OpenOrReportAsync(CancellationToken ct)
    {
        try { return await OpenAsync(ct); }
        catch (InvalidOperationException error) when (error.Message.StartsWith("READ_ONLY_", StringComparison.Ordinal))
        {
            Console.WriteLine("BLOCKED: " + error.Message);
            return null;
        }
        catch (SqlException)
        {
            Console.WriteLine("BLOCKED: READ_ONLY_CONNECTION_UNAVAILABLE: SQL connection/privilege preflight failed; check local configuration. Provider details suppressed.");
            return null;
        }
    }
    internal static async Task<HotelOpsDbContext> OpenAsync(CancellationToken ct)
    {
        var config = new ConfigurationBuilder().AddUserSecrets(SecretsId).AddEnvironmentVariables().Build();
        var value = config.GetConnectionString("AuthorizationReadOnly");
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("READ_ONLY_SETUP_REQUIRED: configure ConnectionStrings:AuthorizationReadOnly in separate verification User Secrets or environment; no fallback to HotelOpsDb.");
        var settings = new SqlConnectionStringBuilder(value);
        if (settings.InitialCatalog != "HotelOpsDB" || settings.AttachDBFilename.Length != 0)
            throw new InvalidOperationException("Read verification target must be HotelOpsDB without AttachDBFilename.");
        settings.ApplicationIntent = ApplicationIntent.ReadOnly; // Hint only; privilege checks below enforce the policy.
        settings.ApplicationName = "HotelOps Authorization SELECT verification";
        var connection = new SqlConnection(settings.ConnectionString);
        try
        {
            await connection.OpenAsync(ct);
            await using var check = connection.CreateCommand();
            check.CommandText = """
                SELECT CASE WHEN DB_NAME()=N'HotelOpsDB' THEN 1 ELSE 0 END,
                  CASE WHEN EXISTS (SELECT 1 FROM sys.fn_my_permissions(NULL,'SERVER')
                    WHERE permission_name NOT IN ('CONNECT SQL','VIEW ANY DATABASE','VIEW ANY DEFINITION','VIEW SERVER STATE','VIEW SERVER PERFORMANCE STATE','VIEW SERVER SECURITY STATE'))
                    OR EXISTS (SELECT 1 FROM sys.fn_my_permissions(NULL,'DATABASE')
                    WHERE permission_name NOT IN ('CONNECT','SELECT','VIEW DEFINITION','VIEW DATABASE STATE','VIEW DATABASE PERFORMANCE STATE','VIEW DATABASE SECURITY STATE'))
                    OR EXISTS (SELECT 1 FROM sys.objects o CROSS APPLY sys.fn_my_permissions(QUOTENAME(SCHEMA_NAME(o.schema_id))+'.'+QUOTENAME(o.name),'OBJECT') p
                      WHERE o.is_ms_shipped=0 AND p.permission_name NOT IN ('SELECT','VIEW DEFINITION','REFERENCES'))
                    OR EXISTS (SELECT 1 FROM sys.columns c JOIN sys.objects o ON o.object_id=c.object_id
                      WHERE o.is_ms_shipped=0 AND o.type IN ('U','V')
                        AND HAS_PERMS_BY_NAME(QUOTENAME(SCHEMA_NAME(o.schema_id))+'.'+QUOTENAME(o.name),'OBJECT','UPDATE',c.name,'COLUMN')=1)
                    THEN 1 ELSE 0 END
                """;
            await using (var reader = await check.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct) || reader.GetInt32(0) != 1) throw new InvalidOperationException("Read-only verification database identity rejected.");
                if (reader.GetInt32(1) != 0) throw new InvalidOperationException("READ_ONLY_PRIVILEGE_REJECTED: configured principal has write, DDL, execution or administrative privileges; request a SELECT-only principal. No tests executed with this credential.");
            }
            return new ReadOnlyContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
                .UseSqlServer(connection, contextOwnsConnection: true).AddInterceptors(new SelectOnlyInterceptor()).Options);
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    internal static void AssertSelect(string sql)
    {
        // Deliberately conservative: only single SELECT commands, no batches, SELECT INTO or EXEC.
        var normalized = Regex.Replace(sql, @"N?'(?:''|[^'])*'", "''", RegexOptions.IgnoreCase);
        if (!Regex.IsMatch(normalized, @"^\s*SELECT\b", RegexOptions.IgnoreCase)
            || Regex.IsMatch(normalized, @";|--|/\*|\b(INSERT|UPDATE|DELETE|MERGE|ALTER|DROP|CREATE|TRUNCATE|INTO|EXEC|EXECUTE|GRANT|DENY|REVOKE|USE|DBCC|BACKUP|RESTORE|OPENROWSET|OPENQUERY|OPENDATASOURCE|NEXT\s+VALUE)\b", RegexOptions.IgnoreCase))
            throw new InvalidOperationException("SELECT_ONLY_GUARD: command rejected before SQL execution.");
    }

    private sealed class ReadOnlyContext(DbContextOptions<HotelOpsDbContext> options) : HotelOpsDbContext(options)
    {
        public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw new InvalidOperationException("Read-only verification cannot SaveChanges.");
        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Read-only verification cannot SaveChangesAsync.");
    }

    internal sealed class SelectOnlyInterceptor : DbCommandInterceptor
    {
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r) { AssertSelect(c.CommandText); return r; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default) { AssertSelect(c.CommandText); return ValueTask.FromResult(r); }
        public override InterceptionResult<object> ScalarExecuting(DbCommand c, CommandEventData e, InterceptionResult<object> r) { AssertSelect(c.CommandText); return r; }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<object> r, CancellationToken ct = default) { AssertSelect(c.CommandText); return ValueTask.FromResult(r); }
        public override InterceptionResult<int> NonQueryExecuting(DbCommand c, CommandEventData e, InterceptionResult<int> r) => throw new InvalidOperationException("SELECT_ONLY_GUARD: nonquery rejected.");
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<int> r, CancellationToken ct = default) => throw new InvalidOperationException("SELECT_ONLY_GUARD: nonquery rejected.");
    }
}
