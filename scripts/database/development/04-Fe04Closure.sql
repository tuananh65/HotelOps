-- FE04 closure foundation. Guarded recovery installer only; no legacy records or grants are synthesized.
SET XACT_ABORT ON;
IF OBJECT_ID('dbo.notification_operation_reservations') IS NOT NULL OR OBJECT_ID('dbo.notification_publications') IS NOT NULL OR OBJECT_ID('dbo.notification_delivery_intents') IS NOT NULL
 THROW 51990,'Existing outbox requires explicit reconciliation; no overwrite',1;
IF OBJECT_ID('dbo.fs014_cleaning_blocker_resolutions') IS NULL THROW 51990,'Resume prerequisite missing',1;
CREATE TABLE dbo.notification_operation_reservations(
 actor_user_id bigint NOT NULL REFERENCES dbo.users(user_id),operation_key uniqueidentifier NOT NULL,
 fingerprint_version int NOT NULL CHECK(fingerprint_version=1),fingerprint binary(32) NOT NULL,
 completed bit NOT NULL DEFAULT 0,result_json nvarchar(max) NULL,
 created_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),row_version rowversion NOT NULL,
 CONSTRAINT PK_notification_operation_v1 PRIMARY KEY(actor_user_id,operation_key),
 CONSTRAINT CK_notification_result_v1 CHECK((completed=0 AND result_json IS NULL) OR(completed=1 AND result_json IS NOT NULL AND ISJSON(result_json)=1)));
CREATE TABLE dbo.notification_publications(
 publication_id uniqueidentifier NOT NULL PRIMARY KEY,
 actor_user_id bigint NOT NULL,operation_key uniqueidentifier NOT NULL,
 event_type varchar(60) NOT NULL CHECK(event_type='CLEANING_TASK_ASSIGNED'),schema_version int NOT NULL CHECK(schema_version=1),
 assignment_id bigint NOT NULL UNIQUE REFERENCES dbo.cleaning_assignments(assignment_id),
 source_revision binary(8) NOT NULL,payload_json nvarchar(max) NOT NULL CHECK(ISJSON(payload_json)=1),created_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_publication_operation_v1 FOREIGN KEY(actor_user_id,operation_key) REFERENCES dbo.notification_operation_reservations(actor_user_id,operation_key));
CREATE TABLE dbo.notification_delivery_intents(
 intent_id bigint IDENTITY PRIMARY KEY,publication_id uniqueidentifier NOT NULL REFERENCES dbo.notification_publications(publication_id),
 user_id bigint NOT NULL REFERENCES dbo.users(user_id),delivery_channel varchar(30) NOT NULL CHECK(delivery_channel='IN_APP'),
 status varchar(30) NOT NULL DEFAULT 'PENDING' CHECK(status IN('PENDING','DELIVERED','TERMINAL')),
 attempt_count int NOT NULL DEFAULT 0 CHECK(attempt_count>=0),next_attempt_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 claim_id uniqueidentifier NULL,claimed_until datetime2(7) NULL,failure_code varchar(60) NULL,
 inbox_notification_id bigint NULL,row_version rowversion NOT NULL,
 CONSTRAINT UQ_delivery_publication_recipient_v1 UNIQUE(publication_id,user_id,delivery_channel),
 CONSTRAINT CK_delivery_inbox_v1 CHECK((status='DELIVERED' AND inbox_notification_id IS NOT NULL) OR(status<>'DELIVERED' AND inbox_notification_id IS NULL)),
 CONSTRAINT CK_delivery_claim_v1 CHECK((claim_id IS NULL AND claimed_until IS NULL) OR(claim_id IS NOT NULL AND claimed_until IS NOT NULL)),
 CONSTRAINT FK_delivery_inbox_v1 FOREIGN KEY(inbox_notification_id,user_id,delivery_channel) REFERENCES dbo.notification_recipients(notification_id,user_id,delivery_channel));

IF COL_LENGTH('dbo.notification_publications','occurrence_key') IS NOT NULL THROW 51980,'No overwrite of closure schema',1;
DECLARE @eventCheck sysname=(SELECT name FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.notification_publications') AND definition LIKE '%CLEANING_TASK_ASSIGNED%');
IF @eventCheck IS NULL THROW 51980,'Existing candidate publication contract required',1;
DECLARE @ddl nvarchar(max)=N'ALTER TABLE dbo.notification_publications DROP CONSTRAINT '+QUOTENAME(@eventCheck);
EXEC(@ddl);
ALTER TABLE dbo.notification_publications ADD CONSTRAINT CK_fs015_publication_event CHECK(event_type IN('CLEANING_TASK_ASSIGNED','CLEANING_BLOCKER_REPORTED','CLEANING_TASK_COMPLETED'));
DECLARE @assignmentUnique sysname=(SELECT k.name FROM sys.key_constraints k WHERE k.parent_object_id=OBJECT_ID('dbo.notification_publications') AND k.type='UQ'
 AND EXISTS(SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=k.parent_object_id AND ic.index_id=k.unique_index_id AND c.name='assignment_id'));
IF @assignmentUnique IS NULL THROW 51980,'Assignment publication unique key required',1;
SET @ddl=N'ALTER TABLE dbo.notification_publications DROP CONSTRAINT '+QUOTENAME(@assignmentUnique);
EXEC(@ddl);
CREATE UNIQUE INDEX UX_fs015_assignment_publication ON dbo.notification_publications(assignment_id) WHERE event_type='CLEANING_TASK_ASSIGNED';
ALTER TABLE dbo.notification_publications ADD occurrence_key varchar(120) NULL;
EXEC(N'CREATE UNIQUE INDEX UX_fs015_occurrence ON dbo.notification_publications(occurrence_key) WHERE occurrence_key IS NOT NULL;');
EXEC(N'ALTER TABLE dbo.notification_publications ADD CONSTRAINT CK_fs015_occurrence CHECK(event_type=''CLEANING_TASK_ASSIGNED'' OR occurrence_key IS NOT NULL);');
ALTER TABLE dbo.room_readiness_events DROP CONSTRAINT CK_dev3_readiness_start;
ALTER TABLE dbo.room_readiness_events ADD CONSTRAINT CK_dev3_readiness_start CHECK(readiness IN('PENDING_CLEANING','CLEANING_IN_PROGRESS','PENDING_INSPECTION'));
-- Completion actor/time is durable in operation result + task audit + completion progress log.
-- Reuse FS014 blocker state; no additional blocker table, roles or permission grants.

IF OBJECT_ID('dbo.fe04_cleaner_attributions') IS NOT NULL
 OR OBJECT_ID('dbo.fe04_capture_cleaner_attribution') IS NOT NULL
 THROW 51995,'Fresh candidate required; no overwrite',1;

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

CREATE UNIQUE INDEX UX_fs009_publication_operation ON dbo.notification_publications(actor_user_id,operation_key);
GO
CREATE TRIGGER dbo.TR_fs009_publication_immutable ON dbo.notification_publications AFTER UPDATE,DELETE AS
BEGIN
 IF EXISTS(SELECT 1 FROM deleted) THROW 51913,'Publication is immutable',1;
END;
GO
CREATE TRIGGER dbo.TR_fs009_operation_immutable ON dbo.notification_operation_reservations AFTER UPDATE,DELETE AS
BEGIN
 IF EXISTS(SELECT 1 FROM deleted WHERE completed=1) THROW 51914,'Completed operation is immutable',1;
END;
GO
CREATE TRIGGER dbo.TR_dev3_closure_publication_source ON dbo.notification_publications AFTER INSERT AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted p WHERE NOT EXISTS(
  SELECT 1 FROM dbo.cleaning_assignments a JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=a.cleaning_task_id
  JOIN dbo.rooms r ON r.room_id=t.room_id
  JOIN dbo.notification_operation_reservations o ON o.actor_user_id=p.actor_user_id AND o.operation_key=p.operation_key
  WHERE a.assignment_id=p.assignment_id AND p.source_revision=t.row_version AND o.completed=1
   AND ((p.event_type='CLEANING_TASK_ASSIGNED' AND TRY_CONVERT(datetime2(7),JSON_VALUE(p.payload_json,'$.dueAtUtc'))=t.due_at)
    OR (p.event_type IN('CLEANING_BLOCKER_REPORTED','CLEANING_TASK_COMPLETED')
     AND a.assignee_user_id=p.actor_user_id AND a.assignment_status='ACTIVE' AND a.ended_at IS NULL
     AND JSON_VALUE(p.payload_json,'$.sourceETag')=CHAR(34)+CONVERT(varchar(16),p.source_revision,2)+CHAR(34)
     AND JSON_VALUE(o.result_json,'$.sourceETag')=JSON_VALUE(p.payload_json,'$.sourceETag')
     AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(o.result_json,'$.operationId'))=p.operation_key
     AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(o.result_json,'$.publicationId'))=p.publication_id
     AND TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.id'))=t.cleaning_task_id
     AND TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.branchId'))=r.branch_id
     AND TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.assignmentId'))=a.assignment_id
     AND TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.taskId'))=t.cleaning_task_id
     AND TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.roomId'))=r.room_id
     AND TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.branchId'))=r.branch_id
     AND TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.actorId'))=p.actor_user_id
     AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(p.payload_json,'$.operationId'))=p.operation_key
     AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(p.payload_json,'$.publicationId'))=p.publication_id
     AND TRY_CONVERT(datetime2(7),JSON_VALUE(o.result_json,'$.occurredAtUtc'))=p.created_at
     AND t.workflow_status=CASE p.event_type WHEN 'CLEANING_BLOCKER_REPORTED' THEN 'ON_HOLD' ELSE 'PENDING_INSPECTION' END
     AND JSON_VALUE(o.result_json,'$.state')=t.workflow_status
     AND EXISTS(SELECT 1 FROM dbo.cleaning_progress_logs g WHERE g.cleaning_task_id=t.cleaning_task_id
       AND g.progress_id=TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.progressId'))
       AND g.progress_id=TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.progressId'))
       AND g.logged_by=p.actor_user_id AND g.logged_at=p.created_at
       AND g.status=CASE p.event_type WHEN 'CLEANING_BLOCKER_REPORTED' THEN 'BLOCKED' ELSE 'COMPLETED' END)
     AND EXISTS(SELECT 1 FROM dbo.work_intervals w WHERE w.interval_id=TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.closedIntervalId'))
       AND w.work_item_kind='CLEANING' AND w.work_item_id=t.cleaning_task_id AND w.user_id=p.actor_user_id
       AND w.branch_id=r.branch_id AND w.closed_at=p.created_at AND w.closed_by=p.actor_user_id)
     AND EXISTS(SELECT 1 FROM dbo.audit_logs l WHERE l.actor_user_id=p.actor_user_id AND l.branch_id=r.branch_id
       AND l.entity_type='cleaning_tasks' AND l.entity_id=CONVERT(nvarchar(30),t.cleaning_task_id) AND l.action='UPDATE'
       AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(CASE WHEN ISJSON(l.new_values)=1 THEN l.new_values ELSE N'{}' END,'$.operationId'))=p.operation_key
       AND JSON_VALUE(CASE WHEN ISJSON(l.new_values)=1 THEN l.new_values ELSE N'{}' END,'$.action')=
         CASE p.event_type WHEN 'CLEANING_BLOCKER_REPORTED' THEN 'REPORT_CLEANING_BLOCKER' ELSE 'COMPLETE_CLEANING' END)
    ))
 )) THROW 51915,'Publication lacks exact persisted source/interval/audit evidence',1;
END;