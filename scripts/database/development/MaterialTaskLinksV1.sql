-- UC092 additive task links, confirmed development identity.
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
DECLARE @Apply bit=CONVERT(bit,'$(ApplyChanges)');
IF ISNULL(COL_LENGTH('dbo.material_requests','row_version'),0)<>8 OR OBJECT_ID('dbo.task_progress_versions','U') IS NULL
 THROW 51931,'Material concurrency and task workflow schema required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:MaterialTaskLinks:Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51931,'Schema lock unavailable.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.MaterialTaskLinks.V1')
 BEGIN
  IF OBJECT_ID('dbo.material_request_task_links') IS NOT NULL THROW 51931,'Unexpected existing link schema; preserve and review.',1;
  CREATE TABLE dbo.material_request_task_links(
   request_id bigint NOT NULL,
   task_id bigint NOT NULL,
   audit_id bigint NOT NULL,
   CONSTRAINT PK_material_request_task_links PRIMARY KEY(request_id,task_id),
   CONSTRAINT FK_mr_task_request FOREIGN KEY(request_id) REFERENCES dbo.material_requests(request_id),
   CONSTRAINT FK_mr_task_task FOREIGN KEY(task_id) REFERENCES dbo.operational_tasks(task_id),
   CONSTRAINT FK_mr_task_audit FOREIGN KEY(audit_id) REFERENCES dbo.audit_logs(audit_id)
  );
  CREATE UNIQUE INDEX UX_mr_task_audit ON dbo.material_request_task_links(audit_id);
  CREATE INDEX IX_mr_task_reverse ON dbo.material_request_task_links(task_id,request_id);
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.MaterialTaskLinks.V1',@value=N'1';
 END;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.MaterialTaskLinks.V1' AND CONVERT(nvarchar(20),value)='1')
  OR (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.material_request_task_links'))<>3
  OR (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.material_request_task_links') AND name IN('request_id','task_id','audit_id') AND system_type_id=127 AND is_nullable=0)<>3
  OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.material_request_task_links') AND name IN('FK_mr_task_request','FK_mr_task_task','FK_mr_task_audit') AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0 AND update_referential_action=0)<>3
  OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.material_request_task_links') AND name='PK_material_request_task_links' AND is_primary_key=1 AND is_disabled=0)
  THROW 51931,'Material task-link schema mismatch.',1;
 IF @Apply=1 COMMIT; ELSE ROLLBACK;
 SELECT @Apply AS Applied;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
