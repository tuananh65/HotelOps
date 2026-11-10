-- FAST014 owned isolated COMPONENT candidate only, not shared/production deployment.
-- Caller supplies the exact ownership UUID from the new FAST014 environment receipt.
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE 'HotelOps[_]Canonical[_]%[_]Fresh'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME()
 OR ISNULL(CONVERT(nvarchar(128),SESSION_CONTEXT(N'FE05.FAST014.Owner')),N'')=''
 OR ISNULL(CONVERT(nvarchar(128),SESSION_CONTEXT(N'FE05.FAST014.Owner')),N'')<>
    ISNULL((SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE05INT001.Owner'),N'')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE05.FAST014.Purpose' AND CONVERT(nvarchar(128),value)=N'OWNED_COMPONENT_ONLY')
 THROW 51997,'FAST014 exact owned component target required',1;
IF OBJECT_ID('dbo.fe05_reinspection_bindings_v1') IS NOT NULL OR OBJECT_ID('dbo.fe05_reinspection_defect_results_v1') IS NOT NULL
 OR EXISTS(SELECT 1 FROM dbo.cleaning_reworks)
 THROW 51997,'No overwrite or legacy backfill; empty rework inventory required',1;
IF COL_LENGTH('dbo.cleaning_rework_defects','remediation_note') IS NOT NULL
 THROW 51997,'Existing remediation contract requires review; do not overwrite',1;
BEGIN TRANSACTION;
-- Same remediation fields/FK/consistency rule as the existing FS016 candidate, new target only.
ALTER TABLE dbo.cleaning_rework_defects ADD remediation_note nvarchar(500) NULL,
 remediated_by bigint NULL REFERENCES dbo.users(user_id),remediated_at datetime2(7) NULL;
EXEC(N'ALTER TABLE dbo.cleaning_rework_defects ADD CONSTRAINT CK_fe05_trial_remediation CHECK(
 (remediation_note IS NULL AND remediated_by IS NULL AND remediated_at IS NULL) OR
 (LEN(LTRIM(RTRIM(remediation_note)))>0 AND remediated_by IS NOT NULL AND remediated_at IS NOT NULL));');
CREATE TABLE dbo.fe05_reinspection_bindings_v1(
 inspection_id bigint NOT NULL PRIMARY KEY REFERENCES dbo.room_inspections(inspection_id),
 rework_id bigint NOT NULL REFERENCES dbo.cleaning_reworks(rework_id),
 original_inspection_id bigint NOT NULL REFERENCES dbo.room_inspections(inspection_id),
 cleaning_task_id bigint NOT NULL REFERENCES dbo.cleaning_tasks(cleaning_task_id),
 room_id bigint NOT NULL REFERENCES dbo.rooms(room_id),branch_id bigint NOT NULL REFERENCES dbo.branches(branch_id),
 cycle_revision binary(8) NOT NULL,original_version_id bigint NOT NULL REFERENCES dbo.inspection_checklist_versions(version_id),
 assigned_version_id bigint NOT NULL REFERENCES dbo.inspection_checklist_versions(version_id),
 rework_revision binary(8) NOT NULL,inspection_revision binary(8) NOT NULL,
 reinspector_user_id bigint NOT NULL REFERENCES dbo.users(user_id),bound_by bigint NOT NULL REFERENCES dbo.users(user_id),
 bound_at datetime2(7) NOT NULL,touched_at datetime2(7) NOT NULL,row_version rowversion NOT NULL,
 CONSTRAINT UQ_fe05_reinspection_pair_v1 UNIQUE(inspection_id,rework_id),
 CONSTRAINT CK_fe05_reinspection_not_original_v1 CHECK(inspection_id<>original_inspection_id));
CREATE TABLE dbo.fe05_reinspection_defect_results_v1(
 inspection_id bigint NOT NULL,rework_id bigint NOT NULL,defect_id bigint NOT NULL,
 fixed bit NULL,note nvarchar(max) NULL,recorded_by bigint NULL REFERENCES dbo.users(user_id),recorded_at datetime2(7) NULL,
 row_version rowversion NOT NULL,
 CONSTRAINT PK_fe05_reinspection_defect_v1 PRIMARY KEY(inspection_id,defect_id),
 CONSTRAINT FK_fe05_reinspection_binding_v1 FOREIGN KEY(inspection_id,rework_id) REFERENCES dbo.fe05_reinspection_bindings_v1(inspection_id,rework_id),
 CONSTRAINT FK_fe05_reinspection_selected_v1 FOREIGN KEY(rework_id,defect_id) REFERENCES dbo.cleaning_rework_defects(rework_id,defect_id),
 CONSTRAINT CK_fe05_reinspection_answer_v1 CHECK(
 (fixed IS NULL AND recorded_by IS NULL AND recorded_at IS NULL AND note IS NULL) OR
 (fixed IS NOT NULL AND recorded_by IS NOT NULL AND recorded_at IS NOT NULL)));
EXEC(N'CREATE TRIGGER dbo.TR_fe05_reinspection_binding_guard_v1 ON dbo.fe05_reinspection_bindings_v1 AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted n ON n.inspection_id=d.inspection_id
  WHERE n.inspection_id IS NULL OR n.rework_id<>d.rework_id OR n.original_inspection_id<>d.original_inspection_id
  OR n.cleaning_task_id<>d.cleaning_task_id OR n.room_id<>d.room_id OR n.branch_id<>d.branch_id
  OR n.original_version_id<>d.original_version_id OR n.assigned_version_id<>d.assigned_version_id
  OR n.reinspector_user_id<>d.reinspector_user_id OR n.bound_by<>d.bound_by OR n.bound_at<>d.bound_at
  OR n.cycle_revision<>d.cycle_revision OR n.rework_revision<>d.rework_revision OR n.inspection_revision<>d.inspection_revision)
  THROW 51997,''Immutable candidate binding; reassignment policy required'',1;
 IF EXISTS(SELECT 1 FROM inserted b WHERE NOT EXISTS(
  SELECT 1 FROM dbo.room_inspections i JOIN dbo.rooms r ON r.room_id=i.room_id
  JOIN dbo.room_inspections o ON o.inspection_id=b.original_inspection_id
  JOIN dbo.cleaning_reworks w ON w.rework_id=b.rework_id
  JOIN dbo.room_cleaning_cycle_state c ON c.room_id=b.room_id
  WHERE i.inspection_id=b.inspection_id AND i.room_id=b.room_id AND r.branch_id=b.branch_id
   AND i.cleaning_task_id=b.cleaning_task_id AND i.version_id=b.assigned_version_id AND i.inspector_user_id=b.reinspector_user_id
   AND i.workflow_status IN(''ASSIGNED'',''INSPECTING'',''PENDING_CONCLUSION'')
   AND o.version_id=b.original_version_id AND o.cleaning_task_id=b.cleaning_task_id AND o.room_id=b.room_id
   AND o.workflow_status=''FAILED'' AND o.result=''FAIL'' AND o.status=''COMPLETED''
   AND w.inspection_id=o.inspection_id AND w.cleaning_task_id=b.cleaning_task_id
   AND w.workflow_status=''PENDING_REINSPECTION'' AND w.status=''IN_PROGRESS''
   AND c.cleaning_task_id=b.cleaning_task_id AND c.row_version=b.cycle_revision AND w.row_version=b.rework_revision))
  THROW 51997,''Candidate binding source/cycle/phase mismatch'',1;
END');
EXEC(N'CREATE TRIGGER dbo.TR_fe05_reinspection_result_revision_v1 ON dbo.fe05_reinspection_defect_results_v1 AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted x JOIN dbo.fe05_reinspection_bindings_v1 b ON b.inspection_id=x.inspection_id
   JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id
   WHERE i.inspector_user_id<>b.reinspector_user_id OR (x.recorded_by IS NOT NULL AND x.recorded_by<>b.reinspector_user_id)
     OR i.workflow_status NOT IN(''ASSIGNED'',''INSPECTING'',''PENDING_CONCLUSION''))
  THROW 51997,''Candidate result actor/phase mismatch'',1;
 UPDATE b SET touched_at=SYSUTCDATETIME() FROM dbo.fe05_reinspection_bindings_v1 b
 JOIN (SELECT inspection_id FROM inserted UNION SELECT inspection_id FROM deleted) x ON x.inspection_id=b.inspection_id;
 UPDATE i SET summary_note=summary_note FROM dbo.room_inspections i
 JOIN (SELECT inspection_id FROM inserted UNION SELECT inspection_id FROM deleted) x ON x.inspection_id=i.inspection_id;
END');
EXEC(N'CREATE TRIGGER dbo.TR_fe05_trial_remediation_revision ON dbo.cleaning_rework_defects AFTER INSERT,UPDATE,DELETE AS
 BEGIN SET NOCOUNT ON; UPDATE w SET note=note FROM dbo.cleaning_reworks w JOIN
 (SELECT rework_id FROM inserted UNION SELECT rework_id FROM deleted) x ON x.rework_id=w.rework_id; END');
EXEC sys.sp_addextendedproperty @name=N'HotelOps.FE05.ReinspectionCandidate',@value=N'FAST014_COMPONENT_NOT_ACCEPTED';
COMMIT;
