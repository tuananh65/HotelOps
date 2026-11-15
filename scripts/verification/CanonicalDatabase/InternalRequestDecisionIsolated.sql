SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51894,'Exact owned disposable database required.',1;
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
