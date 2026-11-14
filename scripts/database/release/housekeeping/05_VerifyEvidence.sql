IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.Housekeeping.EvidenceRevision.V1'
 AND LEN(CONVERT(nvarchar(128),value))=64)
 OR (SELECT COUNT(*) FROM sys.triggers WHERE
 (object_id=OBJECT_ID(N'dbo.TR_hk_defect_files_v1') AND parent_id=OBJECT_ID(N'dbo.inspection_defect_attachments')) OR
 (object_id=OBJECT_ID(N'dbo.TR_hk_result_files_v1') AND parent_id=OBJECT_ID(N'dbo.inspection_result_attachments')) OR
 (object_id=OBJECT_ID(N'dbo.TR_hk_attachment_revision_v1') AND parent_id=OBJECT_ID(N'dbo.attachments')))<>3
 OR EXISTS(SELECT 1 FROM sys.triggers WHERE object_id IN(OBJECT_ID(N'dbo.TR_hk_defect_files_v1'),OBJECT_ID(N'dbo.TR_hk_result_files_v1'),OBJECT_ID(N'dbo.TR_hk_attachment_revision_v1'))
 AND (is_disabled=1 OR is_not_for_replication=1))
 OR (SELECT COUNT(*) FROM sys.trigger_events WHERE object_id IN(OBJECT_ID(N'dbo.TR_hk_defect_files_v1'),OBJECT_ID(N'dbo.TR_hk_result_files_v1'),OBJECT_ID(N'dbo.TR_hk_attachment_revision_v1'))
 AND type_desc IN('INSERT','UPDATE','DELETE'))<>9
 THROW 51730,'Evidence revision amendment missing or incompatible.',1;
-- Exact package hash and all three OBJECT_DEFINITION values are verified by the runner.
