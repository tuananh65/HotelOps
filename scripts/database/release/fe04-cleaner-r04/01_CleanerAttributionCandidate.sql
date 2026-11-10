-- REVIEW CANDIDATE ONLY. Do not run on shared SQL. No historical backfill or runtime grants.
SET XACT_ABORT ON;
IF DB_NAME() IN(N'master',N'model',N'msdb',N'tempdb')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
   AND name=N'HotelOps.FE04.UnblockR01.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_DISPOSABLE')
 THROW 51995,'Separately approved disposable target required',1;
IF OBJECT_ID('dbo.fe04_cleaner_attributions') IS NOT NULL
 OR OBJECT_ID('dbo.fe04_capture_cleaner_attribution') IS NOT NULL
 THROW 51995,'Fresh candidate required; no overwrite',1;
BEGIN TRANSACTION;
CREATE TABLE dbo.fe04_cleaner_attributions(
 cleaning_task_id bigint NOT NULL PRIMARY KEY REFERENCES dbo.cleaning_tasks(cleaning_task_id),
 room_id bigint NOT NULL REFERENCES dbo.rooms(room_id),
 branch_id bigint NOT NULL REFERENCES dbo.branches(branch_id),
 assignment_id bigint NOT NULL REFERENCES dbo.cleaning_assignments(assignment_id),
 cleaner_user_id bigint NOT NULL REFERENCES dbo.users(user_id),
 completion_progress_id bigint NOT NULL UNIQUE REFERENCES dbo.cleaning_progress_logs(progress_id),
 completed_at datetime2(7) NOT NULL, completion_revision binary(8) NOT NULL,
 writer_transaction_id bigint NOT NULL CHECK(writer_transaction_id>0),
 contract_version int NOT NULL DEFAULT(1) CHECK(contract_version=1));
EXEC(N'CREATE TRIGGER dbo.TR_fe04_cleaner_immutable ON dbo.fe04_cleaner_attributions AFTER UPDATE,DELETE AS
 BEGIN SET NOCOUNT ON; THROW 51995,''Original cleaner attribution is immutable'',1; END');
EXEC(N'CREATE PROCEDURE dbo.fe04_capture_cleaner_attribution
 @task bigint,@assignment bigint,@cleaner bigint,@progress bigint
 WITH EXECUTE AS OWNER AS
 BEGIN
 SET NOCOUNT ON;
 SET XACT_ABORT ON;
 IF @@TRANCOUNT=0 OR NOT EXISTS(SELECT 1 FROM sys.dm_exec_sessions
   WHERE session_id=@@SPID AND transaction_isolation_level=4)
   THROW 51995,''Caller-owned serializable completion transaction required'',1;
 -- No independent commit. All writes roll back with completion/audit/notification.
 -- A different assignee anywhere in original assignment/progress history is ambiguous.
 -- Do not elect the latest assignment, assigned_by, or final log actor.
 IF EXISTS(SELECT 1 FROM dbo.fe04_cleaner_attributions WITH(UPDLOCK,HOLDLOCK) WHERE cleaning_task_id=@task)
   THROW 51995,''Attribution already exists; do not replace original cleaner'',1;
 IF EXISTS(SELECT 1 FROM dbo.cleaning_assignments WITH(HOLDLOCK) WHERE cleaning_task_id=@task AND assignee_user_id<>@cleaner)
   OR EXISTS(SELECT 1 FROM dbo.cleaning_progress_logs WITH(HOLDLOCK) WHERE cleaning_task_id=@task AND logged_by<>@cleaner)
   RETURN;
 INSERT dbo.fe04_cleaner_attributions(cleaning_task_id,room_id,branch_id,assignment_id,cleaner_user_id,
   completion_progress_id,completed_at,completion_revision,writer_transaction_id)
 SELECT t.cleaning_task_id,t.room_id,r.branch_id,a.assignment_id,a.assignee_user_id,p.progress_id,
   t.actual_end_at,t.row_version,CURRENT_TRANSACTION_ID()
 FROM dbo.cleaning_tasks t WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=t.room_id AND r.is_active=1
 JOIN dbo.room_cleaning_cycle_state c WITH(UPDLOCK,HOLDLOCK) ON c.room_id=t.room_id AND c.cleaning_task_id=t.cleaning_task_id
 JOIN dbo.cleaning_assignments a WITH(HOLDLOCK) ON a.cleaning_task_id=t.cleaning_task_id
 JOIN dbo.cleaning_progress_logs p WITH(HOLDLOCK) ON p.cleaning_task_id=t.cleaning_task_id
 JOIN dbo.users u WITH(HOLDLOCK) ON u.user_id=a.assignee_user_id AND u.deleted_at IS NULL
 WHERE t.cleaning_task_id=@task AND t.workflow_status=''PENDING_INSPECTION'' AND t.actual_start_at IS NOT NULL
   AND t.actual_end_at>=t.actual_start_at AND a.assignment_id=@assignment AND a.assignee_user_id=@cleaner
   AND a.assignment_status=''ACTIVE'' AND a.ended_at IS NULL AND a.assigned_at<=t.actual_end_at
   AND p.progress_id=@progress AND p.status=''COMPLETED'' AND p.progress_percent=100
   AND p.logged_by=@cleaner AND p.logged_at=t.actual_end_at
   AND (SELECT COUNT(*) FROM dbo.cleaning_assignments ca WITH(HOLDLOCK) WHERE ca.cleaning_task_id=@task
     AND ca.assignment_status=''ACTIVE'' AND ca.ended_at IS NULL)=1;
 IF @@ROWCOUNT<>1 THROW 51995,''Exact current completion source required'',1;
 END');
DENY INSERT,UPDATE,DELETE ON dbo.fe04_cleaner_attributions TO public;
-- Deployment must separately approve an EXECUTE grant to the completion writer identity.
-- No data repair, inferred historical INSERT, automatic role seed, or consumer authority change.
EXEC sys.sp_addextendedproperty @name=N'HotelOps.FE04.CleanerAttribution',@value=N'R04_V1';
COMMIT;
