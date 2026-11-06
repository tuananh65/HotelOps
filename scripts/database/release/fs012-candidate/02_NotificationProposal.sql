-- Development-only exact-target interlock. Shared/production deployment is not enabled.
IF DB_NAME()<>N'HotelOps_Canonical_20261106_f120001_Fresh' OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME() OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'')<>N'SQLEXPRESS' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS012.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_FS012')
 THROW 51899,'FS012 isolated target/owner required.',1;
-- FS012 development proposal; NOT accepted A01/A02. No runtime publisher is activated by these tables.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.notification_operation_reservations') IS NOT NULL
BEGIN
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS012.NotificationProposal')
  THROW 51890,'Existing notification package requires reconciliation, not overwrite.',1;
 IF OBJECT_ID('dbo.notification_publications') IS NULL OR OBJECT_ID('dbo.notification_delivery_intents') IS NULL
  THROW 51891,'Partial notification package.',1;
 COMMIT; RETURN;
END;
IF OBJECT_ID('dbo.notification_publications') IS NOT NULL OR OBJECT_ID('dbo.notification_delivery_intents') IS NOT NULL
 THROW 51891,'Partial notification package.',1;
CREATE TABLE dbo.notification_operation_reservations(
 actor_user_id bigint NOT NULL REFERENCES dbo.users(user_id),operation_key uniqueidentifier NOT NULL,
 fingerprint_version int NOT NULL CHECK(fingerprint_version=1),fingerprint binary(32) NOT NULL,
 completed bit NOT NULL DEFAULT 0,result_json nvarchar(max) NULL,
 created_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),row_version rowversion NOT NULL,
 CONSTRAINT PK_notification_operation_v1 PRIMARY KEY(actor_user_id,operation_key),
 CONSTRAINT CK_notification_result_v1 CHECK((completed=0 AND result_json IS NULL) OR(completed=1 AND result_json IS NOT NULL AND ISJSON(result_json)=1)));
CREATE TABLE dbo.notification_publications(
 publication_id uniqueidentifier NOT NULL PRIMARY KEY,
 actor_user_id bigint NOT NULL,operation_key uniqueidentifier NOT NULL,
 event_type varchar(60) NOT NULL CHECK(event_type='CLEANING_TASK_ASSIGNED'),schema_version int NOT NULL CHECK(schema_version=1),
 assignment_id bigint NOT NULL UNIQUE REFERENCES dbo.cleaning_assignments(assignment_id),
 source_revision binary(8) NOT NULL,payload_json nvarchar(max) NOT NULL CHECK(ISJSON(payload_json)=1),created_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_publication_operation_v1 FOREIGN KEY(actor_user_id,operation_key) REFERENCES dbo.notification_operation_reservations(actor_user_id,operation_key));
CREATE TABLE dbo.notification_delivery_intents(
 intent_id bigint IDENTITY PRIMARY KEY,publication_id uniqueidentifier NOT NULL REFERENCES dbo.notification_publications(publication_id),
 user_id bigint NOT NULL REFERENCES dbo.users(user_id),delivery_channel varchar(30) NOT NULL CHECK(delivery_channel='IN_APP'),
 status varchar(30) NOT NULL DEFAULT 'PENDING' CHECK(status IN('PENDING','DELIVERED','TERMINAL')),
 attempt_count int NOT NULL DEFAULT 0 CHECK(attempt_count>=0),next_attempt_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 claim_id uniqueidentifier NULL,claimed_until datetime2(7) NULL,failure_code varchar(60) NULL,
 inbox_notification_id bigint NULL,row_version rowversion NOT NULL,
 CONSTRAINT UQ_delivery_publication_recipient_v1 UNIQUE(publication_id,user_id,delivery_channel),
 CONSTRAINT CK_delivery_inbox_v1 CHECK((status='DELIVERED' AND inbox_notification_id IS NOT NULL) OR(status<>'DELIVERED' AND inbox_notification_id IS NULL)),
 CONSTRAINT CK_delivery_claim_v1 CHECK((claim_id IS NULL AND claimed_until IS NULL) OR(claim_id IS NOT NULL AND claimed_until IS NOT NULL)),
 CONSTRAINT FK_delivery_inbox_v1 FOREIGN KEY(inbox_notification_id,user_id,delivery_channel) REFERENCES dbo.notification_recipients(notification_id,user_id,delivery_channel));
EXEC sys.sp_addextendedproperty @name=N'HotelOps.FS012.NotificationProposal',@value=N'FS012-PROPOSED-V1';
COMMIT;
