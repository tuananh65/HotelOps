-- UC055/139: concurrency only; no new workflow status, grant or generated EF mapping.
SET XACT_ABORT ON;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%'
 OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51843,'Verified disposable identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51843,'ApplyChanges must be 0 or 1.',1;
IF OBJECT_ID('dbo.maintenance_works','U') IS NULL THROW 51843,'Maintenance schema required.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:MaintenanceExecutionVersion:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51843,'Migration lock unavailable.',1;
IF COL_LENGTH('dbo.maintenance_works','row_version') IS NULL
BEGIN
 IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.maintenance_works') AND system_type_id=189)
  THROW 51843,'Existing concurrency column requires review.',1;
 ALTER TABLE dbo.maintenance_works ADD row_version rowversion NOT NULL;
END;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.maintenance_works')
 AND name='row_version' AND system_type_id=189 AND max_length=8 AND is_nullable=0)
 THROW 51843,'Maintenance concurrency schema mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.MaintenanceExecutionVersion.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.MaintenanceExecutionVersion.V1',@value=N'1';
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
