-- UC008 native concurrency token; additive, no role/permission grants or EF changes.
SET XACT_ABORT ON;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%' OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000') THROW 51849,'Verified disposable identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51849,'ApplyChanges must be 0 or 1.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:RoleMetadataVersion:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51882,'Role metadata schema lock unavailable.',1;
IF OBJECT_ID(N'dbo.roles',N'U') IS NULL THROW 51882,'Role storage missing.',1;
IF COL_LENGTH(N'dbo.roles',N'row_version') IS NULL
 ALTER TABLE dbo.roles ADD row_version rowversion NOT NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.roles')
 AND name=N'row_version' AND system_type_id=189 AND max_length=8 AND is_nullable=0)
 THROW 51882,'Incompatible role version column.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.RoleMetadataVersion.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.RoleMetadataVersion.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.RoleMetadataVersion.V1' AND CONVERT(nvarchar(20),value)=N'1')
 THROW 51882,'Conflicting role metadata capability.',1;
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
-- Application rollback: withdraw metadata write route; retain the additive column.
-- Never drop the version column while writers/clients still rely on its tokens.
