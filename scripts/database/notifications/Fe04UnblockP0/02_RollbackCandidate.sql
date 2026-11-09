-- REVIEW-ONLY. Stop producers first, central lease + preservation capture required.
SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'KIENPC' OR DB_NAME()<>N'HotelOps_DEV2_FullStack_Trial'
 OR ISNULL(CONVERT(nvarchar(128),SESSION_CONTEXT(N'HotelOps.FE04.ApprovedTrial')),N'')<>N'FE04-P0-APPROVED-SCHEMA'
 OR @@TRANCOUNT<>0 THROW 51962,'Exact owner-approved isolated schema session required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE04.NotificationReconciliation'
 AND CONVERT(nvarchar(128),value)=N'FE04-P0-FS013-N01-CANDIDATE-V1') THROW 51962,'Exact reconciliation marker required.',1;
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM dbo.notification_publications WITH(UPDLOCK,HOLDLOCK) WHERE event_type IN('inspection.failed','cleaning.rework.requested'))
 OR EXISTS(SELECT 1 FROM dbo.notification_publications WITH(UPDLOCK,HOLDLOCK) GROUP BY actor_user_id,operation_key HAVING COUNT(*)>1)
 THROW 51962,'Rollback would strand N01/multi-event history. Preserve data and request owner reconciliation.',1;
DROP INDEX UX_fs009_publication_operation ON dbo.notification_publications;
CREATE UNIQUE INDEX UX_fs009_publication_operation ON dbo.notification_publications(actor_user_id,operation_key);
-- Exact original bc0110e9 semantic body; owner must compare with captured pre-upgrade definition.
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_fs009_publication_source ON dbo.notification_publications AFTER INSERT AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted p WHERE NOT EXISTS(
 SELECT 1 FROM dbo.cleaning_assignments a JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=a.cleaning_task_id
 WHERE a.assignment_id=p.assignment_id AND p.source_revision=t.row_version
 AND TRY_CONVERT(datetime2(7),JSON_VALUE(p.payload_json,''$.dueAtUtc''))=t.due_at))
 THROW 51915,''Publication transition revision/deadline does not match persisted assignment task'',1;
END;');
EXEC sys.sp_dropextendedproperty @name=N'HotelOps.FE04.NotificationReconciliation';
COMMIT;
-- N01 columns/constraints/markers and domain result_json/completed are never removed here.
