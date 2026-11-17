-- UC092 additive immutable links to actual preventive maintenance occurrences.
-- No source identity/stock/approval rewrite, no grants or historic backfill.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
IF DB_NAME()<>N'$(ExpectedDatabase)' OR N'$(ExpectedDatabase)'<>N'HotelOpsDB'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
 AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51872,'Confirmed development identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51872,'ApplyChanges must be 0 or 1.',1;
IF ISNULL(COL_LENGTH('dbo.material_requests','row_version'),0)<>8 OR OBJECT_ID('dbo.preventive_maintenance_runs','U') IS NULL
 THROW 51873,'Material concurrency and occurrence tables required.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:MaterialOccurrenceLinks:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51873,'Schema lock unavailable.',1;
IF OBJECT_ID('dbo.material_request_occurrence_links','U') IS NULL
BEGIN
 CREATE TABLE dbo.material_request_occurrence_links(
  request_id bigint NOT NULL,run_id bigint NOT NULL,audit_id bigint NOT NULL,
  CONSTRAINT PK_material_request_occurrence_links PRIMARY KEY(request_id,run_id),
  CONSTRAINT FK_mr_occurrence_request FOREIGN KEY(request_id) REFERENCES dbo.material_requests(request_id),
  CONSTRAINT FK_mr_occurrence_task FOREIGN KEY(run_id) REFERENCES dbo.preventive_maintenance_runs(run_id),
  CONSTRAINT FK_mr_occurrence_audit FOREIGN KEY(audit_id) REFERENCES dbo.audit_logs(audit_id));
 CREATE UNIQUE INDEX UX_mr_occurrence_audit ON dbo.material_request_occurrence_links(audit_id);
 CREATE INDEX IX_mr_occurrence_reverse ON dbo.material_request_occurrence_links(run_id,request_id);
END;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.material_request_occurrence_links')
  AND name IN('request_id','run_id','audit_id') AND system_type_id=127 AND is_nullable=0)<>3
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.material_request_occurrence_links')
  AND name IN('FK_mr_occurrence_request','FK_mr_occurrence_task','FK_mr_occurrence_audit')
  AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0 AND update_referential_action=0)<>3
 THROW 51873,'Existing occurrence link schema mismatch; preserve and review.',1;
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_mr_occurrence_immutable_v1 ON dbo.material_request_occurrence_links AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51873,''Material occurrence link history is immutable.'',1;
END;');
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.MaterialOccurrenceLinks.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.MaterialOccurrenceLinks.V1',@value=N'1';
IF N'$(ApplyChanges)'='1' COMMIT; ELSE ROLLBACK;
