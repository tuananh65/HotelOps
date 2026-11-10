-- FAST016 proposal persistence ONLY; no operational lifecycle/status/schema acceptance.
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE 'HotelOps[_]Canonical[_]%[_]Fresh'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME()
 OR ISNULL(CONVERT(nvarchar(128),SESSION_CONTEXT(N'FE05.FAST014.Owner')),N'')=''
 OR ISNULL(CONVERT(nvarchar(128),SESSION_CONTEXT(N'FE05.FAST014.Owner')),N'')<>
    ISNULL((SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE05INT001.Owner'),N'')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE05.ReinspectionCandidate' AND CONVERT(nvarchar(128),value)=N'FAST014_COMPONENT_NOT_ACCEPTED')
 THROW 51997,'Exact owned reinspection proposal target required',1;
IF OBJECT_ID('dbo.fe05_reinspection_conclusion_proposals_v1') IS NOT NULL
 OR EXISTS(SELECT 1 FROM dbo.fe05_reinspection_bindings_v1)
 THROW 51997,'Fresh candidate schema only; no overwrite/backfill',1;
BEGIN TRANSACTION;
CREATE TABLE dbo.fe05_reinspection_conclusion_proposals_v1(
 inspection_id bigint NOT NULL PRIMARY KEY,rework_id bigint NOT NULL,
 operation_id uniqueidentifier NOT NULL UNIQUE,
 branch_id bigint NOT NULL REFERENCES dbo.branches(branch_id),actor_user_id bigint NOT NULL REFERENCES dbo.users(user_id),
 request_canonical nvarchar(max) NOT NULL,decisions_json nvarchar(max) NOT NULL,
 proposed_rework_state varchar(16) NOT NULL CHECK(proposed_rework_state IN('COMPLETED','FAILED')),
 inspection_revision binary(8) NOT NULL,binding_revision binary(8) NOT NULL,rework_revision binary(8) NOT NULL,
 created_at datetime2(7) NOT NULL,proposal_revision rowversion NOT NULL,
 FOREIGN KEY(inspection_id,rework_id) REFERENCES dbo.fe05_reinspection_bindings_v1(inspection_id,rework_id),
 CHECK(ISJSON(request_canonical)=1 AND ISJSON(decisions_json)=1));
EXEC(N'CREATE TRIGGER dbo.TR_fe05_proposal_immutable_v1 ON dbo.fe05_reinspection_conclusion_proposals_v1 AFTER INSERT,UPDATE,DELETE AS
 BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51997,''Conclusion proposal is immutable'',1;
 IF EXISTS(SELECT 1 FROM inserted p WHERE NOT EXISTS(
  SELECT 1 FROM dbo.fe05_reinspection_bindings_v1 b JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id
  JOIN dbo.cleaning_reworks w ON w.rework_id=b.rework_id JOIN dbo.room_cleaning_cycle_state c ON c.room_id=b.room_id
  WHERE b.inspection_id=p.inspection_id AND b.rework_id=p.rework_id AND b.branch_id=p.branch_id
   AND b.reinspector_user_id=p.actor_user_id AND i.inspector_user_id=p.actor_user_id
   AND i.row_version=p.inspection_revision AND b.row_version=p.binding_revision AND w.row_version=p.rework_revision
   AND c.cleaning_task_id=b.cleaning_task_id AND c.row_version=b.cycle_revision
   AND w.workflow_status=''PENDING_REINSPECTION'' AND i.workflow_status IN(''ASSIGNED'',''INSPECTING'',''PENDING_CONCLUSION'')))
 THROW 51997,''Proposal source mismatch'',1;
 IF EXISTS(SELECT 1 FROM inserted p JOIN dbo.fe05_reinspection_defect_results_v1 r ON r.inspection_id=p.inspection_id
  WHERE r.fixed IS NULL OR r.recorded_by<>p.actor_user_id OR r.recorded_at IS NULL)
 THROW 51997,''Incomplete proposal results'',1;
 END');
-- Independent protection of already-reviewed storage; trigger order does not matter:
-- a failure rolls back the entire statement, including other revision triggers.
EXEC(N'CREATE TRIGGER dbo.TR_fe05_proposal_result_freeze_v1 ON dbo.fe05_reinspection_defect_results_v1 AFTER INSERT,UPDATE,DELETE AS
 BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM dbo.fe05_reinspection_conclusion_proposals_v1 p JOIN
 (SELECT inspection_id FROM inserted UNION SELECT inspection_id FROM deleted) x ON x.inspection_id=p.inspection_id)
 THROW 51997,''Reviewed proposal results cannot be mutated'',1; END');
EXEC sys.sp_addextendedproperty @name=N'HotelOps.FE05.ReinspectionConclusionProposal',@value=N'FAST016_NOT_ACCEPTED';
COMMIT;
