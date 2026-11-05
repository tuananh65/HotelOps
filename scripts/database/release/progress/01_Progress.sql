SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UserDirectory.Trial' AND CONVERT(nvarchar(128),value)=N'Task17')
 THROW 51740,'Guarded disposable trial required.',1;
IF EXISTS(SELECT 1 FROM sys.tables WHERE name IN('cleaning_item_definitions','cleaning_progress_versions','cleaning_task_items','task_progress_versions','task_completion_criteria','progress_evidence_links'))
 THROW 51740,'Partial progress schema requires explicit recovery.',1;
CREATE TABLE dbo.cleaning_item_definitions(
 item_id bigint IDENTITY PRIMARY KEY,room_type_id bigint NOT NULL REFERENCES dbo.room_types(room_type_id),
 label nvarchar(250) NOT NULL,is_required bit NOT NULL,sort_order int NOT NULL,
 CONSTRAINT UQ_cleaning_item_sort_v1 UNIQUE(room_type_id,sort_order),
 CONSTRAINT CK_cleaning_item_text_v1 CHECK(LEN(LTRIM(RTRIM(label)))>0 AND sort_order>=0));
CREATE TABLE dbo.cleaning_progress_versions(
 cleaning_task_id bigint PRIMARY KEY REFERENCES dbo.cleaning_tasks(cleaning_task_id),row_version rowversion NOT NULL);
CREATE TABLE dbo.cleaning_task_items(
 item_id bigint IDENTITY PRIMARY KEY,cleaning_task_id bigint NOT NULL REFERENCES dbo.cleaning_progress_versions(cleaning_task_id),
 source_item_id bigint NOT NULL REFERENCES dbo.cleaning_item_definitions(item_id),
 label nvarchar(250) NOT NULL,is_required bit NOT NULL,sort_order int NOT NULL,is_done bit NOT NULL DEFAULT 0,note nvarchar(1000) NULL,
 CONSTRAINT UQ_cleaning_task_item_v1 UNIQUE(cleaning_task_id,source_item_id),
 CONSTRAINT CK_cleaning_task_item_text_v1 CHECK(LEN(LTRIM(RTRIM(label)))>0 AND sort_order>=0));
CREATE TABLE dbo.task_progress_versions(
 task_id bigint PRIMARY KEY REFERENCES dbo.operational_tasks(task_id),workflow_status varchar(20) NOT NULL,
 row_version rowversion NOT NULL,CONSTRAINT CK_task_progress_state_v1 CHECK(workflow_status IN('IN_PROGRESS','PAUSED')));
CREATE TABLE dbo.task_completion_criteria(
 criterion_id bigint IDENTITY PRIMARY KEY,task_id bigint NOT NULL REFERENCES dbo.task_progress_versions(task_id),
 label nvarchar(250) NOT NULL,is_required bit NOT NULL,sort_order int NOT NULL,outcome varchar(4) NULL,note nvarchar(1000) NULL,
 CONSTRAINT UQ_task_criterion_sort_v1 UNIQUE(task_id,sort_order),
 CONSTRAINT CK_task_criterion_text_v1 CHECK(LEN(LTRIM(RTRIM(label)))>0 AND sort_order>=0),
 CONSTRAINT CK_task_criterion_outcome_v1 CHECK(outcome IS NULL OR outcome IN('PASS','FAIL')));
CREATE TABLE dbo.progress_evidence_links(
 attachment_id bigint PRIMARY KEY REFERENCES dbo.attachments(attachment_id),
 cleaning_progress_id bigint NULL REFERENCES dbo.cleaning_progress_logs(progress_id),
 task_progress_id bigint NULL REFERENCES dbo.operational_task_progress(progress_id),
 CONSTRAINT CK_progress_evidence_owner_v1 CHECK((cleaning_progress_id IS NULL AND task_progress_id IS NOT NULL) OR (cleaning_progress_id IS NOT NULL AND task_progress_id IS NULL)));
CREATE INDEX IX_cleaning_progress_log_order_v1 ON dbo.cleaning_progress_logs(cleaning_task_id,logged_at,progress_id);
CREATE INDEX IX_task_progress_log_order_v1 ON dbo.operational_task_progress(task_id,reported_at,progress_id);
GO
CREATE TRIGGER dbo.TR_cleaning_progress_items_v1 ON dbo.cleaning_task_items AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.item_id=d.item_id WHERE i.item_id IS NULL OR i.cleaning_task_id<>d.cleaning_task_id OR i.source_item_id<>d.source_item_id OR i.label<>d.label OR i.is_required<>d.is_required OR i.sort_order<>d.sort_order)
  THROW 51741,'Cleaning snapshot identity is immutable.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=i.cleaning_task_id JOIN dbo.rooms r ON r.room_id=t.room_id JOIN dbo.cleaning_item_definitions d ON d.item_id=i.source_item_id
  WHERE t.status<>'IN_PROGRESS' OR (r.room_type_id<>d.room_type_id AND NOT EXISTS(SELECT 1 FROM deleted old WHERE old.item_id=i.item_id)))
  THROW 51741,'Cleaning item ownership or phase is invalid.',1;
 UPDATE v SET cleaning_task_id=v.cleaning_task_id FROM dbo.cleaning_progress_versions v JOIN (SELECT cleaning_task_id FROM inserted UNION SELECT cleaning_task_id FROM deleted) x ON x.cleaning_task_id=v.cleaning_task_id;
END;
GO
CREATE TRIGGER dbo.TR_task_progress_criteria_v1 ON dbo.task_completion_criteria AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.criterion_id=d.criterion_id WHERE i.criterion_id IS NULL OR i.task_id<>d.task_id OR i.label<>d.label OR i.is_required<>d.is_required OR i.sort_order<>d.sort_order)
  THROW 51741,'Task criterion identity is immutable.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.operational_tasks t ON t.task_id=i.task_id JOIN dbo.task_progress_versions v ON v.task_id=t.task_id WHERE t.status<>'IN_PROGRESS' OR v.workflow_status NOT IN('IN_PROGRESS','PAUSED'))
  THROW 51741,'Task progress phase is invalid.',1;
 UPDATE v SET task_id=v.task_id FROM dbo.task_progress_versions v JOIN (SELECT task_id FROM inserted UNION SELECT task_id FROM deleted) x ON x.task_id=v.task_id;
END;
GO
CREATE TRIGGER dbo.TR_progress_evidence_v1 ON dbo.progress_evidence_links AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.attachments a ON a.attachment_id=i.attachment_id
  LEFT JOIN dbo.cleaning_progress_logs c ON c.progress_id=i.cleaning_progress_id
  LEFT JOIN dbo.operational_task_progress t ON t.progress_id=i.task_progress_id
  WHERE a.deleted_at IS NOT NULL OR
   (i.cleaning_progress_id IS NOT NULL AND (a.entity_type<>'CLEANING_TASK' OR a.entity_id<>c.cleaning_task_id OR a.uploaded_by<>c.logged_by)) OR
   (i.task_progress_id IS NOT NULL AND (a.entity_type<>'TASK' OR a.entity_id<>t.task_id OR a.uploaded_by<>t.reported_by)))
  THROW 51741,'Progress evidence source ownership mismatch.',1;
END;
GO
CREATE TRIGGER dbo.TR_progress_cleaning_parent_v1 ON dbo.cleaning_tasks AFTER UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.cleaning_progress_versions v ON v.cleaning_task_id=i.cleaning_task_id
  WHERE i.status='COMPLETED' AND EXISTS(SELECT 1 FROM dbo.cleaning_task_items x WHERE x.cleaning_task_id=i.cleaning_task_id AND x.is_required=1 AND x.is_done=0))
  THROW 51741,'Required cleaning items are not complete.',1;
 UPDATE v SET cleaning_task_id=v.cleaning_task_id FROM dbo.cleaning_progress_versions v JOIN inserted i ON i.cleaning_task_id=v.cleaning_task_id;
END;
GO
CREATE TRIGGER dbo.TR_progress_task_parent_v1 ON dbo.operational_tasks AFTER UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.task_progress_versions v ON v.task_id=i.task_id
  WHERE i.status='COMPLETED' AND EXISTS(SELECT 1 FROM dbo.task_completion_criteria x WHERE x.task_id=i.task_id AND x.is_required=1 AND x.outcome IS NULL))
  THROW 51741,'Required task criteria have no result.',1;
 UPDATE v SET task_id=v.task_id FROM dbo.task_progress_versions v JOIN inserted i ON i.task_id=v.task_id;
END;
GO
CREATE TRIGGER dbo.TR_progress_cleaning_owner_v1 ON dbo.cleaning_assignments AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 UPDATE v SET cleaning_task_id=v.cleaning_task_id FROM dbo.cleaning_progress_versions v JOIN (SELECT cleaning_task_id FROM inserted UNION SELECT cleaning_task_id FROM deleted) x ON x.cleaning_task_id=v.cleaning_task_id;
END;
GO
CREATE TRIGGER dbo.TR_progress_task_owner_v1 ON dbo.operational_task_assignments AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 UPDATE v SET task_id=v.task_id FROM dbo.task_progress_versions v JOIN (SELECT task_id FROM inserted UNION SELECT task_id FROM deleted) x ON x.task_id=v.task_id;
END;
