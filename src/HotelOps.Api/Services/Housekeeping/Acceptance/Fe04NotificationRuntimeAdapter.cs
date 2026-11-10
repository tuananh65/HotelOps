using System.Data;
using System.Security;
using HotelOps.Api.Services.Notifications;
using Microsoft.Data.SqlClient;

namespace HotelOps.Api.Services.Housekeeping.Acceptance;

// A domain-owned implementation must call the real session/branch/source authorization flow.
// No implementation is registered by this candidate. Fixture JSON/boolean assertions cannot supply it.
public interface IFe04AcceptanceSourceAuthority
{
    string ApprovalPayloadSha256 { get; }
    Task RequireCurrentSessionSourceAndFixtureOwnershipAsync(SqlConnection connection, SqlTransaction transaction,
        CleaningAssignmentCommand command, CancellationToken ct);
}

// Isolated acceptance preflight; deliberately does NOT replace any DEV2 store/activation guard.
public sealed class Fe04NotificationRuntimeAdapter : IAsyncDisposable
{
    public const string RunnerLock = "HotelOps:NotificationSchema002:Runner";
    private readonly Fe04NotificationTrialContract contract;
    private readonly SqlConnection connection;
    private readonly TimeProvider clock;
    private readonly string sourceRoot;
    private readonly string fixtureManifest;
    private readonly string approvalPayloadSha256;
    private bool lockHeld;
    private bool disposed;

    private Fe04NotificationRuntimeAdapter(Fe04NotificationTrialContract c, SqlConnection sql, TimeProvider time, string root, string manifest, string digest)
        => (contract, connection, clock, sourceRoot, fixtureManifest, approvalPayloadSha256) = (c, sql, time, root, manifest, digest);

    public static async Task<Fe04NotificationRuntimeAdapter> OpenAsync(Fe04SignedApproval approval,
        IReadOnlyDictionary<string, string> ownerPins, SecureString password, string sourceRoot,
        string fixtureManifest, TimeProvider clock, CancellationToken ct)
    {
        // Authenticate all owner decisions and source bytes BEFORE opening a connection.
        var c = Fe04NotificationApproval.Verify(approval, ownerPins, clock.GetUtcNow());
        await Fe04NotificationApproval.VerifyFilesAsync(c, sourceRoot, fixtureManifest, ct);
        var sql = new SqlConnection(ConnectionString(), new SqlCredential(c.Login, password));
        var adapter = new Fe04NotificationRuntimeAdapter(c, sql, clock, sourceRoot, fixtureManifest, Fe04NotificationApproval.Hash(approval.Payload));
        try
        {
            await sql.OpenAsync(ct); // SqlClient validates TLS; no callback/trust bypass.
            await adapter.VerifyRuntimeAsync(null, ct);
            await using var command = sql.CreateCommand();
            command.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; SELECT @r;";
            command.CommandTimeout = 10;
            command.Parameters.AddWithValue("@resource", RunnerLock);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) < 0)
                throw new InvalidOperationException("EXCLUSIVE_CENTRAL_RUNNER_LEASE_REQUIRED");
            adapter.lockHeld = true;
            await adapter.VerifyRuntimeAsync(null, ct);
            return adapter;
        }
        catch { await adapter.DisposeAsync(); throw; }
    }

    public static string ConnectionString() => new SqlConnectionStringBuilder
    {
        // Fixed owner-reported TLS wire endpoint; actual ServerName is still verified independently.
        DataSource = "tcp:DESKTOP-JI8U50T,1433",
        InitialCatalog = NotifWriter004AContract.Database,
        IntegratedSecurity = false, Encrypt = SqlConnectionEncryptOption.Mandatory,
        TrustServerCertificate = false, PersistSecurityInfo = false, Pooling = false,
        ConnectTimeout = 10, ApplicationName = "FE04-NOTIF-UNBLOCK-FINAL-002-preflight"
    }.ConnectionString;

    public async Task VerifyRuntimeAsync(SqlTransaction? transaction, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Fe04NotificationApproval.Require(contract, clock.GetUtcNow());
        await Fe04NotificationApproval.VerifyFilesAsync(contract, sourceRoot, fixtureManifest, ct);
        if (transaction is not null) Fe04AcceptanceTransaction.Require(connection, transaction);
        await using (var command = Command(IdentitySql, transaction))
        {
            command.Parameters.AddWithValue("@lease", contract.LeaseId.ToString());
            command.Parameters.AddWithValue("@start", contract.LeaseStart.UtcDateTime);
            command.Parameters.AddWithValue("@end", contract.LeaseEnd.UtcDateTime);
            command.Parameters.AddWithValue("@login", contract.Login);
            command.Parameters.AddWithValue("@user", contract.DatabaseUser);
            command.Parameters.AddWithValue("@guid", contract.DatabaseGuid);
            command.Parameters.AddWithValue("@transactions", transaction is null ? 0 : 1);
            command.Parameters.AddWithValue("@resource", RunnerLock);
            command.Parameters.AddWithValue("@lockRequired", lockHeld);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 1)
                throw new InvalidOperationException("ACTUAL_TARGET_IDENTITY_TLS_LEASE_OR_PRIVILEGE_REJECTED");
        }
        foreach (var permission in contract.Permissions)
        {
            await using var command = Command("""
                SELECT CASE WHEN @column IS NULL THEN HAS_PERMS_BY_NAME(@object,'OBJECT',@permission)
                 ELSE HAS_PERMS_BY_NAME(@object,'OBJECT',@permission,@column,'COLUMN') END,
                 CASE WHEN EXISTS(SELECT 1 FROM sys.database_permissions
                   WHERE grantee_principal_id=USER_ID() AND class=1 AND major_id=OBJECT_ID(@object)
                   AND minor_id=0 AND state='D' AND permission_name=@permission) THEN 1 ELSE 0 END;
                """, transaction);
            command.Parameters.AddWithValue("@object", permission.Object);
            command.Parameters.AddWithValue("@permission", permission.Permission);
            command.Parameters.AddWithValue("@column", (object?)permission.Column ?? DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct) || reader.IsDBNull(0) || reader.GetInt32(0) != (permission.Allowed ? 1 : 0) ||
                (!permission.Allowed && permission.Column is null && reader.GetInt32(1) != 1))
                throw new InvalidOperationException("EFFECTIVE_PERMISSION_OR_EXPLICIT_DENY_MISMATCH");
        }
        foreach (var module in contract.SqlModules)
        {
            await using var command = Command("""
                SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),definition)),2)
                FROM sys.sql_modules WHERE object_id=OBJECT_ID(@module)
                 AND execute_as_principal_id IS NULL AND definition IS NOT NULL;
                """, transaction);
            command.Parameters.AddWithValue("@module", module.Key);
            if (await command.ExecuteScalarAsync(ct) is not string hash || hash != module.Value)
                throw new InvalidOperationException("MODULE_BYTES_OR_CALLER_EXECUTION_CONTEXT_MISMATCH");
        }
        await using (var command = Command(CatalogSql, transaction))
        {
            // FOR JSON may return multiple chunks: concatenate all rows without truncation.
            await using var reader = await command.ExecuteReaderAsync(ct);
            var text = new System.Text.StringBuilder();
            while (await reader.ReadAsync(ct)) text.Append(reader.GetString(0));
            if (Fe04NotificationApproval.Hash(text.ToString()) != contract.CatalogSha256)
                throw new InvalidOperationException("EXACT_APPROVED_CATALOG_REQUIRED");
        }
    }

    // Callback is a transaction-local probe, not a second business writer or a commit owner.
    // Public API acceptance must still execute the unchanged real FE04 caller after owner wiring.
    public async Task ProbeRollbackAsync(CleaningAssignmentCommand command,
        Func<SqlConnection, SqlTransaction, CancellationToken, Task<IFe04AcceptanceSourceAuthority>> createSourceAuthority,
        Func<SqlConnection, SqlTransaction, CancellationToken, Task> probe, CancellationToken ct)
    {
        if (!lockHeld) throw new InvalidOperationException("EXCLUSIVE_CENTRAL_RUNNER_LEASE_REQUIRED");
        await using var physical = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await Fe04AcceptanceTransaction.RollbackOnExitAsync(connection, physical, async token =>
        {
            await VerifyRuntimeAsync(physical, token);
            CleaningAssignmentProfile.ValidateCommand(command);
            // Accepted harness attaches its exact DbContext using UseTransactionAsync; no nested transaction.
            var sourceAuthority = await createSourceAuthority(connection, physical, token);
            if (sourceAuthority.ApprovalPayloadSha256 != approvalPayloadSha256)
                throw new InvalidOperationException("SAME_OWNER_APPROVAL_SOURCE_AUTHORITY_REQUIRED");
            await sourceAuthority.RequireCurrentSessionSourceAndFixtureOwnershipAsync(connection, physical, command, token);
            Fe04NotificationApproval.Require(contract, clock.GetUtcNow());
            NotifWriter004AContract.Require(new(contract.Server, contract.Database, contract.DatabaseGuid, contract.Owner,
                contract.LeaseId.ToString(), contract.LeaseEnd, lockHeld, contract.Login, contract.DatabaseUser,
                contract.Authentication, contract.Module, contract.ExecutionContext, 1, true, true, true, false, true),
                new(contract.Version, contract.Login, contract.DatabaseUser, contract.Authentication, contract.Module,
                    contract.ExecutionContext, contract.LeaseId.ToString()), clock.GetUtcNow());
            await probe(connection, physical, token);
            await VerifyRuntimeAsync(physical, token);
        }, ct);
    }

    private SqlCommand Command(string sql, SqlTransaction? transaction)
        => new(sql, connection, transaction) { CommandTimeout = 15 };

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        // Pooling=False. Closing releases this session's runner lock; no KILL/cleanup/DDL.
        await connection.DisposeAsync();
        lockHeld = false;
    }

    public const string IdentitySql = """
        SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))=N'DESKTOP-JI8U50T\SQLEXPRESS'
         AND DB_NAME()=N'HotelOps_ENVNOTIF001_Trial_20261109' AND ORIGINAL_LOGIN()=@login AND SUSER_SNAME()=@login AND USER_NAME()=@user
         AND EXISTS(SELECT 1 FROM sys.database_principals dp JOIN sys.server_principals sp ON sp.sid=dp.sid
           WHERE dp.name=@user AND sp.name=@login AND dp.authentication_type_desc='INSTANCE' AND sp.type='S')
         AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid=@guid)
         AND IS_SRVROLEMEMBER('sysadmin')=0 AND HAS_PERMS_BY_NAME(NULL,'SERVER','CONTROL SERVER')=0
         AND IS_SRVROLEMEMBER('securityadmin')=0 AND IS_SRVROLEMEMBER('serveradmin')=0
         AND IS_SRVROLEMEMBER('setupadmin')=0 AND IS_SRVROLEMEMBER('processadmin')=0
         AND IS_SRVROLEMEMBER('diskadmin')=0 AND IS_SRVROLEMEMBER('dbcreator')=0 AND IS_SRVROLEMEMBER('bulkadmin')=0
         AND HAS_PERMS_BY_NAME(NULL,'SERVER','ALTER ANY LOGIN')=0
         AND HAS_PERMS_BY_NAME(NULL,'SERVER','IMPERSONATE ANY LOGIN')=0
         AND HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL')=0 AND HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER')=0
         AND HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','IMPERSONATE ANY USER')=0
         AND NOT EXISTS(SELECT 1 FROM sys.database_role_members WHERE member_principal_id=USER_ID())
         AND EXISTS(SELECT 1 FROM sys.dm_exec_connections WHERE session_id=@@SPID AND encrypt_option='TRUE' AND auth_scheme='SQL')
         AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.NotificationSchemaOwner'
           AND CONVERT(nvarchar(128),value)=N'DEV1 ENV-001 / DEV2')
         AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.NotificationSchemaTrial'
           AND CONVERT(nvarchar(128),value)=@lease)
         AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.NotificationSchemaLeaseEndUtc'
           AND TRY_CONVERT(datetimeoffset,value)=TODATETIMEOFFSET(@end,'+00:00'))
         AND @start<=SYSUTCDATETIME() AND @end>SYSUTCDATETIME() AND @@TRANCOUNT=@transactions
         AND XACT_STATE()=@transactions
         AND (@lockRequired=0 OR APPLOCK_MODE('public',@resource,'Session')='Exclusive')
         THEN 1 ELSE 0 END;
        """;

    public const string CatalogSql = """
        SELECT o.name AS [object],o.type_desc,
         JSON_QUERY((SELECT i.name,i.is_unique,i.is_disabled,i.has_filter,i.filter_definition,
           JSON_QUERY((SELECT c.name,ic.key_ordinal,ic.is_descending_key,ic.is_included_column
             FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
             WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id ORDER BY ic.index_column_id FOR JSON PATH)) AS columns
           FROM sys.indexes i WHERE i.object_id=o.object_id AND i.name IS NOT NULL ORDER BY i.name FOR JSON PATH)) AS indexes,
         JSON_QUERY((SELECT cc.name,cc.definition,cc.is_disabled,cc.is_not_trusted FROM sys.check_constraints cc
           WHERE cc.parent_object_id=o.object_id ORDER BY cc.name FOR JSON PATH)) AS checks,
         JSON_QUERY((SELECT fk.name,fk.is_disabled,fk.is_not_trusted,OBJECT_NAME(fk.referenced_object_id) AS referenced_object
           FROM sys.foreign_keys fk WHERE fk.parent_object_id=o.object_id ORDER BY fk.name FOR JSON PATH)) AS foreign_keys,
         JSON_QUERY((SELECT tr.name,tr.is_disabled,tr.is_instead_of_trigger,
           CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition)),2) AS definition_sha256
           FROM sys.triggers tr LEFT JOIN sys.sql_modules m ON m.object_id=tr.object_id
           WHERE tr.parent_id=o.object_id ORDER BY tr.name FOR JSON PATH)) AS triggers
        FROM sys.objects o WHERE o.schema_id=SCHEMA_ID('dbo') AND o.name IN
         ('notification_operation_reservations','notification_publications','notification_delivery_intents','cleaning_assignments')
        ORDER BY o.name FOR JSON PATH;
        """;
}
