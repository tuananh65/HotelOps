-- DEV3 decision 64: extend only the existing workflow authority, never legacy statuses.
-- This script is deliberately restricted to the owned disposable trial.
SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) <> N'meita'
 OR DB_NAME() <> N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51864,'Exact owned disposable database required.',1;
IF ISNULL(COL_LENGTH('dbo.task_progress_versions','workflow_status'),-1)<>20
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.task_progress_versions') AND name='row_version' AND system_type_id=189)
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.operational_tasks') AND name='CK_operational_tasks_status' AND is_disabled=0 AND is_not_trusted=0)
 THROW 51864,'Existing task schema incompatible.',1;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.CK_task_progress_state_entry_v1','C') IS NULL
BEGIN
 IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_task_progress_state_v1' AND parent_object_id=OBJECT_ID('dbo.task_progress_versions') AND is_disabled=0 AND is_not_trusted=0)
  THROW 51864,'Original workflow constraint must be enabled and trusted.',1;
 IF EXISTS(SELECT 1 FROM dbo.task_progress_versions v JOIN dbo.operational_tasks t ON t.task_id=v.task_id
   WHERE v.workflow_status NOT IN('IN_PROGRESS','PAUSED') OR t.status<>'IN_PROGRESS')
  THROW 51864,'Existing workflow rows require explicit reconciliation.',1;
 ALTER TABLE dbo.task_progress_versions DROP CONSTRAINT CK_task_progress_state_v1;
 ALTER TABLE dbo.task_progress_versions WITH CHECK ADD CONSTRAINT CK_task_progress_state_entry_v1
  CHECK(workflow_status IN('ASSIGNED','ACCEPTED','IN_PROGRESS','PAUSED'));
END;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_task_progress_state_entry_v1' AND is_disabled=0 AND is_not_trusted=0)
 THROW 51864,'Entry constraint must be enabled and trusted.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskEntry.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.TaskEntry.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskEntry.V1' AND CONVERT(nvarchar(20),value)='1')
 THROW 51864,'Conflicting entry capability marker.',1;
COMMIT;
SELECT DB_NAME() AS databaseName,'TASK_ENTRY_V1' AS capability,
 (SELECT COUNT(*) FROM dbo.task_progress_versions) AS workflowRows;
