SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51894,'Exact owned disposable database required.',1;
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