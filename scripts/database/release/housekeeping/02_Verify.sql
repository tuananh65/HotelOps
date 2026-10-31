IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.Checklists.V1')
 OR (SELECT COUNT(*) FROM sys.columns WHERE (object_id=OBJECT_ID(N'dbo.cleaning_tasks') AND name IN('workflow_status','row_version'))
 OR (object_id=OBJECT_ID(N'dbo.room_inspections') AND name IN('workflow_status','row_version','due_at'))
 OR (object_id=OBJECT_ID(N'dbo.cleaning_reworks') AND name IN('workflow_status','row_version','due_at'))
 OR (object_id=OBJECT_ID(N'dbo.inspection_defects') AND name='room_location'))<>9
 OR (SELECT COUNT(*) FROM sys.columns WHERE object_id IN(OBJECT_ID(N'dbo.cleaning_tasks'),OBJECT_ID(N'dbo.room_inspections'),OBJECT_ID(N'dbo.cleaning_reworks')) AND name='row_version' AND system_type_id=189 AND is_nullable=0)<>3
 OR (SELECT COUNT(*) FROM sys.columns WHERE object_id IN(OBJECT_ID(N'dbo.cleaning_tasks'),OBJECT_ID(N'dbo.room_inspections'),OBJECT_ID(N'dbo.cleaning_reworks')) AND name='workflow_status' AND system_type_id=167 AND max_length=30 AND is_nullable=1)<>3
 OR (SELECT COUNT(*) FROM sys.columns WHERE object_id IN(OBJECT_ID(N'dbo.room_inspections'),OBJECT_ID(N'dbo.cleaning_reworks')) AND name='due_at' AND system_type_id=42 AND scale=7 AND is_nullable=1)<>2
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.inspection_defects') AND name='room_location' AND system_type_id=231 AND max_length=500 AND is_nullable=1)
 OR (SELECT COUNT(*) FROM sys.indexes WHERE name IN('PK_rework_defects_v1','PK_defect_attachments_v1','PK_result_attachments_v1','IX_rework_defects_reverse_v1','IX_results_version_lookup_v1') AND is_disabled=0)<>5
 OR (SELECT COUNT(*) FROM sys.check_constraints WHERE name IN('CK_cleaning_workflow_v1','CK_inspection_workflow_v1','CK_rework_workflow_v1') AND is_disabled=0 AND is_not_trusted=0)<>3
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN('FK_rework_defects_rework_v1','FK_rework_defects_defect_v1','FK_defect_attachment_defect_v1','FK_defect_attachment_file_v1','FK_result_attachment_result_v1','FK_result_attachment_file_v1') AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>6
 OR (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_hk_inspections_v1','TR_hk_results_v1','TR_hk_defects_v1','TR_hk_rework_links_v1','TR_hk_defect_files_v1','TR_hk_result_files_v1','TR_hk_used_criteria_v1','TR_hk_used_version_v1') AND is_disabled=0)<>8
 THROW 51730,'Housekeeping V1 schema incompatible.',1;
