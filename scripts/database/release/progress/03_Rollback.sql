IF EXISTS(SELECT 1 FROM dbo.cleaning_item_definitions) OR EXISTS(SELECT 1 FROM dbo.cleaning_progress_versions)
 OR EXISTS(SELECT 1 FROM dbo.task_progress_versions) OR EXISTS(SELECT 1 FROM dbo.progress_evidence_links)
 THROW 51740,'Populated progress module requires forward recovery; rollback will not discard business data.',1;
DROP TRIGGER dbo.TR_progress_cleaning_parent_v1;
DROP TRIGGER dbo.TR_progress_task_parent_v1;
DROP TRIGGER dbo.TR_progress_cleaning_owner_v1;
DROP TRIGGER dbo.TR_progress_task_owner_v1;
DROP TABLE dbo.progress_evidence_links;
DROP TABLE dbo.task_completion_criteria;
DROP TABLE dbo.task_progress_versions;
DROP TABLE dbo.cleaning_task_items;
DROP TABLE dbo.cleaning_progress_versions;
DROP TABLE dbo.cleaning_item_definitions;
DROP INDEX IX_cleaning_progress_log_order_v1 ON dbo.cleaning_progress_logs;
DROP INDEX IX_task_progress_log_order_v1 ON dbo.operational_task_progress;
EXEC sys.sp_dropextendedproperty @name=N'HotelOps.Progress.V1';
