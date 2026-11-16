-- UC055 GBR-050: per-log progress, not a second workflow state. Disposable only.
SET XACT_ABORT ON;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%'
 OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51844,'Verified disposable identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51844,'ApplyChanges must be 0 or 1.',1;
IF OBJECT_ID('dbo.maintenance_work_logs','U') IS NULL THROW 51844,'Maintenance log schema required.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:MaintenanceProgress:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51844,'Migration lock unavailable.',1;
IF OBJECT_ID('dbo.maintenance_log_progress','U') IS NULL
BEGIN
 CREATE TABLE dbo.maintenance_log_progress(
   log_id bigint NOT NULL CONSTRAINT PK_maintenance_log_progress PRIMARY KEY,
   progress_percent int NOT NULL,
   CONSTRAINT FK_maintenance_log_progress_log FOREIGN KEY(log_id) REFERENCES dbo.maintenance_work_logs(log_id),
   CONSTRAINT CK_maintenance_log_progress_percent CHECK(progress_percent BETWEEN 0 AND 100));
 EXEC(N'CREATE TRIGGER dbo.TR_maintenance_log_progress_immutable ON dbo.maintenance_log_progress AFTER UPDATE AS BEGIN SET NOCOUNT ON; THROW 51844,''Record a new progress log instead of rewriting history.'',1; END');
END;
IF ISNULL(COL_LENGTH('dbo.maintenance_log_progress','progress_percent'),-1)<>4
 OR NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.maintenance_log_progress')
   AND name='FK_maintenance_log_progress_log' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.maintenance_log_progress')
   AND name='CK_maintenance_log_progress_percent' AND is_disabled=0 AND is_not_trusted=0)
 OR NOT EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_maintenance_log_progress_immutable') AND is_disabled=0)
 THROW 51844,'Maintenance progress schema mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.MaintenanceProgress.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.MaintenanceProgress.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.MaintenanceProgress.V1' AND CONVERT(nvarchar(20),value)='1')
 THROW 51844,'Conflicting progress capability.',1;
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
