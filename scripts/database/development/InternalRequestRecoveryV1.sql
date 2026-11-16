-- Recovery-specific V1 deployment of reviewed IR authority/decision/assignment/completion.
-- Original installers remain unchanged; this bundle supplies its own exact identity guard.
-- Default trial rolls back all nested migration transactions. No grants or business rows.
SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
DECLARE @Apply bit=0;
IF @@TRANCOUNT<>0 THROW 51930,'Run as standalone deployment.',1;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita\MSSQLSERVER01'
 OR DB_NAME()<>'HotelOps_DEV3_142A_Recovery_2b221f40'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='5270D07C-7C27-477B-A60E-86485ADD7FAD')
 THROW 51930,'Exact recovery disposable required.',1;
IF NOT EXISTS(SELECT 1 FROM msdb.dbo.backupset b WHERE b.backup_set_id=4 AND b.database_name=DB_NAME()
 AND b.database_guid=(SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID())
 AND b.type='D' AND b.is_damaged=0 AND b.has_backup_checksums=1 AND b.backup_finish_date>=DATEADD(day,-1,GETDATE())
 AND EXISTS(SELECT 1 FROM msdb.dbo.restorehistory r WHERE r.backup_set_id=b.backup_set_id AND r.restore_type='D'
 AND r.destination_database_name='HotelOps_DEV3_142A_FE10Restore_77c41aba' AND r.restore_date>=b.backup_finish_date)
 AND EXISTS(SELECT 1 FROM sys.databases WHERE name='HotelOps_DEV3_142A_FE10Restore_77c41aba' AND state=0))
 THROW 51930,'Recent checksum backup and actual disposable restore required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:InternalRequest:Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51930,'Schema lock unavailable.',1;
-- Original DepartmentManagementIsolated.sql SHA256 55C293980E3C62F7FD115674464DED5A77CDB91B4E18273F7C9852EA013CCAE9
IF ISNULL(COL_LENGTH('dbo.users','user_id'),0)<>8
 OR ISNULL(COL_LENGTH('dbo.departments','department_id'),0)<>8
 OR ISNULL(COL_LENGTH('dbo.departments','branch_id'),0)<>8
 THROW 51893,'Department authority baseline incompatible.',1;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.department_management_access','U') IS NULL
BEGIN
 IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.departments') AND name='UX_departments_authority_pair')
  THROW 51893,'Unexpected pre-existing authority index; preserve and review.',1;
 -- Existing department key stays unchanged. This alternate key permits a composite
 -- FK that makes a cross-branch department mapping physically impossible.
 CREATE UNIQUE INDEX UX_departments_authority_pair ON dbo.departments(department_id,branch_id);
 CREATE TABLE dbo.department_management_access(
  user_id bigint NOT NULL,
  branch_id bigint NOT NULL,
  department_id bigint NOT NULL,
  granted_at datetime2(7) NOT NULL,
  revoked_at datetime2(7) NULL,
  revision rowversion NOT NULL,
  CONSTRAINT PK_department_management_access PRIMARY KEY(user_id,branch_id,department_id),
  CONSTRAINT FK_department_management_user FOREIGN KEY(user_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_department_management_department FOREIGN KEY(department_id,branch_id) REFERENCES dbo.departments(department_id,branch_id),
  CONSTRAINT CK_department_management_dates CHECK(revoked_at IS NULL OR revoked_at>=granted_at)
 );
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.DepartmentAuthority.V1',@value=N'1';
END;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.DepartmentAuthority.V1' AND CONVERT(nvarchar(20),value)='1')
 OR ISNULL(COL_LENGTH('dbo.department_management_access','revision'),0)<>8
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.department_management_access')
   AND name IN('FK_department_management_user','FK_department_management_department')
   AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>2
 THROW 51893,'Existing department authority contract differs; preserve and review.',1;
-- No inferred mappings, default grants, production seed, or receipt modifications.
COMMIT;

-- Original InternalRequestDecisionIsolated.sql SHA256 8FCE8FF21C288559DAEF840BE261C7859E284C777A69689E6A5D82D4D34C45E3
IF ISNULL(COL_LENGTH('dbo.internal_requests','status'),0)<>40
 OR ISNULL(COL_LENGTH('dbo.internal_request_events','event_id'),0)<>8
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.DepartmentAuthority.V1' AND CONVERT(nvarchar(20),value)='1')
 THROW 51894,'Verified request/department authority baseline required.',1;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestDecision.V1')
BEGIN
 IF COL_LENGTH('dbo.internal_requests','row_version') IS NOT NULL OR OBJECT_ID('dbo.internal_request_decision_outbox') IS NOT NULL
  THROW 51894,'Unexpected pre-existing decision schema; preserve and review.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.internal_requests')
   AND name='CK_internal_requests_status' AND is_disabled=0 AND is_not_trusted=0
   AND definition=N'([status]=''CANCELLED'' OR [status]=''REJECTED'' OR [status]=''CLOSED'' OR [status]=''COMPLETED'' OR [status]=''IN_PROGRESS'' OR [status]=''SUBMITTED'' OR [status]=''DRAFT'')')
  THROW 51894,'Status constraint differs from inspected baseline; preserve and review.',1;
 ALTER TABLE dbo.internal_requests DROP CONSTRAINT CK_internal_requests_status;
 ALTER TABLE dbo.internal_requests WITH CHECK ADD CONSTRAINT CK_internal_requests_status
  CHECK(status IN('DRAFT','SUBMITTED','PENDING_ACCEPTANCE','ACCEPTED','IN_PROGRESS','COMPLETED','CLOSED','REJECTED','CANCELLED'));
 ALTER TABLE dbo.internal_requests ADD row_version rowversion NOT NULL;
 CREATE TABLE dbo.internal_request_decision_outbox(
  event_id bigint NOT NULL CONSTRAINT PK_internal_request_decision_outbox PRIMARY KEY,
  requester_id bigint NOT NULL,
  created_at datetime2(7) NOT NULL,
  notification_id bigint NULL,
  CONSTRAINT FK_ir_decision_event FOREIGN KEY(event_id) REFERENCES dbo.internal_request_events(event_id),
  CONSTRAINT FK_ir_decision_requester FOREIGN KEY(requester_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_ir_decision_notification FOREIGN KEY(notification_id) REFERENCES dbo.notifications(notification_id)
 );
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.InternalRequestDecision.V1',@value=N'1';
END;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestDecision.V1' AND CONVERT(nvarchar(20),value)='1')
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.internal_requests') AND name='row_version' AND system_type_id=189 AND is_nullable=0)
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.internal_requests') AND name='CK_internal_requests_status'
   AND is_disabled=0 AND is_not_trusted=0 AND definition LIKE '%''PENDING_ACCEPTANCE''%' AND definition LIKE '%''ACCEPTED''%')
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.internal_request_decision_outbox')
   AND name IN('FK_ir_decision_event','FK_ir_decision_requester','FK_ir_decision_notification') AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>3
 THROW 51894,'Decision schema incompatible.',1;
-- Existing SUBMITTED rows are deliberately not relabelled. Their provenance must be reviewed separately.
COMMIT;

-- Original InternalRequestAssignmentIsolated.sql SHA256 A91A3E79F5F17BF60F78B958D5B70E5950559902E3789D88509466D0F802446A
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestDecision.V1' AND CONVERT(nvarchar(20),value)='1')
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.internal_request_assignments')
  AND name='CK_internal_request_assignments_status' AND is_disabled=0 AND is_not_trusted=0
  AND definition=N'([status]=''CANCELLED'' OR [status]=''REASSIGNED'' OR [status]=''COMPLETED'' OR [status]=''ACTIVE'')')
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.internal_request_assignments')
  AND name='UX_internal_request_assignments_active' AND is_unique=1 AND is_disabled=0 AND filter_definition=N'([status]=''ACTIVE'')')
 THROW 51895,'Verified decision and assignment baseline required.',1;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestAssignment.V1')
BEGIN
 IF OBJECT_ID('dbo.internal_request_assignment_outbox') IS NOT NULL
  THROW 51895,'Unexpected assignment outbox; preserve and review.',1;
 CREATE TABLE dbo.internal_request_assignment_outbox(
  event_id bigint NOT NULL, recipient_id bigint NOT NULL, assignment_id bigint NOT NULL,
  created_at datetime2(7) NOT NULL, notification_id bigint NULL,
  CONSTRAINT PK_internal_request_assignment_outbox PRIMARY KEY(event_id,recipient_id),
  CONSTRAINT FK_ir_assignment_event FOREIGN KEY(event_id) REFERENCES dbo.internal_request_events(event_id),
  CONSTRAINT FK_ir_assignment_recipient FOREIGN KEY(recipient_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_ir_assignment_assignment FOREIGN KEY(assignment_id) REFERENCES dbo.internal_request_assignments(assignment_id),
  CONSTRAINT FK_ir_assignment_notification FOREIGN KEY(notification_id) REFERENCES dbo.notifications(notification_id)
 );
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.InternalRequestAssignment.V1',@value=N'1';
END;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestAssignment.V1' AND CONVERT(nvarchar(20),value)='1')
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.internal_request_assignment_outbox')
  AND name IN('FK_ir_assignment_event','FK_ir_assignment_recipient','FK_ir_assignment_assignment','FK_ir_assignment_notification')
  AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>4
 THROW 51895,'Assignment outbox incompatible.',1;
COMMIT;

-- Original InternalRequestCompletionIsolated.sql SHA256 42E05B3F4BB72816DAE91C30DC9BB3210760FBC635C53F2FC3456C31436C5C90
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestAssignment.V1' AND CONVERT(nvarchar(20),value)='1')
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.internal_requests') AND name='row_version' AND system_type_id=189 AND is_nullable=0)
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.internal_request_assignments') AND name='UX_internal_request_assignments_active'
  AND is_unique=1 AND is_disabled=0 AND filter_definition=N'([status]=''ACTIVE'')')
 THROW 51895,'Verified assignment and request concurrency baseline required.',1;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestCompletion.V1')
BEGIN
 IF OBJECT_ID('dbo.internal_request_closures') IS NOT NULL OR OBJECT_ID('dbo.internal_request_completion_outbox') IS NOT NULL OR OBJECT_ID('dbo.internal_request_escalations') IS NOT NULL OR OBJECT_ID('dbo.internal_request_material_links') IS NOT NULL
  THROW 51910,'Unexpected completion storage; preserve for review.',1;
 CREATE TABLE dbo.internal_request_closures(
  event_id bigint NOT NULL CONSTRAINT PK_ir_closures PRIMARY KEY,
  internal_request_id bigint NOT NULL,assignment_id bigint NOT NULL,audit_id bigint NOT NULL,
  result_note nvarchar(4000) NOT NULL,closed_at datetime2(7) NOT NULL,
  CONSTRAINT FK_ir_closure_event FOREIGN KEY(event_id) REFERENCES dbo.internal_request_events(event_id),
  CONSTRAINT FK_ir_closure_request FOREIGN KEY(internal_request_id) REFERENCES dbo.internal_requests(internal_request_id),
  CONSTRAINT FK_ir_closure_assignment FOREIGN KEY(assignment_id) REFERENCES dbo.internal_request_assignments(assignment_id),
  CONSTRAINT FK_ir_closure_audit FOREIGN KEY(audit_id) REFERENCES dbo.audit_logs(audit_id),
  CONSTRAINT CK_ir_closure_result CHECK(LEN(LTRIM(RTRIM(result_note)))>0));
 CREATE TABLE dbo.internal_request_completion_outbox(
  event_id bigint NOT NULL CONSTRAINT PK_ir_completion_outbox PRIMARY KEY,
  requester_id bigint NOT NULL,created_at datetime2(7) NOT NULL,notification_id bigint NULL,
  CONSTRAINT FK_ir_completion_event FOREIGN KEY(event_id) REFERENCES dbo.internal_request_closures(event_id),
  CONSTRAINT FK_ir_completion_requester FOREIGN KEY(requester_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_ir_completion_notification FOREIGN KEY(notification_id) REFERENCES dbo.notifications(notification_id));
 CREATE TABLE dbo.internal_request_escalations(
  escalation_id bigint IDENTITY NOT NULL CONSTRAINT PK_ir_escalations PRIMARY KEY,
  internal_request_id bigint NOT NULL,opened_event_id bigint NOT NULL,
  state varchar(10) NOT NULL,opened_at datetime2(7) NOT NULL,closed_at datetime2(7) NULL,closed_event_id bigint NULL,
  CONSTRAINT FK_ir_escalation_request FOREIGN KEY(internal_request_id) REFERENCES dbo.internal_requests(internal_request_id),
  CONSTRAINT FK_ir_escalation_open FOREIGN KEY(opened_event_id) REFERENCES dbo.internal_request_events(event_id),
  CONSTRAINT FK_ir_escalation_close FOREIGN KEY(closed_event_id) REFERENCES dbo.internal_request_closures(event_id),
  CONSTRAINT CK_ir_escalation_state CHECK((state='OPEN' AND closed_at IS NULL AND closed_event_id IS NULL) OR
   (state='CLOSED' AND closed_at IS NOT NULL AND closed_at>=opened_at AND closed_event_id IS NOT NULL)));
 CREATE UNIQUE INDEX UX_ir_escalation_open ON dbo.internal_request_escalations(internal_request_id) WHERE state='OPEN';
 CREATE TABLE dbo.internal_request_material_links(
  internal_request_id bigint NOT NULL,material_request_id bigint NOT NULL,is_mandatory bit NOT NULL,audit_id bigint NOT NULL,
  CONSTRAINT PK_ir_material_links PRIMARY KEY(internal_request_id,material_request_id),
  CONSTRAINT FK_ir_material_request FOREIGN KEY(internal_request_id) REFERENCES dbo.internal_requests(internal_request_id),
  CONSTRAINT FK_ir_material_target FOREIGN KEY(material_request_id) REFERENCES dbo.material_requests(request_id),
  CONSTRAINT FK_ir_material_audit FOREIGN KEY(audit_id) REFERENCES dbo.audit_logs(audit_id));
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.InternalRequestCompletion.V1',@value=N'1';
END;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(OBJECT_ID('dbo.internal_request_closures'),OBJECT_ID('dbo.internal_request_completion_outbox'),OBJECT_ID('dbo.internal_request_escalations'),OBJECT_ID('dbo.internal_request_material_links')) AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>13
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.internal_request_escalations') AND name='UX_ir_escalation_open' AND is_unique=1 AND is_disabled=0 AND filter_definition=N'([state]=''OPEN'')')
 OR COL_LENGTH('dbo.internal_request_closures','result_note')<>8000
 OR (SELECT COUNT(*) FROM sys.check_constraints WHERE name IN('CK_ir_closure_result','CK_ir_escalation_state') AND is_disabled=0 AND is_not_trusted=0)<>2
 THROW 51910,'Completion storage incompatible.',1;
COMMIT;
 IF @@TRANCOUNT<>1 THROW 51930,'Unexpected transaction nesting.',1;
 IF @Apply=1 COMMIT; ELSE ROLLBACK;
 SELECT @Apply AS Applied;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
