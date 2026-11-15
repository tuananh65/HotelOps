SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) <> N'meita'
 OR DB_NAME() <> N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51892,'Exact owned disposable database required.',1;
IF COL_LENGTH('dbo.permissions','permission_code')<>100
 OR COL_LENGTH('dbo.permissions','action_code')<>40
 OR NOT EXISTS(SELECT 1 FROM dbo.permissions WHERE permission_code='IR_CREATE')
 THROW 51892,'Permission baseline incompatible.',1;
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM dbo.permissions v WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.permissions c ON c.permission_code='IR_CREATE'
 WHERE v.permission_code='IR_VIEW' AND (v.module_code<>c.module_code OR v.resource_code<>c.resource_code OR v.action_code<>'VIEW'))
 THROW 51892,'Conflicting IR_VIEW catalog entry; preserve and review.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.permissions WITH(UPDLOCK,HOLDLOCK) WHERE permission_code='IR_VIEW')
 INSERT dbo.permissions(permission_code,permission_name,module_code,resource_code,action_code,description)
 SELECT 'IR_VIEW',N'View internal request history',module_code,resource_code,'VIEW',N'UC123 read only; no intake or processing authority.'
 FROM dbo.permissions WHERE permission_code='IR_CREATE';
-- No default role grant. Each assignment still needs an explicit current grant/scope.
COMMIT;
