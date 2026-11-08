-- FS016 isolated candidate only. Synthetic proofs are not accepted Inspection writer output.
SET XACT_ABORT ON;
IF DB_NAME()<>N'HotelOps_Canonical_FS016_Rework_V1_Fresh'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME()
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS016.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_FS016')
 THROW 51981,'FS016 isolated trial required',1;
IF OBJECT_ID('dbo.fs016_rework_source_proofs') IS NOT NULL THROW 51981,'No overwrite',1;
BEGIN TRANSACTION;
ALTER TABLE dbo.cleaning_reworks DROP CONSTRAINT CK_rework_workflow_v1;
ALTER TABLE dbo.cleaning_reworks ADD CONSTRAINT CK_rework_workflow_v1 CHECK(workflow_status IS NULL OR
 (workflow_status IN('PENDING_ACCEPTANCE','ACCEPTED') AND status='PENDING') OR
 (workflow_status IN('IN_PROGRESS','PENDING_REINSPECTION') AND status='IN_PROGRESS') OR
 (workflow_status='COMPLETED' AND status='COMPLETED') OR (workflow_status='CANCELLED' AND status='CANCELLED'));
CREATE UNIQUE INDEX UX_fs016_open_rework ON dbo.cleaning_reworks(inspection_id) WHERE status IN('PENDING','IN_PROGRESS');
ALTER TABLE dbo.cleaning_rework_defects ADD remediation_note nvarchar(500) NULL,remediated_by bigint NULL REFERENCES dbo.users(user_id),remediated_at datetime2(7) NULL;
EXEC(N'ALTER TABLE dbo.cleaning_rework_defects ADD CONSTRAINT CK_fs016_remediation CHECK(
 (remediation_note IS NULL AND remediated_by IS NULL AND remediated_at IS NULL) OR
 (LEN(LTRIM(RTRIM(remediation_note)))>0 AND remediated_by IS NOT NULL AND remediated_at IS NOT NULL));');
CREATE TABLE dbo.fs016_rework_evidence(rework_id bigint NOT NULL,defect_id bigint NOT NULL,attachment_id bigint NOT NULL REFERENCES dbo.attachments(attachment_id),
 PRIMARY KEY(rework_id,defect_id,attachment_id),FOREIGN KEY(rework_id,defect_id) REFERENCES dbo.cleaning_rework_defects(rework_id,defect_id));
CREATE TABLE dbo.fs016_rework_source_proofs(rework_id bigint NOT NULL PRIMARY KEY REFERENCES dbo.cleaning_reworks(rework_id),
 inspection_id bigint NOT NULL REFERENCES dbo.room_inspections(inspection_id),inspection_revision binary(8) NOT NULL,
 source_digest binary(32) NOT NULL,origin varchar(30) NOT NULL CHECK(origin='SYNTHETIC_TEST_ONLY'));
ALTER TABLE dbo.notification_publications DROP CONSTRAINT CK_fs015_publication_event;
ALTER TABLE dbo.notification_publications ALTER COLUMN assignment_id bigint NULL;
ALTER TABLE dbo.notification_publications ADD rework_id bigint NULL REFERENCES dbo.cleaning_reworks(rework_id);
ALTER TABLE dbo.notification_publications ADD CONSTRAINT CK_fs016_publication_event CHECK(event_type IN('CLEANING_TASK_ASSIGNED','CLEANING_BLOCKER_REPORTED','CLEANING_TASK_COMPLETED','REWORK_SUBMITTED'));
EXEC(N'ALTER TABLE dbo.notification_publications ADD CONSTRAINT CK_fs016_publication_source CHECK(
 (event_type=''REWORK_SUBMITTED'' AND rework_id IS NOT NULL AND assignment_id IS NULL) OR
 (event_type<>''REWORK_SUBMITTED'' AND rework_id IS NULL AND assignment_id IS NOT NULL));');
ALTER TABLE dbo.room_readiness_events DROP CONSTRAINT CK_fs015_readiness;
ALTER TABLE dbo.room_readiness_events ADD CONSTRAINT CK_fs016_readiness CHECK(readiness IN('PENDING_CLEANING','CLEANING_IN_PROGRESS','PENDING_INSPECTION','REWORK_REQUIRED'));
EXEC(N'CREATE TRIGGER dbo.TR_fs016_remediation_revision ON dbo.cleaning_rework_defects AFTER INSERT,UPDATE,DELETE AS
 BEGIN SET NOCOUNT ON; UPDATE w SET note=note FROM dbo.cleaning_reworks w JOIN
 (SELECT rework_id FROM inserted UNION SELECT rework_id FROM deleted) x ON x.rework_id=w.rework_id; END');
EXEC(N'CREATE TRIGGER dbo.TR_fs016_evidence_revision ON dbo.fs016_rework_evidence AFTER INSERT,UPDATE,DELETE AS
 BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted e JOIN dbo.attachments a ON a.attachment_id=e.attachment_id
 JOIN dbo.cleaning_reworks r ON r.rework_id=e.rework_id
 WHERE a.entity_type<>''CLEANING_TASK'' OR a.entity_id<>r.cleaning_task_id OR a.deleted_at IS NOT NULL)
 THROW 51982,''Rework evidence source mismatch'',1;
 UPDATE w SET note=note FROM dbo.cleaning_reworks w JOIN
 (SELECT rework_id FROM inserted UNION SELECT rework_id FROM deleted) x ON x.rework_id=w.rework_id; END');
EXEC sys.sp_addextendedproperty @name=N'HotelOps.FS016.Rework',@value=N'PROVISIONAL_NOT_ACCEPTED';
COMMIT;
