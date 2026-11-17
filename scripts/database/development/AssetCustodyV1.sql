-- UC069 additive immutable submission versions. No grants or historical backfill.
-- SQLCMD variables: ExpectedServer, ExpectedDatabase, ExpectedDatabaseGuid, ApplyChanges (0 rollback / 1 commit).
SET XACT_ABORT ON;
IF @@TRANCOUNT<>0 THROW 51989,'Standalone installer required.',1;
IF DB_NAME()<>N'$(ExpectedDatabase)' OR N'$(ExpectedDatabase)'<>N'HotelOpsDB'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51989,'Confirmed development identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51989,'ApplyChanges must be 0 or 1.',1;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:AssetCustody:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51989,'Migration lock unavailable.',1;
 IF OBJECT_ID('dbo.asset_handovers','U') IS NULL OR OBJECT_ID('dbo.audit_logs','U') IS NULL
  THROW 51989,'Existing asset handover and audit schema required.',1;
 IF EXISTS(SELECT asset_id FROM dbo.asset_handovers WHERE handover_status='PENDING' GROUP BY asset_id HAVING COUNT(*)>1)
  THROW 51989,'Existing duplicate pending handovers require explicit business resolution.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.asset_handovers') AND name='UX_asset_handover_pending_v1')
  CREATE UNIQUE INDEX UX_asset_handover_pending_v1 ON dbo.asset_handovers(asset_id) WHERE handover_status='PENDING';
 IF OBJECT_ID('dbo.asset_handover_versions','U') IS NULL
 BEGIN
  CREATE TABLE dbo.asset_handover_versions(
   handover_id bigint NOT NULL CONSTRAINT PK_asset_handover_versions PRIMARY KEY,
   previous_handover_id bigint NULL,
   submission_audit_id bigint NOT NULL,
   snapshot_json nvarchar(max) NOT NULL,
   CONSTRAINT FK_asset_handover_version_receipt FOREIGN KEY(handover_id) REFERENCES dbo.asset_handovers(handover_id),
   CONSTRAINT FK_asset_handover_version_previous FOREIGN KEY(previous_handover_id) REFERENCES dbo.asset_handovers(handover_id),
   CONSTRAINT FK_asset_handover_version_audit FOREIGN KEY(submission_audit_id) REFERENCES dbo.audit_logs(audit_id),
   CONSTRAINT CK_asset_handover_version_json CHECK(ISJSON(snapshot_json)=1),
   CONSTRAINT CK_asset_handover_version_previous CHECK(previous_handover_id IS NULL OR previous_handover_id<handover_id));
  CREATE UNIQUE INDEX UX_asset_handover_version_previous ON dbo.asset_handover_versions(previous_handover_id) WHERE previous_handover_id IS NOT NULL;
  CREATE UNIQUE INDEX UX_asset_handover_version_audit ON dbo.asset_handover_versions(submission_audit_id);
 END;
 IF ISNULL(COL_LENGTH('dbo.asset_handover_versions','handover_id'),0)<>8
  OR ISNULL(COL_LENGTH('dbo.asset_handover_versions','submission_audit_id'),0)<>8
  OR ISNULL(COL_LENGTH('dbo.asset_handover_versions','snapshot_json'),0)<>-1
  THROW 51989,'Incompatible custody version schema.',1;
 IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.asset_handover_versions')
    AND name IN('FK_asset_handover_version_receipt','FK_asset_handover_version_previous','FK_asset_handover_version_audit')
    AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>3
  OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.asset_handovers')
    AND name='UX_asset_handover_pending_v1' AND is_unique=1 AND is_disabled=0 AND has_filter=1)
  OR (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.asset_handover_versions')
    AND name IN('PK_asset_handover_versions','UX_asset_handover_version_previous','UX_asset_handover_version_audit') AND is_unique=1 AND is_disabled=0)<>3
  OR (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.asset_handover_versions')
    AND name IN('CK_asset_handover_version_json','CK_asset_handover_version_previous') AND is_disabled=0 AND is_not_trusted=0)<>2
  THROW 51989,'Trusted custody integrity constraints required.',1;
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_asset_handover_versions_immutable ON dbo.asset_handover_versions AFTER UPDATE,DELETE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM deleted) THROW 51989,''Custody submission versions are immutable.'',1;
 END;');
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.AssetCustody.V1')
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.AssetCustody.V1',@value=N'1';
 ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.AssetCustody.V1' AND CONVERT(nvarchar(10),value)='1')
  THROW 51989,'Conflicting capability version.',1;
 IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
-- ApplyChanges=0 rolls back DDL. After use, retain immutable records and disable writers instead of deleting evidence.
