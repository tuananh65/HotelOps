-- Exact recovery deployment of existing Inventory rowversion contract only.
-- Source SHA256 55437d8b08767c9722d323b808231552837c561663e6edc192ce4670979a0d8e
SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
DECLARE @Apply bit=0;
IF @@TRANCOUNT<>0 THROW 51930,'Run as standalone deployment.',1;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita\MSSQLSERVER01'
 OR DB_NAME()<>'HotelOps_DEV3_142A_Recovery_2b221f40'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='5270D07C-7C27-477B-A60E-86485ADD7FAD')
 THROW 51930,'Exact recovery disposable required.',1;
IF NOT EXISTS(SELECT 1 FROM msdb.dbo.backupset b WHERE b.backup_set_id=4 AND b.database_name=DB_NAME()
 AND b.database_guid=(SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID())
 AND b.type='D' AND b.is_damaged=0 AND b.has_backup_checksums=1 AND b.backup_finish_date>=DATEADD(day,-1,GETDATE())
 AND EXISTS(SELECT 1 FROM msdb.dbo.restorehistory r WHERE r.backup_set_id=b.backup_set_id AND r.restore_type='D'
 AND r.destination_database_name='HotelOps_DEV3_142A_FE10Restore_77c41aba' AND r.restore_date>=b.backup_finish_date)
 AND EXISTS(SELECT 1 FROM sys.databases WHERE name='HotelOps_DEV3_142A_FE10Restore_77c41aba' AND state=0))
 THROW 51930,'Recent checksum backup and actual disposable restore required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:Inventory:Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51930,'Schema lock unavailable.',1;
    DECLARE @tableName sysname, @sql nvarchar(max);
    DECLARE inventoryRoots CURSOR LOCAL FAST_FORWARD FOR
        SELECT name FROM (VALUES
            (N'inventory_items'), (N'units_of_measure'), (N'uom_conversions'),
            (N'warehouses'), (N'stock_thresholds'), (N'stock_documents'), (N'stocktakes')
        ) AS roots(name);
    OPEN inventoryRoots;
    FETCH NEXT FROM inventoryRoots INTO @tableName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF OBJECT_ID(N'dbo.' + @tableName, N'U') IS NULL
            THROW 51002, 'Required Inventory aggregate table is missing.', 1;

        IF COL_LENGTH(N'dbo.' + @tableName, N'row_version') IS NULL
        BEGIN
            SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@tableName)
                + N' ADD row_version rowversion NOT NULL;';
            EXEC sys.sp_executesql @sql;
        END
        ELSE IF NOT EXISTS
        (
            SELECT 1 FROM sys.columns
            WHERE object_id = OBJECT_ID(N'dbo.' + @tableName)
                AND name = N'row_version' AND system_type_id = 189 AND is_nullable = 0
        )
            THROW 51003, 'Existing row_version does not match the approved rowversion contract.', 1;

        FETCH NEXT FROM inventoryRoots INTO @tableName;
    END;
    CLOSE inventoryRoots;
    DEALLOCATE inventoryRoots;


 IF @@TRANCOUNT<>1 THROW 51930,'Unexpected transaction nesting.',1;
 IF @Apply=1 COMMIT; ELSE ROLLBACK;
 SELECT @Apply AS Applied;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
