-- REVIEW-ONLY. NOT EXECUTED. Existing FS013 + owner-reviewed N01 candidate must already exist.
SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'KIENPC' OR DB_NAME()<>N'HotelOps_DEV2_FullStack_Trial'
 OR ISNULL(CONVERT(nvarchar(128),SESSION_CONTEXT(N'HotelOps.FE04.ApprovedTrial')),N'')<>N'FE04-P0-APPROVED-SCHEMA'
 OR @@TRANCOUNT<>0 THROW 51962,'Dedicated owner-approved isolated schema session required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.IntegrationTrial'
 AND CONVERT(nvarchar(128),value)=N'DEV2-INTEGRATION-02-SQL-PROVISIONAL')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.N01.SingleGraphCandidate'
 AND CONVERT(nvarchar(128),value)=N'IMPLEMENT06-FS016-V1')
 THROW 51962,'Existing owner-provisioned single FS013 graph and N01 candidate required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.sql_modules WHERE object_id=OBJECT_ID('dbo.TR_fs009_publication_source'))
 OR SESSION_CONTEXT(N'HotelOps.FE04.PreviousTriggerSha256') IS NULL OR
 CONVERT(varbinary(32),SESSION_CONTEXT(N'HotelOps.FE04.PreviousTriggerSha256'))<>
 (SELECT HASHBYTES('SHA2_256',CONVERT(varbinary(max),definition)) FROM sys.sql_modules WHERE object_id=OBJECT_ID('dbo.TR_fs009_publication_source'))
 THROW 51962,'Owner must capture/review exact prior trigger definition/hash before replacement.',1;
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE04.NotificationReconciliation')
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_publications')
 AND name='UX_fs009_publication_operation' AND is_unique=1 AND is_disabled=0 AND has_filter=0)
 THROW 51962,'Exact original unfiltered index required; no overwrite.',1;
-- Keep the index name required by DEV1 AvailableAsync, narrow only to its UC029 family.
DROP INDEX UX_fs009_publication_operation ON dbo.notification_publications;
CREATE UNIQUE INDEX UX_fs009_publication_operation ON dbo.notification_publications(actor_user_id,operation_key)
 WHERE event_type='CLEANING_TASK_ASSIGNED';
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
EXEC sys.sp_addextendedproperty @name=N'HotelOps.FE04.NotificationReconciliation',@value=N'FE04-P0-FS013-N01-CANDIDATE-V1';
COMMIT;
-- No new graph/queue/table, no new event key, no grant/issuer/worker or startup migration.
-- N01 metadata, receipt ownership and old event source checks retained. Other candidate families
-- (e.g. FS016 REWORK_SUBMITTED) still require their separate owner source-trigger reconciliation.
