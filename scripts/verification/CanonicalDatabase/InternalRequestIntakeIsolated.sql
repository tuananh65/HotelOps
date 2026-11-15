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
 AND name='description' AND system_type_id=231 AND (max_length=-1 OR max_length>=8000))
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.internal_requests')
 AND name='subject' AND system_type_id=231 AND max_length>=400)
 THROW 51896,'Intake storage must support subject 200 and description 4000 characters.',1;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestIntake.V1')
BEGIN
 IF OBJECT_ID('dbo.internal_request_type_policy') IS NOT NULL OR OBJECT_ID('dbo.internal_request_intake_outbox') IS NOT NULL THROW 51896,'Unexpected intake outbox; preserve and review.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.attachments')
   AND name='CK_attachments_entity_type' AND is_disabled=0 AND is_not_trusted=0
   AND definition=N'([entity_type]=''INTERNAL_REQUEST_PROGRESS'' OR [entity_type]=''ROOM'' OR [entity_type]=''CLEANING_TASK'' OR [entity_type]=''INSPECTION'' OR [entity_type]=''INCIDENT'' OR [entity_type]=''MAINTENANCE'' OR [entity_type]=''ASSET'' OR [entity_type]=''STOCK_DOCUMENT'' OR [entity_type]=''REQUEST'' OR [entity_type]=''TASK'')')
  THROW 51898,'Verified request evidence baseline required; preserve unknown discriminator definitions.',1;
 ALTER TABLE dbo.attachments DROP CONSTRAINT CK_attachments_entity_type;
 ALTER TABLE dbo.attachments WITH CHECK ADD CONSTRAINT CK_attachments_entity_type
  CHECK(entity_type IN('TASK','REQUEST','STOCK_DOCUMENT','ASSET','MAINTENANCE','INCIDENT','INSPECTION','CLEANING_TASK','ROOM','INTERNAL_REQUEST_PROGRESS','INTERNAL_REQUEST_INTAKE'));
 -- No existing type is classified automatically; approved values must be supplied separately.
 CREATE TABLE dbo.internal_request_type_policy(
  request_type_id bigint NOT NULL CONSTRAINT PK_internal_request_type_policy PRIMARY KEY,
  is_material_need bit NOT NULL,
  CONSTRAINT FK_ir_intake_type FOREIGN KEY(request_type_id) REFERENCES dbo.internal_request_types(request_type_id)
 );
 CREATE TABLE dbo.internal_request_intake_outbox(
  event_id bigint NOT NULL,recipient_id bigint NOT NULL,branch_id bigint NOT NULL,
  department_id bigint NOT NULL,
  created_at datetime2(7) NOT NULL,notification_id bigint NULL,
  CONSTRAINT PK_internal_request_intake_outbox PRIMARY KEY(event_id,recipient_id),
  CONSTRAINT FK_ir_intake_event FOREIGN KEY(event_id) REFERENCES dbo.internal_request_events(event_id),
  CONSTRAINT FK_ir_intake_recipient FOREIGN KEY(recipient_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_ir_intake_department FOREIGN KEY(department_id,branch_id) REFERENCES dbo.departments(department_id,branch_id),
  CONSTRAINT FK_ir_intake_notification FOREIGN KEY(notification_id) REFERENCES dbo.notifications(notification_id)
 );
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.InternalRequestIntake.V1',@value=N'1';
END;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestIntake.V1' AND CONVERT(nvarchar(20),value)='1')
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.internal_request_intake_outbox')
  AND name IN('FK_ir_intake_event','FK_ir_intake_recipient','FK_ir_intake_department','FK_ir_intake_notification')
  AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>4
 THROW 51896,'Intake outbox incompatible.',1;
IF COL_LENGTH('dbo.internal_request_type_policy','is_material_need')<>1 OR NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_ir_intake_type' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)
 THROW 51896,'Intake type policy incompatible.',1;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.attachments')
 AND name='CK_attachments_entity_type' AND is_disabled=0 AND is_not_trusted=0 AND definition LIKE '%''INTERNAL_REQUEST_INTAKE''%')
 THROW 51898,'Intake attachment capability incompatible.',1;
COMMIT;
