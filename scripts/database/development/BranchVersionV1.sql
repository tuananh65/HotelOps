-- Additive branch concurrency; no lifecycle remapping, grants, receipts or history changes.
-- SQLCMD variables: ExpectedServer, ExpectedDatabase, ExpectedDatabaseGuid, ApplyChanges (0 rollback / 1 commit).
SET XACT_ABORT ON;
IF @@TRANCOUNT<>0 THROW 51986,'Standalone installer required.',1;
IF DB_NAME()<>N'$(ExpectedDatabase)' OR N'$(ExpectedDatabase)'<>N'HotelOpsDB'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51986,'Confirmed development identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51986,'ApplyChanges must be 0 or 1.',1;
IF OBJECT_ID('dbo.branches','U') IS NULL OR COL_LENGTH('dbo.branches','branch_id')<>8
 THROW 51986,'Expected branches schema required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.branches')
 AND name='branch_code' AND collation_name LIKE N'%[_]CI[_]%')
 THROW 51986,'Case-insensitive branch code schema required; do not rewrite historical codes automatically.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes i JOIN sys.index_columns k ON k.object_id=i.object_id AND k.index_id=i.index_id
 WHERE i.object_id=OBJECT_ID('dbo.branches') AND i.is_unique=1 AND i.is_disabled=0 AND i.has_filter=0
 AND k.key_ordinal=1 AND COL_NAME(k.object_id,k.column_id)='branch_code'
 AND NOT EXISTS(SELECT 1 FROM sys.index_columns extra WHERE extra.object_id=i.object_id AND extra.index_id=i.index_id AND extra.key_ordinal>1))
 THROW 51986,'Existing unique branch-code index required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:BranchVersion:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51986,'Migration lock unavailable.',1;
 IF COL_LENGTH('dbo.branches','row_version') IS NULL
 BEGIN
  IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.branches') AND system_type_id=189)
   THROW 51986,'Existing rowversion has another name; review without replacing it.',1;
  ALTER TABLE dbo.branches ADD row_version rowversion NOT NULL;
 END;
 IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.branches')
   AND name='row_version' AND system_type_id=189 AND max_length=8 AND is_nullable=0)
  THROW 51986,'Incompatible branch rowversion.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.BranchVersion.V1')
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.BranchVersion.V1',@value=N'1';
 ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.BranchVersion.V1' AND CONVERT(nvarchar(10),value)=N'1')
  THROW 51986,'Conflicting capability version.',1;
 IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
-- Before commit, ApplyChanges=0 reverses the DDL atomically. After deployment,
-- preserve rowversion and disable the writer if rollback is needed; never reset live concurrency tokens.
