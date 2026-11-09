using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HotelOps.Api.Data;

/// <summary>Integration checkpoint is closed until an explicit disposable target and catalog are verified.</summary>
public static class IntegrationSqlGuard
{
    public const string Server = "KIENPC";
    public const string Database = "HotelOps_DEV2_FullStack_Trial";
    public const string Login = "HotelOps_DEV2_FullStack_Runtime";
    public const string Marker = "DEV2-INTEGRATION-02-SQL-PROVISIONAL";

    // Historical SQL suites remain closed until their fixtures/contracts are reconciled.
    public static void RequireRuntimeAuthorization() => throw new InvalidOperationException(
        "SQL_TARGET=BLOCKED_EXTERNAL: historical SQL runner is not authorized; use the Integration02 trial runner.");

    public static string ValidateConnectionString(string? connectionString, bool requirePassword = true)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Integration02 requires an explicit trial connection; fallback is forbidden.");
        SqlConnectionStringBuilder target;
        try { target = new(connectionString); }
        catch (ArgumentException) { throw new InvalidOperationException("Invalid Integration02 connection configuration."); }
        if (!string.Equals(target.DataSource, Server, StringComparison.Ordinal)
            || !string.Equals(target.InitialCatalog, Database, StringComparison.Ordinal)
            || !string.Equals(target.UserID, Login, StringComparison.Ordinal)
            || target.IntegratedSecurity || (requirePassword && string.IsNullOrEmpty(target.Password))
            || target.PersistSecurityInfo || !string.IsNullOrEmpty(target.AttachDBFilename)
            || !string.IsNullOrEmpty(target.FailoverPartner) || target.UserInstance
            || target.MultiSubnetFailover || target.Encrypt == SqlConnectionEncryptOption.Optional)
            throw new InvalidOperationException("Integration02 target/credential is outside the exact trial allowlist.");
        return connectionString;
    }

    public static async Task VerifyOpenConnectionAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (!string.Equals(connection.DataSource, Server, StringComparison.Ordinal)
            || !string.Equals(connection.Database, Database, StringComparison.Ordinal))
            throw new InvalidOperationException("Integration02 actual connection target mismatch.");
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 10;
        command.CommandText = """
            SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),DB_NAME(),ORIGINAL_LOGIN(),
                   IS_SRVROLEMEMBER('sysadmin'),HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL'),
                   HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER'),
                   (SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties
                    WHERE class=0 AND name=N'HotelOps.IntegrationTrial')
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)
            || reader.GetString(0) != Server || reader.GetString(1) != Database
            || reader.GetString(2) != Login || reader.GetInt32(3) != 0
            || reader.GetInt32(4) != 0 || reader.GetInt32(5) != 0
            || reader.IsDBNull(6) || reader.GetString(6) != Marker)
            throw new InvalidOperationException("Integration02 server, catalog marker or least-privilege identity verification failed.");
    }
}

/// <summary>Checks configuration before connecting and actual identity on every EF connection lease.</summary>
public sealed class IntegrationTrialConnectionInterceptor : DbConnectionInterceptor
{
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
        ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        IntegrationSqlGuard.ValidateConnectionString(connection.ConnectionString, requirePassword: false);
        return ValueTask.FromResult(result);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default) =>
        await IntegrationSqlGuard.VerifyOpenConnectionAsync(connection, cancellationToken);

    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result) => throw new InvalidOperationException("Trial database operations must be asynchronous.");
}
