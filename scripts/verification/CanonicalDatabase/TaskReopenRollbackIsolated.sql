-- Optional Database First capability for UC102/103. Never run on shared HotelOpsDB.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
 THROW 51870,'Exact owned disposable database required.',1;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.task_reopen_events') IS NOT NULL
BEGIN
 IF EXISTS(SELECT 1 FROM dbo.task_reopen_events) THROW 51876,'Populated cycle history must not be deleted. Drop only the owned disposable clone.',1;
 DROP TABLE dbo.task_reopen_events;
END;
IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskReopen.V1')
 EXEC sys.sp_dropextendedproperty @name=N'HotelOps.TaskReopen.V1';
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_task_progress_criteria_v1 ON dbo.task_completion_criteria AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.criterion_id=d.criterion_id WHERE i.criterion_id IS NULL OR i.task_id<>d.task_id OR i.label<>d.label OR i.is_required<>d.is_required OR i.sort_order<>d.sort_order)
  THROW 51741,''Task criterion identity is immutable.'',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.operational_tasks t ON t.task_id=i.task_id LEFT JOIN dbo.task_progress_versions v ON v.task_id=t.task_id
  WHERE v.task_id IS NULL OR NOT(
   (t.status=''IN_PROGRESS'' AND v.workflow_status IN(''IN_PROGRESS'',''PAUSED''))
   OR (t.status=''TODO'' AND v.workflow_status=''PENDING_ASSIGNMENT'' AND i.outcome IS NULL AND i.note IS NULL
    AND NOT EXISTS(SELECT 1 FROM deleted)
    AND EXISTS(SELECT 1 FROM dbo.operational_task_definitions d WHERE d.task_id=t.task_id AND d.creation_key IS NOT NULL AND d.requested_result IS NOT NULL)
    AND NOT EXISTS(SELECT 1 FROM dbo.operational_task_assignments a WHERE a.task_id=t.task_id)
    AND NOT EXISTS(SELECT 1 FROM dbo.operational_task_progress p WHERE p.task_id=t.task_id)
    AND NOT EXISTS(SELECT 1 FROM dbo.audit_logs a WHERE a.entity_type=''operational_tasks'' AND a.entity_id=CONVERT(nvarchar(50),t.task_id) AND a.action=''CREATE''))
  )) THROW 51741,''Task progress phase is invalid.'',1;
 UPDATE v SET task_id=v.task_id FROM dbo.task_progress_versions v JOIN (SELECT task_id FROM inserted UNION SELECT task_id FROM deleted) x ON x.task_id=v.task_id;
END;');

COMMIT;
SELECT 'TASK_REOPEN_REMOVED' AS result;
