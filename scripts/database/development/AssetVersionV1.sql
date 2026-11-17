-- Additive asset concurrency; no lifecycle remapping, grants, receipts or history changes.
-- SQLCMD variables: ExpectedServer, ExpectedDatabase, ExpectedDatabaseGuid, ApplyChanges (0 rollback / 1 commit).
SET XACT_ABORT ON;
IF @@TRANCOUNT<>0 THROW 51984,'Standalone installer required.',1;
IF DB_NAME()<>N'$(ExpectedDatabase)' OR N'$(ExpectedDatabase)'<>N'HotelOpsDB'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51984,'Confirmed development identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51984,'ApplyChanges must be 0 or 1.',1;
IF OBJECT_ID('dbo.assets','U') IS NULL OR COL_LENGTH('dbo.assets','asset_id')<>8
 THROW 51984,'Expected assets schema required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:AssetVersion:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51984,'Migration lock unavailable.',1;
 IF COL_LENGTH('dbo.assets','row_version') IS NULL
 BEGIN
  IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.assets') AND system_type_id=189)
   THROW 51984,'Existing rowversion has another name; review without replacing it.',1;
  ALTER TABLE dbo.assets ADD row_version rowversion NOT NULL;
 END;
 IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.assets')
   AND name='row_version' AND system_type_id=189 AND max_length=8 AND is_nullable=0)
  THROW 51984,'Incompatible asset rowversion.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.AssetVersion.V1')
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.AssetVersion.V1',@value=N'1';
 ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.AssetVersion.V1' AND CONVERT(nvarchar(10),value)=N'1')
  THROW 51984,'Conflicting capability version.',1;
 IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
-- Before commit, ApplyChanges=0 reverses the DDL atomically. After deployment,
-- preserve rowversion and disable the writer if rollback is needed; never reset live concurrency tokens.
