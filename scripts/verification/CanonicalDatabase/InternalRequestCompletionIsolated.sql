SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51894,'Exact owned disposable database required.',1;
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
