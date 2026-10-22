/*
HotelOps FE-01 / safety-preserving PARTIAL schema rollback.
PREPARED ONLY. This file has NOT been executed by Codex.

This reverses ONLY session_key_hash -> refresh_token_hash. It intentionally
KEEPS failed_login_count, locked_until, temporary_password_expires_at and their
default/check constraints, even when empty. No DROP COLUMN / state-clearing DML.
Full restoration of the old schema is NOT the purpose of this script.

SAFE ROLLBACK CONDITIONS (operator must establish, not inferred from zero rows):
  - Maintenance/drain ALL instances/writers/jobs; backup and dependencies reviewed.
  - Matching rollback artifact and EF mapping prepared and tested.
  - Before any new credential writes: original artifact may be usable only after
    proving it respects all required security state and hashing behavior.
  - After login/reset/password-change writes: prefer roll-forward or a compatible
    rollback artifact enforcing the SAME lockout/temp/session restrictions.
    Do not reopen with an old artifact that ignores the retained users state.
  - Revoke/invalidate issued sessions under a SEPARATE reviewed operational plan
    before restoring incompatible consumers; this script performs no revocation.
  - Rename does NOT transform session-key hashes into refresh-token hashes.
    Never re-enable a refresh flow just because the old column name is restored.
  - Do not restore an old backup blindly: it can lose password changes, lockout,
    temporary-password state and audit history. Do not reactivate revoked sessions.

Set @RollbackSafetyConfirmed=1 only after establishing all applicable conditions.
Set @ExpectedServer explicitly to the reviewed SERVERPROPERTY('ServerName').
It defaults to NULL in Git, so no instance is implicitly authorized.
Use the confirmed development instance only in your execution copy.
No automated dropping of the three users columns is provided: even expired
locked_until/temp expiry or nonzero counters can still be security-relevant.
Any later removal requires a separate plan proving security-state preservation.
This is strict post-update rollback, not an idempotent deployment-state repair.
Requires VIEW DEFINITION, SELECT and appropriate ALTER permissions.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;

DECLARE @ExpectedServer sysname = NULL; -- REQUIRED operator configuration.
DECLARE @ExpectedDatabase sysname = N'HotelOpsDB';
DECLARE @RollbackSafetyConfirmed bit = 0;
DECLARE @ActualServer sysname = CONVERT(sysname, SERVERPROPERTY(N'ServerName'));
DECLARE @ActualDatabase sysname = DB_NAME();

IF NULLIF(LTRIM(RTRIM(@ExpectedServer)), N'') IS NULL
    THROW 51112, 'Configure ExpectedServer explicitly before execution.', 1;
IF @ActualServer IS NULL OR @ActualServer <> @ExpectedServer
    THROW 51113, 'SQL instance mismatch or unavailable server identity. Execution refused.', 1;
IF NULLIF(LTRIM(RTRIM(@ExpectedDatabase)), N'') IS NULL
   OR @ActualDatabase IS NULL OR @ActualDatabase <> @ExpectedDatabase
    THROW 51100, 'Wrong database. Select the reviewed HotelOpsDB explicitly.', 1;
IF @RollbackSafetyConfirmed <> 1
    THROW 51101, 'Establish maintenance, compatible artifact and security-state preservation first.', 1;
IF @@TRANCOUNT <> 0
    THROW 51102, 'Run standalone, outside an existing transaction.', 1;
IF ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'VIEW DEFINITION'), 0) <> 1
    THROW 51103, 'Database VIEW DEFINITION is required for dependency checks.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @Users int = OBJECT_ID(N'dbo.users', N'U');
    DECLARE @Sessions int = OBJECT_ID(N'dbo.user_sessions', N'U');
    IF @Users IS NULL OR @Sessions IS NULL
        THROW 51104, 'Required dbo tables are missing.', 1;
    DECLARE @Rows bigint;
    SELECT @Rows = COUNT_BIG(*) FROM dbo.users WITH (TABLOCKX, HOLDLOCK);
    SELECT @Rows = COUNT_BIG(*) FROM dbo.user_sessions WITH (TABLOCKX, HOLDLOCK);

    DECLARE @HashColumn int;
    DECLARE @HashCollation sysname;
    SELECT @HashColumn = column_id, @HashCollation = collation_name
    FROM sys.columns WHERE object_id = @Sessions AND name = N'session_key_hash'
      AND user_type_id = TYPE_ID(N'varchar') AND max_length = 255
      AND is_nullable = 0 AND is_computed = 0;
    IF @HashColumn IS NULL
       OR EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @Sessions AND name = N'refresh_token_hash')
        THROW 51105, 'Expected post-update hash column state not found.', 1;
    IF (SELECT COUNT(*) FROM sys.columns WHERE object_id = @Users AND
        ((name = N'failed_login_count' AND user_type_id = TYPE_ID(N'int') AND is_nullable = 0)
         OR (name IN (N'locked_until', N'temporary_password_expires_at')
             AND user_type_id = TYPE_ID(N'datetime2') AND scale = 7 AND is_nullable = 1))) <> 3
        THROW 51106, 'Expected three retained users security-state columns were not found.', 1;
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
                   WHERE parent_object_id = @Users AND name = N'CK_users_failed_login_count'
                     AND is_disabled = 0 AND is_not_trusted = 0)
       OR NOT EXISTS (SELECT 1 FROM sys.default_constraints d JOIN sys.columns c
                       ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
                      WHERE d.parent_object_id = @Users AND c.name = N'failed_login_count'
                        AND d.name = N'DF_users_failed_login_count'
                        AND REPLACE(REPLACE(REPLACE(d.definition, N'(', N''), N')', N''), N' ', N'') = N'0')
        THROW 51111, 'Expected retained counter protections are missing or incompatible.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes i WHERE i.object_id = @Sessions
          AND i.is_unique_constraint = 1 AND i.is_unique = 1
          AND i.is_disabled = 0 AND i.has_filter = 0 AND i.is_hypothetical = 0
          AND EXISTS (SELECT 1 FROM sys.index_columns ic
                      WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                        AND ic.column_id = @HashColumn AND ic.key_ordinal = 1)
          AND (SELECT COUNT(*) FROM sys.index_columns ic
               WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                 AND ic.key_ordinal > 0) = 1
    )
        THROW 51107, 'Active single-key UNIQUE hash constraint missing.', 1;
    IF EXISTS (SELECT 1 FROM sys.sql_expression_dependencies WHERE referenced_id = @Sessions)
       OR EXISTS (SELECT 1 FROM sys.sql_modules WHERE definition LIKE N'%session_key_hash%'
                    OR definition LIKE N'%refresh_token_hash%')
       OR EXISTS (SELECT 1 FROM sys.triggers WHERE parent_id IN (@Users, @Sessions))
        THROW 51108, 'Dependency/trigger found; coordinate rollback separately before proceeding.', 1;

    SELECT i.index_id, i.name, i.type, i.is_unique, i.is_unique_constraint,
           i.is_disabled, i.has_filter, i.filter_definition,
           ic.index_column_id, ic.column_id, ic.key_ordinal,
           ic.is_descending_key, ic.is_included_column
    INTO #AuthRollbackIndexesBefore
    FROM sys.indexes i JOIN sys.index_columns ic
      ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    WHERE i.object_id = @Sessions;

    EXEC sys.sp_rename N'dbo.user_sessions.session_key_hash', N'refresh_token_hash', N'COLUMN';

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @Sessions
                   AND column_id = @HashColumn AND name = N'refresh_token_hash'
                   AND user_type_id = TYPE_ID(N'varchar') AND max_length = 255
                   AND is_nullable = 0 AND collation_name = @HashCollation)
       OR EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @Sessions AND name = N'session_key_hash')
        THROW 51109, 'Hash rollback postconditions failed.', 1;

    SELECT i.index_id, i.name, i.type, i.is_unique, i.is_unique_constraint,
           i.is_disabled, i.has_filter, i.filter_definition,
           ic.index_column_id, ic.column_id, ic.key_ordinal,
           ic.is_descending_key, ic.is_included_column
    INTO #AuthRollbackIndexesAfter
    FROM sys.indexes i JOIN sys.index_columns ic
      ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    WHERE i.object_id = @Sessions;
    IF EXISTS (SELECT * FROM #AuthRollbackIndexesBefore EXCEPT SELECT * FROM #AuthRollbackIndexesAfter)
       OR EXISTS (SELECT * FROM #AuthRollbackIndexesAfter EXCEPT SELECT * FROM #AuthRollbackIndexesBefore)
        THROW 51110, 'Session indexes changed unexpectedly during rollback.', 1;

    DROP TABLE #AuthRollbackIndexesBefore;
    DROP TABLE #AuthRollbackIndexesAfter;
    COMMIT TRANSACTION;
    PRINT 'Hash column name restored; all three users security-state columns intentionally retained.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
