-- Read-only; run only against the separate approved Tester database, no credentials here.
IF DB_NAME()<>N'HotelOps_Canonical_20261106_f11tester_Fresh' THROW 51990,'Wrong Tester target',1;
DECLARE @task bigint=NULL,@actor bigint=NULL,@operation uniqueidentifier=NULL,@publication uniqueidentifier=NULL;
-- Set exact synthetic IDs from the executed API response; NULL returns no business rows.
SELECT assignment_id,cleaning_task_id,assignee_user_id,assigned_by,assignment_status,ended_at FROM dbo.cleaning_assignments WHERE cleaning_task_id=@task;
SELECT audit_id,actor_user_id,branch_id,entity_type,entity_id,action FROM dbo.audit_logs WHERE entity_type='cleaning_tasks' AND entity_id=CONVERT(nvarchar(30),@task);
SELECT actor_user_id,operation_key,completed FROM dbo.notification_operation_reservations WHERE actor_user_id=@actor AND operation_key=@operation;
SELECT publication_id,assignment_id,source_revision FROM dbo.notification_publications WHERE publication_id=@publication;
SELECT publication_id,user_id,delivery_channel,status,attempt_count,inbox_notification_id FROM dbo.notification_delivery_intents WHERE publication_id=@publication;
SELECT room_id,cleaning_task_id FROM dbo.room_cleaning_cycle_state WHERE cleaning_task_id=@task;
SELECT room_id,cleaning_task_id,readiness,has_effective_block FROM dbo.room_readiness_events WHERE cleaning_task_id=@task;
