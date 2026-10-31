IF EXISTS(SELECT 1 FROM dbo.inspection_checklists WHERE branch_id IS NOT NULL)
 OR EXISTS(SELECT 1 FROM dbo.inspection_checklist_room_types)
 OR EXISTS(SELECT 1 FROM dbo.inspection_checklist_items WHERE criterion_group IS NOT NULL)
 THROW 51720,'Rollback refused: V1 data exists; retain module and use reviewed forward recovery.',1;
DROP TRIGGER dbo.TR_room_types_checklist_v1;
DROP TRIGGER dbo.TR_inspection_checklist_selection_v1;
DROP TRIGGER dbo.TR_checklist_versions_v1;
DROP TRIGGER dbo.TR_checklist_items_immutable_v1;
DROP TRIGGER dbo.TR_checklist_types_scope_v1;
DROP TRIGGER dbo.TR_checklists_scope_v1;
DROP TABLE dbo.inspection_checklist_room_types;
DROP INDEX IX_checklists_branch_v1 ON dbo.inspection_checklists;
ALTER TABLE dbo.inspection_checklists DROP CONSTRAINT FK_checklists_branch_v1;
ALTER TABLE dbo.inspection_checklists DROP COLUMN branch_id;
ALTER TABLE dbo.inspection_checklists DROP COLUMN row_version;
ALTER TABLE dbo.inspection_checklist_items DROP COLUMN criterion_group;
EXEC sys.sp_dropextendedproperty @name=N'HotelOps.Checklists.V1';
