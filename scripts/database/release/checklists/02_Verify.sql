IF COL_LENGTH(N'dbo.inspection_checklists',N'branch_id')<>8 OR COL_LENGTH(N'dbo.inspection_checklist_items',N'criterion_group')<>200
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.inspection_checklists') AND name=N'row_version' AND system_type_id=189 AND is_nullable=0)
 OR (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.inspection_checklist_room_types'))<>2
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN(N'FK_checklists_branch_v1',N'FK_checklist_types_checklist_v1',N'FK_checklist_types_type_v1') AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>3
 OR (SELECT COUNT(*) FROM sys.indexes WHERE name IN(N'IX_checklists_branch_v1',N'IX_checklist_types_reverse_v1',N'PK_checklist_room_types_v1') AND is_disabled=0)<>3
 OR (SELECT COUNT(*) FROM sys.triggers WHERE name IN(N'TR_checklists_scope_v1',N'TR_checklist_types_scope_v1',N'TR_checklist_items_immutable_v1',N'TR_checklist_versions_v1',N'TR_inspection_checklist_selection_v1',N'TR_room_types_checklist_v1') AND is_disabled=0)<>6
 THROW 51720,'Checklist schema is incompatible.',1;
IF EXISTS(SELECT 1 FROM dbo.inspection_checklist_room_types a JOIN dbo.inspection_checklists c ON c.checklist_id=a.checklist_id
 JOIN dbo.room_types t ON t.room_type_id=a.room_type_id WHERE c.branch_id IS NULL OR t.branch_id IS NULL OR c.branch_id<>t.branch_id)
 THROW 51720,'Checklist applicability mismatch.',1;
