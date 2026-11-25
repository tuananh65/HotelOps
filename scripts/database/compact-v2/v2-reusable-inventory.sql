SET XACT_ABORT ON;SET NOCOUNT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'Isolated V2 TEST database required.',1;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.reusable_positions') IS NOT NULL THROW 51991,'Reusable foundation exists; reseed refused.',1;
CREATE TABLE dbo.reusable_positions(
 position_id bigint IDENTITY PRIMARY KEY,branch_id bigint NOT NULL,item_id bigint NOT NULL,warehouse_id bigint NULL,user_id bigint NULL,room_id bigint NULL,
 location_key varchar(40) NOT NULL,status varchar(20) NOT NULL,quantity decimal(18,4) NOT NULL,row_version rowversion NOT NULL,
 CONSTRAINT FK_reusable_profile FOREIGN KEY(branch_id,item_id) REFERENCES dbo.item_lifecycle_profiles(branch_id,item_id),
 CONSTRAINT FK_reusable_warehouse FOREIGN KEY(warehouse_id) REFERENCES dbo.warehouses(warehouse_id),
 CONSTRAINT FK_reusable_user FOREIGN KEY(user_id) REFERENCES dbo.users(user_id),
 CONSTRAINT FK_reusable_room FOREIGN KEY(room_id) REFERENCES dbo.rooms(room_id),
 CONSTRAINT CK_reusable_quantity CHECK(quantity>=0),
 CONSTRAINT CK_reusable_location CHECK((warehouse_id IS NOT NULL AND user_id IS NULL AND room_id IS NULL AND location_key=CONCAT('W:',warehouse_id)) OR (user_id IS NOT NULL AND warehouse_id IS NULL AND room_id IS NULL AND location_key=CONCAT('U:',user_id)) OR(room_id IS NOT NULL AND warehouse_id IS NULL AND user_id IS NULL AND location_key=CONCAT('R:',room_id))),
 CONSTRAINT CK_reusable_status CHECK(status IN('READY','STAFF_HOLDING','IN_USE','DIRTY','LAUNDRY','DAMAGED','IN_TRANSIT','WRITTEN_OFF','LEGACY_REVIEW')),
 CONSTRAINT UQ_reusable_location UNIQUE(branch_id,item_id,location_key,status));
CREATE TABLE dbo.reusable_movements(
 reusable_movement_id bigint IDENTITY PRIMARY KEY,branch_id bigint NOT NULL,item_id bigint NOT NULL,source_position_id bigint NULL,destination_position_id bigint NULL,
 quantity decimal(18,4) NOT NULL,kind varchar(20) NOT NULL,document_line_id bigint NULL,work_id bigint NULL,actor_user_id bigint NOT NULL,
 operation_key uniqueidentifier NOT NULL,reason nvarchar(2000) NOT NULL,occurred_at datetime2 NOT NULL,
 CONSTRAINT FK_reusablemove_profile FOREIGN KEY(branch_id,item_id) REFERENCES dbo.item_lifecycle_profiles(branch_id,item_id),
 CONSTRAINT FK_reusablemove_source FOREIGN KEY(source_position_id) REFERENCES dbo.reusable_positions(position_id),
 CONSTRAINT FK_reusablemove_destination FOREIGN KEY(destination_position_id) REFERENCES dbo.reusable_positions(position_id),
 CONSTRAINT FK_reusablemove_line FOREIGN KEY(document_line_id) REFERENCES dbo.inventory_document_lines(line_id),
 CONSTRAINT FK_reusablemove_work FOREIGN KEY(work_id) REFERENCES dbo.work_items(work_id),
 CONSTRAINT FK_reusablemove_actor FOREIGN KEY(actor_user_id) REFERENCES dbo.users(user_id),
 CONSTRAINT CK_reusablemove_quantity CHECK(quantity>0),
 CONSTRAINT CK_reusablemove_kind CHECK(kind IN('MOVE','RECEIPT','HANDOVER','REPLACEMENT','LAUNDRY','DAMAGE','RECOVERY','WRITE_OFF','LEGACY_OPENING')),
 CONSTRAINT CK_reusablemove_locations CHECK(source_position_id IS NOT NULL OR destination_position_id IS NOT NULL));
CREATE UNIQUE INDEX UX_reusablemove_operation ON dbo.reusable_movements(actor_user_id,operation_key,item_id,kind);
CREATE TABLE dbo.reusable_write_offs(
 write_off_id bigint IDENTITY PRIMARY KEY,branch_id bigint NOT NULL,position_id bigint NOT NULL,quantity decimal(18,4) NOT NULL,reason nvarchar(2000) NOT NULL,
 status varchar(20) NOT NULL,created_by bigint NOT NULL,created_at datetime2 NOT NULL,decided_by bigint NULL,decided_at datetime2 NULL,decision_reason nvarchar(2000) NULL,
 operation_key uniqueidentifier NOT NULL,row_version rowversion NOT NULL,
 CONSTRAINT FK_writeoff_branch FOREIGN KEY(branch_id) REFERENCES dbo.branches(branch_id),
 CONSTRAINT FK_writeoff_position FOREIGN KEY(position_id) REFERENCES dbo.reusable_positions(position_id),
 CONSTRAINT FK_writeoff_creator FOREIGN KEY(created_by) REFERENCES dbo.users(user_id),
 CONSTRAINT FK_writeoff_decider FOREIGN KEY(decided_by) REFERENCES dbo.users(user_id),
 CONSTRAINT CK_writeoff_quantity CHECK(quantity>0),
 CONSTRAINT CK_writeoff_decision CHECK((status='PENDING' AND decided_by IS NULL AND decided_at IS NULL) OR (status IN('APPROVED','REJECTED') AND decided_by IS NOT NULL AND decided_at IS NOT NULL AND decided_by<>created_by)),
 CONSTRAINT UQ_writeoff_operation UNIQUE(created_by,operation_key));
-- Dedicated custody documents retain the shared physical stock ledger, without calling
-- reusable handovers consumption. Keep all original constraints/trigger clauses intact.
DECLARE @constraint sysname,@definition nvarchar(max),@sql nvarchar(max);
DECLARE checks CURSOR LOCAL FAST_FORWARD FOR SELECT name,definition FROM sys.check_constraints
 WHERE parent_object_id=OBJECT_ID('dbo.inventory_documents') AND name IN('CK_inventory_documents_kind','CK_inventory_documents_kind_status','CK_inventory_documents_posted_state');
OPEN checks;FETCH NEXT FROM checks INTO @constraint,@definition;
WHILE @@FETCH_STATUS=0 BEGIN
 SET @sql=N'ALTER TABLE dbo.inventory_documents DROP CONSTRAINT '+QUOTENAME(@constraint)+N'; ALTER TABLE dbo.inventory_documents WITH CHECK ADD CONSTRAINT '+QUOTENAME(@constraint)+N' CHECK(('+@definition+N') OR (kind IN(''REUSABLE_ISSUE'',''REUSABLE_RETURN'') AND ((status=''DRAFT'' AND posted_at IS NULL) OR(status=''POSTED'' AND posted_at IS NOT NULL))));';
 EXEC sp_executesql @sql;
 FETCH NEXT FROM checks INTO @constraint,@definition;
END;
CLOSE checks;DEALLOCATE checks;
SET @sql=OBJECT_DEFINITION(OBJECT_ID('dbo.TR_stock_movements_posted'));
IF @sql NOT LIKE '%SUPPLY_ISSUE%' THROW 51993,'Unexpected stock guard baseline.',1;
SET @sql=REPLACE(@sql,'''SUPPLY_ISSUE'',''SUPPLY_RETURN''','''SUPPLY_ISSUE'',''SUPPLY_RETURN'',''REUSABLE_ISSUE'',''REUSABLE_RETURN''');
SET @sql=REPLACE(@sql,'CREATE TRIGGER','ALTER TRIGGER');EXEC sp_executesql @sql;
SET @sql=OBJECT_DEFINITION(OBJECT_ID('dbo.TR_stock_movements_quantity'));
SET @sql=REPLACE(REPLACE(@sql,'''TRANSFER_RECEIPT'',''SUPPLY_RETURN''','''TRANSFER_RECEIPT'',''SUPPLY_RETURN'',''REUSABLE_RETURN'''),'''TRANSFER'',''SUPPLY_ISSUE''','''TRANSFER'',''SUPPLY_ISSUE'',''REUSABLE_ISSUE''');
SET @sql=REPLACE(@sql,'CREATE TRIGGER','ALTER TRIGGER');EXEC sp_executesql @sql;
-- Repeated room assessments can release/re-reserve. Consumption remains exactly once.
DROP INDEX UX_supply_ledger_reservation_once ON dbo.staff_supply_ledger;
CREATE UNIQUE INDEX UX_supply_ledger_use_once ON dbo.staff_supply_ledger(reservation_id,kind) WHERE reservation_id IS NOT NULL AND kind='USE';
-- Reconstruct only demonstrable stock and unused staff custody; no assumed recovered consumption.
INSERT dbo.reusable_positions(branch_id,item_id,warehouse_id,location_key,status,quantity)
SELECT w.branch_id,m.item_id,m.warehouse_id,CONCAT('W:',m.warehouse_id),'READY',SUM(m.signed_base_qty)
FROM dbo.stock_movements m JOIN dbo.warehouses w ON w.warehouse_id=m.warehouse_id
JOIN dbo.item_lifecycle_profiles p ON p.branch_id=w.branch_id AND p.item_id=m.item_id AND p.classification='REUSABLE'
GROUP BY w.branch_id,m.item_id,m.warehouse_id HAVING SUM(m.signed_base_qty)>0;
INSERT dbo.reusable_positions(branch_id,item_id,user_id,location_key,status,quantity)
SELECT a.branch_id,b.item_id,a.user_id,CONCAT('U:',a.user_id),'STAFF_HOLDING',SUM(b.initial_issued+b.additional_issued-b.used-b.returned)
FROM dbo.staff_supply_balances b JOIN dbo.staff_supply_allocations a ON a.allocation_id=b.allocation_id
JOIN dbo.item_lifecycle_profiles p ON p.branch_id=a.branch_id AND p.item_id=b.item_id AND p.classification='REUSABLE'
GROUP BY a.branch_id,b.item_id,a.user_id HAVING SUM(b.initial_issued+b.additional_issued-b.used-b.returned)>0;
-- Room placement was previously charged as use. Keep that historical ledger unchanged;
-- quarantine the evidenced placements until the physical room count is reconciled.
INSERT dbo.reusable_positions(branch_id,item_id,room_id,location_key,status,quantity)
SELECT w.branch_id,b.item_id,r.room_id,CONCAT('R:',r.room_id),'LEGACY_REVIEW',SUM(r.quantity)
FROM dbo.supply_reservations r JOIN dbo.staff_supply_balances b ON b.balance_id=r.balance_id JOIN dbo.work_items w ON w.work_id=r.work_id
JOIN dbo.item_lifecycle_profiles p ON p.branch_id=w.branch_id AND p.item_id=b.item_id AND p.classification='REUSABLE'
WHERE r.status='CONSUMED' GROUP BY w.branch_id,b.item_id,r.room_id;
-- Obsolete reservations are released by the next authorized assessment/reassignment,
-- with the actual authenticated actor. A migration never impersonates a staff user.
DECLARE @key uniqueidentifier=NEWID();
INSERT dbo.audit_logs(entity_type,entity_key,action,operation_key,occurred_at,after_json)
VALUES('V2_REUSABLE_MIGRATION','261125-v1','APPLIED',@key,SYSUTCDATETIME(),'{"historicalDocumentsPreserved":true,"roomPlacementRequiresPhysicalReconciliation":true}');
COMMIT;
SELECT COUNT(*) tables FROM sys.tables WHERE is_ms_shipped=0;
SELECT COUNT(*) foreign_keys FROM sys.foreign_keys;
SELECT COUNT(*) checks FROM sys.check_constraints;
