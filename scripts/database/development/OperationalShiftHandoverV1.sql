-- UC140 additive storage. Execute inside the DEV installer transaction after backup.
-- Approved independent SHIFT_HANDOVER permissions are catalog entries only; no grants.
SET XACT_ABORT ON;
IF @@TRANCOUNT<>1 THROW 51972,'Installer transaction required.',1;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita\MSSQLSERVER01'
 OR DB_NAME()<>N'HotelOpsDB'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E')
 THROW 51972,'Confirmed development identity required.',1;
IF COL_LENGTH('dbo.workdays','row_version')<>8
 OR COL_LENGTH('dbo.shift_assignments','shift_assignment_id')<>8
 OR COL_LENGTH('dbo.audit_logs','audit_id')<>8
 OR COL_LENGTH('dbo.notifications','notification_id')<>8
 THROW 51972,'Workday, shift, audit and notification baseline required.',1;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:ShiftHandover:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51972,'Shift handover schema lock unavailable.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.ShiftHandover.V1')
BEGIN
 IF OBJECT_ID('dbo.operational_shift_handovers') IS NOT NULL
  OR OBJECT_ID('dbo.operational_shift_handover_outbox') IS NOT NULL
  THROW 51972,'Unexpected handover storage; preserve existing data.',1;
 CREATE TABLE dbo.operational_shift_handovers(
  handover_id bigint IDENTITY NOT NULL CONSTRAINT PK_operational_shift_handovers PRIMARY KEY,
  request_key uniqueidentifier NOT NULL,
  workday_id bigint NOT NULL,branch_id bigint NOT NULL,department_id bigint NOT NULL,
  sender_id bigint NOT NULL,receiver_id bigint NOT NULL,receiver_shift_assignment_id bigint NOT NULL,
  previous_handover_id bigint NULL,
  snapshot_json nvarchar(max) NOT NULL,general_note nvarchar(4000) NULL,
  state varchar(30) NOT NULL,submitted_at datetime2(7) NOT NULL,submission_audit_id bigint NOT NULL,
  decision_at datetime2(7) NULL,decision_audit_id bigint NULL,rejection_reason nvarchar(4000) NULL,
  row_version rowversion NOT NULL,
  CONSTRAINT UQ_operational_shift_handover_request UNIQUE(sender_id,request_key),
  CONSTRAINT FK_shift_handover_workday FOREIGN KEY(workday_id) REFERENCES dbo.workdays(workday_id),
  CONSTRAINT FK_shift_handover_branch FOREIGN KEY(branch_id) REFERENCES dbo.branches(branch_id),
  CONSTRAINT FK_shift_handover_department FOREIGN KEY(department_id) REFERENCES dbo.departments(department_id),
  CONSTRAINT FK_shift_handover_sender FOREIGN KEY(sender_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_shift_handover_receiver FOREIGN KEY(receiver_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_shift_handover_shift FOREIGN KEY(receiver_shift_assignment_id) REFERENCES dbo.shift_assignments(shift_assignment_id),
  CONSTRAINT FK_shift_handover_previous FOREIGN KEY(previous_handover_id) REFERENCES dbo.operational_shift_handovers(handover_id),
  CONSTRAINT FK_shift_handover_submit_audit FOREIGN KEY(submission_audit_id) REFERENCES dbo.audit_logs(audit_id),
  CONSTRAINT FK_shift_handover_decision_audit FOREIGN KEY(decision_audit_id) REFERENCES dbo.audit_logs(audit_id),
  CONSTRAINT CK_shift_handover_identity CHECK(sender_id<>receiver_id AND (previous_handover_id IS NULL OR previous_handover_id<>handover_id)),
  CONSTRAINT CK_shift_handover_snapshot CHECK(ISJSON(snapshot_json)=1),
  CONSTRAINT CK_shift_handover_state CHECK(
   (state='PENDING_ACKNOWLEDGMENT' AND decision_at IS NULL AND decision_audit_id IS NULL AND rejection_reason IS NULL)
   OR (state='ACKNOWLEDGED' AND decision_at IS NOT NULL AND decision_at>=submitted_at AND decision_audit_id IS NOT NULL AND rejection_reason IS NULL)
   OR (state='REJECTED' AND decision_at IS NOT NULL AND decision_at>=submitted_at AND decision_audit_id IS NOT NULL
     AND rejection_reason IS NOT NULL AND LEN(LTRIM(RTRIM(rejection_reason)))>=10))
 );
 CREATE INDEX IX_shift_handover_receiver ON dbo.operational_shift_handovers(receiver_id,state,handover_id);
 CREATE INDEX IX_shift_handover_sender ON dbo.operational_shift_handovers(sender_id,state,handover_id);
 CREATE TABLE dbo.operational_shift_handover_outbox(
  event_audit_id bigint NOT NULL,handover_id bigint NOT NULL,recipient_id bigint NOT NULL,
  recipient_kind varchar(16) NOT NULL,created_at datetime2(7) NOT NULL,notification_id bigint NULL,
  CONSTRAINT PK_operational_shift_handover_outbox PRIMARY KEY(event_audit_id,recipient_id),
  CONSTRAINT FK_shift_handover_notice_event FOREIGN KEY(event_audit_id) REFERENCES dbo.audit_logs(audit_id),
  CONSTRAINT FK_shift_handover_notice_handover FOREIGN KEY(handover_id) REFERENCES dbo.operational_shift_handovers(handover_id),
  CONSTRAINT FK_shift_handover_notice_recipient FOREIGN KEY(recipient_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_shift_handover_notice_notification FOREIGN KEY(notification_id) REFERENCES dbo.notifications(notification_id),
  CONSTRAINT CK_shift_handover_notice_kind CHECK(recipient_kind IN('RECEIVER','SENDER','HEAD'))
 );
 EXEC(N'CREATE TRIGGER dbo.TR_operational_shift_handover_immutable ON dbo.operational_shift_handovers
 AFTER UPDATE,DELETE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.handover_id=d.handover_id
   WHERE i.handover_id IS NULL OR d.state<>''PENDING_ACKNOWLEDGMENT''
    OR i.state NOT IN(''ACKNOWLEDGED'',''REJECTED''))
   THROW 51973,''Only a single acknowledgment or rejection is permitted.'',1;
  IF EXISTS(SELECT handover_id,request_key,workday_id,branch_id,department_id,sender_id,receiver_id,receiver_shift_assignment_id,
     previous_handover_id,CONVERT(varbinary(max),snapshot_json),CONVERT(varbinary(max),general_note),submitted_at,submission_audit_id FROM deleted
    EXCEPT SELECT handover_id,request_key,workday_id,branch_id,department_id,sender_id,receiver_id,receiver_shift_assignment_id,
     previous_handover_id,CONVERT(varbinary(max),snapshot_json),CONVERT(varbinary(max),general_note),submitted_at,submission_audit_id FROM inserted)
   THROW 51973,''Submitted handover snapshot and provenance are immutable.'',1;
 END;');
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.ShiftHandover.V1',@value=N'1';
END;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.ShiftHandover.V1' AND CONVERT(nvarchar(20),value)='1')
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN
   (OBJECT_ID('dbo.operational_shift_handovers'),OBJECT_ID('dbo.operational_shift_handover_outbox'))
   AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0 AND update_referential_action=0)<>13
 OR NOT EXISTS(SELECT 1 FROM sys.triggers WHERE parent_id=OBJECT_ID('dbo.operational_shift_handovers')
   AND name='TR_operational_shift_handover_immutable' AND is_disabled=0)
 THROW 51972,'Incompatible handover schema; preserve and review.',1;
-- Any failure rolls back in the owning installer. After commit retain all receipts;
-- rollback application activation, or restore backup into a separate recovery DB.
-- Never cascade-delete audit/receipts or disable constraints to perform retention.

DECLARE @codes TABLE(code varchar(100),action varchar(30));
INSERT @codes VALUES('SHIFT_HANDOVER_VIEW','view'),('SHIFT_HANDOVER_SUBMIT','submit'),('SHIFT_HANDOVER_DECIDE','decide');
IF EXISTS(SELECT 1 FROM @codes c JOIN dbo.permissions p ON p.permission_code=c.code
 WHERE p.module_code<>'WORK_TIME' OR p.resource_code<>'shift_handover' OR p.action_code<>c.action)
 THROW 51972,'Existing handover permission semantics conflict.',1;
INSERT dbo.permissions(permission_code,permission_name,module_code,resource_code,action_code,description)
 SELECT c.code,N'Operational shift handover '+c.action,'WORK_TIME','shift_handover',c.action,
 N'UC140: current role, branch scope and designated sender/receiver or mapped Department Head; independent grant required.'
 FROM @codes c WHERE NOT EXISTS(SELECT 1 FROM dbo.permissions p WITH(UPDLOCK,HOLDLOCK) WHERE p.permission_code=c.code);
