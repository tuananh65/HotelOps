SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
 THROW 51870,'Exact owned disposable database required.',1;
IF EXISTS(SELECT 1 FROM dbo.operational_tasks) OR EXISTS(SELECT 1 FROM dbo.task_completion_criteria) OR EXISTS(SELECT 1 FROM dbo.audit_logs)
 THROW 51870,'Rollback requires empty source; drop populated trial clone instead.',1;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.operational_task_definitions') IS NOT NULL
BEGIN
 IF EXISTS(SELECT 1 FROM dbo.operational_task_definitions) OR EXISTS(SELECT 1 FROM dbo.task_category_processes)
  THROW 51870,'Planning data remains.',1;
 DROP TABLE dbo.operational_task_definitions;
 DROP TABLE dbo.task_category_processes;
END;
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_task_progress_criteria_v1 ON dbo.task_completion_criteria AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.criterion_id=d.criterion_id WHERE i.criterion_id IS NULL OR i.task_id<>d.task_id OR i.label<>d.label OR i.is_required<>d.is_required OR i.sort_order<>d.sort_order)
  THROW 51741,''Task criterion identity is immutable.'',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.operational_tasks t ON t.task_id=i.task_id JOIN dbo.task_progress_versions v ON v.task_id=t.task_id WHERE t.status<>''IN_PROGRESS'' OR v.workflow_status NOT IN(''IN_PROGRESS'',''PAUSED''))
  THROW 51741,''Task progress phase is invalid.'',1;
 UPDATE v SET task_id=v.task_id FROM dbo.task_progress_versions v JOIN (SELECT task_id FROM inserted UNION SELECT task_id FROM deleted) x ON x.task_id=v.task_id;
END;');
IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskPlanning.V1')
BEGIN
 ALTER TABLE dbo.task_progress_versions DROP CONSTRAINT CK_task_progress_state_entry_v1;
 ALTER TABLE dbo.task_progress_versions WITH CHECK ADD CONSTRAINT CK_task_progress_state_entry_v1
  CHECK(workflow_status IN('PENDING_ASSIGNMENT','ASSIGNED','ACCEPTED','IN_PROGRESS','PAUSED','PENDING_VERIFICATION','CLOSED'));
 EXEC sys.sp_dropextendedproperty @name=N'HotelOps.TaskPlanning.V1';
END;
COMMIT;
SELECT 'TASK_PLANNING_REMOVED' AS result;
