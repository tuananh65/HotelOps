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

IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.N01.Accept07Combined'
 AND CONVERT(nvarchar(128),value)=N'ACCEPT07-FS013-FS016-V1') THROW 51967,'Exact ACCEPT07 candidate required.',1;
BEGIN TRY
BEGIN TRANSACTION;
DECLARE @original nvarchar(4000)=CONVERT(nvarchar(4000),SESSION_CONTEXT(N'HotelOps.N01.OriginalTriggerDefinition'));
IF @original IS NULL OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
 AND name=N'HotelOps.N01.Accept07PreviousTriggerSha256'
 AND CONVERT(varbinary(32),value)=HASHBYTES('SHA2_256',CONVERT(varbinary(max),@original)))
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties e JOIN sys.sql_modules m ON m.object_id=OBJECT_ID('dbo.TR_fs009_publication_source')
 WHERE e.class=0 AND e.name=N'HotelOps.N01.Accept07TriggerSha256'
 AND CONVERT(varbinary(32),e.value)=HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition)))
 THROW 51967,'Exact captured original definition and unchanged installed trigger required.',1;
IF EXISTS(SELECT 1 FROM dbo.notification_publications WITH(UPDLOCK,HOLDLOCK) GROUP BY actor_user_id,operation_key HAVING COUNT(*)>1)
 THROW 51967,'Rollback cannot strand multi-event history.',1;
-- Restore the exact reviewed CREATE OR ALTER definition BEFORE removing its N01 columns.
EXEC sys.sp_executesql @original;
DROP INDEX UX_n01_operation_event_v1 ON dbo.notification_publications;
DROP INDEX UX_fs009_publication_operation ON dbo.notification_publications;
CREATE UNIQUE INDEX UX_fs009_publication_operation ON dbo.notification_publications(actor_user_id,operation_key);
IF EXISTS(SELECT 1 FROM dbo.notification_publications WITH(UPDLOCK,HOLDLOCK)
 WHERE event_type IN('inspection.failed','cleaning.rework.requested') OR inspection_id IS NOT NULL OR n01_payload_hash IS NOT NULL)
 OR EXISTS(SELECT 1 FROM dbo.notification_operation_reservations WITH(UPDLOCK,HOLDLOCK)
 WHERE n01_operation_hash IS NOT NULL OR n01_receipt_json IS NOT NULL)
 THROW 51960,'N01 records exist: preserve data, stop and request owner reconciliation; do not delete history.',1;
ALTER TABLE dbo.notification_publications DROP CONSTRAINT FK_n01_publication_inspection,CK_n01_publication_event,CK_n01_publication_source;
ALTER TABLE dbo.notification_publications DROP COLUMN inspection_id,n01_payload_hash;
ALTER TABLE dbo.notification_operation_reservations DROP CONSTRAINT CK_n01_operation_metadata;
ALTER TABLE dbo.notification_operation_reservations DROP COLUMN n01_operation_hash,n01_receipt_json;
ALTER TABLE dbo.notification_publications WITH CHECK ADD
 CONSTRAINT CK_fs016_publication_event CHECK(event_type IN('CLEANING_TASK_ASSIGNED','CLEANING_BLOCKER_REPORTED','CLEANING_TASK_COMPLETED','REWORK_SUBMITTED')),
 CONSTRAINT CK_fs016_publication_source CHECK(
   (event_type='REWORK_SUBMITTED' AND rework_id IS NOT NULL AND assignment_id IS NULL) OR
   (event_type<>'REWORK_SUBMITTED' AND rework_id IS NULL AND assignment_id IS NOT NULL));
EXEC sys.sp_dropextendedproperty @name=N'HotelOps.N01.SingleGraphCandidate';

EXEC sys.sp_dropextendedproperty @name=N'HotelOps.N01.Accept07PreviousTriggerSha256';
EXEC sys.sp_dropextendedproperty @name=N'HotelOps.N01.Accept07TriggerSha256';
EXEC sys.sp_dropextendedproperty @name=N'HotelOps.N01.Accept07Combined';
COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
-- Any N01 metadata/history refuses rollback; no delete, reset or receipt overwrite.
