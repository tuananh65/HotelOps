/*
HotelOps FE-01 / Task 1 / Minimal Database Changes B
PREPARED ONLY. This file has NOT been executed by Codex.
Run as one batch on the explicitly selected HotelOpsDB; no EF migration.

Observed read-only metadata (2026-10-18): dbo.user_sessions.refresh_token_hash
varchar(255) NOT NULL, SQL_Latin1_General_CP1_CI_AS; enabled, unfiltered
single-key UNIQUE constraint. Users has none of the three new columns.
Generated index/constraint names differ from checked-in EF names.

Operator prerequisites:
  - Backup/recovery plan and tested rollback; inspect dependencies and all
    external consumers, dynamic SQL, jobs and encrypted modules separately.
  - Maintenance window: stop/drain ALL backend instances and relevant writers.
  - Prepare matching Database First backend artifact BEFORE production cutover.
  - Review script; explicitly set @MaintenanceConfirmed = 1 to run.
  - Set @ExpectedServer explicitly to the reviewed SERVERPROPERTY('ServerName').
    It defaults to NULL in Git, so no instance is implicitly authorized.
    Use the confirmed development instance only in your execution copy.
  - Requires VIEW DEFINITION, SELECT and appropriate ALTER permissions.

Strict baseline script: rejects reruns/partial deployments instead of guessing.
No existing hash/password/session values are updated, deleted or invalidated.
Existing users receive count=0 and NULL deadlines; this does NOT classify
legacy passwords as valid regular credentials. Provisioning/reset is separate.
Rename preserves hash semantics/data; it does NOT convert refresh-token hashes
to cookie session keys. Do not reuse old/sample sessions for authentication.
No index/constraint renaming or recreation is needed for the hash column.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;

DECLARE @ExpectedServer sysname = NULL; -- REQUIRED operator configuration.
DECLARE @ExpectedDatabase sysname = N'HotelOpsDB';
DECLARE @MaintenanceConfirmed bit = 0;
DECLARE @ActualServer sysname = CONVERT(sysname, SERVERPROPERTY(N'ServerName'));
DECLARE @ActualDatabase sysname = DB_NAME();

IF NULLIF(LTRIM(RTRIM(@ExpectedServer)), N'') IS NULL
    THROW 51016, 'Configure ExpectedServer explicitly before execution.', 1;
IF @ActualServer IS NULL OR @ActualServer <> @ExpectedServer
    THROW 51017, 'SQL instance mismatch or unavailable server identity. Execution refused.', 1;
IF NULLIF(LTRIM(RTRIM(@ExpectedDatabase)), N'') IS NULL
   OR @ActualDatabase IS NULL OR @ActualDatabase <> @ExpectedDatabase
    THROW 51000, 'Wrong database. Select the reviewed HotelOpsDB explicitly.', 1;
IF @MaintenanceConfirmed <> 1
    THROW 51001, 'Stop/drain consumers and confirm maintenance before execution.', 1;
IF @@TRANCOUNT <> 0
    THROW 51002, 'Run standalone, outside an existing transaction.', 1;
IF ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'VIEW DEFINITION'), 0) <> 1
    THROW 51003, 'Database VIEW DEFINITION is required for dependency checks.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Users int = OBJECT_ID(N'dbo.users', N'U');
    DECLARE @Sessions int = OBJECT_ID(N'dbo.user_sessions', N'U');
    IF @Users IS NULL OR @Sessions IS NULL
        THROW 51004, 'Required dbo tables are missing.', 1;

    -- Serialize this cutover with writers; not a substitute for stopping them.
    DECLARE @Rows bigint;
    SELECT @Rows = COUNT_BIG(*) FROM dbo.users WITH (TABLOCKX, HOLDLOCK);
    SELECT @Rows = COUNT_BIG(*) FROM dbo.user_sessions WITH (TABLOCKX, HOLDLOCK);

    DECLARE @HashColumn int;
    DECLARE @HashCollation sysname;
    SELECT @HashColumn = column_id, @HashCollation = collation_name
    FROM sys.columns
    WHERE object_id = @Sessions AND name = N'refresh_token_hash'
      AND system_type_id = TYPE_ID(N'varchar')
      AND user_type_id = TYPE_ID(N'varchar')
      AND max_length = 255 AND is_nullable = 0 AND is_computed = 0;

    IF @HashColumn IS NULL
        THROW 51005, 'Expected varchar(255) NOT NULL refresh_token_hash is missing or incompatible.', 1;
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @Sessions AND name = N'session_key_hash')
        THROW 51006, 'Target hash column already exists; inspect deployment state manually.', 1;
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @Users
               AND name IN (N'failed_login_count', N'locked_until', N'temporary_password_expires_at'))
        THROW 51007, 'A target users column already exists; strict baseline required.', 1;
    IF OBJECT_ID(N'dbo.DF_users_failed_login_count') IS NOT NULL
       OR OBJECT_ID(N'dbo.CK_users_failed_login_count') IS NOT NULL
        THROW 51008, 'Target constraint name is occupied.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes i
        WHERE i.object_id = @Sessions AND i.is_unique = 1
          AND i.is_unique_constraint = 1 AND i.is_disabled = 0
          AND i.is_hypothetical = 0 AND i.has_filter = 0
          AND EXISTS (SELECT 1 FROM sys.index_columns ic
                      WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                        AND ic.column_id = @HashColumn AND ic.key_ordinal = 1)
          AND (SELECT COUNT(*) FROM sys.index_columns ic
               WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                 AND ic.key_ordinal > 0) = 1
    )
        THROW 51009, 'Expected active single-key UNIQUE hash constraint was not found.', 1;

    -- Fail closed for dependencies; do not alter unrelated modules automatically.
    IF EXISTS (SELECT 1 FROM sys.sql_expression_dependencies
               WHERE referenced_id = @Sessions)
       OR EXISTS (SELECT 1 FROM sys.sql_modules
                  WHERE definition LIKE N'%refresh_token_hash%'
                     OR definition LIKE N'%session_key_hash%')
       OR EXISTS (SELECT 1 FROM sys.triggers
                  WHERE parent_id IN (@Users, @Sessions))
        THROW 51010, 'SQL dependency/trigger found; inspect and prepare coordinated changes first.', 1;

    -- Snapshot index identities/definitions by column_id, unaffected by rename.
    SELECT i.index_id, i.name, i.type, i.is_unique, i.is_unique_constraint,
           i.is_disabled, i.has_filter, i.filter_definition,
           ic.index_column_id, ic.column_id, ic.key_ordinal,
           ic.is_descending_key, ic.is_included_column
    INTO #AuthIndexesBefore
    FROM sys.indexes i JOIN sys.index_columns ic
      ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    WHERE i.object_id = @Sessions;

    EXEC sys.sp_rename N'dbo.user_sessions.refresh_token_hash', N'session_key_hash', N'COLUMN';

    ALTER TABLE dbo.users ADD
        failed_login_count int NOT NULL
            CONSTRAINT DF_users_failed_login_count DEFAULT (0) WITH VALUES,
        locked_until datetime2(7) NULL,
        temporary_password_expires_at datetime2(7) NULL;

    -- Dynamic batch avoids compile-time binding to a column added above.
    EXEC sys.sp_executesql N'ALTER TABLE dbo.users WITH CHECK ADD
        CONSTRAINT CK_users_failed_login_count CHECK (failed_login_count >= 0);';

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @Sessions
                   AND column_id = @HashColumn AND name = N'session_key_hash'
                   AND user_type_id = TYPE_ID(N'varchar') AND max_length = 255
                   AND is_nullable = 0 AND collation_name = @HashCollation)
       OR EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @Sessions AND name = N'refresh_token_hash')
        THROW 51011, 'Hash rename/type/nullability/collation postcondition failed.', 1;

    SELECT i.index_id, i.name, i.type, i.is_unique, i.is_unique_constraint,
           i.is_disabled, i.has_filter, i.filter_definition,
           ic.index_column_id, ic.column_id, ic.key_ordinal,
           ic.is_descending_key, ic.is_included_column
    INTO #AuthIndexesAfter
    FROM sys.indexes i JOIN sys.index_columns ic
      ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    WHERE i.object_id = @Sessions;
    IF EXISTS (SELECT * FROM #AuthIndexesBefore EXCEPT SELECT * FROM #AuthIndexesAfter)
       OR EXISTS (SELECT * FROM #AuthIndexesAfter EXCEPT SELECT * FROM #AuthIndexesBefore)
        THROW 51012, 'Session indexes changed unexpectedly.', 1;

    IF (SELECT COUNT(*) FROM sys.columns WHERE object_id = @Users AND
        ((name = N'failed_login_count' AND user_type_id = TYPE_ID(N'int') AND is_nullable = 0)
         OR (name IN (N'locked_until', N'temporary_password_expires_at')
             AND user_type_id = TYPE_ID(N'datetime2') AND scale = 7 AND is_nullable = 1))) <> 3
        THROW 51013, 'Users column postconditions failed.', 1;
    IF NOT EXISTS (SELECT 1 FROM sys.default_constraints d JOIN sys.columns c
                    ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
                   WHERE d.parent_object_id = @Users AND c.name = N'failed_login_count'
                     AND d.name = N'DF_users_failed_login_count'
                     AND REPLACE(REPLACE(REPLACE(d.definition, N'(', N''), N')', N''), N' ', N'') = N'0')
        THROW 51014, 'Counter default postcondition failed.', 1;
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
                   WHERE parent_object_id = @Users AND name = N'CK_users_failed_login_count'
                     AND is_disabled = 0 AND is_not_trusted = 0)
        THROW 51015, 'Trusted counter check postcondition failed.', 1;

    DROP TABLE #AuthIndexesBefore;
    DROP TABLE #AuthIndexesAfter;
    COMMIT TRANSACTION;
    PRINT 'Authentication schema update committed; deploy matching backend before reopening traffic.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
