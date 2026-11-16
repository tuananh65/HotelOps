-- Approved independent MR_VIEW catalog entry only. Never grants it to a role/user.
-- Exact verified disposable recovery clone; default rolls back. No shared DB use.
SET XACT_ABORT ON;
SET NOCOUNT ON;
DECLARE @Apply bit = 0;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) <> N'meita\MSSQLSERVER01'
 OR DB_NAME() <> N'HotelOps_DEV3_142A_Recovery_2b221f40'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid='5270D07C-7C27-477B-A60E-86485ADD7FAD')
 THROW 51899,'Exact verified disposable recovery database required.',1;
IF COL_LENGTH('dbo.permissions','permission_code')<>100
 OR COL_LENGTH('dbo.permissions','action_code')<>40
 OR COLUMNPROPERTY(OBJECT_ID('dbo.permissions'),'permission_id','IsIdentity')<>1
 THROW 51899,'Permission baseline incompatible.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:MaterialRequestView:Install',
   @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51899,'Cannot lock catalog installation.',1;
 IF (SELECT COUNT(*) FROM dbo.permissions WITH(UPDLOCK,HOLDLOCK) WHERE permission_code='MR_CREATE')<>1
  THROW 51899,'Unique existing MR_CREATE catalog source required.',1;
 IF EXISTS(SELECT 1 FROM dbo.permissions v WITH(UPDLOCK,HOLDLOCK)
   JOIN dbo.permissions c ON c.permission_code='MR_CREATE'
   WHERE v.permission_code='MR_VIEW' AND (v.module_code<>c.module_code OR v.resource_code<>c.resource_code OR v.action_code<>'VIEW'))
  THROW 51899,'Conflicting MR_VIEW catalog entry; preserve and review.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.permissions WITH(UPDLOCK,HOLDLOCK) WHERE permission_code='MR_VIEW')
  INSERT dbo.permissions(permission_code,permission_name,module_code,resource_code,action_code,description)
  SELECT 'MR_VIEW',N'View material requests',module_code,resource_code,'VIEW',
    N'Independent material read permission; current branch/data scope required. No create, approve or fulfillment authority.'
  FROM dbo.permissions WHERE permission_code='MR_CREATE';
 IF (SELECT COUNT(*) FROM dbo.permissions WHERE permission_code='MR_VIEW')<>1
  THROW 51899,'MR_VIEW uniqueness required.',1;
 IF @Apply=1 COMMIT TRANSACTION; ELSE ROLLBACK TRANSACTION;
 SELECT @Apply AS Applied,N'MR_VIEW' AS PermissionCode;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
