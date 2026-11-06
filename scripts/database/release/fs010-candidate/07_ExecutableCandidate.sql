-- PROVISIONAL_NOT_ACCEPTED. Exact disposable target only, no event catalog table exists in canonical SQL.
IF DB_NAME()<>N'HotelOps_Canonical_20261106_f100001_Fresh' OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME() OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'')<>N'SQLEXPRESS' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS010.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_FS010') THROW 51909,'FS010 isolated target required',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_publications') AND name='UX_fs009_publication_operation') CREATE UNIQUE INDEX UX_fs009_publication_operation ON dbo.notification_publications(actor_user_id,operation_key);
GO
CREATE OR ALTER PROCEDURE dbo.fs009_verify_assignment_guard @actor bigint,@operation uniqueidentifier
AS
BEGIN
 SET NOCOUNT ON;
 IF @@TRANCOUNT<>1 OR XACT_STATE()<>1 THROW 51910,'Single caller-owned physical transaction required',1;
 IF NOT EXISTS(
 SELECT 1 FROM dbo.notification_operation_reservations o
 JOIN dbo.notification_publications p ON p.actor_user_id=o.actor_user_id AND p.operation_key=o.operation_key
 JOIN dbo.cleaning_assignments a ON a.assignment_id=p.assignment_id
 JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=a.cleaning_task_id
 JOIN dbo.rooms r ON r.room_id=t.room_id
 JOIN dbo.notification_delivery_intents i ON i.publication_id=p.publication_id AND i.user_id=a.assignee_user_id AND i.delivery_channel='IN_APP'
 WHERE o.actor_user_id=@actor AND o.operation_key=@operation AND o.completed=1
 AND a.assigned_by=@actor AND p.event_type='CLEANING_TASK_ASSIGNED' AND p.schema_version=1
 AND TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.actorId'))=@actor
 AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(p.payload_json,'$.operationId'))=@operation
 AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(p.payload_json,'$.publicationId'))=p.publication_id
 AND TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.assignmentId'))=a.assignment_id
 AND TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.assigneeUserId'))=a.assignee_user_id
 AND TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.taskId'))=t.cleaning_task_id
 AND TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.taskCycleId'))=t.cleaning_task_id
 AND TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.roomId'))=r.room_id
 AND TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.branchId'))=r.branch_id
 AND TRY_CONVERT(datetime2(7),JSON_VALUE(p.payload_json,'$.assignedAtUtc'))=a.assigned_at
 AND REPLACE(JSON_VALUE(p.payload_json,'$.sourceRevision'),'"','')=CONVERT(varchar(16),p.source_revision,2)
 AND TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.assignmentId'))=a.assignment_id
 AND TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.assigneeUserId'))=a.assignee_user_id
 AND TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.id'))=t.cleaning_task_id
 AND TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.branchId'))=r.branch_id
 AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(o.result_json,'$.operationId'))=@operation
 AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(o.result_json,'$.publicationId'))=p.publication_id
 AND JSON_VALUE(o.result_json,'$.sourceETag')=JSON_VALUE(p.payload_json,'$.sourceRevision')
 AND EXISTS(SELECT 1 FROM dbo.audit_logs log WHERE log.entity_type='cleaning_tasks' AND log.entity_id=CONVERT(nvarchar(30),t.cleaning_task_id) AND log.actor_user_id=@actor AND log.branch_id=r.branch_id AND TRY_CONVERT(bigint,JSON_VALUE(log.new_values,'$.assignmentId'))=a.assignment_id AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(log.new_values,'$.operationId'))=@operation)
 ) THROW 51911,'Mandatory persisted assignment/audit/operation/publication/intent proof missing or inconsistent',1;
 IF (SELECT COUNT(*) FROM dbo.notification_delivery_intents i JOIN dbo.notification_publications p ON p.publication_id=i.publication_id WHERE p.actor_user_id=@actor AND p.operation_key=@operation)<>1 THROW 51912,'Exactly one persisted recipient required',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_fs009_publication_immutable ON dbo.notification_publications AFTER UPDATE,DELETE AS
BEGIN
 IF EXISTS(SELECT 1 FROM deleted) THROW 51913,'Publication is immutable',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_fs009_operation_immutable ON dbo.notification_operation_reservations AFTER UPDATE,DELETE AS
BEGIN
 IF EXISTS(SELECT 1 FROM deleted WHERE completed=1) THROW 51914,'Completed operation is immutable',1;
END;
GO
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS010.ExecutableCandidate') EXEC sys.sp_addextendedproperty @name=N'HotelOps.FS010.ExecutableCandidate',@value=N'V1';
GO
CREATE OR ALTER TRIGGER dbo.TR_fs009_publication_source ON dbo.notification_publications AFTER INSERT AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted p WHERE NOT EXISTS(
 SELECT 1 FROM dbo.cleaning_assignments a JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=a.cleaning_task_id
 WHERE a.assignment_id=p.assignment_id AND p.source_revision=t.row_version
 AND TRY_CONVERT(datetime2(7),JSON_VALUE(p.payload_json,'$.dueAtUtc'))=t.due_at
 )) THROW 51915,'Publication transition revision/deadline does not match persisted assignment task',1;
END;