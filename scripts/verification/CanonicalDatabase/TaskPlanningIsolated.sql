-- Optional Database First capability for UC102/103. Never run on shared HotelOpsDB.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
 THROW 51870,'Exact owned disposable database required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskDecline.V1' AND CONVERT(nvarchar(20),value)='1')
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_task_progress_state_entry_v1' AND is_disabled=0 AND is_not_trusted=0)
 OR COL_LENGTH('dbo.operational_tasks','title')<>400 OR COL_LENGTH('dbo.task_completion_criteria','label')<>500
 THROW 51870,'Verified FE11 schema required.',1;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskPlanning.V1')
BEGIN
 IF OBJECT_ID('dbo.operational_task_definitions') IS NOT NULL OR OBJECT_ID('dbo.task_category_processes') IS NOT NULL
  THROW 51870,'Unreviewed planning table already exists.',1;
 CREATE TABLE dbo.operational_task_definitions(
  task_id bigint NOT NULL CONSTRAINT PK_operational_task_definitions PRIMARY KEY,
  requested_result nvarchar(4000) NULL,
  sla_minutes int NULL,
  creation_key uniqueidentifier NULL,
  creation_hash binary(32) NULL,
  CONSTRAINT FK_operational_task_definitions_task FOREIGN KEY(task_id) REFERENCES dbo.operational_tasks(task_id),
  CONSTRAINT CK_operational_task_definition_result CHECK(
    (requested_result IS NULL AND creation_key IS NULL AND creation_hash IS NULL)
    OR (requested_result IS NOT NULL AND LEN(LTRIM(RTRIM(requested_result)))>0 AND creation_key IS NOT NULL AND creation_hash IS NOT NULL)),
  CONSTRAINT CK_operational_task_definition_sla CHECK(sla_minutes IS NULL OR sla_minutes>0)
 );
 CREATE UNIQUE INDEX UQ_operational_task_creation_key ON dbo.operational_task_definitions(creation_key) WHERE creation_key IS NOT NULL;
 -- Explicit classification, not a guess from translated category names. Only
 -- reviewed OPERATIONAL categories appear in the create form; missing policy denies.
 CREATE TABLE dbo.task_category_processes(
  category_id bigint NOT NULL CONSTRAINT PK_task_category_processes PRIMARY KEY,
  process_code varchar(20) NOT NULL,
  CONSTRAINT FK_task_category_processes_category FOREIGN KEY(category_id) REFERENCES dbo.work_categories(category_id),
  CONSTRAINT CK_task_category_process CHECK(process_code IN('OPERATIONAL','CLEANING','INCIDENT','MATERIAL'))
 );
 ALTER TABLE dbo.task_progress_versions DROP CONSTRAINT CK_task_progress_state_entry_v1;
 ALTER TABLE dbo.task_progress_versions WITH CHECK ADD CONSTRAINT CK_task_progress_state_entry_v1
  CHECK(workflow_status IN('PENDING_ASSIGNMENT','ASSIGNED','ACCEPTED','IN_PROGRESS','PAUSED','PENDING_VERIFICATION','CLOSED','CANCELLED'));
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.TaskPlanning.V1',@value=N'1';
END;
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_task_definition_v1 ON dbo.operational_task_definitions AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.task_id=d.task_id WHERE i.task_id IS NULL
   OR ISNULL(i.requested_result,N'''')<>ISNULL(d.requested_result,N'''')
   OR ISNULL(CONVERT(nvarchar(36),i.creation_key),N'''')<>ISNULL(CONVERT(nvarchar(36),d.creation_key),N'''')
   OR ISNULL(i.creation_hash,0x)<>ISNULL(d.creation_hash,0x))
  THROW 51870,''Task creation definition is immutable.'',1;
 UPDATE v SET task_id=v.task_id FROM dbo.task_progress_versions v JOIN inserted i ON i.task_id=v.task_id;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_task_progress_criteria_v1 ON dbo.task_completion_criteria AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.criterion_id=d.criterion_id WHERE i.criterion_id IS NULL OR i.task_id<>d.task_id OR i.label<>d.label OR i.is_required<>d.is_required OR i.sort_order<>d.sort_order)
  THROW 51741,''Task criterion identity is immutable.'',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.operational_tasks t ON t.task_id=i.task_id LEFT JOIN dbo.task_progress_versions v ON v.task_id=t.task_id
  WHERE v.task_id IS NULL OR NOT(
   (t.status=''IN_PROGRESS'' AND v.workflow_status IN(''IN_PROGRESS'',''PAUSED''))
   OR (t.status=''TODO'' AND v.workflow_status=''PENDING_ASSIGNMENT'' AND i.outcome IS NULL AND i.note IS NULL
    AND NOT EXISTS(SELECT 1 FROM deleted)
    AND EXISTS(SELECT 1 FROM dbo.operational_task_definitions d WHERE d.task_id=t.task_id AND d.creation_key IS NOT NULL AND d.requested_result IS NOT NULL)
    AND NOT EXISTS(SELECT 1 FROM dbo.operational_task_assignments a WHERE a.task_id=t.task_id)
    AND NOT EXISTS(SELECT 1 FROM dbo.operational_task_progress p WHERE p.task_id=t.task_id)
    AND NOT EXISTS(SELECT 1 FROM dbo.audit_logs a WHERE a.entity_type=''operational_tasks'' AND a.entity_id=CONVERT(nvarchar(50),t.task_id) AND a.action=''CREATE''))
  )) THROW 51741,''Task progress phase is invalid.'',1;
 UPDATE v SET task_id=v.task_id FROM dbo.task_progress_versions v JOIN (SELECT task_id FROM inserted UNION SELECT task_id FROM deleted) x ON x.task_id=v.task_id;
END;');
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskPlanning.V1' AND CONVERT(nvarchar(20),value)='1')
 OR COL_LENGTH('dbo.operational_task_definitions','requested_result')<>8000
 OR COL_LENGTH('dbo.operational_task_definitions','creation_hash')<>32
 THROW 51870,'Conflicting planning capability.',1;
COMMIT;
SELECT 'TASK_PLANNING_INSTALLED' AS result;
