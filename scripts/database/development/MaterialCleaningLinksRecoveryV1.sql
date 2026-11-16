-- Additive UC092 cleaning-task links. Prepared only; default rollback.
SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
DECLARE @Apply bit=0;
IF @@TRANCOUNT<>0 THROW 51931,'Run as standalone deployment.',1;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita\MSSQLSERVER01'
 OR DB_NAME()<>'HotelOps_DEV3_142A_Recovery_2b221f40'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='5270D07C-7C27-477B-A60E-86485ADD7FAD')
 THROW 51931,'Exact recovery disposable required.',1;
IF NOT EXISTS(SELECT 1 FROM msdb.dbo.backupset b WHERE b.backup_set_id=4 AND b.database_name=DB_NAME()
 AND b.database_guid=(SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID())
 AND b.type='D' AND b.is_damaged=0 AND b.has_backup_checksums=1 AND b.backup_finish_date>=DATEADD(day,-1,GETDATE())
 AND EXISTS(SELECT 1 FROM msdb.dbo.restorehistory r WHERE r.backup_set_id=b.backup_set_id AND r.restore_type='D'
 AND r.destination_database_name='HotelOps_DEV3_142A_FE10Restore_77c41aba' AND r.restore_date>=b.backup_finish_date)
 AND EXISTS(SELECT 1 FROM sys.databases WHERE name='HotelOps_DEV3_142A_FE10Restore_77c41aba' AND state=0))
 THROW 51931,'Recent checksum backup and actual disposable restore required.',1;

IF ISNULL(COL_LENGTH('dbo.material_requests','row_version'),0)<>8 OR COL_LENGTH('dbo.cleaning_tasks','workflow_status') IS NULL
 THROW 51931,'Material concurrency and task workflow schema required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:MaterialCleaningLinks:Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51931,'Schema lock unavailable.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.MaterialCleaningLinks.V1')
 BEGIN
  IF OBJECT_ID('dbo.material_request_cleaning_links') IS NOT NULL THROW 51931,'Unexpected existing link schema; preserve and review.',1;
  CREATE TABLE dbo.material_request_cleaning_links(
   request_id bigint NOT NULL,
   task_id bigint NOT NULL,
   audit_id bigint NOT NULL,
   CONSTRAINT PK_material_request_cleaning_links PRIMARY KEY(request_id,task_id),
   CONSTRAINT FK_mr_cleaning_request FOREIGN KEY(request_id) REFERENCES dbo.material_requests(request_id),
   CONSTRAINT FK_mr_cleaning_task FOREIGN KEY(task_id) REFERENCES dbo.cleaning_tasks(cleaning_task_id),
   CONSTRAINT FK_mr_cleaning_audit FOREIGN KEY(audit_id) REFERENCES dbo.audit_logs(audit_id)
  );
  CREATE UNIQUE INDEX UX_mr_cleaning_audit ON dbo.material_request_cleaning_links(audit_id);
  CREATE INDEX IX_mr_cleaning_reverse ON dbo.material_request_cleaning_links(task_id,request_id);
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.MaterialCleaningLinks.V1',@value=N'1';
 END;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.MaterialCleaningLinks.V1' AND CONVERT(nvarchar(20),value)='1')
  OR (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.material_request_cleaning_links'))<>3
  OR (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.material_request_cleaning_links') AND name IN('request_id','task_id','audit_id') AND system_type_id=127 AND is_nullable=0)<>3
  OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.material_request_cleaning_links') AND name IN('FK_mr_cleaning_request','FK_mr_cleaning_task','FK_mr_cleaning_audit') AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0 AND update_referential_action=0)<>3
  OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.material_request_cleaning_links') AND name='PK_material_request_cleaning_links' AND is_primary_key=1 AND is_disabled=0)
  THROW 51931,'Material task-link schema mismatch.',1;
 IF @Apply=1 COMMIT; ELSE ROLLBACK;
 SELECT @Apply AS Applied;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
