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
-- Only a cycle event is added; task_progress_versions remains the state authority.
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskPlanning.V1' AND CONVERT(nvarchar(20),value)='1')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskVerification.V1' AND CONVERT(nvarchar(20),value)='1')
 THROW 51876,'Planning and verification capabilities required.',1;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskReopen.V1')
BEGIN
 IF OBJECT_ID('dbo.task_reopen_events') IS NOT NULL THROW 51876,'Unreviewed reopen table exists.',1;
 CREATE TABLE dbo.task_reopen_events(
  previous_submission_id bigint NOT NULL CONSTRAINT PK_task_reopen_events PRIMARY KEY,
  task_id bigint NOT NULL,
  assignment_id bigint NOT NULL CONSTRAINT UQ_task_reopen_assignment UNIQUE,
  reopened_by bigint NOT NULL,
  reopened_at datetime2(7) NOT NULL,
  interval_cutoff bigint NOT NULL,
  reason nvarchar(4000) NOT NULL,
  instructions nvarchar(4000) NOT NULL,
  audit_id bigint NOT NULL CONSTRAINT UQ_task_reopen_audit UNIQUE,
  CONSTRAINT FK_task_reopen_submission FOREIGN KEY(previous_submission_id) REFERENCES dbo.task_verification_events(submission_id),
  CONSTRAINT FK_task_reopen_task FOREIGN KEY(task_id) REFERENCES dbo.operational_tasks(task_id),
  CONSTRAINT FK_task_reopen_assignment FOREIGN KEY(assignment_id) REFERENCES dbo.operational_task_assignments(assignment_id),
  CONSTRAINT FK_task_reopen_actor FOREIGN KEY(reopened_by) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_task_reopen_interval FOREIGN KEY(interval_cutoff) REFERENCES dbo.work_intervals(interval_id),
  CONSTRAINT FK_task_reopen_audit FOREIGN KEY(audit_id) REFERENCES dbo.audit_logs(audit_id),
  CONSTRAINT CK_task_reopen_v1 CHECK(LEN(LTRIM(RTRIM(reason)))>=10 AND LEN(LTRIM(RTRIM(instructions)))>0)
 );
 CREATE INDEX IX_task_reopen_task ON dbo.task_reopen_events(task_id,previous_submission_id);
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.TaskReopen.V1',@value=N'1';
END;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskReopen.V1' AND CONVERT(nvarchar(20),value)='1')
 OR COL_LENGTH('dbo.task_reopen_events','reason')<>8000 OR COL_LENGTH('dbo.task_reopen_events','instructions')<>8000
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.task_reopen_events') AND is_disabled=0 AND is_not_trusted=0)<>6
 THROW 51876,'Conflicting reopen schema.',1;
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_task_reopen_immutable_v1 ON dbo.task_reopen_events AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted) THROW 51876,''Reopen history is immutable.'',1; END;');
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
   OR (t.status=''TODO'' AND v.workflow_status=''ASSIGNED'' AND t.completed_at IS NULL AND t.progress_percent=0
    AND i.outcome IS NULL AND i.note IS NULL
    AND EXISTS(SELECT 1 FROM deleted d WHERE d.criterion_id=i.criterion_id)
    AND EXISTS(SELECT 1 FROM dbo.task_reopen_events r
      JOIN dbo.task_submission_events s ON s.progress_id=r.previous_submission_id AND s.task_id=r.task_id
      JOIN dbo.task_verification_events z ON z.submission_id=s.progress_id AND z.decision=''ACCEPT'' AND z.decided_at<=r.reopened_at
      JOIN dbo.operational_task_assignments a ON a.assignment_id=r.assignment_id AND a.task_id=r.task_id
        AND a.assignment_status=''ACTIVE'' AND a.ended_at IS NULL AND a.assigned_by=r.reopened_by AND a.assigned_at=r.reopened_at
      JOIN dbo.audit_logs q ON q.audit_id=r.audit_id AND q.actor_user_id=r.reopened_by AND q.occurred_at=r.reopened_at
        AND q.entity_type=''operational_tasks'' AND q.entity_id=CONVERT(nvarchar(50),r.task_id) AND q.branch_id=t.branch_id
      WHERE r.task_id=t.task_id AND t.updated_at=r.reopened_at
        AND JSON_VALUE(CASE WHEN ISJSON(q.new_values)=1 THEN q.new_values ELSE N''[]'' END,''$.operation'')=''REOPEN''
        AND NOT EXISTS(SELECT 1 FROM dbo.work_intervals w WHERE w.work_item_kind=''TASK'' AND w.work_item_id=r.task_id AND w.interval_id>r.interval_cutoff)))
  )) THROW 51741,''Task progress phase is invalid.'',1;
 UPDATE v SET task_id=v.task_id FROM dbo.task_progress_versions v JOIN (SELECT task_id FROM inserted UNION SELECT task_id FROM deleted) x ON x.task_id=v.task_id;
END;');

COMMIT;
SELECT 'TASK_REOPEN_INSTALLED' AS result;
