-- UC111 approved independent permission. No role/user grant changes.
-- Executed only by the identity-guarded installer after a fresh copy-only backup.
SET XACT_ABORT ON;
IF @@TRANCOUNT<>1 THROW 51961,'Installer transaction required.',1;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita\MSSQLSERVER01'
 OR DB_NAME()<>N'HotelOpsDB'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E')
 THROW 51961,'Confirmed development identity required.',1;
IF COL_LENGTH('dbo.time_correction_requests','original_interval_version')<>8
 OR COL_LENGTH('dbo.time_correction_requests','reason')<>2000
 OR COL_LENGTH('dbo.time_correction_requests','decision_reason')<>2000
 OR COL_LENGTH('dbo.permissions','permission_code')<>100
 THROW 51961,'Correction/catalog schema incompatible.',1;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:TimeCorrectionPermission:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51961,'Catalog lock unavailable.',1;
IF EXISTS(SELECT 1 FROM dbo.permissions WITH(UPDLOCK,HOLDLOCK) WHERE permission_code='TIME_CORRECTION_DECIDE'
 AND (module_code<>'WORK_TIME' OR resource_code<>'time_correction' OR action_code<>'decide'))
 THROW 51961,'Existing permission semantics conflict.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.permissions WITH(UPDLOCK,HOLDLOCK) WHERE permission_code='TIME_CORRECTION_DECIDE')
 INSERT dbo.permissions(permission_code,permission_name,module_code,resource_code,action_code,description)
 VALUES('TIME_CORRECTION_DECIDE',N'Decide time correction','WORK_TIME','time_correction','decide',
 N'UC111: current Department Head, explicit workday department mapping and branch scope; no self approval. Independent grant required.');
IF (SELECT COUNT(*) FROM dbo.permissions WHERE permission_code='TIME_CORRECTION_DECIDE')<>1
 THROW 51961,'Permission uniqueness required.',1;
IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.TimeCorrectionPermission.V1' AND CONVERT(nvarchar(128),value)<>N'1')
 THROW 51961,'Conflicting catalog version.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.TimeCorrectionPermission.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.TimeCorrectionPermission.V1',@value=1;
-- Rollback on installation failure is owned by the installer. After successful installation,
-- reverting application code may leave this ungranted catalog entry; do not delete historical corrections.
