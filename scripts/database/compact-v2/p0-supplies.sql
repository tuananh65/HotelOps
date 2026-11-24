/* P0 supplies foundation. Apply ONLY to an independently restored test database.
   No seed by name, no guessed warehouse, no manufactured inventory, no role grants.
   Existing inventory constraints and triggers are extended, never disabled. */
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'HotelOpsCompact%Test%' THROW 51500,'Independent Compact test database required.',1;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.supply_configurations') IS NULL
BEGIN
 CREATE TABLE dbo.supply_configurations(
  configuration_id bigint IDENTITY PRIMARY KEY,branch_id bigint NOT NULL,item_id bigint NULL,asset_id bigint NULL,
  supply_group varchar(1) NOT NULL,warehouse_id bigint NULL,unit_id bigint NULL,
  initial_quantity decimal(18,4) NOT NULL,minimum_stock decimal(18,4) NOT NULL,per_guest_quantity decimal(18,4) NOT NULL,
  is_active bit NOT NULL,revision int NOT NULL,is_current bit NOT NULL,updated_by bigint NOT NULL,updated_at datetime2 NOT NULL,row_version rowversion NOT NULL,
  CONSTRAINT FK_supply_config_branch FOREIGN KEY(branch_id) REFERENCES dbo.branches(branch_id),
  CONSTRAINT FK_supply_config_item FOREIGN KEY(item_id) REFERENCES dbo.inventory_items(item_id),
  CONSTRAINT FK_supply_config_asset FOREIGN KEY(asset_id,branch_id) REFERENCES dbo.assets(asset_id,branch_id),
  CONSTRAINT FK_supply_config_warehouse FOREIGN KEY(warehouse_id,branch_id) REFERENCES dbo.warehouses(warehouse_id,branch_id),
  CONSTRAINT FK_supply_config_unit FOREIGN KEY(unit_id) REFERENCES dbo.reference_values(reference_id),
  CONSTRAINT FK_supply_config_actor FOREIGN KEY(updated_by) REFERENCES dbo.users(user_id),
  CONSTRAINT CK_supply_config_subject CHECK((item_id IS NOT NULL AND asset_id IS NULL AND unit_id IS NOT NULL) OR (item_id IS NULL AND asset_id IS NOT NULL AND unit_id IS NULL AND warehouse_id IS NULL AND supply_group='C' AND minimum_stock=0)),
  CONSTRAINT CK_supply_config_group CHECK(supply_group IN('A','B','C') AND initial_quantity>=0 AND minimum_stock>=0 AND per_guest_quantity>=0 AND revision>0 AND
   (supply_group='A' OR (initial_quantity=0 AND per_guest_quantity=0)) AND
   (supply_group<>'A' OR is_active=0 OR (warehouse_id IS NOT NULL AND initial_quantity>0 AND per_guest_quantity>0)))
 );
 CREATE UNIQUE INDEX UX_supply_config_current_item ON dbo.supply_configurations(branch_id,item_id) WHERE is_current=1 AND item_id IS NOT NULL;
 CREATE UNIQUE INDEX UX_supply_config_current_asset ON dbo.supply_configurations(branch_id,asset_id) WHERE is_current=1 AND asset_id IS NOT NULL;
 CREATE UNIQUE INDEX UX_supply_config_item_revision ON dbo.supply_configurations(branch_id,item_id,revision) WHERE item_id IS NOT NULL;
 CREATE UNIQUE INDEX UX_supply_config_asset_revision ON dbo.supply_configurations(branch_id,asset_id,revision) WHERE asset_id IS NOT NULL;
 CREATE TABLE dbo.staff_supply_allocations(
  allocation_id bigint IDENTITY PRIMARY KEY,branch_id bigint NOT NULL,user_id bigint NOT NULL,assignment_id bigint NOT NULL UNIQUE,
  status varchar(20) NOT NULL,shortage_reason nvarchar(2000) NULL,created_at datetime2 NOT NULL,closed_at datetime2 NULL,row_version rowversion NOT NULL,
  CONSTRAINT FK_supply_allocation_branch FOREIGN KEY(branch_id) REFERENCES dbo.branches(branch_id),
  CONSTRAINT FK_supply_allocation_user FOREIGN KEY(user_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_supply_allocation_assignment FOREIGN KEY(assignment_id) REFERENCES dbo.workday_assignments(assignment_id),
  CONSTRAINT CK_supply_allocation_status CHECK((status IN('OPEN','SHORT') AND closed_at IS NULL) OR (status='CLOSED' AND closed_at>=created_at))
 );
 CREATE TABLE dbo.staff_supply_balances(
  balance_id bigint IDENTITY PRIMARY KEY,allocation_id bigint NOT NULL,configuration_id bigint NOT NULL,item_id bigint NOT NULL,warehouse_id bigint NOT NULL,unit_id bigint NOT NULL,
  initial_target decimal(18,4) NOT NULL,per_guest_quantity decimal(18,4) NOT NULL,
  initial_issued decimal(18,4) NOT NULL DEFAULT 0,additional_issued decimal(18,4) NOT NULL DEFAULT 0,reserved decimal(18,4) NOT NULL DEFAULT 0,used decimal(18,4) NOT NULL DEFAULT 0,returned decimal(18,4) NOT NULL DEFAULT 0,row_version rowversion NOT NULL,
  CONSTRAINT UQ_supply_balance_item UNIQUE(allocation_id,item_id),
  CONSTRAINT FK_supply_balance_allocation FOREIGN KEY(allocation_id) REFERENCES dbo.staff_supply_allocations(allocation_id),
  CONSTRAINT FK_supply_balance_config FOREIGN KEY(configuration_id) REFERENCES dbo.supply_configurations(configuration_id),
  CONSTRAINT FK_supply_balance_item FOREIGN KEY(item_id) REFERENCES dbo.inventory_items(item_id),
  CONSTRAINT FK_supply_balance_warehouse FOREIGN KEY(warehouse_id) REFERENCES dbo.warehouses(warehouse_id),
  CONSTRAINT FK_supply_balance_unit FOREIGN KEY(unit_id) REFERENCES dbo.reference_values(reference_id),
  CONSTRAINT CK_supply_balance_nonnegative CHECK(initial_target>0 AND per_guest_quantity>0 AND initial_issued>=0 AND initial_issued<=initial_target AND additional_issued>=0 AND reserved>=0 AND used>=0 AND returned>=0 AND initial_issued+additional_issued-used-returned-reserved>=0)
 );
 CREATE TABLE dbo.supply_reservations(
  reservation_id bigint IDENTITY PRIMARY KEY,balance_id bigint NOT NULL,work_id bigint NOT NULL,room_id bigint NOT NULL,capacity int NOT NULL,
  per_guest_quantity decimal(18,4) NOT NULL,quantity decimal(18,4) NOT NULL,status varchar(20) NOT NULL,created_at datetime2 NOT NULL,resolved_at datetime2 NULL,row_version rowversion NOT NULL,
  CONSTRAINT FK_supply_reservation_balance FOREIGN KEY(balance_id) REFERENCES dbo.staff_supply_balances(balance_id),
  CONSTRAINT FK_supply_reservation_work FOREIGN KEY(work_id) REFERENCES dbo.work_items(work_id),
  CONSTRAINT FK_supply_reservation_room FOREIGN KEY(room_id) REFERENCES dbo.rooms(room_id),
  CONSTRAINT CK_supply_reservation_qty CHECK(capacity>0 AND per_guest_quantity>0 AND quantity=capacity*per_guest_quantity),
  CONSTRAINT CK_supply_reservation_status CHECK((status='RESERVED' AND resolved_at IS NULL) OR (status IN('CONSUMED','RELEASED') AND resolved_at>=created_at))
 );
 CREATE UNIQUE INDEX UX_supply_reservation_active ON dbo.supply_reservations(work_id,balance_id) WHERE status='RESERVED';
 CREATE UNIQUE INDEX UX_supply_reservation_used ON dbo.supply_reservations(work_id,balance_id) WHERE status='CONSUMED';
 CREATE TABLE dbo.supply_stock_documents(
  document_id bigint NOT NULL PRIMARY KEY,allocation_id bigint NOT NULL,warehouse_id bigint NOT NULL,purpose varchar(20) NOT NULL,operation_key uniqueidentifier NOT NULL,request_work_id bigint NULL,
  CONSTRAINT FK_supply_document_stock FOREIGN KEY(document_id) REFERENCES dbo.inventory_documents(document_id),
  CONSTRAINT FK_supply_document_allocation FOREIGN KEY(allocation_id) REFERENCES dbo.staff_supply_allocations(allocation_id),
  CONSTRAINT FK_supply_document_warehouse FOREIGN KEY(warehouse_id) REFERENCES dbo.warehouses(warehouse_id),
  CONSTRAINT FK_supply_document_request FOREIGN KEY(request_work_id) REFERENCES dbo.work_items(work_id),
  CONSTRAINT UQ_supply_document_operation UNIQUE(allocation_id,warehouse_id,purpose,operation_key),
  CONSTRAINT CK_supply_document_purpose CHECK((purpose IN('INITIAL','RETURN') AND request_work_id IS NULL) OR (purpose='ADDITIONAL' AND request_work_id IS NOT NULL))
 );
 CREATE UNIQUE INDEX UX_supply_document_initial ON dbo.supply_stock_documents(allocation_id,warehouse_id) WHERE purpose='INITIAL';
 CREATE UNIQUE INDEX UX_supply_document_return ON dbo.supply_stock_documents(allocation_id,warehouse_id) WHERE purpose='RETURN';
 CREATE UNIQUE INDEX UX_supply_document_refill ON dbo.supply_stock_documents(allocation_id,warehouse_id,request_work_id) WHERE purpose='ADDITIONAL';
 CREATE TABLE dbo.staff_supply_ledger(
  ledger_id bigint IDENTITY PRIMARY KEY,balance_id bigint NOT NULL,kind varchar(20) NOT NULL,quantity decimal(18,4) NOT NULL,
  work_id bigint NULL,reservation_id bigint NULL,document_line_id bigint NULL,operation_key uniqueidentifier NOT NULL,actor_user_id bigint NOT NULL,occurred_at datetime2 NOT NULL,
  CONSTRAINT FK_supply_ledger_balance FOREIGN KEY(balance_id) REFERENCES dbo.staff_supply_balances(balance_id),
  CONSTRAINT FK_supply_ledger_work FOREIGN KEY(work_id) REFERENCES dbo.work_items(work_id),
  CONSTRAINT FK_supply_ledger_reservation FOREIGN KEY(reservation_id) REFERENCES dbo.supply_reservations(reservation_id),
  CONSTRAINT FK_supply_ledger_document FOREIGN KEY(document_line_id) REFERENCES dbo.inventory_document_lines(line_id),
  CONSTRAINT FK_supply_ledger_actor FOREIGN KEY(actor_user_id) REFERENCES dbo.users(user_id),
  CONSTRAINT UQ_supply_ledger_operation UNIQUE(balance_id,kind,operation_key),
  CONSTRAINT CK_supply_ledger_kind CHECK(quantity>0 AND ((kind IN('INITIAL','ADDITIONAL','RETURN') AND document_line_id IS NOT NULL) OR (kind IN('RESERVE','RELEASE','USE') AND reservation_id IS NOT NULL AND work_id IS NOT NULL)))
 );
 CREATE UNIQUE INDEX UX_supply_ledger_reservation_once ON dbo.staff_supply_ledger(reservation_id,kind) WHERE reservation_id IS NOT NULL;
 CREATE UNIQUE INDEX UX_supply_ledger_stock_once ON dbo.staff_supply_ledger(document_line_id) WHERE document_line_id IS NOT NULL;
END;

-- Retain each installed check verbatim; add only the two dedicated automation kinds.
DECLARE @constraint sysname,@definition nvarchar(max),@sql nvarchar(max);
DECLARE checks CURSOR LOCAL FAST_FORWARD FOR SELECT name,definition FROM sys.check_constraints
 WHERE parent_object_id=OBJECT_ID('dbo.inventory_documents') AND name IN('CK_inventory_documents_kind','CK_inventory_documents_kind_status','CK_inventory_documents_posted_state');
OPEN checks;FETCH NEXT FROM checks INTO @constraint,@definition;
WHILE @@FETCH_STATUS=0
BEGIN
 IF @definition NOT LIKE '%SUPPLY_ISSUE%'
 BEGIN
  SET @sql=N'ALTER TABLE dbo.inventory_documents DROP CONSTRAINT '+QUOTENAME(@constraint)+N'; ALTER TABLE dbo.inventory_documents WITH CHECK ADD CONSTRAINT '+QUOTENAME(@constraint)+N' CHECK(('+@definition+N') OR (kind IN(''SUPPLY_ISSUE'',''SUPPLY_RETURN'') AND ((status=''DRAFT'' AND posted_at IS NULL) OR (status=''POSTED'' AND posted_at IS NOT NULL))));';
  EXEC sp_executesql @sql;
 END;
 FETCH NEXT FROM checks INTO @constraint,@definition;
END;
CLOSE checks;DEALLOCATE checks;
IF (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.inventory_documents') AND name IN('CK_inventory_documents_kind','CK_inventory_documents_kind_status','CK_inventory_documents_posted_state'))<>3 THROW 51501,'Inventory checks missing; inspect installed schema.',1;

-- Preserve all other installed trigger clauses, including negative-stock and reversal guards.
SET @sql=OBJECT_DEFINITION(OBJECT_ID('dbo.TR_stock_movements_posted'));
IF @sql IS NULL THROW 51501,'Stock posting guard missing.',1;
IF @sql NOT LIKE '%SUPPLY_ISSUE%'
BEGIN
 IF CHARINDEX('''RECEIPT'',''ISSUE'',''RETURN'',''TRANSFER'',''TRANSFER_RECEIPT'',''ADJUSTMENT'',''REVERSAL''',@sql)=0 THROW 51501,'Unrecognized stock posting guard; review before patch.',1;
 SET @sql=REPLACE(@sql,'''RECEIPT'',''ISSUE'',''RETURN'',''TRANSFER'',''TRANSFER_RECEIPT'',''ADJUSTMENT'',''REVERSAL''','''RECEIPT'',''ISSUE'',''RETURN'',''TRANSFER'',''TRANSFER_RECEIPT'',''ADJUSTMENT'',''REVERSAL'',''SUPPLY_ISSUE'',''SUPPLY_RETURN''');
 SET @sql=REPLACE(@sql,'CREATE TRIGGER','ALTER TRIGGER');EXEC sp_executesql @sql;
END;
SET @sql=OBJECT_DEFINITION(OBJECT_ID('dbo.TR_stock_movements_quantity'));
IF @sql IS NULL THROW 51501,'Stock quantity guard missing.',1;
IF @sql NOT LIKE '%SUPPLY_ISSUE%'
BEGIN
 IF CHARINDEX('''RECEIPT'',''RETURN'',''TRANSFER_RECEIPT''',@sql)=0 OR CHARINDEX('''ISSUE'',''TRANSFER''',@sql)=0 THROW 51501,'Unrecognized stock quantity guard; review before patch.',1;
 SET @sql=REPLACE(REPLACE(@sql,'''RECEIPT'',''RETURN'',''TRANSFER_RECEIPT''','''RECEIPT'',''RETURN'',''TRANSFER_RECEIPT'',''SUPPLY_RETURN'''),'''ISSUE'',''TRANSFER''','''ISSUE'',''TRANSFER'',''SUPPLY_ISSUE''');
 SET @sql=REPLACE(@sql,'CREATE TRIGGER','ALTER TRIGGER');EXEC sp_executesql @sql;
END;

EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_supply_configuration_immutable ON dbo.supply_configurations AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT configuration_id,branch_id,item_id,asset_id,supply_group,warehouse_id,unit_id,initial_quantity,minimum_stock,per_guest_quantity,is_active,revision,updated_by,updated_at FROM deleted
 EXCEPT SELECT configuration_id,branch_id,item_id,asset_id,supply_group,warehouse_id,unit_id,initial_quantity,minimum_stock,per_guest_quantity,is_active,revision,updated_by,updated_at FROM inserted)
 OR EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.configuration_id=d.configuration_id WHERE d.is_current=0 OR i.is_current<>0)
 THROW 51502,''Supply configuration revisions are immutable.'',1;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_supply_allocation_scope ON dbo.staff_supply_allocations AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.workday_assignments a ON a.assignment_id=i.assignment_id JOIN dbo.workday_shifts s ON s.shift_id=a.shift_id WHERE i.user_id<>a.user_id OR i.branch_id<>s.branch_id)
 THROW 51503,''Supply allocation must belong to its workday and branch.'',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.allocation_id=d.allocation_id WHERE i.user_id<>d.user_id OR i.branch_id<>d.branch_id OR i.assignment_id<>d.assignment_id OR d.status=''CLOSED'')
 THROW 51503,''Supply allocation identity or closed allocation cannot change.'',1;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_supply_balance_scope ON dbo.staff_supply_balances AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted b JOIN dbo.staff_supply_allocations a ON a.allocation_id=b.allocation_id JOIN dbo.supply_configurations c ON c.configuration_id=b.configuration_id
 JOIN dbo.inventory_items item ON item.item_id=b.item_id JOIN dbo.warehouses w ON w.warehouse_id=b.warehouse_id
 WHERE a.branch_id<>c.branch_id OR w.branch_id<>a.branch_id OR c.supply_group<>''A'' OR c.item_id<>b.item_id OR c.warehouse_id<>b.warehouse_id OR c.unit_id<>b.unit_id OR item.base_unit_id<>b.unit_id OR b.initial_target<>c.initial_quantity OR b.per_guest_quantity<>c.per_guest_quantity)
 THROW 51504,''Supply balance snapshot or branch mismatch.'',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.balance_id=d.balance_id WHERE i.allocation_id<>d.allocation_id OR i.configuration_id<>d.configuration_id OR i.item_id<>d.item_id OR i.warehouse_id<>d.warehouse_id OR i.unit_id<>d.unit_id OR i.initial_target<>d.initial_target OR i.per_guest_quantity<>d.per_guest_quantity OR i.initial_issued<d.initial_issued OR i.additional_issued<d.additional_issued OR i.used<d.used OR i.returned<d.returned)
 THROW 51504,''Supply snapshot and cumulative balances are immutable.'',1;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_supply_reservation_scope ON dbo.supply_reservations AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted r JOIN dbo.staff_supply_balances b ON b.balance_id=r.balance_id JOIN dbo.staff_supply_allocations a ON a.allocation_id=b.allocation_id JOIN dbo.work_items w ON w.work_id=r.work_id JOIN dbo.rooms room ON room.room_id=r.room_id WHERE w.kind<>''CLEANING'' OR w.branch_id<>a.branch_id OR room.branch_id<>a.branch_id OR w.room_id<>r.room_id OR r.per_guest_quantity<>b.per_guest_quantity)
 THROW 51505,''Reservation requires a cleaning task in the same branch and room.'',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.reservation_id=d.reservation_id WHERE d.status<>''RESERVED'' OR i.status NOT IN(''CONSUMED'',''RELEASED'') OR i.balance_id<>d.balance_id OR i.work_id<>d.work_id OR i.room_id<>d.room_id OR i.capacity<>d.capacity OR i.quantity<>d.quantity OR i.per_guest_quantity<>d.per_guest_quantity)
 THROW 51505,''Resolved reservations and snapshot values are immutable.'',1;
 IF EXISTS(SELECT r.work_id,b.item_id FROM dbo.supply_reservations r JOIN dbo.staff_supply_balances b ON b.balance_id=r.balance_id WHERE r.status IN(''RESERVED'',''CONSUMED'') AND r.work_id IN(SELECT work_id FROM inserted) GROUP BY r.work_id,b.item_id HAVING COUNT(*)>1)
 THROW 51505,''A cleaning item can be reserved or consumed only once.'',1;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_supply_stock_provenance ON dbo.stock_movements AFTER INSERT AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted m JOIN dbo.inventory_document_lines l ON l.line_id=m.line_id JOIN dbo.inventory_documents d ON d.document_id=l.document_id
 LEFT JOIN dbo.supply_stock_documents sd ON sd.document_id=d.document_id LEFT JOIN dbo.staff_supply_allocations a ON a.allocation_id=sd.allocation_id
 LEFT JOIN dbo.staff_supply_balances b ON b.allocation_id=a.allocation_id AND b.item_id=m.item_id AND b.warehouse_id=m.warehouse_id
 WHERE d.kind IN(''SUPPLY_ISSUE'',''SUPPLY_RETURN'') AND (sd.document_id IS NULL OR b.balance_id IS NULL OR a.status=''CLOSED'' OR a.branch_id<>m.branch_id OR sd.warehouse_id<>m.warehouse_id OR l.unit_id<>b.unit_id OR l.conversion_factor<>1
 OR (d.kind=''SUPPLY_ISSUE'' AND (sd.purpose NOT IN(''INITIAL'',''ADDITIONAL'') OR NOT EXISTS(SELECT 1 FROM dbo.workday_assignments wa WHERE wa.assignment_id=a.assignment_id AND wa.status=''WORKING'') OR (sd.purpose=''INITIAL'' AND (l.actual_qty<>b.initial_target OR b.initial_issued<>0))))
 OR (d.kind=''SUPPLY_RETURN'' AND (sd.purpose<>''RETURN'' OR b.reserved<>0 OR l.actual_qty<>b.initial_issued+b.additional_issued-b.used-b.returned))))
 THROW 51506,''Stock supply movement lacks valid workday, allocation or balance provenance.'',1;
 -- Generic reversals would leave staff balances inconsistent. A verified supply workflow is required.
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.stock_movements original ON original.movement_id=i.reversal_of_movement_id JOIN dbo.inventory_document_lines l ON l.line_id=original.line_id JOIN dbo.supply_stock_documents sd ON sd.document_id=l.document_id)
 THROW 51506,''Supply documents cannot be reversed through a generic stock operation.'',1;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_supply_ledger_immutable ON dbo.staff_supply_ledger AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; THROW 51507,''Staff supply ledger is append-only.'',1; END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_supply_documents_immutable ON dbo.supply_stock_documents AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; THROW 51507,''Supply stock provenance is append-only.'',1; END;');
COMMIT;
