SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UserDirectory.Trial' AND CONVERT(nvarchar(128),value)=N'Task17')
 THROW 51730,'Guarded disposable trial required.',1;
IF COL_LENGTH(N'dbo.inspection_checklists',N'branch_id') IS NULL OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.Checklists.V1')
 THROW 51730,'Checklist V1 prerequisite required.',1;
IF COL_LENGTH(N'dbo.cleaning_tasks',N'workflow_status') IS NOT NULL OR COL_LENGTH(N'dbo.room_inspections',N'workflow_status') IS NOT NULL
 OR COL_LENGTH(N'dbo.cleaning_reworks',N'workflow_status') IS NOT NULL OR COL_LENGTH(N'dbo.inspection_defects',N'room_location') IS NOT NULL
 OR OBJECT_ID(N'dbo.cleaning_rework_defects') IS NOT NULL OR OBJECT_ID(N'dbo.inspection_defect_attachments') IS NOT NULL OR OBJECT_ID(N'dbo.inspection_result_attachments') IS NOT NULL
 THROW 51730,'Partial Housekeeping schema requires explicit recovery.',1;
ALTER TABLE dbo.cleaning_tasks ADD workflow_status varchar(30) NULL,row_version rowversion NOT NULL;
ALTER TABLE dbo.room_inspections ADD workflow_status varchar(30) NULL,due_at datetime2(7) NULL,row_version rowversion NOT NULL;
ALTER TABLE dbo.cleaning_reworks ADD workflow_status varchar(30) NULL,due_at datetime2(7) NULL,row_version rowversion NOT NULL;
ALTER TABLE dbo.inspection_defects ADD room_location nvarchar(250) NULL;
GO
ALTER TABLE dbo.cleaning_tasks WITH CHECK ADD CONSTRAINT CK_cleaning_workflow_v1 CHECK(workflow_status IS NULL OR
 (workflow_status IN('PENDING_ASSIGNMENT','ASSIGNED') AND status='PENDING') OR
 (workflow_status IN('IN_PROGRESS','ON_HOLD','PENDING_INSPECTION','REWORK_REQUIRED') AND status='IN_PROGRESS') OR
 (workflow_status='COMPLETED' AND status='COMPLETED') OR (workflow_status='CANCELLED' AND status='CANCELLED'));
ALTER TABLE dbo.room_inspections WITH CHECK ADD CONSTRAINT CK_inspection_workflow_v1 CHECK(workflow_status IS NULL OR
 (workflow_status='ASSIGNED' AND status='PENDING' AND result IS NULL) OR
 (workflow_status IN('INSPECTING','PENDING_CONCLUSION') AND status='IN_PROGRESS' AND result IS NULL) OR
 (workflow_status='PASSED' AND status='COMPLETED' AND result='PASS') OR
 (workflow_status='FAILED' AND status='COMPLETED' AND result='FAIL') OR (workflow_status='CANCELLED' AND status='CANCELLED'));
ALTER TABLE dbo.cleaning_reworks WITH CHECK ADD CONSTRAINT CK_rework_workflow_v1 CHECK(workflow_status IS NULL OR
 (workflow_status='PENDING_ACCEPTANCE' AND status='PENDING') OR
 (workflow_status IN('IN_PROGRESS','PENDING_REINSPECTION') AND status='IN_PROGRESS') OR
 (workflow_status='COMPLETED' AND status='COMPLETED') OR (workflow_status='CANCELLED' AND status='CANCELLED'));
CREATE TABLE dbo.cleaning_rework_defects(rework_id bigint NOT NULL,defect_id bigint NOT NULL,
 CONSTRAINT PK_rework_defects_v1 PRIMARY KEY(rework_id,defect_id),
 CONSTRAINT FK_rework_defects_rework_v1 FOREIGN KEY(rework_id) REFERENCES dbo.cleaning_reworks(rework_id),
 CONSTRAINT FK_rework_defects_defect_v1 FOREIGN KEY(defect_id) REFERENCES dbo.inspection_defects(defect_id));
CREATE INDEX IX_rework_defects_reverse_v1 ON dbo.cleaning_rework_defects(defect_id,rework_id);
CREATE TABLE dbo.inspection_defect_attachments(defect_id bigint NOT NULL,attachment_id bigint NOT NULL,
 CONSTRAINT PK_defect_attachments_v1 PRIMARY KEY(defect_id,attachment_id),
 CONSTRAINT FK_defect_attachment_defect_v1 FOREIGN KEY(defect_id) REFERENCES dbo.inspection_defects(defect_id),
 CONSTRAINT FK_defect_attachment_file_v1 FOREIGN KEY(attachment_id) REFERENCES dbo.attachments(attachment_id));
CREATE TABLE dbo.inspection_result_attachments(result_id bigint NOT NULL,attachment_id bigint NOT NULL,
 CONSTRAINT PK_result_attachments_v1 PRIMARY KEY(result_id,attachment_id),
 CONSTRAINT FK_result_attachment_result_v1 FOREIGN KEY(result_id) REFERENCES dbo.inspection_results(result_id),
 CONSTRAINT FK_result_attachment_file_v1 FOREIGN KEY(attachment_id) REFERENCES dbo.attachments(attachment_id));
CREATE INDEX IX_results_version_lookup_v1 ON dbo.inspection_results(inspection_id,checklist_item_id);
GO
CREATE TRIGGER dbo.TR_hk_inspections_v1 ON dbo.room_inspections AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.inspection_id=i.inspection_id WHERE d.workflow_status IS NOT NULL AND
  (i.workflow_status IS NULL OR i.room_id<>d.room_id OR ISNULL(i.cleaning_task_id,0)<>ISNULL(d.cleaning_task_id,0) OR i.version_id<>d.version_id))
  THROW 51731,'Owned inspection source/version is immutable.',1;
 IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON t.cleaning_task_id=i.cleaning_task_id
  WHERE i.workflow_status IS NOT NULL AND (t.cleaning_task_id IS NULL OR t.room_id<>i.room_id))
  THROW 51731,'Inspection must reference its own cleaning cycle.',1;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.workflow_status IS NOT NULL AND i.status IN('PENDING','IN_PROGRESS')
  AND EXISTS(SELECT 1 FROM dbo.room_inspections other WITH(UPDLOCK,HOLDLOCK) WHERE other.cleaning_task_id=i.cleaning_task_id AND other.inspection_id<>i.inspection_id AND other.status IN('PENDING','IN_PROGRESS')))
  THROW 51731,'Only one open inspection per cleaning cycle.',1;
END
GO
CREATE TRIGGER dbo.TR_hk_results_v1 ON dbo.inspection_results AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM (SELECT inspection_id FROM inserted UNION SELECT inspection_id FROM deleted) a
  JOIN dbo.room_inspections r WITH(UPDLOCK,HOLDLOCK) ON r.inspection_id=a.inspection_id WHERE r.workflow_status IS NOT NULL AND r.workflow_status<>'INSPECTING')
  THROW 51731,'Results writable only while Inspecting.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.room_inspections r ON r.inspection_id=i.inspection_id JOIN dbo.inspection_checklist_items c ON c.item_id=i.checklist_item_id
  WHERE r.workflow_status IS NOT NULL AND (c.version_id<>r.version_id OR c.is_active=0 OR
   EXISTS(SELECT 1 FROM dbo.inspection_results x WITH(UPDLOCK,HOLDLOCK) WHERE x.inspection_id=i.inspection_id AND x.checklist_item_id=i.checklist_item_id AND x.result_id<>i.result_id)))
  THROW 51731,'Result must be unique and belong to assigned active criterion.',1;
 UPDATE r SET summary_note=summary_note FROM dbo.room_inspections r JOIN
  (SELECT inspection_id FROM inserted UNION SELECT inspection_id FROM deleted) a ON a.inspection_id=r.inspection_id WHERE r.workflow_status IS NOT NULL;
END
GO
CREATE TRIGGER dbo.TR_hk_defects_v1 ON dbo.inspection_defects AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM (SELECT inspection_id FROM inserted UNION SELECT inspection_id FROM deleted) a JOIN dbo.room_inspections r WITH(UPDLOCK,HOLDLOCK) ON r.inspection_id=a.inspection_id
  WHERE r.workflow_status IS NOT NULL AND r.workflow_status NOT IN('INSPECTING','PENDING_CONCLUSION'))
  THROW 51731,'Concluded inspection defects cannot change.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.room_inspections r ON r.inspection_id=i.inspection_id
  WHERE r.workflow_status IS NOT NULL AND (i.checklist_item_id IS NULL OR LEN(LTRIM(RTRIM(i.description)))=0
   OR NOT EXISTS(SELECT 1 FROM dbo.inspection_results x JOIN dbo.inspection_checklist_items c ON c.item_id=x.checklist_item_id
    WHERE x.inspection_id=r.inspection_id AND x.checklist_item_id=i.checklist_item_id AND x.outcome='FAIL' AND c.version_id=r.version_id)))
  THROW 51731,'Defect requires a failed criterion from this inspection.',1;
 UPDATE r SET summary_note=summary_note FROM dbo.room_inspections r JOIN
  (SELECT inspection_id FROM inserted UNION SELECT inspection_id FROM deleted) a ON a.inspection_id=r.inspection_id WHERE r.workflow_status IS NOT NULL;
END
GO
CREATE TRIGGER dbo.TR_hk_rework_links_v1 ON dbo.cleaning_rework_defects AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.cleaning_reworks r ON r.rework_id=i.rework_id JOIN dbo.inspection_defects d ON d.defect_id=i.defect_id
  JOIN dbo.room_inspections x ON x.inspection_id=r.inspection_id JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=r.cleaning_task_id
  WHERE d.inspection_id<>r.inspection_id OR x.room_id<>t.room_id OR x.cleaning_task_id<>t.cleaning_task_id)
  THROW 51731,'Cross-cycle rework defect link refused.',1;
END
GO
CREATE TRIGGER dbo.TR_hk_defect_files_v1 ON dbo.inspection_defect_attachments AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.inspection_defects d ON d.defect_id=i.defect_id JOIN dbo.attachments a ON a.attachment_id=i.attachment_id
  WHERE a.entity_type<>'INSPECTION' OR a.entity_id<>d.inspection_id OR a.deleted_at IS NOT NULL)
  THROW 51731,'Attachment must belong to this inspection.',1;
END
GO
CREATE TRIGGER dbo.TR_hk_used_criteria_v1 ON dbo.inspection_checklist_items AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM (SELECT version_id FROM inserted UNION SELECT version_id FROM deleted) v
  JOIN dbo.room_inspections r WITH(HOLDLOCK) ON r.version_id=v.version_id WHERE r.workflow_status IS NOT NULL)
  THROW 51731,'Assigned Housekeeping checklist criteria are immutable, including legacy templates.',1;
END
GO
CREATE TRIGGER dbo.TR_hk_used_version_v1 ON dbo.inspection_checklist_versions AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.version_id=d.version_id
  WHERE EXISTS(SELECT 1 FROM dbo.room_inspections r WITH(HOLDLOCK) WHERE r.version_id=d.version_id AND r.workflow_status IS NOT NULL)
  AND (i.version_id IS NULL OR i.version_no<>d.version_no OR i.checklist_id<>d.checklist_id
   OR ISNULL(i.effective_at,'19000101')<>ISNULL(d.effective_at,'19000101') OR ISNULL(i.published_by,0)<>ISNULL(d.published_by,0)
   OR i.status<>d.status AND NOT(d.status='PUBLISHED' AND i.status='RETIRED')))
  THROW 51731,'Assigned version content is immutable except retirement.',1;
END
GO
CREATE TRIGGER dbo.TR_hk_result_files_v1 ON dbo.inspection_result_attachments AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.inspection_results r ON r.result_id=i.result_id JOIN dbo.attachments a ON a.attachment_id=i.attachment_id
  WHERE a.entity_type<>'INSPECTION' OR a.entity_id<>r.inspection_id OR a.deleted_at IS NOT NULL)
  THROW 51731,'Attachment must belong to this inspection.',1;
END
