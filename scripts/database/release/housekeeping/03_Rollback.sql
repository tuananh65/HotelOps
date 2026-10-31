IF EXISTS(SELECT 1 FROM dbo.cleaning_tasks WHERE workflow_status IS NOT NULL) OR EXISTS(SELECT 1 FROM dbo.room_inspections WHERE workflow_status IS NOT NULL)
 OR EXISTS(SELECT 1 FROM dbo.cleaning_reworks WHERE workflow_status IS NOT NULL) OR EXISTS(SELECT 1 FROM dbo.inspection_defects WHERE room_location IS NOT NULL)
 OR EXISTS(SELECT 1 FROM dbo.cleaning_rework_defects) OR EXISTS(SELECT 1 FROM dbo.inspection_defect_attachments) OR EXISTS(SELECT 1 FROM dbo.inspection_result_attachments)
 THROW 51730,'Owned data exists; rollback refused. Use reviewed forward recovery.',1;
DROP TRIGGER dbo.TR_hk_inspections_v1;
DROP TRIGGER dbo.TR_hk_used_criteria_v1;
DROP TRIGGER dbo.TR_hk_used_version_v1;
DROP TRIGGER dbo.TR_hk_results_v1;
DROP TRIGGER dbo.TR_hk_defects_v1;
DROP TRIGGER dbo.TR_hk_rework_links_v1;
DROP TRIGGER dbo.TR_hk_defect_files_v1;
DROP TRIGGER dbo.TR_hk_result_files_v1;
DROP TABLE dbo.cleaning_rework_defects;
DROP TABLE dbo.inspection_defect_attachments;
DROP TABLE dbo.inspection_result_attachments;
DROP INDEX IX_results_version_lookup_v1 ON dbo.inspection_results;
ALTER TABLE dbo.cleaning_tasks DROP CONSTRAINT CK_cleaning_workflow_v1;
ALTER TABLE dbo.room_inspections DROP CONSTRAINT CK_inspection_workflow_v1;
ALTER TABLE dbo.cleaning_reworks DROP CONSTRAINT CK_rework_workflow_v1;
ALTER TABLE dbo.cleaning_tasks DROP COLUMN workflow_status,row_version;
ALTER TABLE dbo.room_inspections DROP COLUMN workflow_status,due_at,row_version;
ALTER TABLE dbo.cleaning_reworks DROP COLUMN workflow_status,due_at,row_version;
ALTER TABLE dbo.inspection_defects DROP COLUMN room_location;
EXEC sys.sp_dropextendedproperty @name=N'HotelOps.Housekeeping.V1';
