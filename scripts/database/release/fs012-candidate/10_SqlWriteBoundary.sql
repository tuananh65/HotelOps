-- FS012 trial only. No login, server grants, global trigger or generated EF mapping change.
IF DB_NAME()<>N'HotelOps_Canonical_20261106_f120001_Fresh' OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME() OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'')<>N'SQLEXPRESS' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS012.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_FS012') THROW 51940,'FS012 isolated target required',1;
GO
CREATE OR ALTER PROCEDURE dbo.fs012_assign_with_evidence
 @actor bigint,@branch bigint,@task bigint,@assignee bigint,@operation uniqueidentifier,
 @fingerprint binary(32),@expected binary(8),@assignedAt datetime2(7),@dueAt datetime2(7),@reason nvarchar(500)
AS
BEGIN
 SET NOCOUNT ON;
 SET XACT_ABORT ON;
 IF DB_NAME()<>N'HotelOps_Canonical_20261106_f120001_Fresh' THROW 51940,'FS012 isolated target required',1;
 IF @@TRANCOUNT<>1 OR XACT_STATE()<>1 THROW 51941,'One committable caller transaction required',1;
 IF @actor IS NULL OR @actor<=0 OR @branch IS NULL OR @branch<=0 OR @assignee IS NULL OR @assignee<=0 OR @operation IS NULL OR @operation='00000000-0000-0000-0000-000000000000' OR @fingerprint IS NULL OR @expected IS NULL OR @assignedAt IS NULL OR @dueAt IS NULL OR @dueAt<=SYSUTCDATETIME() OR NULLIF(LTRIM(RTRIM(@reason)),N'') IS NULL THROW 51942,'Invalid assignment command',1;
 -- Authorization/session/shift policy remains in the service. This SQL principal is a trusted app writer.
 DECLARE @room bigint,@revision binary(8),@state varchar(30),@lock int;
 -- Match BatchAsync lock order for both EF callers and direct approved-module callers.
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:RoomTypes:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
 IF @lock<0 THROW 51943,'Assignment lock unavailable',1;
 SELECT @room=t.room_id,@revision=t.row_version,@state=t.workflow_status
 FROM dbo.cleaning_tasks t WITH(UPDLOCK,HOLDLOCK) JOIN dbo.rooms r ON r.room_id=t.room_id
 WHERE t.cleaning_task_id=@task AND r.branch_id=@branch AND r.is_active=1;
 IF @room IS NULL OR @revision<>@expected OR @state<>'PENDING_ASSIGNMENT' OR @state IS NULL THROW 51944,'Stale or ineligible source',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.room_cleaning_cycle_state WITH(UPDLOCK,HOLDLOCK) WHERE room_id=@room AND cleaning_task_id=@task) THROW 51944,'Current cycle required',1;
 IF EXISTS(SELECT 1 FROM dbo.cleaning_assignments WITH(UPDLOCK,HOLDLOCK) WHERE cleaning_task_id=@task AND assignment_status='ACTIVE' AND ended_at IS NULL) THROW 51945,'Active assignment exists',1;
 IF EXISTS(SELECT 1 FROM dbo.notification_operation_reservations WITH(UPDLOCK,HOLDLOCK) WHERE actor_user_id=@actor AND operation_key=@operation) THROW 51946,'Operation already reserved; use authorized durable replay',1;
 INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status,reason)
 VALUES(@task,@assignee,@actor,@assignedAt,'ACTIVE',@reason);
 DECLARE @assignment bigint=CONVERT(bigint,SCOPE_IDENTITY()),@publication uniqueidentifier=NEWID();
 UPDATE dbo.cleaning_tasks SET workflow_status='ASSIGNED',status='PENDING',due_at=@dueAt WHERE cleaning_task_id=@task;
 SELECT @revision=row_version FROM dbo.cleaning_tasks WHERE cleaning_task_id=@task;
 DECLARE @tag varchar(18)='"'+CONVERT(varchar(16),@revision,2)+'"',@result nvarchar(max),@payload nvarchar(max);
 SELECT @result=(SELECT @task AS id,@assignment AS assignmentId,@branch AS branchId,@assignee AS assigneeUserId,@operation AS operationId,@publication AS publicationId,'ASSIGNED' AS workflowState,'PENDING_DELIVERY' AS notification,@tag AS sourceETag,'PROVISIONAL_NOT_ACCEPTED' AS contract FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 SELECT @payload=(SELECT @operation AS operationId,@publication AS publicationId,@actor AS actorId,@branch AS branchId,@task AS taskId,@task AS taskCycleId,@room AS roomId,@assignment AS assignmentId,@assignee AS assigneeUserId,@assignedAt AS assignedAtUtc,@tag AS sourceRevision,@dueAt AS dueAtUtc FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,old_values,new_values,changed_fields,occurred_at)
 VALUES(@branch,@actor,'cleaning_tasks',CONVERT(nvarchar(30),@task),'UPDATE',N'{"state":"PENDING_ASSIGNMENT"}',
 (SELECT @assignment AS assignmentId,@operation AS operationId,JSON_QUERY(@result) AS entity FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'["assignee","workflowStatus","dueAt"]',@assignedAt);
 INSERT dbo.notification_operation_reservations(actor_user_id,operation_key,fingerprint_version,fingerprint,completed,result_json,created_at) VALUES(@actor,@operation,1,@fingerprint,1,@result,@assignedAt);
 INSERT dbo.notification_publications(publication_id,actor_user_id,operation_key,event_type,schema_version,assignment_id,source_revision,payload_json,created_at) VALUES(@publication,@actor,@operation,'CLEANING_TASK_ASSIGNED',1,@assignment,@revision,@payload,@assignedAt);
 INSERT dbo.notification_delivery_intents(publication_id,user_id,delivery_channel,next_attempt_at) VALUES(@publication,@assignee,'IN_APP',@assignedAt);
 EXEC dbo.fs009_verify_assignment_guard @actor,@operation;
 SELECT @result AS [Value];
 -- No COMMIT/ROLLBACK: all five records live or die with the caller's transaction.
END;
GO
-- Deliberately ungranted developer canary: a module owned by dbo can chain writes.
-- The proof must show the restricted principal cannot execute arbitrary such modules.
CREATE OR ALTER PROCEDURE dbo.fs012_unapproved_writer_probe
AS UPDATE dbo.cleaning_assignments SET reason=N'FS012_UNAPPROVED_WRITER_PROBE';
GO
-- Dedicated NOLOGIN principal is exercised using an administrator's test-only EXECUTE AS.
-- This does not deploy an application login or change any existing runtime identity.
IF USER_ID(N'fs012_app') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS012.SqlBoundary') THROW 51947,'Unknown existing principal',1;
IF USER_ID(N'fs012_app') IS NULL CREATE USER fs012_app WITHOUT LOGIN;
GRANT SELECT ON SCHEMA::dbo TO fs012_app;
GRANT VIEW DEFINITION TO fs012_app;
GRANT EXECUTE ON dbo.fs012_assign_with_evidence TO fs012_app;
GRANT EXECUTE ON dbo.fs009_verify_assignment_guard TO fs012_app;
DENY INSERT,UPDATE,DELETE ON dbo.cleaning_assignments TO fs012_app;
DENY INSERT,UPDATE,DELETE ON dbo.notification_operation_reservations TO fs012_app;
DENY INSERT,UPDATE,DELETE ON dbo.notification_publications TO fs012_app;
DENY INSERT,DELETE ON dbo.notification_delivery_intents TO fs012_app;
GRANT UPDATE(status,attempt_count,next_attempt_at,claim_id,claimed_until,failure_code,inbox_notification_id) ON dbo.notification_delivery_intents TO fs012_app;
DENY UPDATE,DELETE ON dbo.audit_logs TO fs012_app;
GRANT INSERT ON dbo.audit_logs TO fs012_app;
-- Existing UC028/UC032 writes remain EF commands. The SQL boundary protects Assignment creation.
GRANT INSERT,UPDATE ON dbo.cleaning_tasks TO fs012_app;
GRANT INSERT,UPDATE ON dbo.room_cleaning_cycle_state TO fs012_app;
GRANT INSERT ON dbo.room_readiness_events TO fs012_app;
GRANT INSERT,UPDATE ON dbo.cleaning_progress_versions TO fs012_app;
GRANT INSERT,UPDATE ON dbo.cleaning_task_items TO fs012_app;
GRANT INSERT ON dbo.cleaning_progress_logs TO fs012_app;
GRANT INSERT ON dbo.progress_evidence_links TO fs012_app;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS012.SqlBoundary') EXEC sys.sp_addextendedproperty @name=N'HotelOps.FS012.SqlBoundary',@value=N'V1';
