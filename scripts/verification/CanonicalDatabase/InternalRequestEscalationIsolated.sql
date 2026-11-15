SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51920,'Exact owned disposable database required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestCompletion.V1' AND CONVERT(nvarchar(20),value)='1')
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.internal_request_escalations') AND name='UX_ir_escalation_open' AND is_unique=1 AND is_disabled=0 AND filter_definition=N'([state]=''OPEN'')')
 THROW 51921,'Verified escalation baseline required.',1;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestEscalation.V1')
BEGIN
 IF OBJECT_ID('dbo.internal_request_escalation_actions') IS NOT NULL OR OBJECT_ID('dbo.internal_request_escalation_outbox') IS NOT NULL
  THROW 51922,'Unexpected escalation storage; preserve for review.',1;
 CREATE TABLE dbo.internal_request_escalation_actions(
  event_id bigint NOT NULL CONSTRAINT PK_ir_escalation_actions PRIMARY KEY,
  audit_id bigint NOT NULL,reason nvarchar(4000) NOT NULL,
  old_priority_id bigint NULL,new_priority_id bigint NULL,old_due_at datetime2(7) NULL,new_due_at datetime2(7) NULL,
  department_id bigint NULL,handler_id bigint NULL,
  CONSTRAINT FK_ir_escalation_action_event FOREIGN KEY(event_id) REFERENCES dbo.internal_request_events(event_id),
  CONSTRAINT FK_ir_escalation_action_audit FOREIGN KEY(audit_id) REFERENCES dbo.audit_logs(audit_id),
  CONSTRAINT CK_ir_escalation_reason CHECK(LEN(LTRIM(RTRIM(reason)))>=10));
 CREATE TABLE dbo.internal_request_escalation_outbox(
  event_id bigint NOT NULL,recipient_id bigint NOT NULL,
  is_requester bit NOT NULL,is_handler bit NOT NULL,is_manager bit NOT NULL,
  created_at datetime2(7) NOT NULL,notification_id bigint NULL,
  CONSTRAINT PK_ir_escalation_outbox PRIMARY KEY(event_id,recipient_id),
  CONSTRAINT FK_ir_escalation_outbox_action FOREIGN KEY(event_id) REFERENCES dbo.internal_request_escalation_actions(event_id),
  CONSTRAINT FK_ir_escalation_outbox_recipient FOREIGN KEY(recipient_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_ir_escalation_outbox_notification FOREIGN KEY(notification_id) REFERENCES dbo.notifications(notification_id),
  CONSTRAINT CK_ir_escalation_recipient CHECK(is_requester=1 OR is_handler=1 OR is_manager=1));
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.InternalRequestEscalation.V1',@value=N'1';
END;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(OBJECT_ID('dbo.internal_request_escalation_actions'),OBJECT_ID('dbo.internal_request_escalation_outbox')) AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>5
 OR COL_LENGTH('dbo.internal_request_escalation_actions','reason')<>8000
 OR (SELECT COUNT(*) FROM sys.indexes WHERE name IN('PK_ir_escalation_actions','PK_ir_escalation_outbox') AND is_primary_key=1 AND is_disabled=0)<>2
 OR (SELECT COUNT(*) FROM sys.check_constraints WHERE name IN('CK_ir_escalation_reason','CK_ir_escalation_recipient') AND is_disabled=0 AND is_not_trusted=0)<>2
 THROW 51922,'Escalation storage incompatible.',1;
COMMIT;
