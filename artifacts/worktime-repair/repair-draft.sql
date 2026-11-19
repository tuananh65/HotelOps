SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;
DECLARE @lan bigint=(SELECT user_id FROM dbo.users WITH(UPDLOCK,HOLDLOCK) WHERE username='lan.dang');
DECLARE @branch bigint=(SELECT primary_branch_id FROM dbo.users WHERE user_id=@lan), @now datetime2(7)=SYSUTCDATETIME();
DECLARE @changes TABLE(entity_type varchar(100),entity_id bigint,old_values nvarchar(max),new_values nvarchar(max));
IF @lan IS NULL THROW 50000,'Lan was not found',1;
IF EXISTS(SELECT 1 FROM dbo.work_intervals WHERE user_id=@lan AND closed_at IS NULL) THROW 50000,'Active interval exists: review required',1;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_rework_workflow_v1' AND parent_object_id=OBJECT_ID('dbo.cleaning_reworks')) THROW 50000,'Unexpected schema',1;
ALTER TABLE dbo.cleaning_reworks DROP CONSTRAINT CK_rework_workflow_v1;
ALTER TABLE dbo.cleaning_reworks WITH CHECK ADD CONSTRAINT CK_rework_workflow_v1 CHECK (
 workflow_status IS NULL OR workflow_status='PENDING_ACCEPTANCE' AND status='PENDING'
 OR workflow_status IN ('PENDING_REINSPECTION','IN_PROGRESS','ON_HOLD') AND status='IN_PROGRESS'
 OR workflow_status='COMPLETED' AND status='COMPLETED'
 OR workflow_status='CANCELLED' AND status='CANCELLED'
 OR workflow_status='ACCEPTED' AND status='PENDING'
 OR workflow_status='FAILED' AND status='COMPLETED');
UPDATE t SET workflow_status='ON_HOLD'
OUTPUT 'CLEANING_TASK',inserted.cleaning_task_id,N'{"workflowStatus":null}',N'{"workflowStatus":"ON_HOLD"}' INTO @changes
FROM dbo.cleaning_tasks t WHERE t.task_no='CLN-20261016-002' AND t.status='IN_PROGRESS' AND t.workflow_status IS NULL AND t.actual_end_at IS NULL
AND EXISTS(SELECT 1 FROM dbo.cleaning_assignments a WHERE a.cleaning_task_id=t.cleaning_task_id AND a.assignee_user_id=@lan AND a.assignment_status='ACTIVE' AND a.ended_at IS NULL);
UPDATE r SET workflow_status='ON_HOLD'
OUTPUT 'CLEANING_REWORK',inserted.rework_id,N'{"workflowStatus":null}',N'{"workflowStatus":"ON_HOLD"}' INTO @changes
FROM dbo.cleaning_reworks r WHERE r.assignee_user_id=@lan AND r.rework_id=8 AND r.status='IN_PROGRESS' AND r.workflow_status IS NULL AND r.completed_at IS NULL
AND EXISTS(SELECT 1 FROM dbo.cleaning_tasks t WHERE t.cleaning_task_id=r.cleaning_task_id AND t.task_no='CLN-20261016-010');
INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,old_values,new_values,changed_fields,request_id,user_agent,occurred_at)
SELECT @branch,NULL,entity_type,CONVERT(varchar(50),entity_id),'UPDATE',old_values,new_values,N'["workflow_status"]','WORKTIME-REPAIR-20261120',N'Approved data repair: hold incomplete historical source; no completion or time inferred',@now FROM @changes;
INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,old_values,new_values,changed_fields,request_id,user_agent,occurred_at)
VALUES(@branch,NULL,'SCHEMA','CK_rework_workflow_v1','UPDATE',NULL,N'{"addedAllowedPair":"ON_HOLD/IN_PROGRESS"}',N'["workflow_status"]','WORKTIME-REPAIR-20261120',N'Align existing constraint with source workflow',@now);
SELECT * FROM @changes;
ROLLBACK; -- dry run; installation wrapper replaces only this final marker after approval.
