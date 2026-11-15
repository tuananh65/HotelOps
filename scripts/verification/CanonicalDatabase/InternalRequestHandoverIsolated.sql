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
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestHandover.V1')
BEGIN
 IF OBJECT_ID('dbo.internal_request_handovers') IS NOT NULL OR OBJECT_ID('dbo.internal_request_handover_outbox') IS NOT NULL
  THROW 51895,'Unexpected handover storage; preserve and review.',1;
 CREATE TABLE dbo.internal_request_handovers(
  handover_id bigint NOT NULL CONSTRAINT PK_internal_request_handovers PRIMARY KEY,
  request_id bigint NOT NULL,branch_id bigint NOT NULL,department_id bigint NOT NULL,
  sender_assignment_id bigint NOT NULL,receiver_id bigint NOT NULL,
  summary nvarchar(4000) NOT NULL,note nvarchar(4000) NULL,
  state varchar(40) NOT NULL,created_at datetime2(7) NOT NULL,
  decision_event_id bigint NULL,decision_at datetime2(7) NULL,rejection_reason nvarchar(4000) NULL,new_assignment_id bigint NULL,
  CONSTRAINT FK_ir_handover_event FOREIGN KEY(handover_id) REFERENCES dbo.internal_request_events(event_id),
  CONSTRAINT FK_ir_handover_request FOREIGN KEY(request_id) REFERENCES dbo.internal_requests(internal_request_id),
  CONSTRAINT FK_ir_handover_department FOREIGN KEY(department_id,branch_id) REFERENCES dbo.departments(department_id,branch_id),
  CONSTRAINT FK_ir_handover_sender FOREIGN KEY(sender_assignment_id) REFERENCES dbo.internal_request_assignments(assignment_id),
  CONSTRAINT FK_ir_handover_receiver FOREIGN KEY(receiver_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_ir_handover_decision FOREIGN KEY(decision_event_id) REFERENCES dbo.internal_request_events(event_id),
  CONSTRAINT FK_ir_handover_assignment FOREIGN KEY(new_assignment_id) REFERENCES dbo.internal_request_assignments(assignment_id),
  CONSTRAINT CK_ir_handover_state CHECK(
   (state='PENDING_ACKNOWLEDGMENT' AND decision_event_id IS NULL AND decision_at IS NULL AND rejection_reason IS NULL AND new_assignment_id IS NULL)
   OR (state='ACKNOWLEDGED' AND decision_event_id IS NOT NULL AND decision_at IS NOT NULL AND decision_at>=created_at AND rejection_reason IS NULL AND new_assignment_id IS NOT NULL)
   OR (state='REJECTED' AND decision_event_id IS NOT NULL AND decision_at IS NOT NULL AND decision_at>=created_at AND rejection_reason IS NOT NULL AND LEN(LTRIM(RTRIM(rejection_reason)))>0 AND new_assignment_id IS NULL)),
  CONSTRAINT CK_ir_handover_summary CHECK(LEN(LTRIM(RTRIM(summary)))>0)
 );
 CREATE UNIQUE INDEX UX_ir_handover_pending ON dbo.internal_request_handovers(request_id) WHERE state='PENDING_ACKNOWLEDGMENT';
 CREATE TABLE dbo.internal_request_handover_outbox(
  event_id bigint NOT NULL,recipient_id bigint NOT NULL,handover_id bigint NOT NULL,recipient_kind varchar(20) NOT NULL,
  created_at datetime2(7) NOT NULL,notification_id bigint NULL,
  CONSTRAINT PK_internal_request_handover_outbox PRIMARY KEY(event_id,recipient_id),
  CONSTRAINT FK_ir_handover_notice_event FOREIGN KEY(event_id) REFERENCES dbo.internal_request_events(event_id),
  CONSTRAINT FK_ir_handover_notice_recipient FOREIGN KEY(recipient_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_ir_handover_notice_handover FOREIGN KEY(handover_id) REFERENCES dbo.internal_request_handovers(handover_id),
  CONSTRAINT FK_ir_handover_notice_notification FOREIGN KEY(notification_id) REFERENCES dbo.notifications(notification_id),
  CONSTRAINT CK_ir_handover_recipient CHECK(recipient_kind IN('RECEIVER','SENDER','MANAGER'))
 );
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.InternalRequestHandover.V1',@value=N'1';
END;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestHandover.V1' AND CONVERT(nvarchar(20),value)='1')
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(OBJECT_ID('dbo.internal_request_handovers'),OBJECT_ID('dbo.internal_request_handover_outbox'))
  AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>11
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.internal_request_handovers') AND name='UX_ir_handover_pending' AND is_unique=1 AND is_disabled=0 AND filter_definition=N'([state]=''PENDING_ACKNOWLEDGMENT'')')
 OR (SELECT COUNT(*) FROM sys.check_constraints WHERE name IN('CK_ir_handover_state','CK_ir_handover_summary','CK_ir_handover_recipient') AND is_disabled=0 AND is_not_trusted=0)<>3
 THROW 51895,'Handover storage incompatible.',1;
COMMIT;
