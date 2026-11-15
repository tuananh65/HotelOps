SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita' OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
 THROW 51866,'Exact disposable source required',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskSubmission.V1' AND CONVERT(nvarchar(20),value)='1')
 OR OBJECT_ID('dbo.task_submission_events','U') IS NULL
 THROW 51866,'Submission capability required',1;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.task_verification_events','U') IS NULL
BEGIN
 CREATE TABLE dbo.task_verification_events(
  submission_id bigint NOT NULL PRIMARY KEY REFERENCES dbo.task_submission_events(progress_id),
  requested_by bigint NOT NULL REFERENCES dbo.users(user_id),performed_by_json nvarchar(max) NOT NULL,
  verified_by bigint NOT NULL REFERENCES dbo.users(user_id),decision varchar(6) NOT NULL,
  note nvarchar(4000) NOT NULL,decided_at datetime2(7) NOT NULL,actual_effort_seconds bigint NOT NULL,
  audit_id bigint NOT NULL UNIQUE REFERENCES dbo.audit_logs(audit_id),
  CONSTRAINT CK_task_verification_v1 CHECK(decision IN('ACCEPT','RETURN') AND ISJSON(performed_by_json)=1
    AND verified_by<>requested_by AND actual_effort_seconds>=0 AND LEN(LTRIM(RTRIM(note)))>=CASE WHEN decision='RETURN' THEN 10 ELSE 1 END));
 EXEC(N'CREATE TRIGGER dbo.TR_task_verification_immutable_v1 ON dbo.task_verification_events AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted) THROW 51866,''Verification history is immutable'',1; END');
 ALTER TABLE dbo.task_progress_versions DROP CONSTRAINT CK_task_progress_state_entry_v1;
 ALTER TABLE dbo.task_progress_versions WITH CHECK ADD CONSTRAINT CK_task_progress_state_entry_v1 CHECK(workflow_status IN('ASSIGNED','ACCEPTED','IN_PROGRESS','PAUSED','PENDING_VERIFICATION','CLOSED'));
END;
IF ISNULL(COL_LENGTH('dbo.task_verification_events','note'),-1)<>8000
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.task_verification_events') AND is_disabled=0 AND is_not_trusted=0)<>4
 OR NOT EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_task_verification_immutable_v1') AND is_disabled=0)
 THROW 51866,'Verification schema incompatible',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskVerification.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.TaskVerification.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskVerification.V1' AND CONVERT(nvarchar(20),value)='1')
 THROW 51866,'Conflicting verification capability',1;
COMMIT;
SELECT 'TASK_VERIFICATION_V1_INSTALLED' AS result;
