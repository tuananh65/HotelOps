-- UC041/047 narrow disposable trial; no generated entities or physical status enum changes.
SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
 THROW 51999,'Exact disposable reinspection trial required',1;
IF @@TRANCOUNT<>1 THROW 51999,'Caller transaction and cleanup required',1;
DECLARE @commit varchar(40)=CONVERT(varchar(40),SESSION_CONTEXT(N'DEV3.SourceCommit'));
IF @commit IS NULL OR LEN(@commit)<>40 OR @commit COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9a-f]%'
 THROW 51999,'Actual source commit required',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.ReworkSubmission.V1' AND CONVERT(nvarchar(128),value)='FE04_REWORK_SUBMISSION_V1')
 OR OBJECT_ID('dbo.fe05_reinspection_bindings_v1') IS NOT NULL OR OBJECT_ID('dbo.fe05_reinspection_defect_results_v1') IS NOT NULL
 OR OBJECT_ID('dbo.fe05_reinspection_defect_attachments_v1') IS NOT NULL OR OBJECT_ID('dbo.fe05_bind_reinspection') IS NOT NULL
 OR OBJECT_ID('dbo.fe05_commit_reinspection') IS NOT NULL OR DATABASE_PRINCIPAL_ID('hotelops_reinspection_writer') IS NOT NULL
 OR EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.Reinspection.V1')
 THROW 51999,'Submission authority required; existing reinspection contract must not be overwritten',1;
CREATE TABLE dbo.fe05_reinspection_bindings_v1(
 inspection_id bigint NOT NULL PRIMARY KEY REFERENCES dbo.room_inspections(inspection_id),
 rework_id bigint NOT NULL REFERENCES dbo.cleaning_reworks(rework_id),
 original_inspection_id bigint NOT NULL REFERENCES dbo.room_inspections(inspection_id),
 cleaning_task_id bigint NOT NULL REFERENCES dbo.cleaning_tasks(cleaning_task_id),
 room_id bigint NOT NULL REFERENCES dbo.rooms(room_id),branch_id bigint NOT NULL REFERENCES dbo.branches(branch_id),
 version_id bigint NOT NULL REFERENCES dbo.inspection_checklist_versions(version_id),
 cycle_revision binary(8) NOT NULL,rework_revision binary(8) NOT NULL,
 submission_actor bigint NOT NULL,submission_operation uniqueidentifier NOT NULL,
 reinspector_user_id bigint NOT NULL REFERENCES dbo.users(user_id),bound_by bigint NOT NULL REFERENCES dbo.users(user_id),
 bound_at datetime2(7) NOT NULL,touched_at datetime2(7) NOT NULL,row_version rowversion NOT NULL,
 CONSTRAINT UQ_reinspection_pair_v1 UNIQUE(inspection_id,rework_id),
 CONSTRAINT FK_reinspection_submission_v1 FOREIGN KEY(submission_actor,submission_operation)
   REFERENCES dbo.notification_operation_reservations(actor_user_id,operation_key),
 CONSTRAINT CK_reinspection_original_v1 CHECK(inspection_id<>original_inspection_id));
CREATE TABLE dbo.fe05_reinspection_defect_results_v1(
 inspection_id bigint NOT NULL,rework_id bigint NOT NULL,defect_id bigint NOT NULL,
 fixed bit NULL,note nvarchar(500) NULL,recorded_by bigint NULL REFERENCES dbo.users(user_id),recorded_at datetime2(7) NULL,
 CONSTRAINT PK_reinspection_defect_v1 PRIMARY KEY(inspection_id,defect_id),
 CONSTRAINT FK_reinspection_binding_v1 FOREIGN KEY(inspection_id,rework_id) REFERENCES dbo.fe05_reinspection_bindings_v1(inspection_id,rework_id),
 CONSTRAINT FK_reinspection_defect_v1 FOREIGN KEY(rework_id,defect_id) REFERENCES dbo.cleaning_rework_defects(rework_id,defect_id),
 CONSTRAINT CK_reinspection_answer_v1 CHECK((fixed IS NULL AND note IS NULL AND recorded_by IS NULL AND recorded_at IS NULL)
   OR(fixed IS NOT NULL AND recorded_by IS NOT NULL AND recorded_at IS NOT NULL)));
CREATE TABLE dbo.fe05_reinspection_defect_attachments_v1(
 inspection_id bigint NOT NULL,defect_id bigint NOT NULL,attachment_id bigint NOT NULL REFERENCES dbo.attachments(attachment_id),
 CONSTRAINT PK_reinspection_photo_v1 PRIMARY KEY(inspection_id,defect_id,attachment_id),
 CONSTRAINT FK_reinspection_photo_result_v1 FOREIGN KEY(inspection_id,defect_id) REFERENCES dbo.fe05_reinspection_defect_results_v1(inspection_id,defect_id));
EXEC(N'CREATE TRIGGER dbo.TR_reinspection_binding_immutable_v1 ON dbo.fe05_reinspection_bindings_v1 AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted n ON n.inspection_id=d.inspection_id WHERE n.inspection_id IS NULL
  OR n.rework_id<>d.rework_id OR n.original_inspection_id<>d.original_inspection_id OR n.cleaning_task_id<>d.cleaning_task_id
  OR n.room_id<>d.room_id OR n.branch_id<>d.branch_id OR n.version_id<>d.version_id OR n.cycle_revision<>d.cycle_revision
  OR n.rework_revision<>d.rework_revision OR n.submission_actor<>d.submission_actor OR n.submission_operation<>d.submission_operation
  OR n.reinspector_user_id<>d.reinspector_user_id OR n.bound_by<>d.bound_by OR n.bound_at<>d.bound_at)
  THROW 51999,''Immutable reinspection binding'',1;
END');
EXEC(N'CREATE TRIGGER dbo.TR_reinspection_result_revision_v1 ON dbo.fe05_reinspection_defect_results_v1 AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted x JOIN dbo.fe05_reinspection_bindings_v1 b ON b.inspection_id=x.inspection_id
  JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id
  WHERE i.inspector_user_id<>b.reinspector_user_id OR i.workflow_status<>''ASSIGNED''
    OR(x.recorded_by IS NOT NULL AND x.recorded_by<>b.reinspector_user_id)) THROW 51999,''Reinspection result actor/phase mismatch'',1;
 UPDATE b SET touched_at=SYSUTCDATETIME() FROM dbo.fe05_reinspection_bindings_v1 b
 JOIN(SELECT inspection_id FROM inserted UNION SELECT inspection_id FROM deleted) x ON x.inspection_id=b.inspection_id;
 UPDATE i SET summary_note=summary_note FROM dbo.room_inspections i
 JOIN(SELECT inspection_id FROM inserted UNION SELECT inspection_id FROM deleted) x ON x.inspection_id=i.inspection_id;
END');
EXEC(N'CREATE TRIGGER dbo.TR_reinspection_photo_revision_v1 ON dbo.fe05_reinspection_defect_attachments_v1 AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted x JOIN dbo.attachments a ON a.attachment_id=x.attachment_id JOIN dbo.room_inspections i ON i.inspection_id=x.inspection_id
  WHERE a.entity_type<>''INSPECTION'' OR a.entity_id<>x.inspection_id OR a.deleted_at IS NOT NULL OR i.workflow_status<>''ASSIGNED''
    OR a.uploaded_by<>i.inspector_user_id) THROW 51999,''Reinspection evidence source mismatch'',1;
 UPDATE b SET touched_at=SYSUTCDATETIME() FROM dbo.fe05_reinspection_bindings_v1 b
 JOIN(SELECT inspection_id FROM inserted UNION SELECT inspection_id FROM deleted) x ON x.inspection_id=b.inspection_id;
 UPDATE i SET summary_note=summary_note FROM dbo.room_inspections i
 JOIN(SELECT inspection_id FROM inserted UNION SELECT inspection_id FROM deleted) x ON x.inspection_id=i.inspection_id;
END');
-- Only workflow FAILED is new here; existing physical COMPLETED represents the ended attempt.
-- The trial owner captures/restores the exact preexisting CHECK definition.
DECLARE @original nvarchar(max)=(SELECT definition FROM sys.check_constraints
 WHERE parent_object_id=OBJECT_ID('dbo.cleaning_reworks') AND name='CK_rework_workflow_v1' AND is_disabled=0 AND is_not_trusted=0);
IF @original IS NULL OR @original LIKE '%FAILED%' THROW 51999,'Fresh trusted workflow CHECK required',1;
DECLARE @ddl nvarchar(max)=N'ALTER TABLE dbo.cleaning_reworks DROP CONSTRAINT CK_rework_workflow_v1; ALTER TABLE dbo.cleaning_reworks WITH CHECK ADD CONSTRAINT CK_rework_workflow_v1 CHECK ('+@original+N' OR (workflow_status=''FAILED'' AND status=''COMPLETED''));';
EXEC sys.sp_executesql @ddl;
CREATE ROLE hotelops_reinspection_writer;
-- Protected assignment/conclusion writers are installed by the next reviewed batch in the same transaction.
EXEC sys.sp_addextendedproperty @name=N'HotelOps.Reinspection.V1',@value=N'FE05_REINSPECTION_V1';
