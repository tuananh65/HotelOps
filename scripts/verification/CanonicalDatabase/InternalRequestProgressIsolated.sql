SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51896,'Exact owned disposable database required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestDecision.V1' AND CONVERT(nvarchar(20),value)='1')
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.internal_requests') AND name='row_version' AND system_type_id=189 AND is_nullable=0)
 THROW 51896,'Verified request concurrency baseline required.',1;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestProgress.V1')
BEGIN
 IF OBJECT_ID('dbo.internal_request_progress') IS NOT NULL OR OBJECT_ID('dbo.internal_request_progress_outbox') IS NOT NULL
  THROW 51896,'Unexpected pre-existing progress schema; preserve and review.',1;
 -- One current progress snapshot. Request row_version remains the sole concurrency token.
 CREATE TABLE dbo.internal_request_progress(
  internal_request_id bigint NOT NULL CONSTRAINT PK_internal_request_progress PRIMARY KEY,
  progress_percent int NOT NULL,
  note nvarchar(4000) NULL,
  result_note nvarchar(4000) NULL,
  event_id bigint NOT NULL,
  CONSTRAINT CK_ir_progress_percent CHECK(progress_percent BETWEEN 0 AND 100),
  CONSTRAINT FK_ir_progress_request FOREIGN KEY(internal_request_id) REFERENCES dbo.internal_requests(internal_request_id),
  CONSTRAINT FK_ir_progress_event FOREIGN KEY(event_id) REFERENCES dbo.internal_request_events(event_id)
 );
 CREATE TABLE dbo.internal_request_progress_outbox(
  event_id bigint NOT NULL CONSTRAINT PK_internal_request_progress_outbox PRIMARY KEY,
  requester_id bigint NOT NULL,
  created_at datetime2(7) NOT NULL,
  notification_id bigint NULL,
  CONSTRAINT FK_ir_progress_notice_event FOREIGN KEY(event_id) REFERENCES dbo.internal_request_events(event_id),
  CONSTRAINT FK_ir_progress_notice_requester FOREIGN KEY(requester_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_ir_progress_notice_notification FOREIGN KEY(notification_id) REFERENCES dbo.notifications(notification_id)
 );
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.InternalRequestProgress.V1',@value=N'1';
END;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestProgress.V1' AND CONVERT(nvarchar(20),value)='1')
 OR ISNULL(COL_LENGTH('dbo.internal_request_progress','progress_percent'),0)<>4
 OR ISNULL(COL_LENGTH('dbo.internal_request_progress','note'),0)<>8000
 OR ISNULL(COL_LENGTH('dbo.internal_request_progress','result_note'),0)<>8000
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN('FK_ir_progress_request','FK_ir_progress_event','FK_ir_progress_notice_event','FK_ir_progress_notice_requester','FK_ir_progress_notice_notification')
   AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>5
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.internal_request_progress') AND name='CK_ir_progress_percent' AND is_disabled=0 AND is_not_trusted=0)
 THROW 51896,'Progress schema incompatible.',1;
COMMIT;
