-- UC025: reviewed additive provenance; no role/grant seed or inferred ownership.
-- Default is a transactional trial. Run only after fresh backup/preflight.
SET XACT_ABORT ON;
DECLARE @Apply bit=0;
IF @@TRANCOUNT<>0 THROW 51981,'Standalone installer required.',1;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita\MSSQLSERVER01'
 OR DB_NAME()<>'HotelOps_DEV3_142A_Recovery_2b221f40'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid='5270D07C-7C27-477B-A60E-86485ADD7FAD')
 THROW 51981,'Verified disposable database identity required.',1;
IF NOT EXISTS(SELECT 1 FROM msdb.dbo.backupset WHERE database_name=DB_NAME()
 AND database_guid='5270D07C-7C27-477B-A60E-86485ADD7FAD' AND type='D'
 AND has_backup_checksums=1 AND is_damaged=0 AND backup_finish_date>=DATEADD(day,-1,GETDATE()))
 THROW 51981,'Recent checksum backup required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
 AND name='HotelOps.DepartmentAuthority.V1' AND CONVERT(nvarchar(10),value)='1')
 OR ISNULL(COL_LENGTH('dbo.department_management_access','revision'),0)<>8
 THROW 51981,'Department authority V1 required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:RoomTypes:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51981,'Catalog lock unavailable.',1;
 IF COL_LENGTH('dbo.departments','head_transfer_version') IS NULL
  ALTER TABLE dbo.departments ADD head_transfer_version rowversion NOT NULL;
 IF COL_LENGTH('dbo.department_management_access','authority_origin') IS NULL
 BEGIN
  ALTER TABLE dbo.department_management_access ADD authority_origin varchar(24) NOT NULL
    CONSTRAINT DF_department_authority_origin DEFAULT 'LEGACY_UNKNOWN' WITH VALUES;
  ALTER TABLE dbo.department_management_access ADD CONSTRAINT CK_department_authority_origin
    CHECK(authority_origin IN ('LEGACY_UNKNOWN','INDEPENDENT','DESIGNATION','BOTH'));
 END;
 IF COL_LENGTH('dbo.department_management_access','authority_origin')<>24
  OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.department_management_access')
   AND name='CK_department_authority_origin' AND is_disabled=0 AND is_not_trusted=0)
  THROW 51981,'Provenance schema differs; preserve and review.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.DepartmentHeadTransfer.V1')
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.DepartmentHeadTransfer.V1',@value=N'1';
 IF @Apply=1 COMMIT; ELSE ROLLBACK;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
-- Post-commit rollback: preserve all provenance and restore the verified backup
-- into a NEW disposable database. Never drop this column after live transfers:
-- doing so would erase the distinction between independent/designation authority.
