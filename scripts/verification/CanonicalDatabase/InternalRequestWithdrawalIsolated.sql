SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51894,'Exact owned disposable database required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestDecision.V1' AND CONVERT(nvarchar(20),value)='1')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.DepartmentAuthority.V1' AND CONVERT(nvarchar(20),value)='1')
 THROW 51896,'Verified decision and authority baseline required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.internal_requests')
 AND name='cancellation_reason' AND system_type_id=231 AND (max_length=-1 OR max_length>=8000))
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.internal_request_events')
 AND name='event_note' AND system_type_id=231 AND (max_length=-1 OR max_length>=8000))
 THROW 51896,'Withdrawal reason storage must support the verified 4000-character contract.',1;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestWithdrawal.V1')
BEGIN
 IF OBJECT_ID('dbo.internal_request_withdrawal_outbox') IS NOT NULL THROW 51896,'Unexpected withdrawal outbox; preserve and review.',1;
 CREATE TABLE dbo.internal_request_withdrawal_outbox(
  event_id bigint NOT NULL,recipient_id bigint NOT NULL,branch_id bigint NOT NULL,
  department_id bigint NOT NULL,
  created_at datetime2(7) NOT NULL,notification_id bigint NULL,
  CONSTRAINT PK_internal_request_withdrawal_outbox PRIMARY KEY(event_id,recipient_id),
  CONSTRAINT FK_ir_withdrawal_event FOREIGN KEY(event_id) REFERENCES dbo.internal_request_events(event_id),
  CONSTRAINT FK_ir_withdrawal_recipient FOREIGN KEY(recipient_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_ir_withdrawal_department FOREIGN KEY(department_id,branch_id) REFERENCES dbo.departments(department_id,branch_id),
  CONSTRAINT FK_ir_withdrawal_notification FOREIGN KEY(notification_id) REFERENCES dbo.notifications(notification_id)
 );
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.InternalRequestWithdrawal.V1',@value=N'1';
END;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestWithdrawal.V1' AND CONVERT(nvarchar(20),value)='1')
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.internal_request_withdrawal_outbox')
  AND name IN('FK_ir_withdrawal_event','FK_ir_withdrawal_recipient','FK_ir_withdrawal_department','FK_ir_withdrawal_notification')
  AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>4
 THROW 51896,'Withdrawal outbox incompatible.',1;
COMMIT;
