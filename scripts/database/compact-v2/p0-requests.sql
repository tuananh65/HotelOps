/* P0 normalized request routing. Requires p0-supplies.sql. Test clone only.
   Existing WorkItem IDs and stock ledger remain authoritative. No stock or business data seed. */
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'HotelOpsCompactWorkdayTestP0%' THROW 51600,'P0 restored test clone required.',1;
IF OBJECT_ID('dbo.staff_supply_allocations') IS NULL THROW 51600,'Install P0 supplies first.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 IF OBJECT_ID('dbo.request_workflows') IS NULL
 BEGIN
  CREATE TABLE dbo.request_workflows(
   work_id bigint NOT NULL CONSTRAINT PK_request_workflows PRIMARY KEY,
   branch_id bigint NOT NULL,request_type varchar(50) NOT NULL,status varchar(40) NOT NULL,
   queue_role_id bigint NULL,handler_user_id bigint NULL,allocation_id bigint NULL,warehouse_id bigint NULL,
   document_id bigint NULL,parent_request_work_id bigint NULL,correlation_id uniqueidentifier NOT NULL,
   updated_at datetime2 NOT NULL,row_version rowversion NOT NULL,
   CONSTRAINT UQ_request_workflow_branch UNIQUE(work_id,branch_id),
   CONSTRAINT FK_request_workflow_work FOREIGN KEY(work_id,branch_id) REFERENCES dbo.work_items(work_id,branch_id),
   CONSTRAINT FK_request_workflow_queue FOREIGN KEY(queue_role_id) REFERENCES dbo.roles(role_id),
   CONSTRAINT FK_request_workflow_handler FOREIGN KEY(handler_user_id) REFERENCES dbo.users(user_id),
   CONSTRAINT FK_request_workflow_allocation FOREIGN KEY(allocation_id) REFERENCES dbo.staff_supply_allocations(allocation_id),
   CONSTRAINT FK_request_workflow_warehouse FOREIGN KEY(warehouse_id,branch_id) REFERENCES dbo.warehouses(warehouse_id,branch_id),
   CONSTRAINT FK_request_workflow_document FOREIGN KEY(document_id,branch_id) REFERENCES dbo.inventory_documents(document_id,branch_id),
   CONSTRAINT FK_request_workflow_parent FOREIGN KEY(parent_request_work_id,branch_id) REFERENCES dbo.request_workflows(work_id,branch_id),
   CONSTRAINT CK_request_workflow_parent CHECK(parent_request_work_id IS NULL OR parent_request_work_id<>work_id),
   CONSTRAINT CK_request_workflow_type CHECK(request_type IN('GENERAL_INTERNAL_REQUEST','CONSUMABLE_REFILL_REQUEST','WAREHOUSE_REPLENISHMENT_REQUEST','ROOM_DURABLE_ITEM_MISSING','ROOM_EQUIPMENT_DAMAGE','DURABLE_ITEM_REMEDIATION','MAINTENANCE_REMEDIATION','ROOM_REINSPECTION')),
   CONSTRAINT CK_request_workflow_status CHECK(
    (request_type='CONSUMABLE_REFILL_REQUEST' AND allocation_id IS NOT NULL AND warehouse_id IS NULL AND status IN('PENDING_SUPERVISOR','PENDING_STOREKEEPER','FULFILLED','REJECTED','CANCELLED')) OR
    (request_type='WAREHOUSE_REPLENISHMENT_REQUEST' AND allocation_id IS NULL AND warehouse_id IS NOT NULL AND status IN('PENDING_OPERATIONS_MANAGER','PENDING_RECEIPT','FULFILLED','REJECTED','CANCELLED')) OR
    (request_type NOT IN('CONSUMABLE_REFILL_REQUEST','WAREHOUSE_REPLENISHMENT_REQUEST') AND allocation_id IS NULL AND warehouse_id IS NULL AND status IN('PENDING_SUPERVISOR','PENDING_STOREKEEPER','PENDING_MAINTENANCE_SUPERVISOR','ACCEPTED','IN_PROGRESS','PENDING_REINSPECTION','REWORK_REQUIRED','FULFILLED','COMPLETED','REJECTED','CANCELLED'))),
   CONSTRAINT CK_request_workflow_terminal_queue CHECK((status IN('FULFILLED','COMPLETED','REJECTED','CANCELLED') AND queue_role_id IS NULL AND handler_user_id IS NULL) OR (status NOT IN('FULFILLED','COMPLETED','REJECTED','CANCELLED') AND queue_role_id IS NOT NULL))
  );
  CREATE UNIQUE INDEX UX_request_workflow_document ON dbo.request_workflows(document_id) WHERE document_id IS NOT NULL;
  CREATE UNIQUE INDEX UX_request_workflow_open_refill ON dbo.request_workflows(allocation_id) WHERE allocation_id IS NOT NULL AND status<>'FULFILLED' AND status<>'REJECTED' AND status<>'CANCELLED';
  CREATE INDEX IX_request_workflow_queue ON dbo.request_workflows(branch_id,queue_role_id,status,handler_user_id);
  CREATE TABLE dbo.request_workflow_lines(
   request_line_id bigint IDENTITY NOT NULL CONSTRAINT PK_request_workflow_lines PRIMARY KEY,
   work_id bigint NOT NULL,item_id bigint NOT NULL,unit_id bigint NOT NULL,
   requested_quantity decimal(18,4) NOT NULL,approved_quantity decimal(18,4) NULL,
   stock_at_request decimal(18,4) NOT NULL,minimum_at_request decimal(18,4) NOT NULL,document_line_id bigint NULL,
   CONSTRAINT UQ_request_workflow_item UNIQUE(work_id,item_id),
   CONSTRAINT FK_request_line_workflow FOREIGN KEY(work_id) REFERENCES dbo.request_workflows(work_id),
   CONSTRAINT FK_request_line_item FOREIGN KEY(item_id) REFERENCES dbo.inventory_items(item_id),
   CONSTRAINT FK_request_line_unit FOREIGN KEY(unit_id) REFERENCES dbo.reference_values(reference_id),
   CONSTRAINT FK_request_line_document FOREIGN KEY(document_line_id) REFERENCES dbo.inventory_document_lines(line_id),
   CONSTRAINT CK_request_line_quantities CHECK(requested_quantity>0 AND stock_at_request>=0 AND minimum_at_request>=0 AND (approved_quantity IS NULL OR (approved_quantity>0 AND approved_quantity<=requested_quantity)))
  );
  CREATE UNIQUE INDEX UX_request_line_document ON dbo.request_workflow_lines(document_line_id) WHERE document_line_id IS NOT NULL;
  CREATE TABLE dbo.request_workflow_events(
   request_event_id bigint IDENTITY NOT NULL CONSTRAINT PK_request_workflow_events PRIMARY KEY,
   work_id bigint NOT NULL,branch_id bigint NOT NULL,action varchar(40) NOT NULL,
   previous_status varchar(40) NULL,new_status varchar(40) NOT NULL,
   previous_queue_role_id bigint NULL,new_queue_role_id bigint NULL,previous_handler_user_id bigint NULL,new_handler_user_id bigint NULL,
   actor_user_id bigint NOT NULL,actor_role_id bigint NOT NULL,occurred_at datetime2 NOT NULL,reason nvarchar(2000) NULL,
   related_document_id bigint NULL,correlation_id uniqueidentifier NOT NULL,operation_key uniqueidentifier NOT NULL,payload_hash varchar(64) NOT NULL,previous_version binary(8) NULL,
   CONSTRAINT UQ_request_event_operation UNIQUE(actor_user_id,operation_key),
   CONSTRAINT FK_request_event_workflow FOREIGN KEY(work_id,branch_id) REFERENCES dbo.request_workflows(work_id,branch_id),
   CONSTRAINT FK_request_event_old_queue FOREIGN KEY(previous_queue_role_id) REFERENCES dbo.roles(role_id),
   CONSTRAINT FK_request_event_new_queue FOREIGN KEY(new_queue_role_id) REFERENCES dbo.roles(role_id),
   CONSTRAINT FK_request_event_old_handler FOREIGN KEY(previous_handler_user_id) REFERENCES dbo.users(user_id),
   CONSTRAINT FK_request_event_new_handler FOREIGN KEY(new_handler_user_id) REFERENCES dbo.users(user_id),
   CONSTRAINT FK_request_event_actor FOREIGN KEY(actor_user_id) REFERENCES dbo.users(user_id),
   CONSTRAINT FK_request_event_actor_role FOREIGN KEY(actor_role_id) REFERENCES dbo.roles(role_id),
   CONSTRAINT FK_request_event_document FOREIGN KEY(related_document_id,branch_id) REFERENCES dbo.inventory_documents(document_id,branch_id),
   CONSTRAINT CK_request_event_hash CHECK(LEN(payload_hash)=64),
   CONSTRAINT CK_request_event_rejection_reason CHECK(action NOT IN('reject','rework','fail') OR LEN(LTRIM(RTRIM(ISNULL(reason,N''))))>=10)
  );
  CREATE INDEX IX_request_event_timeline ON dbo.request_workflow_events(work_id,request_event_id);
 END;

 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_request_workflow_provenance ON dbo.request_workflows AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.work_id=i.work_id WHERE i.branch_id<>d.branch_id OR i.request_type<>d.request_type OR i.correlation_id<>d.correlation_id OR ISNULL(i.allocation_id,-1)<>ISNULL(d.allocation_id,-1) OR ISNULL(i.warehouse_id,-1)<>ISNULL(d.warehouse_id,-1) OR ISNULL(i.parent_request_work_id,-1)<>ISNULL(d.parent_request_work_id,-1) OR (d.document_id IS NOT NULL AND ISNULL(i.document_id,-1)<>d.document_id))
   THROW 51601,''Request origin and correlation are immutable.'',1;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.work_items w ON w.work_id=i.work_id LEFT JOIN dbo.staff_supply_allocations a ON a.allocation_id=i.allocation_id WHERE i.request_type IN(''CONSUMABLE_REFILL_REQUEST'',''WAREHOUSE_REPLENISHMENT_REQUEST'') AND (w.kind<>''INTERNAL_REQUEST'' OR (i.allocation_id IS NOT NULL AND (a.branch_id<>i.branch_id OR a.user_id<>w.created_by))))
   THROW 51602,''Typed request root or allocation owner mismatch.'',1;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.inventory_documents d ON d.document_id=i.document_id WHERE i.request_type=''WAREHOUSE_REPLENISHMENT_REQUEST'' AND (d.kind<>''REPLENISHMENT'' OR d.source_work_id IS NULL OR d.source_work_id<>i.work_id OR ISNULL(d.destination_warehouse_id,-1)<>i.warehouse_id))
   THROW 51602,''Replenishment document provenance mismatch.'',1;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.roles r ON r.role_id=i.queue_role_id WHERE
   (i.status=''PENDING_SUPERVISOR'' AND r.code<>''HOUSEKEEPING_SUPERVISOR'') OR
   (i.status IN(''PENDING_STOREKEEPER'',''PENDING_RECEIPT'') AND r.code<>''STOREKEEPER'') OR
   (i.status=''PENDING_OPERATIONS_MANAGER'' AND r.code<>''OPERATION_MANAGER'') OR
   (i.status=''PENDING_MAINTENANCE_SUPERVISOR'' AND r.code<>''MAINTENANCE_SUPERVISOR'') OR
   (i.status=''PENDING_REINSPECTION'' AND r.code<>''HOUSEKEEPING_SUPERVISOR''))
   THROW 51603,''Request queue does not match typed routing.'',1;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.request_workflows p ON p.work_id=i.parent_request_work_id WHERE p.correlation_id<>i.correlation_id)
   THROW 51604,''Child request must preserve parent correlation.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_request_lines_provenance ON dbo.request_workflow_lines AFTER INSERT,UPDATE,DELETE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.request_line_id=d.request_line_id WHERE i.request_line_id IS NULL OR i.work_id<>d.work_id OR i.item_id<>d.item_id OR i.unit_id<>d.unit_id OR i.requested_quantity<>d.requested_quantity OR i.stock_at_request<>d.stock_at_request OR i.minimum_at_request<>d.minimum_at_request OR (d.approved_quantity IS NOT NULL AND (i.approved_quantity IS NULL OR i.approved_quantity<>d.approved_quantity)) OR (d.document_line_id IS NOT NULL AND ISNULL(i.document_line_id,-1)<>d.document_line_id))
   THROW 51605,''Request line snapshots and approved quantities are immutable.'',1;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.inventory_items item ON item.item_id=i.item_id WHERE item.base_unit_id<>i.unit_id)
   THROW 51605,''Request quantities must use item base units.'',1;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.inventory_document_lines l ON l.line_id=i.document_line_id JOIN dbo.request_workflows r ON r.work_id=i.work_id JOIN dbo.inventory_documents d ON d.document_id=l.document_id WHERE l.item_id<>i.item_id OR l.unit_id<>i.unit_id OR d.branch_id<>r.branch_id OR d.source_work_id IS NULL OR d.source_work_id<>r.work_id)
   THROW 51605,''Request stock line provenance mismatch.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_request_events_immutable ON dbo.request_workflow_events AFTER UPDATE,DELETE AS
 BEGIN SET NOCOUNT ON; THROW 51606,''Request transition history is append-only.'',1; END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_request_receipt_approved ON dbo.inventory_documents AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.request_workflows r ON r.document_id=i.source_document_id JOIN dbo.inventory_documents source ON source.document_id=r.document_id WHERE i.kind=''RECEIPT'' AND i.posted_at IS NOT NULL AND (source.approved_by IS NULL OR r.status NOT IN(''PENDING_RECEIPT'',''FULFILLED'') OR ISNULL(i.destination_warehouse_id,-1)<>r.warehouse_id OR i.branch_id<>r.branch_id OR ISNULL(i.source_work_id,-1)<>r.work_id))
   THROW 51607,''Receipt requires an approved request and its destination warehouse.'',1;
 END;');
 IF NOT EXISTS(SELECT 1 FROM dbo.audit_logs WHERE entity_type='P0_REQUEST_SCHEMA' AND entity_key='261124-v1' AND action='APPLIED')
  INSERT dbo.audit_logs(operation_key,entity_type,entity_key,action,after_json,occurred_at)
  VALUES(NEWID(),'P0_REQUEST_SCHEMA','261124-v1','APPLIED',N'{"tables":3,"stockSeeded":false}',SYSUTCDATETIME());
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
