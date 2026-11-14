-- REVIEW-ONLY, NOT EXECUTED. Extends the existing FS016/FS013 graph. No new graph.
-- Schema owner + runtime owner must approve a central exclusive-lease window first.
SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'KIENPC'
 OR DB_NAME()<>N'HotelOps_DEV2_FullStack_Trial'
 OR CONVERT(nvarchar(128),SESSION_CONTEXT(N'HotelOps.N01.ApprovedTrial'))<>N'N01-IMPLEMENT06-APPROVED-ISOLATED-SCHEMA'
 OR SESSION_CONTEXT(N'HotelOps.N01.ApprovedTrial') IS NULL
 THROW 51960,'Exact isolated target and owner-authorized session required; no canonical DDL.',1;
IF @@TRANCOUNT<>0 THROW 51960,'Use a dedicated schema owner session, not a domain transaction.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.IntegrationTrial'
 AND CONVERT(nvarchar(128),value)=N'DEV2-INTEGRATION-02-SQL-PROVISIONAL')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS013.NotificationProposal')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS016.Rework')
 THROW 51960,'Owner-provisioned existing FS013/FS016 trial graph required. This script does not bootstrap another graph.',1;
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.N01.SingleGraphCandidate')
 OR COL_LENGTH('dbo.notification_publications','inspection_id') IS NOT NULL
 OR COL_LENGTH('dbo.notification_publications','n01_payload_hash') IS NOT NULL
 OR COL_LENGTH('dbo.notification_operation_reservations','n01_operation_hash') IS NOT NULL
 OR COL_LENGTH('dbo.notification_operation_reservations','n01_receipt_json') IS NOT NULL
 THROW 51960,'No overwrite: inspect existing candidate package.',1;
IF (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.notification_publications')
 AND name IN('CK_fs016_publication_event','CK_fs016_publication_source') AND is_disabled=0 AND is_not_trusted=0)<>2
 OR ISNULL(COL_LENGTH('dbo.notification_publications','rework_id'),-1)<>8
 OR ISNULL(COL_LENGTH('dbo.notification_publications','occurrence_key'),-1)<>120
 OR (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_publications')
 AND name='UX_fs015_occurrence' AND is_unique=1 AND is_disabled=0)<>1
 OR (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_operation_reservations')
 AND name='PK_notification_operation_v1' AND is_unique=1 AND is_disabled=0)<>1
 OR (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_delivery_intents')
 AND name='UQ_delivery_publication_recipient_v1' AND is_unique=1 AND is_disabled=0)<>1
 THROW 51960,'Exact existing source/uniqueness constraints required.',1;
ALTER TABLE dbo.notification_publications DROP CONSTRAINT CK_fs016_publication_event,CK_fs016_publication_source;
ALTER TABLE dbo.notification_publications ADD inspection_id bigint NULL,
 n01_payload_hash binary(32) NULL;
-- Preserve DEV1's fingerprint/result_json/completed protocol, including FE04 authority receipts.
ALTER TABLE dbo.notification_operation_reservations ADD n01_operation_hash binary(32) NULL,n01_receipt_json nvarchar(max) NULL;
EXEC(N'ALTER TABLE dbo.notification_operation_reservations WITH CHECK ADD CONSTRAINT CK_n01_operation_metadata CHECK(
 (n01_operation_hash IS NULL AND n01_receipt_json IS NULL) OR
 (n01_operation_hash IS NOT NULL AND (n01_receipt_json IS NULL OR ISJSON(n01_receipt_json)=1)));');
EXEC(N'ALTER TABLE dbo.notification_publications WITH CHECK ADD
 CONSTRAINT FK_n01_publication_inspection FOREIGN KEY(inspection_id) REFERENCES dbo.room_inspections(inspection_id),
 CONSTRAINT CK_n01_publication_event CHECK(event_type IN(''CLEANING_TASK_ASSIGNED'',''CLEANING_BLOCKER_REPORTED'',
   ''CLEANING_TASK_COMPLETED'',''REWORK_SUBMITTED'',''inspection.failed'',''cleaning.rework.requested'')),
 CONSTRAINT CK_n01_publication_source CHECK(
   (event_type=''inspection.failed'' AND inspection_id IS NOT NULL AND assignment_id IS NULL AND rework_id IS NULL AND n01_payload_hash IS NOT NULL) OR
   (event_type=''cleaning.rework.requested'' AND inspection_id IS NOT NULL AND assignment_id IS NULL AND rework_id IS NOT NULL AND n01_payload_hash IS NOT NULL) OR
   (event_type=''REWORK_SUBMITTED'' AND inspection_id IS NULL AND assignment_id IS NULL AND rework_id IS NOT NULL AND n01_payload_hash IS NULL) OR
   (event_type IN(''CLEANING_TASK_ASSIGNED'',''CLEANING_BLOCKER_REPORTED'',''CLEANING_TASK_COMPLETED'')
      AND inspection_id IS NULL AND assignment_id IS NOT NULL AND rework_id IS NULL AND n01_payload_hash IS NULL));');
EXEC sys.sp_addextendedproperty @name=N'HotelOps.N01.SingleGraphCandidate',@value=N'IMPLEMENT06-FS016-V1';
COMMIT;
-- Existing records are retained byte-for-byte; WITH CHECK validates backward-compatible arms.
-- No grants, issuers, roles, worker switches, automatic migrations or producer activation.
