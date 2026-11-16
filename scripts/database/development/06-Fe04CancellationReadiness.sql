-- Recovery-only successor: preserve previous-cycle effective readiness after cancellation.
SET XACT_ABORT ON;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.CleaningCancellation.V1'
 AND CONVERT(nvarchar(128),value)='7D61CD67E15BA9710CB30612470E5AA674EB9EECEDF4568B1346168DF1B2F8EF')
 THROW 51990,'Exact cancellation prerequisite required',1;
ALTER TABLE dbo.room_readiness_events DROP CONSTRAINT CK_dev3_readiness_start;
ALTER TABLE dbo.room_readiness_events WITH CHECK ADD CONSTRAINT CK_dev3_readiness_start
 CHECK(readiness IN('INACTIVE','BLOCKED','REWORK_REQUIRED','CLEANING_IN_PROGRESS','PENDING_CLEANING','PENDING_INSPECTION','READY'));
GO
ALTER TRIGGER dbo.TR_dev3_closure_publication_source ON dbo.notification_publications AFTER INSERT AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted p WHERE NOT EXISTS(
  SELECT 1 FROM dbo.cleaning_assignments a JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=a.cleaning_task_id
  JOIN dbo.rooms r ON r.room_id=t.room_id
  JOIN dbo.notification_operation_reservations o ON o.actor_user_id=p.actor_user_id AND o.operation_key=p.operation_key
  WHERE a.assignment_id=p.assignment_id AND p.source_revision=t.row_version AND o.completed=1
   AND ((p.event_type='CLEANING_TASK_ASSIGNED' AND TRY_CONVERT(datetime2(7),JSON_VALUE(p.payload_json,'$.dueAtUtc'))=t.due_at)
    OR (p.event_type='CLEANING_TASK_CANCELLED'
     AND t.workflow_status='CANCELLED' AND t.status='CANCELLED' AND t.actual_start_at IS NULL AND t.actual_end_at IS NULL
     AND a.assignment_status='CANCELLED' AND a.ended_at=p.created_at
     AND JSON_VALUE(o.result_json,'$.state')='CANCELLED'
     AND TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.id'))=t.cleaning_task_id
     AND TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.roomId'))=r.room_id
     AND TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.branchId'))=r.branch_id
     AND TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.assignmentId'))=a.assignment_id
     AND TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.assigneeId'))=a.assignee_user_id
     AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(o.result_json,'$.operationId'))=p.operation_key
     AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(o.result_json,'$.publicationId'))=p.publication_id
     AND TRY_CONVERT(datetime2(7),JSON_VALUE(o.result_json,'$.occurredAtUtc'))=p.created_at
     AND JSON_VALUE(o.result_json,'$.sourceETag')=CHAR(34)+CONVERT(varchar(16),p.source_revision,2)+CHAR(34)
     AND JSON_VALUE(p.payload_json,'$.sourceETag')=JSON_VALUE(o.result_json,'$.sourceETag')
     AND TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.taskId'))=t.cleaning_task_id
     AND TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.branchId'))=r.branch_id
     AND TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.assigneeId'))=a.assignee_user_id
     AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(p.payload_json,'$.operationId'))=p.operation_key
     AND p.occurrence_key=CONCAT('CLEANING:',t.cleaning_task_id,':CANCELLED')
     AND EXISTS(SELECT 1 FROM dbo.room_readiness_events e
       WHERE e.event_id=TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.readinessEventId'))
       AND e.room_id=r.room_id AND e.cleaning_task_id=t.cleaning_task_id AND e.readiness=JSON_VALUE(o.result_json,'$.readiness')
       AND e.has_effective_block=CASE WHEN JSON_VALUE(o.result_json,'$.readiness')='BLOCKED' THEN 1 ELSE 0 END AND e.actor_user_id=p.actor_user_id AND e.occurred_at=p.created_at)
     AND EXISTS(SELECT 1 FROM dbo.audit_logs l WHERE l.actor_user_id=p.actor_user_id AND l.branch_id=r.branch_id
       AND l.entity_type='cleaning_tasks' AND l.entity_id=CONVERT(nvarchar(30),t.cleaning_task_id) AND l.action='UPDATE'
       AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(CASE WHEN ISJSON(l.new_values)=1 THEN l.new_values ELSE N'[]' END,'$.operationId'))=p.operation_key
       AND JSON_VALUE(CASE WHEN ISJSON(l.new_values)=1 THEN l.new_values ELSE N'[]' END,'$.action')='CANCEL_CLEANING')
    )
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