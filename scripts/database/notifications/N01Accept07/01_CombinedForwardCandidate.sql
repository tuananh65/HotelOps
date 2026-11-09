-- REVIEW-ONLY / NOT EXECUTED. One atomic upgrade from original FS013+FS016 ONLY.
-- External schema/runtime owner approval, registered candidate-bound lane, exclusive central
-- lease, owned fixtures and preservation/cleanup evidence REQUIRED. Session markers are NOT approval.
-- Never stack Implement06 + FE04-P0 scripts. Existing candidate installations require owner reconciliation.
SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'KIENPC' OR DB_NAME()<>N'HotelOps_DEV2_FullStack_Trial'
 OR ISNULL(CONVERT(nvarchar(128),SESSION_CONTEXT(N'HotelOps.N01.Accept07Approved')),N'')<>N'ACCEPT07-ISOLATED-SCHEMA'
 OR @@TRANCOUNT<>0 THROW 51967,'Dedicated approved isolated owner session required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.IntegrationTrial'
 AND CONVERT(nvarchar(128),value)=N'DEV2-INTEGRATION-02-SQL-PROVISIONAL')
 THROW 51967,'Exact existing trial marker required.',1;
BEGIN TRY
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS013.NotificationProposal')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS016.Rework')
 OR EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name IN(N'HotelOps.FE04.NotificationReconciliation',N'HotelOps.N01.Accept07Combined'))
 THROW 51967,'Unmodified FS013+FS016 substrate required; no candidate stacking.',1;
DECLARE @priorHash varbinary(32)=(SELECT HASHBYTES('SHA2_256',CONVERT(varbinary(max),definition))
 FROM sys.sql_modules WHERE object_id=OBJECT_ID('dbo.TR_fs009_publication_source'));
IF @priorHash IS NULL OR SESSION_CONTEXT(N'HotelOps.N01.PreviousTriggerSha256') IS NULL
 OR CONVERT(varbinary(32),SESSION_CONTEXT(N'HotelOps.N01.PreviousTriggerSha256'))<>@priorHash
 OR NOT EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_fs009_publication_source') AND is_disabled=0)
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_publications')
 AND name='UX_fs009_publication_operation' AND is_unique=1 AND has_filter=0 AND is_disabled=0)
 THROW 51967,'Owner-reviewed original trigger hash and original index required.',1;
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
EXEC(N'DROP INDEX UX_fs009_publication_operation ON dbo.notification_publications;
CREATE UNIQUE INDEX UX_fs009_publication_operation ON dbo.notification_publications(actor_user_id,operation_key)
 WHERE event_type<>''inspection.failed'' AND event_type<>''cleaning.rework.requested'';
CREATE UNIQUE INDEX UX_n01_operation_event_v1 ON dbo.notification_publications(actor_user_id,operation_key,event_type)
 WHERE inspection_id IS NOT NULL;
');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_fs009_publication_source ON dbo.notification_publications AFTER INSERT AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted p WHERE p.event_type NOT IN(''inspection.failed'',''cleaning.rework.requested'') AND NOT EXISTS(
   SELECT 1 FROM dbo.cleaning_assignments a JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=a.cleaning_task_id
   WHERE a.assignment_id=p.assignment_id AND p.source_revision=t.row_version
     AND TRY_CONVERT(datetime2(7),JSON_VALUE(p.payload_json,''$.dueAtUtc''))=t.due_at))
   THROW 51915,''Publication transition revision/deadline does not match persisted assignment task'',1;
 IF EXISTS(SELECT 1 FROM inserted p WHERE p.event_type IN(''inspection.failed'',''cleaning.rework.requested'') AND NOT EXISTS(
   SELECT 1 FROM dbo.room_inspections i JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=i.cleaning_task_id AND t.room_id=i.room_id
   JOIN dbo.room_cleaning_cycle_state c ON c.room_id=i.room_id AND c.cleaning_task_id=t.cleaning_task_id
   WHERE i.inspection_id=p.inspection_id AND i.row_version=p.source_revision AND i.status=''COMPLETED'' AND i.result=''FAIL''
     AND i.workflow_status=''FAILED'' AND t.workflow_status=''REWORK_REQUIRED''
     AND (p.event_type=''inspection.failed'' AND p.rework_id IS NULL OR p.event_type=''cleaning.rework.requested'' AND EXISTS(
       SELECT 1 FROM dbo.cleaning_reworks w WHERE w.rework_id=p.rework_id AND w.inspection_id=i.inspection_id AND w.cleaning_task_id=t.cleaning_task_id))))
   THROW 51962,''N01 publication source binding invalid'',1;
END;');

EXEC sys.sp_addextendedproperty @name=N'HotelOps.N01.Accept07PreviousTriggerSha256',@value=@priorHash;
DECLARE @newHash varbinary(32)=(SELECT HASHBYTES('SHA2_256',CONVERT(varbinary(max),definition)) FROM sys.sql_modules WHERE object_id=OBJECT_ID('dbo.TR_fs009_publication_source'));
EXEC sys.sp_addextendedproperty @name=N'HotelOps.N01.Accept07TriggerSha256',@value=@newHash;
EXEC sys.sp_addextendedproperty @name=N'HotelOps.N01.Accept07Combined',@value=N'ACCEPT07-FS013-FS016-V1';
COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
-- No table/graph/grant/issuer/activation; legacy rows and domain receipts untouched.
-- Original non-N01 source-trigger arm retained verbatim semantically. REWORK_SUBMITTED
-- remains subject to its old assignment-only restriction, pending its separate owner correction.
