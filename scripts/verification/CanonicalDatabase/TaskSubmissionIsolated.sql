SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51865,'Exact owned disposable database required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskEntry.V1' AND CONVERT(nvarchar(20),value)='1')
 OR ISNULL(COL_LENGTH('dbo.task_progress_versions','workflow_status'),-1)<>20
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_task_progress_state_entry_v1' AND is_disabled=0 AND is_not_trusted=0)
 THROW 51865,'Verified entry capability required.',1;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.task_submission_events','U') IS NULL
BEGIN
 CREATE TABLE dbo.task_submission_events(
  progress_id bigint NOT NULL PRIMARY KEY REFERENCES dbo.operational_task_progress(progress_id),
  task_id bigint NOT NULL REFERENCES dbo.operational_tasks(task_id),
  submitted_by bigint NOT NULL REFERENCES dbo.users(user_id),
  interval_id bigint NOT NULL UNIQUE REFERENCES dbo.work_intervals(interval_id),
  assignment_id bigint NOT NULL REFERENCES dbo.operational_task_assignments(assignment_id),
  audit_id bigint NOT NULL UNIQUE REFERENCES dbo.audit_logs(audit_id),
  submitted_at datetime2(7) NOT NULL,result_note nvarchar(4000) NULL,handover_note nvarchar(4000) NULL,
  criteria_json nvarchar(max) NOT NULL,evidence_json nvarchar(max) NOT NULL,recipients_json nvarchar(max) NOT NULL,
  CONSTRAINT CK_task_submission_json_v1 CHECK(ISJSON(criteria_json)=1 AND ISJSON(evidence_json)=1 AND ISJSON(recipients_json)=1));
 CREATE INDEX IX_task_submission_task_v1 ON dbo.task_submission_events(task_id,progress_id);
 EXEC(N'CREATE TRIGGER dbo.TR_task_submission_immutable_v1 ON dbo.task_submission_events AFTER UPDATE AS BEGIN SET NOCOUNT ON; THROW 51865,''Submission snapshots cannot be rewritten.'',1; END');
 ALTER TABLE dbo.task_progress_versions DROP CONSTRAINT CK_task_progress_state_entry_v1;
 ALTER TABLE dbo.task_progress_versions WITH CHECK ADD CONSTRAINT CK_task_progress_state_entry_v1
  CHECK(workflow_status IN('ASSIGNED','ACCEPTED','IN_PROGRESS','PAUSED','PENDING_VERIFICATION'));
END;
IF ISNULL(COL_LENGTH('dbo.task_submission_events','result_note'),-1)<>8000
 OR ISNULL(COL_LENGTH('dbo.task_submission_events','handover_note'),-1)<>8000
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.task_submission_events') AND is_disabled=0 AND is_not_trusted=0)<>6
 OR NOT EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_task_submission_immutable_v1') AND is_disabled=0)
 THROW 51865,'Submission schema incompatible.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskSubmission.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.TaskSubmission.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskSubmission.V1' AND CONVERT(nvarchar(20),value)='1')
 THROW 51865,'Conflicting submission capability.',1;
COMMIT;
SELECT DB_NAME() AS databaseName,'TASK_SUBMISSION_V1' AS capability,(SELECT COUNT(*) FROM dbo.task_submission_events) AS submissions;
