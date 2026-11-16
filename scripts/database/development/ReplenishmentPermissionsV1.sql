-- Approved UC100/101 independent permission catalog. No role/user grants.
-- Restricted to the previously identified disposable recovery database.
-- Re-run preflight and verify the backup/restore before opting into Apply.
SET XACT_ABORT ON;
SET NOCOUNT ON;
DECLARE @Apply bit=0, @BackupRestoreVerified bit=0, @VerifiedBackupSetId int=NULL;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita\MSSQLSERVER01'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_Recovery_2b221f40'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid='5270D07C-7C27-477B-A60E-86485ADD7FAD')
 THROW 51901,'Exact disposable database identity required; never shared/production.',1;
IF @Apply=1 AND (@BackupRestoreVerified<>1 OR NOT EXISTS(SELECT 1 FROM msdb.dbo.backupset
 WHERE backup_set_id=@VerifiedBackupSetId AND database_name=DB_NAME() AND type='D'
 AND backup_finish_date>=DATEADD(day,-1,GETDATE())))
 THROW 51901,'Verified recent full backup and restore check required before Apply.',1;
IF ISNULL(COL_LENGTH('dbo.permissions','permission_code'),0)<>100
 OR ISNULL(COL_LENGTH('dbo.permissions','action_code'),0)<>40
 OR ISNULL(COLUMNPROPERTY(OBJECT_ID('dbo.permissions'),'permission_id','IsIdentity'),0)<>1
 THROW 51901,'Permission schema incompatible.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:ReplenishmentPermissions:V1',
   @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51901,'Catalog installation lock unavailable.',1;
 DECLARE @catalog table(code varchar(50),name nvarchar(150),action varchar(20));
 INSERT @catalog VALUES
 ('REPLENISHMENT_VIEW',N'View replenishments','view'),
 ('REPLENISHMENT_CREATE',N'Create replenishment','create'),
 ('REPLENISHMENT_CLOSE',N'Close replenishment','close'),
 ('REPLENISHMENT_RECEIVE',N'Receive replenishment','receive');
 IF EXISTS(SELECT 1 FROM dbo.permissions p WITH(UPDLOCK,HOLDLOCK) JOIN @catalog c ON p.permission_code=c.code
   WHERE p.module_code<>'REPLENISHMENT' OR p.resource_code<>'material_replenishment' OR p.action_code<>c.action)
  THROW 51901,'Conflicting permission semantics; preserve existing catalog and review.',1;
 INSERT dbo.permissions(permission_code,permission_name,module_code,resource_code,action_code,description)
 SELECT c.code,c.name,'REPLENISHMENT','material_replenishment',c.action,
   N'UC100/101 independent permission. Current allowed actor, branch/warehouse scope required. Does not grant MR_VIEW or STOCK_RECEIPT_POST.'
 FROM @catalog c WHERE NOT EXISTS(SELECT 1 FROM dbo.permissions p WITH(UPDLOCK,HOLDLOCK) WHERE p.permission_code=c.code);
 IF EXISTS(SELECT c.code FROM @catalog c LEFT JOIN dbo.permissions p ON p.permission_code=c.code GROUP BY c.code HAVING COUNT(p.permission_id)<>1)
  THROW 51901,'Permission uniqueness required.',1;
 IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.ReplenishmentPermissions.V1'
   AND CONVERT(nvarchar(128),value)<>N'1') THROW 51901,'Conflicting catalog version.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.ReplenishmentPermissions.V1')
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.ReplenishmentPermissions.V1',@value=1;
 IF @Apply=1 COMMIT TRANSACTION; ELSE ROLLBACK TRANSACTION;
 SELECT @Apply AS Applied,N'HotelOps.ReplenishmentPermissions.V1' AS CatalogVersion;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
