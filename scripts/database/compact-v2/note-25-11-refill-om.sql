/* NOTE09/14A: OM approves consumables; preserve historical outcomes and inventory.
   Run only after a fresh CHECKSUM backup + RESTORE VERIFYONLY. No stock data changes.
   Apply before running the matching application release. Idempotent forward patch. */
SET XACT_ABORT ON;
IF OBJECT_ID('dbo.request_workflows') IS NULL THROW 51600,'Request workflow schema required.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.roles WHERE code='OPERATION_MANAGER' AND is_active=1)
 THROW 51600,'Active Operation Manager role required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 ALTER TABLE dbo.request_workflows DROP CONSTRAINT CK_request_workflow_status;
 ALTER TABLE dbo.request_workflows WITH CHECK ADD CONSTRAINT CK_request_workflow_status CHECK(
  (request_type='CONSUMABLE_REFILL_REQUEST' AND allocation_id IS NOT NULL AND warehouse_id IS NULL AND status IN('PENDING_SUPERVISOR','PENDING_OPERATIONS_MANAGER','PENDING_STOREKEEPER','FULFILLED','REJECTED','CANCELLED')) OR
  (request_type='WAREHOUSE_REPLENISHMENT_REQUEST' AND allocation_id IS NULL AND warehouse_id IS NOT NULL AND status IN('PENDING_OPERATIONS_MANAGER','PENDING_RECEIPT','FULFILLED','REJECTED','CANCELLED')) OR
  (request_type NOT IN('CONSUMABLE_REFILL_REQUEST','WAREHOUSE_REPLENISHMENT_REQUEST') AND allocation_id IS NULL AND warehouse_id IS NULL AND status IN('PENDING_SUPERVISOR','PENDING_STOREKEEPER','PENDING_MAINTENANCE_SUPERVISOR','ACCEPTED','IN_PROGRESS','PENDING_REINSPECTION','REWORK_REQUIRED','FULFILLED','COMPLETED','REJECTED','CANCELLED')));
 DECLARE @om bigint=(SELECT role_id FROM dbo.roles WHERE code='OPERATION_MANAGER' AND is_active=1);
 DECLARE @changed TABLE(work_id bigint,branch_id bigint,previous_status varchar(40),previous_queue bigint,previous_handler bigint,event_key uniqueidentifier);
 INSERT @changed SELECT work_id,branch_id,status,queue_role_id,handler_user_id,NEWID()
  FROM dbo.request_workflows WITH(UPDLOCK,HOLDLOCK)
  WHERE request_type='CONSUMABLE_REFILL_REQUEST' AND status='PENDING_SUPERVISOR';
 UPDATE r SET status='PENDING_OPERATIONS_MANAGER',queue_role_id=@om,handler_user_id=NULL,updated_at=SYSUTCDATETIME()
 FROM dbo.request_workflows r JOIN @changed c ON c.work_id=r.work_id;
 INSERT dbo.audit_logs(operation_key,entity_type,entity_key,action,branch_id,before_json,after_json,occurred_at)
 SELECT event_key,'RequestWorkflow',CONVERT(varchar(30),work_id),'REFILL_OM_ROUTING',branch_id,
  (SELECT previous_status AS status,previous_queue AS queueRoleId,previous_handler AS handlerUserId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
  (SELECT 'PENDING_OPERATIONS_MANAGER' AS status,@om AS queueRoleId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),SYSUTCDATETIME() FROM @changed;
 INSERT dbo.notifications(branch_id,recipient_user_id,work_id,event_key,notification_type,title,message,requires_acknowledgment,available_at)
 SELECT DISTINCT c.branch_id,ur.user_id,c.work_id,c.event_key,'REQUEST_WORKFLOW',N'Yêu cầu vật tư tiêu hao chờ duyệt',N'Yêu cầu vật tư tiêu hao được chuyển đến quản lý vận hành để duyệt.',0,SYSUTCDATETIME()
 FROM @changed c JOIN dbo.user_roles ur ON ur.branch_id=c.branch_id AND ur.role_id=@om
 JOIN dbo.users u ON u.user_id=ur.user_id
 WHERE ur.revoked_at IS NULL AND ur.assigned_at<=SYSUTCDATETIME() AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME())
 AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL
 AND EXISTS(SELECT 1 FROM dbo.role_permissions g JOIN dbo.permissions p ON p.permission_id=g.permission_id
  WHERE g.role_id=@om AND p.code='REPLENISHMENT_APPROVE' AND p.is_active=1 AND g.data_scope IN('BRANCH','ALL'))
 AND EXISTS(SELECT 1 FROM dbo.memberships m WHERE m.user_id=u.user_id AND m.branch_id=c.branch_id AND m.kind='BRANCH_ACCESS' AND m.valid_from<=SYSUTCDATETIME() AND (m.valid_to IS NULL OR m.valid_to>SYSUTCDATETIME()));
 IF NOT EXISTS(SELECT 1 FROM dbo.audit_logs WHERE entity_type='NOTE_25_11_SCHEMA' AND entity_key='refill-om-v1' AND action='APPLIED')
  INSERT dbo.audit_logs(operation_key,entity_type,entity_key,action,after_json,occurred_at)
  VALUES(NEWID(),'NOTE_25_11_SCHEMA','refill-om-v1','APPLIED',N'{"refillApprover":"OPERATION_MANAGER","stockChanged":false}',SYSUTCDATETIME());
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
