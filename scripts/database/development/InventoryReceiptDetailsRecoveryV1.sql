-- Exact recovery deployment of existing Inventory receipt detail contract only.
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
    IF OBJECT_ID(N'dbo.stock_receipt_line_details', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.stock_receipt_line_details
        (
            stock_document_line_id bigint NOT NULL,
            rejected_qty decimal(18,3) NOT NULL,
            damaged_qty decimal(18,3) NOT NULL,
            discrepancy_reason nvarchar(max) NULL,
            CONSTRAINT PK_stock_receipt_line_details PRIMARY KEY (stock_document_line_id),
            CONSTRAINT FK_stock_receipt_line_details_line FOREIGN KEY (stock_document_line_id)
                REFERENCES dbo.stock_document_lines(line_id)
        );
    END;

 IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.stock_receipt_line_details') AND
     (name='stock_document_line_id' AND system_type_id=127 AND is_nullable=0
      OR name IN ('rejected_qty','damaged_qty') AND system_type_id=106 AND precision=18 AND scale=3 AND is_nullable=0
      OR name='discrepancy_reason' AND system_type_id=231 AND max_length=-1 AND is_nullable=1))<>4
    OR (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.stock_receipt_line_details'))<>4
    THROW 51930,'Receipt detail schema mismatch.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys f JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id
    WHERE f.parent_object_id=OBJECT_ID('dbo.stock_receipt_line_details') AND f.referenced_object_id=OBJECT_ID('dbo.stock_document_lines')
    AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
    AND COL_NAME(c.parent_object_id,c.parent_column_id)='stock_document_line_id'
    AND COL_NAME(c.referenced_object_id,c.referenced_column_id)='line_id')
    THROW 51930,'Receipt detail FK mismatch.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.indexes i JOIN sys.index_columns c ON c.object_id=i.object_id AND c.index_id=i.index_id
    WHERE i.object_id=OBJECT_ID('dbo.stock_receipt_line_details') AND i.is_primary_key=1 AND i.is_disabled=0
    AND c.key_ordinal=1 AND COL_NAME(c.object_id,c.column_id)='stock_document_line_id'
    AND NOT EXISTS(SELECT 1 FROM sys.index_columns other WHERE other.object_id=i.object_id AND other.index_id=i.index_id AND other.key_ordinal>1))
    THROW 51930,'Receipt detail primary key mismatch.',1;
 IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InventoryReceiptDetails.V1' AND CONVERT(nvarchar(20),value)<>'1')
    THROW 51930,'Receipt detail version conflict.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InventoryReceiptDetails.V1')
    EXEC sys.sp_addextendedproperty @name=N'HotelOps.InventoryReceiptDetails.V1',@value=N'1';
 IF @@TRANCOUNT<>1 THROW 51930,'Unexpected transaction nesting.',1;
 IF @Apply=1 COMMIT; ELSE ROLLBACK;
 SELECT @Apply AS Applied;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
