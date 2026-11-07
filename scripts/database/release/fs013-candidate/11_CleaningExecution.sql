-- FS013-only reconstruction of the existing WorkTime model. No shared installation or migration.
IF DB_NAME()<>N'HotelOps_Canonical_20261106_f130001_Fresh' OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME() OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'')<>N'SQLEXPRESS' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS013.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_FS013') THROW 51960,'FS013 isolated target required',1;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.workdays') IS NOT NULL OR OBJECT_ID('dbo.work_intervals') IS NOT NULL OR OBJECT_ID('dbo.fs013_reassignment_operations') IS NOT NULL THROW 51960,'Existing execution schema; no overwrite',1;
CREATE TABLE dbo.workdays(
 workday_id bigint IDENTITY NOT NULL CONSTRAINT PK_workdays PRIMARY KEY,
 user_id bigint NOT NULL CONSTRAINT FK_workdays_user REFERENCES dbo.users(user_id),
 branch_id bigint NOT NULL CONSTRAINT FK_workdays_branch REFERENCES dbo.branches(branch_id),
 department_id bigint NOT NULL CONSTRAINT FK_workdays_department REFERENCES dbo.departments(department_id),
 work_date date NOT NULL,shift_assignment_id bigint NULL CONSTRAINT FK_workdays_assignment REFERENCES dbo.shift_assignments(shift_assignment_id),
 is_unscheduled bit NOT NULL,unscheduled_reason nvarchar(1000) NULL,
 status varchar(10) COLLATE Latin1_General_100_BIN2 NOT NULL CHECK(status IN('WORKING','ENDED')),
 started_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),ended_at datetime2(7) NULL,row_version rowversion NOT NULL,
 CONSTRAINT UQ_workdays_identity UNIQUE(workday_id,user_id,branch_id),
 CONSTRAINT CK_fs013_workday_time CHECK((status='WORKING' AND ended_at IS NULL) OR(status='ENDED' AND ended_at>=started_at)));
CREATE UNIQUE INDEX UX_workdays_one_working ON dbo.workdays(user_id) WHERE status='WORKING';
CREATE INDEX IX_workdays_user_date ON dbo.workdays(user_id,work_date,workday_id);
CREATE TABLE dbo.work_intervals(
 interval_id bigint IDENTITY NOT NULL CONSTRAINT PK_work_intervals PRIMARY KEY,
 workday_id bigint NOT NULL,user_id bigint NOT NULL,branch_id bigint NOT NULL,
 work_item_kind varchar(24) COLLATE Latin1_General_100_BIN2 NOT NULL CHECK(work_item_kind IN('CLEANING','REWORK','TASK')),
 work_item_id bigint NOT NULL,opened_by bigint NOT NULL CONSTRAINT FK_work_intervals_opened_by REFERENCES dbo.users(user_id),
 opened_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),closed_by bigint NULL CONSTRAINT FK_work_intervals_closed_by REFERENCES dbo.users(user_id),
 closed_at datetime2(7) NULL,effective_started_at datetime2(7) NOT NULL,effective_ended_at datetime2(7) NULL,row_version rowversion NOT NULL,
 CONSTRAINT FK_work_intervals_workday FOREIGN KEY(workday_id,user_id,branch_id) REFERENCES dbo.workdays(workday_id,user_id,branch_id),
 CONSTRAINT CK_fs013_interval_time CHECK((closed_at IS NULL AND closed_by IS NULL AND effective_ended_at IS NULL) OR(closed_at>=opened_at AND closed_by IS NOT NULL AND effective_ended_at>=effective_started_at)));
CREATE UNIQUE INDEX UX_work_intervals_one_open ON dbo.work_intervals(user_id) WHERE closed_at IS NULL;
CREATE INDEX IX_work_intervals_user_time ON dbo.work_intervals(user_id,effective_started_at,interval_id);
CREATE INDEX IX_work_intervals_item ON dbo.work_intervals(work_item_kind,work_item_id,interval_id);
-- Existing FS012 readiness table only permitted the UC028 event. Extend this new trial, preserving old trials.
DECLARE @check sysname=(SELECT name FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.room_readiness_events') AND definition LIKE '%PENDING_CLEANING%');
IF @check IS NULL THROW 51960,'Readiness prerequisite missing',1;
DECLARE @dropCheck nvarchar(max)=N'ALTER TABLE dbo.room_readiness_events DROP CONSTRAINT '+QUOTENAME(@check);
EXEC(@dropCheck);
ALTER TABLE dbo.room_readiness_events ADD CONSTRAINT CK_fs013_readiness CHECK(readiness IN('PENDING_CLEANING','CLEANING_IN_PROGRESS'));
CREATE TABLE dbo.fs013_reassignment_operations(
 actor_user_id bigint NOT NULL REFERENCES dbo.users(user_id),operation_key uniqueidentifier NOT NULL,
 fingerprint binary(32) NOT NULL,result_json nvarchar(max) NOT NULL CHECK(ISJSON(result_json)=1),
 task_id bigint NOT NULL REFERENCES dbo.cleaning_tasks(cleaning_task_id),branch_id bigint NOT NULL REFERENCES dbo.branches(branch_id),
 previous_assignment_id bigint NOT NULL REFERENCES dbo.cleaning_assignments(assignment_id),assignment_id bigint NOT NULL UNIQUE REFERENCES dbo.cleaning_assignments(assignment_id),
 occurred_at datetime2(7) NOT NULL,CONSTRAINT PK_fs013_reassignment PRIMARY KEY(actor_user_id,operation_key));
CREATE TABLE dbo.fs013_reassignment_intents(
 intent_id bigint IDENTITY PRIMARY KEY,actor_user_id bigint NOT NULL,operation_key uniqueidentifier NOT NULL,
 user_id bigint NOT NULL REFERENCES dbo.users(user_id),recipient_kind varchar(8) NOT NULL CHECK(recipient_kind IN('PREVIOUS','NEW')),
 status varchar(10) NOT NULL DEFAULT 'PENDING' CHECK(status IN('PENDING','DELIVERED','TERMINAL')),
 attempt_count int NOT NULL DEFAULT 0,next_attempt_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),failure_code varchar(60) NULL,
 inbox_notification_id bigint NULL REFERENCES dbo.notifications(notification_id),
 CONSTRAINT FK_fs013_intent_operation FOREIGN KEY(actor_user_id,operation_key) REFERENCES dbo.fs013_reassignment_operations(actor_user_id,operation_key),
 CONSTRAINT UQ_fs013_reassignment_recipient UNIQUE(actor_user_id,operation_key,user_id),
 CONSTRAINT CK_fs013_reassignment_delivery CHECK((status='DELIVERED' AND inbox_notification_id IS NOT NULL) OR(status<>'DELIVERED' AND inbox_notification_id IS NULL)));
EXEC sys.sp_addextendedproperty @name=N'HotelOps.FS013.Lifecycle',@value=N'V1';
COMMIT;
GO
CREATE OR ALTER PROCEDURE dbo.fs013_verify_reassignment @actor bigint,@operation uniqueidentifier,@mutation bit=0
AS
BEGIN
 SET NOCOUNT ON;
 IF @@TRANCOUNT<>1 OR XACT_STATE()<>1 THROW 51961,'Caller transaction required',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.fs013_reassignment_operations o
 JOIN dbo.cleaning_assignments a ON a.assignment_id=o.assignment_id
 JOIN dbo.cleaning_assignments p ON p.assignment_id=o.previous_assignment_id
 JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=o.task_id
 WHERE o.actor_user_id=@actor AND o.operation_key=@operation AND a.cleaning_task_id=t.cleaning_task_id AND p.cleaning_task_id=t.cleaning_task_id
 AND a.assigned_by=@actor AND a.assigned_at=o.occurred_at AND p.ended_at=o.occurred_at AND p.assignment_status='REASSIGNED' AND a.assignee_user_id<>p.assignee_user_id
 AND EXISTS(SELECT 1 FROM dbo.audit_logs l WHERE l.entity_type='cleaning_tasks' AND l.entity_id=CONVERT(nvarchar(30),t.cleaning_task_id) AND l.actor_user_id=@actor
 AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(l.new_values,'$.operationId'))=@operation AND TRY_CONVERT(bigint,JSON_VALUE(l.new_values,'$.assignmentId'))=a.assignment_id)
 AND EXISTS(SELECT 1 FROM dbo.fs013_reassignment_intents i WHERE i.actor_user_id=@actor AND i.operation_key=@operation AND i.user_id=p.assignee_user_id AND i.recipient_kind='PREVIOUS')
 AND EXISTS(SELECT 1 FROM dbo.fs013_reassignment_intents i WHERE i.actor_user_id=@actor AND i.operation_key=@operation AND i.user_id=a.assignee_user_id AND i.recipient_kind='NEW')
 AND (@mutation=0 OR(a.ended_at IS NULL AND a.assignment_status='ACTIVE' AND t.workflow_status='ASSIGNED' AND t.status='PENDING'
 AND JSON_VALUE(o.result_json,'$.sourceETag')='"'+CONVERT(varchar(16),t.row_version,2)+'"'
 AND NOT EXISTS(SELECT 1 FROM dbo.work_intervals w WHERE w.work_item_kind='CLEANING' AND w.work_item_id=t.cleaning_task_id AND w.closed_at IS NULL)))) THROW 51962,'Reassignment persisted proof incomplete',1;
 IF(SELECT COUNT(*) FROM dbo.fs013_reassignment_intents WHERE actor_user_id=@actor AND operation_key=@operation)<>2 THROW 51962,'Two distinct recipients required',1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.fs013_reassign_with_evidence
 @actor bigint,@branch bigint,@task bigint,@previous bigint,@assignee bigint,@operation uniqueidentifier,@fingerprint binary(32),@expected binary(8),@due datetime2(7),@reason nvarchar(500),@closedInterval bigint=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF DB_NAME()<>'HotelOps_Canonical_20261106_f130001_Fresh' OR @@TRANCOUNT<>1 OR XACT_STATE()<>1 THROW 51961,'FS013 caller transaction required',1;
 IF @due IS NULL OR @due<=SYSUTCDATETIME() OR LEN(LTRIM(RTRIM(@reason)))<10 OR @fingerprint IS NULL OR @operation IS NULL THROW 51963,'Invalid reassignment',1;
 DECLARE @lock int,@oldUser bigint,@room bigint,@oldState varchar(30),@now datetime2(7)=SYSUTCDATETIME();
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:RoomTypes:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
 IF @lock<0 THROW 51963,'Catalog lock unavailable',1;
 SELECT @oldUser=a.assignee_user_id,@room=t.room_id,@oldState=t.workflow_status FROM dbo.cleaning_tasks t WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.cleaning_assignments a ON a.cleaning_task_id=t.cleaning_task_id JOIN dbo.rooms r ON r.room_id=t.room_id
 WHERE t.cleaning_task_id=@task AND t.row_version=@expected AND t.workflow_status IN('ASSIGNED','IN_PROGRESS','ON_HOLD')
 AND r.branch_id=@branch AND r.is_active=1 AND a.assignment_id=@previous AND a.assignment_status='ACTIVE' AND a.ended_at IS NULL;
 IF @oldUser IS NULL OR @oldUser=@assignee OR(SELECT COUNT(*) FROM dbo.cleaning_assignments WHERE cleaning_task_id=@task AND assignment_status='ACTIVE' AND ended_at IS NULL)<>1 THROW 51963,'Stale reassignment source',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.room_cleaning_cycle_state WHERE room_id=@room AND cleaning_task_id=@task) THROW 51963,'Current cycle required',1;
 IF EXISTS(SELECT 1 FROM dbo.work_intervals WHERE work_item_kind='CLEANING' AND work_item_id=@task AND closed_at IS NULL) THROW 51963,'Close previous interval first in caller transaction',1;
 IF @oldState='IN_PROGRESS'
 BEGIN
  SELECT @now=closed_at FROM dbo.work_intervals WHERE interval_id=@closedInterval AND user_id=@oldUser AND branch_id=@branch AND work_item_kind='CLEANING' AND work_item_id=@task AND closed_by=@actor AND closed_at IS NOT NULL;
  IF @@ROWCOUNT<>1 THROW 51963,'Closed interval proof required',1;
 END;
 UPDATE dbo.cleaning_assignments SET assignment_status='REASSIGNED',ended_at=@now WHERE assignment_id=@previous;
 INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status,reason) VALUES(@task,@assignee,@actor,@now,'ACTIVE',@reason);
 DECLARE @assignment bigint=CONVERT(bigint,SCOPE_IDENTITY());
 UPDATE dbo.cleaning_tasks SET workflow_status='ASSIGNED',status='PENDING',due_at=@due WHERE cleaning_task_id=@task;
 DECLARE @tag varchar(18)=(SELECT '"'+CONVERT(varchar(16),row_version,2)+'"' FROM dbo.cleaning_tasks WHERE cleaning_task_id=@task),@result nvarchar(max);
 SELECT @result=(SELECT @task AS id,@branch AS branchId,@previous AS previousAssignmentId,@assignment AS assignmentId,@oldUser AS previousAssigneeId,@assignee AS assigneeId,@operation AS operationId,'ASSIGNED' AS state,'PENDING_DELIVERY' AS notification,@tag AS sourceETag,'PROVISIONAL_NOT_ACCEPTED' AS contract FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,old_values,new_values,changed_fields,occurred_at)
 VALUES(@branch,@actor,'cleaning_tasks',CONVERT(nvarchar(30),@task),'UPDATE',
 (SELECT @previous AS assignmentId,@oldUser AS assigneeId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 (SELECT @operation AS operationId,@assignment AS assignmentId,@reason AS reason,JSON_QUERY(@result) AS entity FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'["reassign","assignee","workflowStatus","dueAt","workInterval","readiness"]',@now);
 INSERT dbo.fs013_reassignment_operations VALUES(@actor,@operation,@fingerprint,@result,@task,@branch,@previous,@assignment,@now);
 INSERT dbo.fs013_reassignment_intents(actor_user_id,operation_key,user_id,recipient_kind) VALUES(@actor,@operation,@oldUser,'PREVIOUS'),(@actor,@operation,@assignee,'NEW');
 INSERT dbo.room_readiness_events(room_id,cleaning_task_id,readiness,has_effective_block,actor_user_id,occurred_at)
 VALUES(@room,@task,'PENDING_CLEANING',CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.room_blocks WHERE room_id=@room AND released_at IS NULL) THEN 1 ELSE 0 END),@actor,@now);
 EXEC dbo.fs013_verify_reassignment @actor,@operation,1;
 SELECT @result AS [Value];
END;
GO
DENY INSERT,UPDATE,DELETE ON dbo.fs013_reassignment_operations TO fs013_app;
DENY INSERT,DELETE ON dbo.fs013_reassignment_intents TO fs013_app;
GRANT UPDATE(status,attempt_count,next_attempt_at,failure_code,inbox_notification_id) ON dbo.fs013_reassignment_intents TO fs013_app;
GRANT EXECUTE ON dbo.fs013_reassign_with_evidence TO fs013_app;
GRANT EXECUTE ON dbo.fs013_verify_reassignment TO fs013_app;
