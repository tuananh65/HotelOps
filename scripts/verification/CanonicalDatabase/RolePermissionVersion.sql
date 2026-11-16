-- Requires RoleMetadataVersion.sql. No grants are created by this installer.
SET XACT_ABORT ON;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%' OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
 AND database_guid<>'00000000-0000-0000-0000-000000000000') THROW 51849,'Verified disposable identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51849,'ApplyChanges must be 0 or 1.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:GlobalAdminEligibility:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51882,'Role schema lock unavailable.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.roles') AND name=N'row_version' AND system_type_id=189)
 THROW 51882,'Install RoleMetadataVersion first.',1;
IF OBJECT_ID(N'dbo.TR_role_permissions_version_v1',N'TR') IS NOT NULL
 AND NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.RolePermissionVersion.V1' AND CONVERT(nvarchar(20),value)=N'1')
 THROW 51882,'Unrecognized role permission trigger.',1;
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_role_permissions_version_v1 ON dbo.role_permissions AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 UPDATE r SET role_name=r.role_name FROM dbo.roles r
 JOIN (SELECT role_id FROM inserted UNION SELECT role_id FROM deleted) touched ON touched.role_id=r.role_id;
END');
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.RolePermissionVersion.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.RolePermissionVersion.V1',@value=N'1';
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
-- Roll back application exposure first. Keep additive version support for existing clients.
